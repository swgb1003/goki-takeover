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
    public static partial class PhaseFiveVerification
    {
        public const string Output = "docs/Validation/Phase5";
        private const string Active = "GokiTakeover.Phase5.Verify";
        private static int checks;
        static PhaseFiveVerification() { EditorApplication.update += Poll; }

        public static void GenerateAndVerify()
        {
            Directory.CreateDirectory(Output);
            try
            {
                string[] kept = { PhaseFourGenerator.ScenePath, PhaseThreeGenerator.ScenePath, PhaseTwoGenerator.ScenePath, BlenderModelIntegration.ScenePath,
                    PolishedCockroachGenerator.PrefabPath, PhaseFourGenerator.SettingsPath, PhaseThreeGenerator.SettingsPath, PhaseThreeGenerator.GridPath, PhaseTwoGenerator.SettingsPath };
                var before = kept.Select(Hash).ToArray();
                PhaseFiveGenerator.Generate();
                string stageGuid = AssetDatabase.AssetPathToGUID(PhaseFiveGenerator.StageScene), titleGuid = AssetDatabase.AssetPathToGUID(PhaseFiveGenerator.TitleScene);
                int assets = AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length;
                PhaseFiveGenerator.Generate();
                Check(kept.Select(Hash).SequenceEqual(before), "Phase 2〜4シーン、Prefab、Phase 2〜4設定、仲間グリッドを保全");
                Check(stageGuid == AssetDatabase.AssetPathToGUID(PhaseFiveGenerator.StageScene) && titleGuid == AssetDatabase.AssetPathToGUID(PhaseFiveGenerator.TitleScene), "再生成でシーンGUIDを維持");
                Check(assets == AssetDatabase.FindAssets("", new[] { GeneratedAssets.Root }).Length, "再生成でアセットが増えない");

                var stages = PhaseFiveGenerator.StageAssets.Select(AssetDatabase.LoadAssetAtPath<StageDefinition>).ToArray();
                Check(stages.All(s => s) && stages[0].playable && stages[0].zones.Length == 4 && stages[1].playable && stages[1].zones.Length == 8 && stages[2].playable && stages[2].zones.Length == 10
                    && stages[0].unlocks == stages[1].id && stages[1].unlocks == stages[2].id && stages[1].unlocksTitle == stages[2].title,
                    "ステージ定義3つ（すべて遊べる・縄張り4・8・10区域、1のクリアで2・2のクリアで3「" + stages[2].title + "」を解放）");
                Check(stages[2].foodGoal > stages[1].foodGoal && stages[2].fearGoal > stages[1].fearGoal && stages[2].holdSeconds > stages[1].holdSeconds && stages[2].zones.Length > stages[1].zones.Length
                    && stages[2].finalDefenceSeconds > stages[1].finalDefenceSeconds && stages[2].nestFumigations.Length > stages[1].nestFumigations.Length && stages[2].humanDetectionScale < stages[1].humanDetectionScale
                    && stages[2].difficulty > stages[1].difficulty && !stages[2].tutorial,
                    "ステージ3はステージ2より難しい（食料 " + stages[2].foodGoal + "・恐怖 " + stages[2].fearGoal + "・区域 " + stages[2].zones.Length + "×" + stages[2].holdSeconds + "秒・防衛 " + stages[2].finalDefenceSeconds + "秒・発見までの時間 x" + stages[2].humanDetectionScale + "）");
                Check(stages[1].foodGoal > stages[0].foodGoal && stages[1].fearGoal > stages[0].fearGoal && stages[1].holdPresence > stages[0].holdPresence && stages[1].holdSeconds > stages[0].holdSeconds
                    && stages[1].finalDefenceSeconds > stages[0].finalDefenceSeconds && stages[1].maxFoggers > stages[0].maxFoggers && stages[1].nestFumigations.Length > stages[0].nestFumigations.Length
                    && stages[1].humanDetectionScale < stages[0].humanDetectionScale && stages[1].difficulty > stages[0].difficulty && !stages[1].tutorial,
                    "ステージ2はすべてのゲージ・最終防衛でステージ1より難しい（食料 " + stages[1].foodGoal + "・恐怖 " + stages[1].fearGoal + "・区域 6×" + stages[1].holdPresence + "匹・防衛 " + stages[1].finalDefenceSeconds + "秒・発見までの時間 x" + stages[1].humanDetectionScale + "（ステージ1 x" + stages[0].humanDetectionScale + "））");

                EditorSceneManager.OpenScene(PhaseFiveGenerator.StageScene);
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "ステージ: 描画カメラ1台");
                var flicker = Coplanar("KitchenCabinet", "OpenRefrigerator");
                Check(flicker.Length == 0, "台所の家具に同じ面で重なってちらつく板がない" + (flicker.Length > 0 ? ": " + string.Join(" / ", flicker.Take(3)) : ""));
                Check(UnityEngine.Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1, "ステージ: 主人公1匹");
                Check(UnityEngine.Object.FindObjectsByType<AllyRoach>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 9, "ステージ: 仲間プール9匹");
                Check(UnityEngine.Object.FindObjectsByType<StageProgress>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<ColonyManager>(FindObjectsSortMode.None).Length == 1, "ステージ進行・コロニー管理を1つずつ");
                var guide = UnityEngine.Object.FindFirstObjectByType<StageTutorial>();
                Check(guide && new SerializedObject(UnityEngine.Object.FindFirstObjectByType<PhaseFiveHud>()).FindProperty("tutorial").objectReferenceValue == guide, "第1章チュートリアル（HUDが参照）");
                Check(stages[0].startAllies == 0 && stages[0].allyCorpseSeconds > 0 && stages[0].slipperReach > 2.9f && stages[0].slipperReach < 4.4f && stages[0].tutorial,
                    "ステージ1: 主人公1匹で開始・仲間は死ぬと戻らない・スリッパは天板（2.9）まで届き冷蔵庫の上（4.4）には届かない");
                Check(!UnityEngine.Object.FindFirstObjectByType<PhaseFourHud>() && UnityEngine.Object.FindFirstObjectByType<PhaseFiveHud>(), "HUDをPhase 5用に置換");
                var commander = UnityEngine.Object.FindFirstObjectByType<SwarmCommander>();
                Check(new SerializedObject(commander).FindProperty("hud").objectReferenceValue is PhaseFiveHud, "軍団モードのクリック判定はPhase 5 HUDを参照");
                var foggers = UnityEngine.Object.FindObjectsByType<SmokeFogger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(foggers.Length == PhaseFiveGenerator.FoggerPool + 1 && foggers.All(f => !f.gameObject.activeSelf && f.GetComponentsInChildren<Collider>(true).Length == 0), "燻煙剤3個＋巣用1個（待機・当たり判定なし）");
                var progress = UnityEngine.Object.FindFirstObjectByType<StageProgress>();
                var flags = (Transform[])typeof(StageProgress).GetField("flags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(progress);
                Check(flags.Length == 4 && flags.All(f => f && !f.gameObject.activeSelf), "縄張りの旗4本（占領まで非表示）");
                var path = new System.Collections.Generic.List<Vector3>();
                foreach (var zone in stages[0].zones)
                    Check(commander.Grid.Walkable(zone.centre) && commander.Grid.FindPath(commander.NestGround, zone.centre, path), "巣から「" + zone.name + "」へ仲間の経路あり");
                var session = UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>();
                Check(session.HasNestArea && session.NestArea.Contains(session.Nest.position + Vector3.up * 0.1f) && session.NestArea.Contains(session.Player.position + Vector3.up * 0.1f)
                    && new SerializedObject(commander).FindProperty("grid").objectReferenceValue == AssetDatabase.LoadAssetAtPath<AllyNavGrid>(PhaseFiveGenerator.GridPath),
                    "巣は流し台下の収納の中（主人公もそこから開始）・収納をくり抜いた床グリッド");
                Check(!GameObject.Find("KitchenCabinet/CabinetBody") && GameObject.Find("KitchenCabinet/OpenDoorHinge/FramedDoor") && !GameObject.Find("NestBeacon"), "収納の中は空洞、流し台下の扉が開く、光る目印なし");
                var piles = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(piles.Length >= 8 && piles.All(f => f.transform.Find("FoodModel") && !f.transform.Find("Crumb"))
                    && UnityEngine.Object.FindObjectsByType<PoisonBait>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(b => b.transform.Find("FoodModel")),
                    "食料と毒餌はBlenderモデル（立方体の仮表示なし）");
                Check(FoodModelIntegration.Names.All(n => AssetDatabase.LoadAssetAtPath<Mesh>(BlenderModelIntegration.Root + "/Meshes/" + n + ".asset") is Mesh m && m.vertexCount > 300 && m.colors.Length == m.vertexCount),
                    "食料5種のメッシュ（頂点カラー付き）");
                var lookSwap = UnityEngine.Object.FindFirstObjectByType<ExterminatorLook>();
                var swapSo = lookSwap ? new SerializedObject(lookSwap) : null;
                Check(swapSo != null && swapSo.FindProperty("renderers").arraySize == 3 && swapSo.FindProperty("exterminatorMeshes").GetArrayElementAtIndex(0).objectReferenceValue is Mesh ext
                    && ext.bindposes.Length == 19 && ext.vertexCount > 3000, "駆除業者モデル（住人と同じ19本の骨・3段階）を住人のスキンに用意");
                var lanceArm = session.Human.GetComponentInChildren<BlenderHumanAnimator>();
                Check(new SerializedObject(lanceArm).FindProperty("nozzleLocal").vector3Value.magnitude > 0.8f && UnityEngine.Object.FindFirstObjectByType<CeilingSprayer>(FindObjectsInactive.Include)
                    && new SerializedObject(UnityEngine.Object.FindFirstObjectByType<StageProgress>()).FindProperty("sprayer").objectReferenceValue is CeilingSprayer,
                    "駆除業者の噴霧器（左手のノズル先端・天井への予告と霧）");
                var stageViewer = UnityEngine.Object.FindFirstObjectByType<CodexViewer>();
                Check(stageViewer && stageViewer.Count == 10 && new SerializedObject(UnityEngine.Object.FindFirstObjectByType<PhaseFiveHud>()).FindProperty("codexViewer").objectReferenceValue == stageViewer, "ポーズの図鑑に食料5種・住人・駆除業者・母・息子・猫の回転台");
                var door = GameObject.Find("Phase5_Stage/BackDoor");
                Check(door && door.GetComponentsInChildren<Collider>().Length == 0, "人間が出ていく勝手口（当たり判定なし）");
                // The kitchen registers its NavMesh at runtime; add it briefly to sample the exit.
                var navigation = (UnityEngine.AI.NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
                var instance = UnityEngine.AI.NavMesh.AddNavMeshData(navigation);
                bool reachable = UnityEngine.AI.NavMesh.SamplePosition(stages[0].exitPoint, out var exit, 1.5f, UnityEngine.AI.NavMesh.AllAreas);
                var route = ((Transform[])typeof(HumanAIController).GetField("patrolPoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(session.Human));
                var walk = new UnityEngine.AI.NavMeshPath();
                bool allReachable = route.All(t => UnityEngine.AI.NavMesh.CalculatePath(session.Human.transform.position, t.position, UnityEngine.AI.NavMesh.AllAreas, walk) && walk.status == UnityEngine.AI.NavMeshPathStatus.PathComplete);
                instance.Remove();
                Check(reachable, "勝手口の前は人間の通り道（NavMesh）");
                Check(route.Length >= 8 && allReachable && route.Max(t => t.position.x) - route.Min(t => t.position.x) > 9 && route.Max(t => t.position.z) - route.Min(t => t.position.z) > 6
                    && route.Any(t => t.position.x < -4) && route.Any(t => t.position.x > 4), "人間の巡回地点は部屋全体に " + route.Length + "か所（すべて歩いて行ける）");

                VerifyStageTwo(stages[1]);
                VerifyStageThree(stages[2]);

                EditorSceneManager.OpenScene(PhaseFiveGenerator.TitleScene);
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "タイトル: 描画カメラ1台");
                Check(!UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>() && !UnityEngine.Object.FindFirstObjectByType<RoachMotor>() && !UnityEngine.Object.FindFirstObjectByType<HumanAIController>()
                    && !UnityEngine.Object.FindFirstObjectByType<SwarmCommander>() && !UnityEngine.Object.FindFirstObjectByType<AllyRoach>(FindObjectsInactive.Include)
                    && !UnityEngine.Object.FindFirstObjectByType<FoodItem>(FindObjectsInactive.Include) && !UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>(),
                    "タイトル: ゲーム処理のコンポーネントなし（見た目だけ）");
                var title = UnityEngine.Object.FindFirstObjectByType<TitleMenu>();
                Check(title && title.StageCount == 3 && UnityEngine.Object.FindFirstObjectByType<TitleBackdrop>(), "タイトルメニュー（ステージ3つ）と背景の動き");
                Check(new SerializedObject(title).FindProperty("codexViewer").objectReferenceValue is CodexViewer titleViewer && titleViewer.Count == 10, "タイトルの図鑑にも同じ回転台");
                Check(Path.GetFileNameWithoutExtension(PhaseFiveGenerator.TitleScene) == GameFlow.TitleScene && Path.GetFileNameWithoutExtension(PhaseFiveGenerator.StageScene) == stages[0].sceneName, "画面遷移のシーン名が一致");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/editmode.txt", "PASS: " + checks + " checks\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(Output + "/editmode-failure.txt", e.ToString()); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Visible faces of two boxes of the same furniture lying in one plane and facing the same way (they flicker:
        /// z-fighting). Walls, floors, ceilings, trim and skirting are left out (their overlaps sit inside the walls),
        /// as are faces that only the floor (bottom) or the wall behind (back) could show.
        /// </summary>
        private static string[] Coplanar(params string[] roots)
        {
            string[] shell = { "Wall", "Floor", "Ceiling", "Skirting", "Facing", "DoorTrim", "Threshold", "Collider" };
            var boxes = roots.Select(GameObject.Find).Where(r => r).SelectMany(r => r.GetComponentsInChildren<MeshRenderer>())
                .Where(r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh && r.GetComponent<MeshFilter>().sharedMesh.name.Contains("Box")
                    && !shell.Any(n => r.name.Contains(n)) && new[] { r.transform.right, r.transform.up, r.transform.forward }.All(d => Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z)) > 0.999f))
                .ToArray();
            var cover = roots.Select(GameObject.Find).Where(r => r).SelectMany(r => r.GetComponentsInChildren<MeshRenderer>()).Where(r => r.enabled).ToArray();
            var hits = new System.Collections.Generic.List<string>();
            for (int i = 0; i < boxes.Length; i++)
                for (int j = i + 1; j < boxes.Length; j++)
                {
                    Bounds a = boxes[i].bounds, b = boxes[j].bounds;
                    for (int axis = 0; axis < 3; axis++)
                        for (int side = 0; side < 2; side++)
                        {
                            if (axis == 1 && side == 0 || axis == 2 && side == 1) continue;   // bottom on the floor, back to the wall
                            float pa = side == 0 ? a.min[axis] : a.max[axis], pb = side == 0 ? b.min[axis] : b.max[axis];
                            if (Mathf.Abs(pa - pb) > 0.004f) continue;
                            int u = (axis + 1) % 3, v = (axis + 2) % 3;
                            float ou = Mathf.Min(a.max[u], b.max[u]) - Mathf.Max(a.min[u], b.min[u]), ov = Mathf.Min(a.max[v], b.max[v]) - Mathf.Max(a.min[v], b.min[v]);
                            if (ou <= 0.02f || ov <= 0.02f || ou * ov <= 0.004f) continue;
                            // Just outside the shared face: inside another part (the side panel, the worktop) it cannot be seen.
                            Vector3 probe = Vector3.zero;
                            probe[axis] = pa + (side == 0 ? -0.01f : 0.01f);
                            probe[u] = (Mathf.Max(a.min[u], b.min[u]) + Mathf.Min(a.max[u], b.max[u])) / 2;
                            probe[v] = (Mathf.Max(a.min[v], b.min[v]) + Mathf.Min(a.max[v], b.max[v])) / 2;
                            if (cover.Any(c => c != boxes[i] && c != boxes[j] && c.bounds.Contains(probe))) continue;
                            hits.Add(boxes[i].transform.parent.name + "/" + boxes[i].name + "×" + boxes[j].name + " " + "xyz"[axis]);
                        }
                }
            return hits.Distinct().ToArray();
        }

        private static void VerifyStageTwo(StageDefinition stage)
        {
            EditorSceneManager.OpenScene(StageTwoGenerator.ScenePath);
            Check(Path.GetFileNameWithoutExtension(StageTwoGenerator.ScenePath) == stage.sceneName && new SerializedObject(UnityEngine.Object.FindFirstObjectByType<StageProgress>()).FindProperty("stage").objectReferenceValue == AssetDatabase.LoadAssetAtPath<StageDefinition>(PhaseFiveGenerator.StageAssets[1]), "ステージ2のシーン名が定義と一致・進行がステージ2の定義を参照");
            Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1, "ステージ2: カメラ1台・主人公1匹");
            var flicker = Coplanar("KitchenCabinet", "OpenRefrigerator", "LivingRoom", "Bedroom", "KidsRoom");
            Check(flicker.Length == 0, "一軒家の家具に同じ面で重なってちらつく板がない" + (flicker.Length > 0 ? ": " + string.Join(" / ", flicker.Take(3)) : ""));
            var session = UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>();
            var humans = UnityEngine.Object.FindObjectsByType<HumanAIController>(FindObjectsSortMode.None);
            Check(humans.Length == 3 && session.Family.Count == 2 && session.Human.Label == "父" && session.Family[0].Label == "母" && session.Family[1].Label == "息子"
                && session.Human.Slaps && session.Family[0].Slaps && !session.Family[1].Slaps, "ステージ2: 人間3人（父・母はスリッパ、息子は網のみ）");
            var mesh = (Func<HumanAIController, Mesh>)(h => h.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh).FirstOrDefault(m => m && m.name.EndsWith("_Detail0")));
            Check(mesh(session.Human).name == "Resident_Detail0" && mesh(session.Family[0]).name == "Mother_Detail0" && mesh(session.Family[1]).name == "Son_Detail0"
                && mesh(session.Family[0]).bindposes.Length == 19 && mesh(session.Family[1]).vertexCount > 3000, "家族のモデル: 父は住人、母と息子は新しいBlenderモデル（同じ19本の骨）");
            Check(session.Family[1].GetComponentInChildren<SkinnedMeshRenderer>(true).transform.parent.localScale.x < 0.8f, "息子は小さい（0.72倍）");
            Check(!session.Family.Any(m => m.GetComponentInChildren<ExterminatorLook>(true)) && session.Human.GetComponentInChildren<ExterminatorLook>(true), "駆除業者に変わるのは父だけ");
            var vacuum = session.Family[0].GetComponent<VacuumAttack>(); var net = session.Family[1].GetComponent<BugNetAttack>();
            Check(vacuum && net && net.Cage && UnityEngine.Object.FindFirstObjectByType<BugCage>(), "母に掃除機・息子に虫取り網と虫かご");
            Check(session.Family.All(m => new SerializedObject(m.GetComponentInChildren<BlenderHumanAnimator>()).FindProperty("nozzleLocal").vector3Value.magnitude > 0.8f), "掃除機と網は左手の先（狙える）");
            var warnings = humans.Select(h => new SerializedObject(h).FindProperty("warning").objectReferenceValue).Distinct().Count();
            Check(warnings == 3, "スリッパの予告の輪は1人に1つ");
            var commander = UnityEngine.Object.FindFirstObjectByType<SwarmCommander>();
            var grid = commander.Grid;
            Check(new SerializedObject(commander).FindProperty("grid").objectReferenceValue == AssetDatabase.LoadAssetAtPath<AllyNavGrid>(StageTwoGenerator.GridPath) && grid.Width == StageTwoGenerator.GridWidth,
                "家全体の床グリッド（" + grid.Width + "×" + grid.Depth + "）");
            var path = new System.Collections.Generic.List<Vector3>();
            foreach (var zone in stage.zones)
                Check(grid.Walkable(zone.centre) && grid.FindPath(commander.NestGround, zone.centre, path), "巣から「" + zone.name + "」へ仲間の経路あり");
            Check(grid.Walkable(new Vector3(7.12f, 0, -3.5f)) && !grid.Walkable(new Vector3(7.12f, 0, 0)) && grid.FindPath(commander.NestGround, new Vector3(19f, 0, -0.5f), path), "台所の巣からリビングの奥まで仲間が歩ける（壁は戸口だけ通れる）");
            Check(commander.Foods.Count(f => f.transform.position.x > 7.3f && f.transform.position.z < 6) == 9 && commander.Foods.Count(f => f.transform.position.z > 6.3f) == 9
                && commander.Foods.Count(f => f.name.StartsWith("FoodMarker_Life_")) == 4, "食料はリビングに9つ・寝室と子ども部屋に9つ・家族が落とす4つ（計" + commander.Foods.Count + "）");
            Check(commander.ScoutPoints.Count(p => p.x > 7.3f && p.z < 6) == 8 && commander.ScoutPoints.Count(p => p.z > 6.3f) == 8 && commander.ScoutPoints.All(p => grid.Walkable(p)), "調査地点: リビング8つ・奥の2部屋8つ");
            Check(GameObject.Find("Room/Bedroom/DoubleBed") && GameObject.Find("Room/Bedroom/WorkDesk") && GameObject.Find("Room/Bedroom/Wardrobe") && GameObject.Find("Room/KidsRoom/KidsBed")
                && GameObject.Find("Room/KidsRoom/StudyDesk") && GameObject.Find("Room/KidsRoom/GameCorner") && GameObject.Find("Room/DiningChair_Mother") && !GameObject.Find("Room/LivingRoom/NightGlass"),
                "奥に寝室（ダブルベッド・机・タンス）と子ども部屋（ベッド・勉強机・ゲーム）、食卓に3つ目の椅子");
            Check(grid.Depth == StageTwoGenerator.GridDepth && grid.Walkable(new Vector3(5.7f, 0, 6.12f)) && grid.Walkable(new Vector3(11.6f, 0, 6.12f)) && grid.Walkable(new Vector3(7.12f, 0, 9.0f))
                && !grid.Walkable(new Vector3(2.0f, 0, 6.12f)) && !grid.Walkable(new Vector3(7.12f, 0, 12.5f)) && grid.Walkable(new Vector3(-1.0f, 0, 12.6f)) && grid.Walkable(new Vector3(19.4f, 0, 12.6f)),
                "奥の2部屋への戸口3つ（台所→寝室・リビング→子ども部屋・2部屋の間）、ベッドの下に入れる");
            Check(Mathf.Approximately(stage.calmSeconds, 30) && Mathf.Approximately(stage.calmBelowFear, 25) && Mathf.Approximately(stage.fearDecayPerSecond, 1) && stage.allyFearCap == 1
                && stage.heatArea.yMax >= StageTwoGenerator.NorthMaxZ && stage.description == StageTwoGenerator.Description,
                "ステージ2の定義: 4部屋・恐怖は毎秒1下がる・見える仲間の恐怖は1匹分まで・恐怖25未満で30秒何も見なければ警戒が下がる・罠の記録は家全体");
            var desk = stage.zones[7];
            bool underDesk = true;
            for (int k = 0; k <= 8; k++)
            {
                Vector3 p = desk.centre + (k == 8 ? Vector3.zero : Quaternion.Euler(0, k * 45, 0) * Vector3.forward * desk.radius) + Vector3.up * 0.15f;
                underDesk &= Physics.Raycast(p, Vector3.up, out var roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore) && roof.transform.IsChildOf(GameObject.Find("Room/KidsRoom/StudyDesk").transform);
            }
            Check(desk.name == "勉強机の下" && underDesk, "「勉強机の下」の区域は全体が机の下（人間・猫から見えない）");
            Check(!GameObject.Find("Phase5_Stage/BackDoor") && GameObject.Find("Phase5_Stage/HallDoor") && GameObject.Find("Room/LivingRoom/Sofa") && GameObject.Find("Room/LivingRoom/TvBoard"),
                "勝手口はリビングへの戸口に。リビング（ソファ・テレビ台）と廊下へのドア");
            Check(GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Wall_Right" && t.parent && t.parent.name == "Room") == 3, "台所の右の壁は戸口の両側と上の3枚");
            var cat = UnityEngine.Object.FindFirstObjectByType<PetCat>();
            var catLink = cat ? new SerializedObject(cat) : null;
            var catGrid = catLink != null ? catLink.FindProperty("grid").objectReferenceValue as AllyNavGrid : null;
            Check(cat && cat.GetComponentInChildren<CatAnimator>() && cat.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh && r.sharedMesh.name == "Cat_Detail0" && r.sharedMesh.vertexCount > 3000)
                && GameObject.Find("Room/LivingRoom/CatBed"), "飼い猫（Blenderモデル・専用の骨格と動き）と寝床");
            Check(catGrid == AssetDatabase.LoadAssetAtPath<AllyNavGrid>(StageTwoGenerator.CatGridPath) && catGrid.Walkable(StageTwoGenerator.CatBed) && !catGrid.Walkable(new Vector3(15.5f, 0, 4.45f))
                && !catGrid.Walkable(session.Nest.position) && catGrid.Walkable(new Vector3(7.12f, 0, -3.5f)) && catGrid.FindPath(StageTwoGenerator.CatBed, new Vector3(-4.0f, 0, 0.6f), path),
                "猫の床: 寝床から台所まで歩ける・ソファの下と巣には入れない");
            Check(!catGrid.Walkable(new Vector3(-1.0f, 0, 12.6f)) && catGrid.Walkable(new Vector3(9.5f, 0, 9.0f)) && catGrid.FindPath(StageTwoGenerator.CatBed, new Vector3(-3.0f, 0, 8.4f), path)
                && Mathf.Approximately(catLink.FindProperty("area").rectValue.yMax, 16f), "猫は奥の部屋も歩く・ベッドの下には入れない");
            var life = UnityEngine.Object.FindFirstObjectByType<HouseholdLife>();
            var lifeLink = life ? new SerializedObject(life) : null;
            var spots = life ? (HouseholdLife.Spot[])typeof(HouseholdLife).GetField("spots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(life) : new HouseholdLife.Spot[0];
            Check(life && spots.Length == 14 && spots.Count(s => s.member == 0) == 5 && spots.Count(s => s.member == 1) == 5 && spots.Count(s => s.member == 2) == 4
                && spots.Count(s => s.kind == HouseholdLife.Kind.Dinner) == 3 && spots.Select(s => s.member * 100 + (int)s.kind).Distinct().Count() == 14,
                "家族の生活: 14か所（父5・母5・息子4、夕食は3人）");
            Check(lifeLink.FindProperty("crumbs").arraySize == 2 && lifeLink.FindProperty("leftovers").arraySize == 2 && lifeLink.FindProperty("vacuumRoute").arraySize >= 8
                && ((Transform)lifeLink.FindProperty("newspaper").objectReferenceValue).parent == session.Human.transform && !((Transform)lifeLink.FindProperty("newspaper").objectReferenceValue).gameObject.activeSelf
                && ((Transform)lifeLink.FindProperty("gamepad").objectReferenceValue).parent == session.Family[1].transform, "食べこぼし2・食べ残し2・掃除の経路・父の新聞と息子のコントローラー（持つ時だけ表示）");
            var nestDoor = GameObject.Find("KitchenCabinet").transform.TransformPoint(new Vector3(-0.45f, 0, -1.45f));
            var wash = spots.First(s => s.kind == HouseholdLife.Kind.Wash);
            Check(Vector3.ProjectOnPlane(wash.stand - nestDoor, Vector3.up).magnitude >= 1.0f, "皿洗いの母は巣の扉をふさがない（" + Vector3.ProjectOnPlane(wash.stand - nestDoor, Vector3.up).magnitude.ToString("F2") + "m）");
            var navigation = (UnityEngine.AI.NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            Check(navigation == AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(StageTwoGenerator.NavPath), "家全体の人間用NavMesh");
            var instance = UnityEngine.AI.NavMesh.AddNavMeshData(navigation);
            try
            {
                var walk = new UnityEngine.AI.NavMeshPath();
                bool exit = UnityEngine.AI.NavMesh.SamplePosition(stage.exitPoint, out var door, 1.5f, UnityEngine.AI.NavMesh.AllAreas);
                Check(exit && humans.All(h => UnityEngine.AI.NavMesh.CalculatePath(h.transform.position, door.position, UnityEngine.AI.NavMesh.AllAreas, walk) && walk.status == UnityEngine.AI.NavMeshPathStatus.PathComplete),
                    "3人とも廊下へのドアまで歩ける（避難・撤退）");
                foreach (var h in humans)
                {
                    var route = (Transform[])typeof(HumanAIController).GetField("patrolPoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(h);
                    Check(route.Length >= 8 && route.All(t => UnityEngine.AI.NavMesh.CalculatePath(h.transform.position, t.position, UnityEngine.AI.NavMesh.AllAreas, walk) && walk.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        && route.Any(t => t.position.x > 8) && route.Any(t => t.position.x < 6) && route.Any(t => t.position.z > 6.3f), h.Label + "の巡回 " + route.Length + "か所（台所・リビング・奥の部屋・すべて歩ける）");
                }
                foreach (var s in spots)
                {
                    var who = session.HumanAt(s.member);
                    Check(UnityEngine.AI.NavMesh.CalculatePath(who.transform.position, s.stand, UnityEngine.AI.NavMesh.AllAreas, walk) && walk.status == UnityEngine.AI.NavMeshPathStatus.PathComplete,
                        who.Label + "の「" + HouseholdLife.Name(s.kind) + "」の場所まで歩ける");
                }
                var lifeRoute = (Vector3[])typeof(HouseholdLife).GetField("vacuumRoute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(life);
                Check(lifeRoute.All(p => UnityEngine.AI.NavMesh.CalculatePath(session.Family[0].transform.position, p, UnityEngine.AI.NavMesh.AllAreas, walk) && walk.status == UnityEngine.AI.NavMeshPathStatus.PathComplete), "掃除の経路 " + lifeRoute.Length + "か所はすべて歩ける");
            }
            finally { instance.Remove(); }
        }

        public static void VerifyPlayMode() => Play(PhaseFiveGenerator.TitleScene, "phase5", Output);
        public static void VerifyStageTwoPlay() => Play(StageTwoGenerator.ScenePath, "stage2", Output + "/stage2");
        public static void VerifyStageThreePlay() => Play(StageThreeGenerator.ScenePath, "stage3", Output + "/stage3");
        // Shared scripts changed again: rerun the earlier suites on their own scenes.
        public static void VerifyPhaseFourRegression() => Play(PhaseFourGenerator.ScenePath, "phase4", Output + "/regression-phase4");
        public static void VerifyPhaseThreeRegression() => Play(PhaseThreeGenerator.ScenePath, "phase3", Output + "/regression-phase3");
        public static void VerifyBlenderRegression() => Play(BlenderModelIntegration.ScenePath, "phase2", Output + "/regression-blender");
        public static void VerifyPhaseTwoRegression() => Play(PhaseTwoGenerator.ScenePath, "phase2", Output + "/regression-phase2");

        private static void Play(string scene, string suite, string output)
        {
            Directory.CreateDirectory(output);
            output = Path.GetFullPath(output);
            string settingsFile = Path.Combine(output, "settings-test.json");
            if (File.Exists(settingsFile)) File.Delete(settingsFile);
            if (File.Exists(settingsFile + ".bak")) File.Delete(settingsFile + ".bak");
            EditorSceneManager.OpenScene(scene);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Active + ".Suite", suite);
            SessionState.SetString(Active + ".Output", output);
            // Tests never touch the player's own settings or save.
            SessionState.SetString(PhaseFourVerification.SettingsKey, settingsFile);
            SessionState.SetString("GokiTakeover.Phase5.Isolate", output);
            SessionState.SetString(Active + ".Start", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Active + ".Start", ""))).TotalSeconds > 600)
            { SessionState.SetBool(Active, false); Debug.LogError("Phase 5テストタイムアウト"); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.unscaledTime < 0.4f) return;   // unscaled: a stage opens paused on its briefing
            SessionState.SetBool(Active, false);
            string output = SessionState.GetString(Active + ".Output", Output);
            Action<bool> done = passed => { EditorApplication.isPlaying = false; EditorApplication.Exit(passed ? 0 : 1); };
            switch (SessionState.GetString(Active + ".Suite", ""))
            {
                case "phase2": { var r = new GameObject("Phase2_Regression").AddComponent<PhaseTwoScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                case "phase3": { var r = new GameObject("Phase3_Regression").AddComponent<PhaseThreeScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                case "phase4": { var r = new GameObject("Phase4_Regression").AddComponent<PhaseFourScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                case "stage2": { var r = new GameObject("Stage2_Verification").AddComponent<StageTwoScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                case "stage3": { var r = new GameObject("Stage3_Verification").AddComponent<StageThreeScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
                default: { var r = new GameObject("Phase5_Verification").AddComponent<PhaseFiveScenarioTest>(); r.OutputFolder = output; r.Completed += done; break; }
            }
        }

        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks++; Debug.Log("[PHASE5 EDIT] PASS: " + label); }
    }
}
