using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover.Editor
{
    [InitializeOnLoad]
    public static class PhaseTwoVerification
    {
        private const string Active = "GokiTakeover.Phase2.Verify";
        private static int checks;
        static PhaseTwoVerification() { EditorApplication.update += Poll; }
        public static void GenerateAndVerify()
        {
            try
            {
                string source = Hash(PolishedKitchenGenerator.ScenePath);
                string prefab = Hash(PolishedCockroachGenerator.PrefabPath);
                PhaseTwoGenerator.Generate();
                string guid = AssetDatabase.AssetPathToGUID(PhaseTwoGenerator.ScenePath);
                string navGuid = AssetDatabase.AssetPathToGUID(PhaseTwoGenerator.NavPath);
                int assets = AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length;
                PhaseTwoGenerator.Generate();
                Check(source == Hash(PolishedKitchenGenerator.ScenePath) && prefab == Hash(PolishedCockroachGenerator.PrefabPath), "改善版シーンとPrefabを保全");
                Check(guid == AssetDatabase.AssetPathToGUID(PhaseTwoGenerator.ScenePath) && navGuid == AssetDatabase.AssetPathToGUID(PhaseTwoGenerator.NavPath), "シーン・NavMeshのGUIDを維持");
                Check(assets == AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length, "再生成でアセット数を維持");
                Check(UnityEngine.Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1, "主人公1匹");
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "描画カメラ1台");
                Check(UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None).Length == 3, "食料3個を確保");
                Check(UnityEngine.Object.FindObjectsByType<HumanAIController>(FindObjectsSortMode.None).Length == 1, "人間AI1体");
                Check(UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.None).Length == 2, "粘着トラップ2個");
                Check(!UnityEngine.Object.FindFirstObjectByType<PolishedHud>() && UnityEngine.Object.FindFirstObjectByType<PhaseTwoHud>(), "仮HP表示を実データHUDに置換");
                var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(PhaseTwoGenerator.NavPath);
                var instance = NavMesh.AddNavMeshData(data);
                try
                {
                    Check(NavMesh.SamplePosition(new Vector3(3.6f, 0, -0.7f), out _, 0.5f, NavMesh.AllAreas), "人間の初期位置にNavMeshあり");
                    var path = new NavMeshPath();
                    Check(NavMesh.CalculatePath(new Vector3(3.6f, 0, -3.8f), new Vector3(4.1f, 0, 3.5f), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "巡回経路が接続");
                    Check(!NavMesh.SamplePosition(new Vector3(-2.25f, 0, -0.9f), out _, 0.2f, NavMesh.AllAreas), "人間は低い机下へ侵入できない");
                }
                finally { instance.Remove(); }
                File.WriteAllText("Logs/Phase2/editmode.txt", "PASS: " + checks + " checks");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void VerifyPlayMode()
        {
            EditorSceneManager.OpenScene(PhaseTwoGenerator.ScenePath);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Active + ".Start", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Active + ".Start", ""))).TotalSeconds > 120)
            { SessionState.SetBool(Active, false); Debug.LogError("Phase 2テストタイムアウト"); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 0.4f) return;
            SessionState.SetBool(Active, false);
            var runner = new GameObject("Phase2_Verification").AddComponent<PhaseTwoScenarioTest>();
            runner.OutputFolder = "Logs/Phase2";
            runner.Completed += passed => { EditorApplication.isPlaying = false; EditorApplication.Exit(passed ? 0 : 1); };
        }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks++; Debug.Log("[PHASE2 EDIT] PASS: " + label); }
    }
}
