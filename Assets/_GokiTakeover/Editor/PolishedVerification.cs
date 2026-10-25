using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GokiTakeover.Editor
{
    [InitializeOnLoad]
    public static class PolishedVerification
    {
        private const string Active = "GokiTakeover.Polished.Verify";
        private static int count;
        static PolishedVerification() { EditorApplication.update += Poll; }
        public static void GenerateAndVerify()
        {
            try
            {
                string originalScene = Hash(KitchenStageGenerator.ScenePath);
                string originalPrefab = Hash(CockroachGeneratorEditor.PrefabPath);
                PolishedKitchenGenerator.Generate();
                string guid = AssetDatabase.AssetPathToGUID(PolishedKitchenGenerator.ScenePath);
                int assets = AssetDatabase.FindAssets("", new[] { PolishedAssets.Root }).Length;
                PolishedKitchenGenerator.Generate();
                Check(guid == AssetDatabase.AssetPathToGUID(PolishedKitchenGenerator.ScenePath), "再生成でシーンGUIDを維持");
                Check(assets == AssetDatabase.FindAssets("", new[] { PolishedAssets.Root }).Length, "再生成でアセットを重複しない");
                Check(Hash(KitchenStageGenerator.ScenePath) == originalScene && Hash(CockroachGeneratorEditor.PrefabPath) == originalPrefab, "元シーンと元Prefabを保全");
                var player = UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
                var visual = player.GetComponentInChildren<CockroachVisualController>();
                Check(visual.Legs.Length == 6 && visual.Antennae.Length == 2, "6脚・2触角を維持");
                Check(player.GetComponentsInChildren<Collider>().Length == 1, "キャラの単一Colliderを維持");
                int triangles = player.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3);
                Check(triangles < 12000, "改善版モデルは12000三角形未満: " + triangles);
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "カメラは1台");
                Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.shadows != LightShadows.None) == 1, "影を描くライトは1灯のみ");
                Check(!UnityEngine.Object.FindFirstObjectByType<PhaseOneSession>() && UnityEngine.Object.FindFirstObjectByType<PolishedPresentation>(), "品質設定は改善版専用");
                Check(UnityEngine.Object.FindFirstObjectByType<PolishedHud>(), "改善版日本語HUDを配置");
                AssetDatabase.SaveAssets();
                File.WriteAllText("Logs/polished-editmode.txt", "PASS: " + count + " checks; triangles=" + triangles);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Fail(exception); }
        }
        public static void VerifyPlayMode()
        {
            EditorSceneManager.OpenScene(PolishedKitchenGenerator.ScenePath);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Active + ".Time", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Active + ".Time", ""))).TotalSeconds > 90)
            {
                SessionState.SetBool(Active, false);
                Fail(new TimeoutException("[GOKI] 改善版Play Modeの開始に失敗しました。"));
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 0.3f) return;
            SessionState.SetBool(Active, false);
            try
            {
                var motor = UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
                var camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>();
                motor.enabled = false;
                camera.enabled = false;
                camera.transform.rotation = Quaternion.identity;
                const float dt = 1f/60;
                Teleport(motor, new Vector3(0,0.05f,-4.8f));
                for(int i=0;i<60;i++) motor.SimulateMove(Vector2.up,false,dt);
                Check(Mathf.Abs(motor.transform.position.z+4.8f-1.65f)<0.08f, "歩行速度を維持");
                Check(Mathf.Abs(motor.transform.position.y)<0.04f, "床に接地");
                Teleport(motor,new Vector3(0,0.05f,-4.8f));
                for(int i=0;i<60;i++) motor.SimulateMove(Vector2.up,true,dt);
                Check(Mathf.Abs(motor.transform.position.z+4.8f-3.3f)<0.08f, "ダッシュ速度を維持");
                Teleport(motor,new Vector3(0,0.05f,-4.8f));
                for(int i=0;i<180;i++) motor.SimulateMove(Vector2.right,true,dt);
                Check(motor.transform.position.x<6.55f && motor.transform.position.x>6.2f, "壁の衝突");
                Teleport(motor,new Vector3(-3.8f,0.05f,-3.4f));
                for(int i=0;i<180;i++) motor.SimulateMove(Vector2.up,true,dt);
                Check(motor.transform.position.z<-2.4f && motor.transform.position.z>-2.7f, "家具脚の衝突");
                Teleport(motor,new Vector3(-2.25f,0.05f,-1));
                for(int i=0;i<30;i++) motor.SimulateMove(Vector2.up,false,dt);
                Check(motor.transform.position.z>-0.3f, "机下を通過");
                Teleport(motor,new Vector3(0.2f,0.05f,-2.5f));
                for(int i=0;i<120;i++) motor.SimulateMove(Vector2.right,true,dt);
                Check(motor.transform.position.x>0.85f && motor.transform.position.x<1.1f, "床の瓶をすり抜けない");
                Teleport(motor,new Vector3(1.8f,0.05f,-4.9f));
                bool contactedCup=false;
                for(int i=0;i<22;i++)
                {
                    motor.SimulateMove(Vector2.up,true,dt);
                    contactedCup|=(motor.GetComponent<CharacterController>().collisionFlags & CollisionFlags.Sides)!=0;
                }
                // Check the initial contact; a long push may legitimately slide around the rounded cup.
                Check(contactedCup, "倒れたカップとの衝突を検知 (position="+motor.transform.position+")");
                Teleport(motor,new Vector3(0,0.05f,-5.3f));
                camera.Follow(0);
                Check(!Physics.CheckSphere(camera.transform.position,0.1f,~(1<<2),QueryTriggerInteraction.Ignore), "低いカメラが壁床と重ならない");
                var visual=motor.GetComponentInChildren<CockroachVisualController>();
                visual.SetMotion(0,false);
                visual.Animate(0.1f,2);
                Check(visual.Legs.All(l => Vector3.Distance(visual.transform.InverseTransformPoint(l.toe.position),l.restFoot)<0.001f), "改善モデルでも停止姿勢を維持");
                Check(QualitySettings.shadows==ShadowQuality.All && QualitySettings.shadowCascades==1,
                    "影マップを1枚に制限 ("+QualitySettings.shadows+", cascades="+QualitySettings.shadowCascades+", GPU="+SystemInfo.graphicsDeviceType+")");
                Check(QualitySettings.antiAliasing==2 && QualitySettings.pixelLightCount==2, "2xAA・ライト上限2");
                File.WriteAllText("Logs/polished-playmode.txt", "PASS: " + count + " checks");
                EditorApplication.isPlaying=false;
                EditorApplication.Exit(0);
            }
            catch(Exception exception) { EditorApplication.isPlaying=false; Fail(exception); }
        }
        private static void Teleport(RoachMotor motor,Vector3 position)
        {
            var controller=motor.GetComponent<CharacterController>();
            controller.enabled=false; motor.transform.position=position; controller.enabled=true;
            Physics.SyncTransforms();
        }
        private static string Hash(string path)
        {
            using(var algorithm=SHA256.Create()) return Convert.ToBase64String(algorithm.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Check(bool value,string description)
        {
            if(!value) throw new InvalidOperationException("[GOKI POLISH] FAIL: "+description);
            count++; Debug.Log("[GOKI POLISH] PASS: "+description);
        }
        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText("Logs/polished-failure.txt",exception.ToString());
            EditorApplication.Exit(1);
        }
    }
}
