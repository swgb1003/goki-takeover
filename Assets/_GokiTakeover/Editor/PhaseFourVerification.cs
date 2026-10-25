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
    public static class PhaseFourVerification
    {
        public const string Output = "docs/Validation/Phase4";
        public const string SettingsKey = "GokiTakeover.Phase4.SettingsPath";
        private const string Active = "GokiTakeover.Phase4.Verify";
        private static int checks;
        static PhaseFourVerification() { EditorApplication.update += Poll; }

        public static void GenerateAndVerify()
        {
            Directory.CreateDirectory(Output);
            try
            {
                string[] kept = { PhaseThreeGenerator.ScenePath, PhaseTwoGenerator.ScenePath, BlenderModelIntegration.ScenePath, PolishedCockroachGenerator.PrefabPath,
                    PhaseThreeGenerator.SettingsPath, PhaseThreeGenerator.GridPath, PhaseTwoGenerator.SettingsPath };
                var before = kept.Select(Hash).ToArray();
                PhaseFourGenerator.Generate();
                string guid = AssetDatabase.AssetPathToGUID(PhaseFourGenerator.ScenePath);
                int assets = AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length;
                PhaseFourGenerator.Generate();
                Check(kept.Select(Hash).SequenceEqual(before), "Phase 2・3シーン、Prefab、Phase 2/3設定、仲間グリッドを保全");
                Check(guid == AssetDatabase.AssetPathToGUID(PhaseFourGenerator.ScenePath), "再生成でシーンGUIDを維持");
                Check(assets == AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length, "再生成でアセットが増えない");
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "描画カメラ1台");
                Check(UnityEngine.Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1, "主人公1匹");
                Check(UnityEngine.Object.FindObjectsByType<AllyRoach>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 9, "仲間プール9匹");
                var foods = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(foods.Length == 8 && foods.All(f => f.GetComponent<FoodValue>()), "食料8個すべてに種類と価値");
                Check(foods.Sum(FoodValue.AmountOf) == 13, "初期配置の食料価値の合計13");
                var baits = UnityEngine.Object.FindObjectsByType<PoisonBait>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(baits.Length == PhaseFourGenerator.BaitPool && baits.All(b => !b.gameObject.activeSelf && !b.GetComponent<FoodItem>() && b.GetComponentsInChildren<Collider>(true).Length == 0),
                    "毒餌3個（待機・食料扱いではない・当たり判定なし）");
                var traps = UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(traps.Length == 5 && traps.Count(t => !t.gameObject.activeSelf) == PhaseFourGenerator.TrapPool, "粘着シート 常設2＋設置用3（待機）");
                Check(UnityEngine.Object.FindObjectsByType<HumanReaction>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<ColonyManager>(FindObjectsSortMode.None).Length == 1, "人間の反応・コロニー管理を1つずつ");
                Check(UnityEngine.Object.FindFirstObjectByType<InsecticideSpray>(FindObjectsInactive.Include), "殺虫スプレー");
                Check(!UnityEngine.Object.FindFirstObjectByType<PhaseThreeHud>() && UnityEngine.Object.FindFirstObjectByType<PhaseFourHud>(), "HUDをPhase 4用に置換");
                var commander = UnityEngine.Object.FindFirstObjectByType<SwarmCommander>();
                Check(new SerializedObject(commander).FindProperty("hud").objectReferenceValue is PhaseFourHud, "軍団モードのクリック判定はPhase 4 HUDを参照");
                var settings = AssetDatabase.LoadAssetAtPath<ColonySettings>(PhaseFourGenerator.SettingsPath);
                Check(settings.upgrades.Length == 5 && settings.breedCost == 3 && settings.startAllies == 4 && settings.allyCapByNestLevel.Last() == 9, "強化5種・繁殖は食料3・開始4匹・上限9匹");
                var colony = UnityEngine.Object.FindFirstObjectByType<ColonyManager>();
                var spots = (Vector3[])typeof(ColonyManager).GetField("respawnSpots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(colony);
                var grid = commander.Grid;
                var path = new System.Collections.Generic.List<Vector3>();
                Check(spots.Length >= 10, "食料の補充地点 " + spots.Length + "か所");
                foreach (var s in spots.Where(s => s.y < 0.5f))
                    Check(grid.FindPath(commander.NestGround, s, path), "巣から補充地点 " + s.ToString("F1") + " へ仲間の経路あり");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/editmode.txt", "PASS: " + checks + " checks\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(Output + "/editmode-failure.txt", e.ToString()); EditorApplication.Exit(1); }
        }

        public static void VerifyPlayMode() => Play(PhaseFourGenerator.ScenePath, "phase4", Output);
        // Shared scripts changed again: rerun the earlier suites on their own scenes.
        public static void VerifyPhaseThreeRegression() => Play(PhaseThreeGenerator.ScenePath, "phase3", Output + "/regression-phase3");
        public static void VerifyBlenderRegression() => Play(BlenderModelIntegration.ScenePath, "phase2", Output + "/regression-blender");
        public static void VerifyPhaseTwoRegression() => Play(PhaseTwoGenerator.ScenePath, "phase2", Output + "/regression-phase2");

        private static void Play(string scene, string suite, string output)
        {
            Directory.CreateDirectory(output);
            string settingsFile = Path.GetFullPath(Path.Combine(output, "settings-test.json"));
            if (File.Exists(settingsFile)) File.Delete(settingsFile);
            if (File.Exists(settingsFile + ".bak")) File.Delete(settingsFile + ".bak");
            EditorSceneManager.OpenScene(scene);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Active + ".Suite", suite);
            SessionState.SetString(Active + ".Output", output);
            SessionState.SetString(SettingsKey, settingsFile);   // tests never touch the player's own settings file
            SessionState.SetString(Active + ".Start", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Active + ".Start", ""))).TotalSeconds > 420)
            { SessionState.SetBool(Active, false); Debug.LogError("Phase 4テストタイムアウト"); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 0.4f) return;
            SessionState.SetBool(Active, false);
            string output = SessionState.GetString(Active + ".Output", Output);
            Action<bool> done = passed => { EditorApplication.isPlaying = false; EditorApplication.Exit(passed ? 0 : 1); };
            switch (SessionState.GetString(Active + ".Suite", ""))
            {
                case "phase2": { var r = new GameObject("Phase2_Regression").AddComponent<PhaseTwoScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                case "phase3": { var r = new GameObject("Phase3_Regression").AddComponent<PhaseThreeScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                default: { var r = new GameObject("Phase4_Verification").AddComponent<PhaseFourScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
            }
        }

        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks++; Debug.Log("[PHASE4 EDIT] PASS: " + label); }
    }
}
