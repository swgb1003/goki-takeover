using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GokiTakeover.Editor
{
    /// <summary>Dependency-free integration checks, executed by Unity itself; never included in a player.</summary>
    [InitializeOnLoad]
    public static class PhaseOneVerification
    {
        private const string Active = "GokiTakeover.Verification.Active";
        private const string StartTime = "GokiTakeover.Verification.Start";
        private static int checks;
        static PhaseOneVerification() { EditorApplication.update += PollPlayMode; }

        public static void GenerateAndVerify()
        {
            try
            {
                Directory.CreateDirectory("Logs");
                GameObject first = CockroachGeneratorEditor.Generate();
                string guid = AssetDatabase.AssetPathToGUID(CockroachGeneratorEditor.PrefabPath);
                int count = AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root + "/Art" }).Length;
                CockroachGeneratorEditor.Generate();
                Check(guid == AssetDatabase.AssetPathToGUID(CockroachGeneratorEditor.PrefabPath), "再生成でPrefab GUIDを維持");
                Check(count == AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root + "/Art" }).Length, "再生成でメッシュ・マテリアル数を維持");
                first = AssetDatabase.LoadAssetAtPath<GameObject>(CockroachGeneratorEditor.PrefabPath);
                var visual = first.GetComponentInChildren<CockroachVisualController>();
                Check(visual && visual.Legs.Length == 6 && visual.Antennae.Length == 2, "6本脚と2本触角");
                var transforms = first.GetComponentsInChildren<Transform>();
                Check(transforms.Count(t => t.name.StartsWith("Eye_")) == 2, "目2個");
                Check(transforms.Count(t => t.name.StartsWith("Shell_")) == 2, "甲殻2枚");
                Check(visual.Legs.Count(l => l.tripod == 0) == 3 && visual.Legs.Count(l => l.tripod == 1) == 3, "交互の三脚グループ");
                Check(first.GetComponentsInChildren<Collider>().Length == 1 && !first.GetComponent<Rigidbody>(), "単一CharacterController・子Colliderなし");
                Check(first.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh).Distinct().Count() == 2, "メッシュ2種類を共有");
                Check(first.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).Distinct().Count() == 4, "マテリアル4種類を共有");
                Check(first.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3) < 3000, "主人公の三角形総数3000未満");
                int originalRoots = SceneManager.GetActiveScene().rootCount;
                CockroachGeneratorEditor.Generate();
                Check(SceneManager.GetActiveScene().rootCount == originalRoots, "Prefab生成でアクティブシーンにルートを追加しない");

                string protectedPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedAssets.Root + "/Data/OwnershipProbe.asset");
                var probe = ScriptableObject.CreateInstance<CockroachAppearance>();
                AssetDatabase.CreateAsset(probe, protectedPath);
                bool protectedExisting = false;
                try { GeneratedAssets.RequireOwned(protectedPath); }
                catch (InvalidOperationException) { protectedExisting = true; }
                finally { AssetDatabase.DeleteAsset(protectedPath); }
                Check(protectedExisting, "生成物以外の既存アセットを保護");

                KitchenStageGenerator.Generate();
                string sceneGuid = AssetDatabase.AssetPathToGUID(KitchenStageGenerator.ScenePath);
                int roots = SceneManager.GetActiveScene().rootCount;
                KitchenStageGenerator.Generate();
                Check(sceneGuid == AssetDatabase.AssetPathToGUID(KitchenStageGenerator.ScenePath), "専用シーン再生成でGUIDを維持");
                Check(roots == SceneManager.GetActiveScene().rootCount, "キッチン再生成でルートを重複させない");
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "有効カメラ1台");
                Check(UnityEngine.Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1, "主人公1匹");
                File.WriteAllText("Logs/phase1-editmode.txt", "PASS: " + checks + " checks; Unity " + Application.unityVersion);
                Debug.Log("[GOKI VERIFY] EditMode PASS: " + checks);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Fail(exception); }
        }

        [MenuItem("Tools/GOKI TAKEOVER/Verify Play Mode", priority = 40)]
        public static void VerifyPlayMode()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(KitchenStageGenerator.ScenePath);
            SessionState.SetBool(Active, true);
            SessionState.SetString(StartTime, DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/GOKI TAKEOVER/Verify Play Mode", true)]
        private static bool CanVerify() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void PollPlayMode()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(StartTime, ""))).TotalSeconds > 90)
            {
                SessionState.SetBool(Active, false);
                Fail(new TimeoutException("Play Mode開始待ちが90秒を超えました。"));
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 0.3f) return;
            SessionState.SetBool(Active, false);
            try
            {
                RunPlayChecks();
                File.WriteAllText("Logs/phase1-playmode.txt", "PASS: " + checks + " checks; Unity " + Application.unityVersion);
                Debug.Log("[GOKI VERIFY] PlayMode PASS: " + checks);
                EditorApplication.isPlaying = false;
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception) { EditorApplication.isPlaying = false; Fail(exception); }
        }

        private static void RunPlayChecks()
        {
            var motor = UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
            var camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>();
            var visual = motor.GetComponentInChildren<CockroachVisualController>();
            motor.enabled = false;
            camera.enabled = false;
            const float dt = 1f / 60;
            Teleport(motor, new Vector3(1, 0.05f, -4.5f));
            for (int i = 0; i < 60; i++) motor.SimulateMove(Vector2.up, false, dt);
            float walked = motor.transform.position.z + 4.5f;
            Check(Mathf.Abs(walked - 1.65f) < 0.08f, "歩行速度1.65m/s");
            Check(Mathf.Abs(motor.transform.position.y) < 0.04f, "重力と床の接地");
            Teleport(motor, new Vector3(1, 0.05f, -4.5f));
            for (int i = 0; i < 60; i++) motor.SimulateMove(Vector2.up, true, dt);
            Check(Mathf.Abs(motor.transform.position.z + 4.5f - walked * 2) < 0.08f, "ダッシュで移動距離2倍");
            Teleport(motor, new Vector3(0, 0.05f, -4.5f));
            for (int i = 0; i < 60; i++) motor.SimulateMove(Vector2.one, false, dt);
            Check(Mathf.Abs(Vector3.Distance(new Vector3(0, motor.transform.position.y, -4.5f), motor.transform.position) - walked) < 0.08f, "斜め移動の速度正規化");
            Teleport(motor, new Vector3(1, 0.05f, -4.5f));
            for (int i = 0; i < 240; i++) motor.SimulateMove(Vector2.right, true, dt);
            Check(motor.transform.position.x < 6.55f && motor.transform.position.x > 6.3f, "壁の突き抜け防止");
            Teleport(motor, new Vector3(-3.8f, 0.05f, -3.4f));
            for (int i = 0; i < 180; i++) motor.SimulateMove(Vector2.up, true, dt);
            Check(motor.transform.position.z < -2.4f && motor.transform.position.z > -2.7f, "机の脚への衝突");
            Teleport(motor, new Vector3(-2.25f, 0.05f, -1.0f));
            for (int i = 0; i < 30; i++) motor.SimulateMove(Vector2.up, false, dt);
            Check(motor.transform.position.z > -0.3f, "机の下を移動可能");
            Teleport(motor, new Vector3(0, 0.05f, -5.3f));
            camera.Follow(0);
            Check(camera.transform.position.z > -5.9f, "背後の壁でカメラを押し戻す");
            Check(!Physics.CheckSphere(camera.transform.position, 0.10f, ~(1 << 2), QueryTriggerInteraction.Ignore), "カメラ球が壁と重ならない");
            Check(Vector3.Distance(camera.transform.position, motor.transform.position) < 2.6f, "カメラが主人公を追従");

            visual.SetMotion(1, false);
            visual.Animate(0.07f, 2);
            int grounded = visual.Legs.Count(l => visual.transform.InverseTransformPoint(l.toe.position).y < 0.03f);
            Check(grounded == 3, "歩行中に片側tripodの3本が接地");
            visual.SetMotion(0, false);
            visual.Animate(0.1f, 3);
            Check(visual.Legs.All(l => Vector3.Distance(visual.transform.InverseTransformPoint(l.toe.position), l.restFoot) < 0.001f), "停止で6本脚が休止姿勢へ戻る");
            Quaternion antenna = visual.Antennae[0].localRotation;
            visual.Animate(0.1f, 4);
            Check(Quaternion.Angle(antenna, visual.Antennae[0].localRotation) > 0.1f, "Idleで触角が動く");
            Check(visual.Legs.All(l => !float.IsNaN(l.lower.position.x)), "脚の座標が有限");
            Check(QualitySettings.shadows == ShadowQuality.Disable && QualitySettings.antiAliasing == 0, "試験中は影とAAを無効化");
        }

        private static void Teleport(RoachMotor motor, Vector3 position)
        {
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            motor.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("[GOKI VERIFY] FAIL: " + description);
            checks++;
            Debug.Log("[GOKI VERIFY] PASS: " + description);
        }
        private static void Fail(Exception exception)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/phase1-failure.txt", exception.ToString());
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
