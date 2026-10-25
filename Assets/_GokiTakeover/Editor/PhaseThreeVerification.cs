using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GokiTakeover.Editor
{
    [InitializeOnLoad]
    public static class PhaseThreeVerification
    {
        public const string Output = "docs/Validation/Phase3";
        private const string Active = "GokiTakeover.Phase3.Verify";
        private static int checks;
        static PhaseThreeVerification() { EditorApplication.update += Poll; }

        public static void GenerateAndVerify()
        {
            Directory.CreateDirectory(Output);
            try
            {
                string phase2 = Hash(PhaseTwoGenerator.ScenePath), blender = Hash(BlenderModelIntegration.ScenePath), prefab = Hash(PolishedCockroachGenerator.PrefabPath);
                string phase2Settings = Hash(PhaseTwoGenerator.SettingsPath);
                PhaseThreeGenerator.Generate();
                string guid = AssetDatabase.AssetPathToGUID(PhaseThreeGenerator.ScenePath), gridGuid = AssetDatabase.AssetPathToGUID(PhaseThreeGenerator.GridPath);
                int assets = AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length;
                PhaseThreeGenerator.Generate();
                Check(phase2 == Hash(PhaseTwoGenerator.ScenePath) && blender == Hash(BlenderModelIntegration.ScenePath) && prefab == Hash(PolishedCockroachGenerator.PrefabPath)
                    && phase2Settings == Hash(PhaseTwoGenerator.SettingsPath), "Phase 2シーン・Blenderモデル版シーン・Prefab・Phase 2設定を保全");
                Check(guid == AssetDatabase.AssetPathToGUID(PhaseThreeGenerator.ScenePath) && gridGuid == AssetDatabase.AssetPathToGUID(PhaseThreeGenerator.GridPath), "再生成でシーン・経路グリッドのGUIDを維持");
                Check(assets == AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length, "再生成でアセットが増えない");
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "描画カメラ1台");
                var motor = UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
                Check(UnityEngine.Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1 && motor.ClimbEnabled, "主人公1匹・壁登りピボット設定済み");
                Check(motor.GetComponentsInChildren<Collider>(true).Length == 1, "主人公の当たり判定はCharacterController 1個のまま");
                var allies = UnityEngine.Object.FindObjectsByType<AllyRoach>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(allies.Length == PhaseThreeGenerator.AllyPool && allies.All(a => a.GetComponentsInChildren<Collider>(true).Length == 0), "仲間プール9匹・物理コライダーなし");
                Check(allies.All(a => a.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == 2)), "仲間はレイキャスト対象外レイヤー（主人公・カメラ判定に干渉しない）");
                Check(UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 8, "食料8個（床3・天板1・隠し4）");
                Check(UnityEngine.Object.FindObjectsByType<ClimbableSurface>(FindObjectsSortMode.None).Length == 8, "登れる面8枚（壁4・天井・食器棚3）");
                Check(!UnityEngine.Object.FindFirstObjectByType<PhaseTwoHud>() && UnityEngine.Object.FindFirstObjectByType<PhaseThreeHud>(), "HUDをPhase 3用に置換");
                var cam = UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>();
                var overview = cam.GetComponent<SwarmViewCamera>();
                Check(overview && !overview.enabled, "軍団視点は同じカメラに無効状態で追加");
                var ceiling = GameObject.Find("Room/Ceiling").GetComponent<Renderer>();
                Check(ceiling.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off, "天井は影を落とさない（照明を変えない）");

                var grid = AssetDatabase.LoadAssetAtPath<AllyNavGrid>(PhaseThreeGenerator.GridPath);
                var nest = new Vector3(-6, 0, -4.5f);
                var path = new System.Collections.Generic.List<Vector3>();
                Check(grid.CellCount == 56 * 48 && grid.Walkable(nest), "床グリッド56×48・巣は通行可");
                Check(grid.Walkable(new Vector3(-2.25f, 0, -0.9f)) && !grid.Walkable(new Vector3(-0.7f, 0, 0.1f)), "机の下は通れて机の脚は通れない");
                Check(!grid.Walkable(new Vector3(1.35f, 0, 4.8f)) && !grid.Walkable(new Vector3(7.5f, 0, 0)), "食器棚の中・部屋の外は通れない");
                Check(grid.IsSticky(new Vector3(2.4f, 0, 1.6f)) && grid.IsSticky(new Vector3(-0.2f, 0, -2.8f)), "粘着シートは高コスト地帯");
                foreach (var food in UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (food.transform.position.y > 0.5f) continue;
                    Check(grid.FindPath(nest, food.transform.position, path) && Vector3.Distance(path[path.Count - 1], new Vector3(food.transform.position.x, 0, food.transform.position.z)) < 0.01f,
                        "巣から床の食料 " + food.name + " へ経路あり");
                }
                foreach (var p in PhaseThreeGenerator.ScoutPoints) Check(grid.FindPath(nest, p, path), "巣から調査地点 " + p + " へ経路あり");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) grid.FindPath(nest, new Vector3(6.2f, 0, -5.2f), path);
                Check(timer.Elapsed.TotalMilliseconds / 100 < 2, "対角の経路探索1回 " + (timer.Elapsed.TotalMilliseconds / 100).ToString("F3") + "ms（Editor計測）");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/editmode.txt", "PASS: " + checks + " checks\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(Output + "/editmode-failure.txt", e.ToString()); EditorApplication.Exit(1); }
        }

        public static void VerifyPlayMode() => Play(PhaseThreeGenerator.ScenePath, "phase3", Output);
        // Shared scripts changed (motor, camera, session, human): rerun the Phase 2 suite on both Phase 2 scenes.
        public static void VerifyPhaseTwoRegression() => Play(PhaseTwoGenerator.ScenePath, "phase2", Output + "/regression-phase2");
        public static void VerifyBlenderRegression() => Play(BlenderModelIntegration.ScenePath, "phase2", Output + "/regression-blender");

        private static void Play(string scene, string suite, string output)
        {
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(scene);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Active + ".Suite", suite);
            SessionState.SetString(Active + ".Output", output);
            SessionState.SetString(Active + ".Start", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Active + ".Start", ""))).TotalSeconds > 300)
            { SessionState.SetBool(Active, false); Debug.LogError("Phase 3テストタイムアウト"); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 0.4f) return;
            SessionState.SetBool(Active, false);
            string output = SessionState.GetString(Active + ".Output", Output);
            Action<bool> done = passed => { EditorApplication.isPlaying = false; EditorApplication.Exit(passed ? 0 : 1); };
            if (SessionState.GetString(Active + ".Suite", "") == "phase2")
            {
                var runner = new GameObject("Phase2_Regression").AddComponent<PhaseTwoScenarioTest>();
                runner.OutputFolder = output; runner.Completed += done;
            }
            else
            {
                var runner = new GameObject("Phase3_Verification").AddComponent<PhaseThreeScenarioTest>();
                runner.OutputFolder = output; runner.Completed += done;
            }
        }

        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks++; Debug.Log("[PHASE3 EDIT] PASS: " + label); }
    }
}
