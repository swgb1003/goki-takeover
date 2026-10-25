using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Phase 5: two new scenes built from copies of the Phase 4 kitchen. The stage adds the stage definition,
    /// territory zones with flags, the exterminator's foggers, a back door for the resident to leave by, the
    /// stage flow and the Phase 5 HUD. The title is the same kitchen with every gameplay component removed,
    /// a fixed low camera and a few wandering roaches. Earlier scenes and assets are not modified.
    /// </summary>
    public static class PhaseFiveGenerator
    {
        public const string StageScene = GeneratedAssets.Root + "/Scenes/Phase5_Stage01_Kitchen.unity";
        public const string TitleScene = GeneratedAssets.Root + "/Scenes/Phase5_Title.unity";
        public const string StageFolder = GeneratedAssets.Root + "/Data/Stages";
        public const string DiscPath = GeneratedAssets.Root + "/Art/Meshes/Phase5_Disc.asset";
        // One playable build, overwritten in place by every update (no new folder per version).
        public const string ReleaseBuild = "Build/GokiTakeover/GokiTakeover.exe";
        public const string NormalBuild = ReleaseBuild;
        public const string ValidationBuild = "Logs/ValidationPlayer/GokiTakeover.exe";   // Development test player, kept out of Build/ (overwritten; Unity refuses Library/)
        public const string PlayerGuide = "docs/遊び方.txt";
        public const int FoggerPool = 3;
        public const string GridPath = GeneratedAssets.Root + "/Data/Phase5AllyGrid.asset";   // the kitchen with the hollow sink cabinet
        // The nest (2026-10-24 revision): inside the cabinet under the sink, entered through a door left ajar.
        public const float DoorOpenDegrees = 60;
        public static readonly Vector3 NestLocalCentre = new Vector3(0, 0, -0.04f);   // in KitchenCabinet space, on the floor
        public static readonly Vector3 NestAreaSize = new Vector3(5.7f, 1.2f, 1.85f);
        public static readonly Vector3 NestRefuge = new Vector3(-2.25f, 0, -0.9f);   // under the table, while the nest is smoked
        // The resident's patrol stops all over the room (the Phase 2 route covered only the right half).
        public static readonly Vector3[] RoomPatrol =
        {
            new Vector3(3.6f, 0, -3.8f), new Vector3(5.2f, 0, -0.8f), new Vector3(4.2f, 0, 2.9f), new Vector3(2.0f, 0, 2.9f), new Vector3(-1.8f, 0, 2.6f),
            new Vector3(-3.8f, 0, 2.4f), new Vector3(-5.9f, 0, 0.4f), new Vector3(-5.4f, 0, -3.9f), new Vector3(-2.2f, 0, -4.7f), new Vector3(0.6f, 0, -3.4f)
        };
        public static readonly string[] StageAssets = { StageFolder + "/Stage01_Kitchen.asset", StageFolder + "/Stage02_House.asset", StageFolder + "/Stage03_Diner.asset" };
        public static readonly string[] ZoneNames = { "テーブルの下", "冷蔵庫の前", "流し台の前", "食品棚のそば" };
        public static readonly Vector3[] ZoneCentres = { new Vector3(-2.3f, 0, -0.9f), new Vector3(-3.9f, 0, 2.7f), new Vector3(-0.9f, 0, 3.0f), new Vector3(6.0f, 0, -0.6f) };
        public static readonly float[] ZoneRadii = { 1.5f, 1.5f, 1.5f, 1.4f };
        // Title: the camera sits low behind the hero and looks across the table toward the glowing fridge.
        public static readonly Vector3 TitleCamera = new Vector3(1.3f, 0.62f, -4.9f), TitleLookAt = new Vector3(-3.0f, 1.35f, 2.6f);
        public static readonly Vector3 TitleHero = new Vector3(-0.55f, 0.04f, -2.55f);

        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 5 Takeover", priority = 29)]
        public static void OpenMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(StageScene) || !File.Exists(TitleScene)) Generate();
            EditorSceneManager.OpenScene(TitleScene);
        }
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 5 Takeover", priority = 30)]
        public static void RebuildMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Generate(); EditorSceneManager.OpenScene(TitleScene); } }
        public static void Build() => BuildRelease();
        // Every scene the game runs: the title, Stage 1 and (2026-10-25) Stage 2.
        public static string[] GameScenes => new[] { TitleScene, StageScene, StageTwoGenerator.ScenePath };
        public static void BuildValidation() => PhaseOneBuild.BuildScenes(GameScenes, ValidationBuild, BuildOptions.Development);
        public static void BuildBoth() { BuildValidation(); Build(); }
        public const string ProbeBuild = "Build/GokiTakeover_RenderProbe/GokiTakeover.exe";
        /// <summary>Development build with GPU frame timing on, for RenderCostProbe ("-goki-render-probe"). Project settings are restored.</summary>
        public static void BuildProbe()
        {
            bool timing = PlayerSettings.enableFrameTimingStats;
            try { PlayerSettings.enableFrameTimingStats = true; PhaseOneBuild.BuildScenes(GameScenes, ProbeBuild, BuildOptions.Development); }
            finally { PlayerSettings.enableFrameTimingStats = timing; }
        }
        /// <summary>Release build (no development code) with the player's guide next to the exe.</summary>
        [MenuItem("Tools/GOKI TAKEOVER/Build Game (Build/GokiTakeover)", priority = 38)]
        public static void BuildRelease()
        {
            PhaseOneBuild.BuildScenes(GameScenes, ReleaseBuild, BuildOptions.None, FullScreenMode.FullScreenWindow);
            File.Copy(PlayerGuide, Path.Combine(Path.GetDirectoryName(ReleaseBuild), Path.GetFileName(PlayerGuide)), true);
        }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 5 Takeover", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 5 Takeover", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Build Game (Build/GokiTakeover)", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate()
        {
            if (!File.Exists(PhaseFourGenerator.ScenePath)) throw new InvalidOperationException("先にPhase 4シーンを生成してください。");
            var stages = Stages();
            GenerateStage(stages[0]);
            StageTwoGenerator.Generate(stages[1]);   // a copy of Stage 1 grown into the family house
            GenerateTitle(Stages());
            AssetDatabase.SaveAssets();
        }

        // ---------------- Stage definitions ----------------
        // Created once with provisional values; later generations keep whatever was tuned in the Inspector.
        public static StageDefinition[] Stages()
        {
            GeneratedAssets.Folder(StageFolder);
            var result = new StageDefinition[StageAssets.Length];
            for (int i = 0; i < StageAssets.Length; i++)
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StageAssets[i]);
                if (!stage)
                {
                    GeneratedAssets.RequireOwned(StageAssets[i]);
                    stage = ScriptableObject.CreateInstance<StageDefinition>();
                    Fill(stage, i);
                    AssetDatabase.CreateAsset(stage, StageAssets[i]); GeneratedAssets.Mark(stage);
                }
                result[i] = stage;
            }
            return result;
        }
        private static void Fill(StageDefinition stage, int index)
        {
            switch (index)
            {
                case 0:
                    var zones = new StageDefinition.Zone[ZoneNames.Length];
                    for (int i = 0; i < zones.Length; i++) zones[i] = new StageDefinition.Zone { name = ZoneNames[i], centre = ZoneCentres[i], radius = ZoneRadii[i] };
                    stage.zones = zones;
                    break;
                case 1:
                    stage.id = "stage02"; stage.title = "一軒家"; stage.subtitle = "4人家族の家"; stage.difficulty = 2; stage.residents = 4;
                    stage.description = "家族が暮らす広い家。侵入ルートは多いが警戒も強い。"; stage.playable = false;
                    stage.unlocks = "stage03"; stage.unlocksTitle = "飲食店"; stage.reward = ""; stage.rewardTitle = ""; stage.sceneName = "";
                    break;
                default:
                    stage.id = "stage03"; stage.title = "飲食店"; stage.subtitle = "夜の定食屋"; stage.difficulty = 3; stage.residents = 3;
                    stage.description = "食べ物の宝庫。だが、人の出入りも多く危険。"; stage.playable = false;
                    stage.unlocks = ""; stage.unlocksTitle = ""; stage.reward = ""; stage.rewardTitle = ""; stage.sceneName = "";
                    break;
            }
        }

        // ---------------- Stage scene ----------------
        public static void GenerateStage(StageDefinition stage)
        {
            GeneratedAssets.RequireOwned(StageScene);
            var scene = EditorSceneManager.OpenScene(PhaseFourGenerator.ScenePath);
            if (!EditorSceneManager.SaveScene(scene, StageScene)) throw new IOException("Phase 5ステージを保存できません。");
            LoadMeshes();
            var session = UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>();
            var commander = UnityEngine.Object.FindFirstObjectByType<SwarmCommander>();
            var colony = UnityEngine.Object.FindFirstObjectByType<ColonyManager>();
            UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindFirstObjectByType<PhaseFourHud>());
            var root = new GameObject("Phase5_Stage").transform;
            var foodArt = FoodModelIntegration.Import();
            DressFoods(foodArt);
            BuildCabinetNest(session, commander);
            RoomWidePatrol(session.Human);
            var exterminator = ExterminatorModelIntegration.Import();
            var residentMeshes = ExterminatorModelIntegration.ResidentMeshes();
            var skins = session.Human.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => Array.IndexOf(residentMeshes, r.sharedMesh) >= 0).OrderBy(r => Array.IndexOf(residentMeshes, r.sharedMesh)).ToArray();
            if (skins.Length != 3) throw new InvalidOperationException("住人のスキン（3段階）が見つかりません");
            var look = skins[0].transform.parent.gameObject.AddComponent<ExterminatorLook>();   // the scene is a fresh copy of Phase 4
            look.Configure(skins, residentMeshes, exterminator);
            // The lance tip in left-hand space, from the rest pose (bones are at rest in the Editor).
            var animator = session.Human.GetComponentInChildren<BlenderHumanAnimator>();
            var leftHand = (Transform)new SerializedObject(animator).FindProperty("leftHand").objectReferenceValue;
            animator.ConfigureLance(leftHand.InverseTransformPoint(skins[0].transform.parent.TransformPoint(ExterminatorModelIntegration.NozzleTip())));
            EditorUtility.SetDirty(animator);
            var warning = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Phase2_Warning.mat");
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + "/Art/Meshes/Phase2_Ring.asset");

            var flags = BuildTerritory(stage, root, commander);

            // Foggers: warning ring and filling disc on the floor, a low translucent smoke dome, the can.
            var fill = GeneratedAssets.Material("Phase5_FoggerFill", new Color(0.62f, 0.28f, 0.08f));
            var smoke = SmokeMaterial();
            var canMaterial = GeneratedAssets.Material("Phase5_FoggerCan", new Color(0.34f, 0.42f, 0.5f));
            var foggersRoot = GeneratedAssets.Empty("Foggers", root, Vector3.zero);
            var foggers = new SmokeFogger[FoggerPool + 1];   // the last one is the exterminator's fogger for the nest
            for (int i = 0; i <= FoggerPool; i++)
            {
                var holder = GeneratedAssets.Empty(i < FoggerPool ? "Fogger_" + i : "NestFogger", foggersRoot, Vector3.zero);
                var r = P.Part("WarningRing", holder, ring, warning, new Vector3(0, 0.024f, 0), Vector3.one, false, false);
                var f = P.Part("WarningFill", holder, Disc(), fill, new Vector3(0, 0.016f, 0), Vector3.one, false, false);
                var dome = P.Part("Smoke", holder, P.SphereMesh, smoke, Vector3.zero, Vector3.one, false, false);
                dome.GetComponent<MeshRenderer>().receiveShadows = false;
                var can = GeneratedAssets.Empty("Can", holder, Vector3.zero);
                P.Rod("Body", can, Vector3.zero, new Vector3(0, 0.34f, 0), 0.17f, canMaterial);
                P.Rod("Cap", can, new Vector3(0, 0.34f, 0), new Vector3(0, 0.41f, 0), 0.11f, warning);
                foggers[i] = holder.gameObject.AddComponent<SmokeFogger>();
                foggers[i].Configure(r, f, dome, can);
                holder.gameObject.SetActive(false);
            }

            // Back door on the right wall: where the resident leaves when the takeover succeeds.
            var door = GeneratedAssets.Material("Phase5_Door", new Color(0.34f, 0.21f, 0.12f));
            var frame = GeneratedAssets.Material("Phase5_DoorFrame", new Color(0.78f, 0.74f, 0.64f));
            var exit = GeneratedAssets.Empty("BackDoor", root, new Vector3(6.97f, 0, stage.exitPoint.z));
            P.Box("Panel", exit, new Vector3(0, 2.15f, 0), new Vector3(0.05f, 4.3f, 1.9f), door, false);
            P.Box("FrameLeft", exit, new Vector3(-0.01f, 2.2f, -1.0f), new Vector3(0.08f, 4.4f, 0.12f), frame, false);
            P.Box("FrameRight", exit, new Vector3(-0.01f, 2.2f, 1.0f), new Vector3(0.08f, 4.4f, 0.12f), frame, false);
            P.Box("FrameTop", exit, new Vector3(-0.01f, 4.38f, 0), new Vector3(0.08f, 0.12f, 2.12f), frame, false);
            P.Sphere("Handle", exit, new Vector3(-0.08f, 2.0f, 0.72f), Vector3.one * 0.14f, frame);

            var progress = root.gameObject.AddComponent<StageProgress>();
            progress.Configure(session, commander, colony, stage, foggers.Take(FoggerPool).ToArray(), flags);
            // The nest smoking: smoke filling the hollow cabinet, the fogger in its door, where the exterminator
            // stands to set it, and a refuge under the table (the slipper cannot reach under it).
            var counter = GameObject.Find("KitchenCabinet").transform;
            var nestSmoke = P.Box("NestSmoke", counter, NestLocalCentre + new Vector3(0, 1.22f, 0), new Vector3(5.72f, 2.42f, 1.86f), smoke, false);
            nestSmoke.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            nestSmoke.GetComponent<MeshRenderer>().receiveShadows = false;
            nestSmoke.gameObject.SetActive(false);
            Vector3 nestDoor = counter.TransformPoint(new Vector3(-0.45f, 0, -1.45f)); nestDoor.y = 0;
            Vector3 approach = counter.TransformPoint(new Vector3(0.6f, 0, -2.9f)); approach.y = 0;
            progress.ConfigureNestSmoking(foggers[FoggerPool], nestSmoke.gameObject, nestDoor, approach, NestRefuge);
            // The backpack sprayer: a ring and a filling disc on the struck surface, the mist cone, the wet splash.
            var sprayRoot = GeneratedAssets.Empty("CeilingSprayer", root, Vector3.zero);
            var sprayMist = SprayMistMaterial();
            var sprayRing = P.Part("WarningRing", sprayRoot, ring, warning, Vector3.zero, Vector3.one, false, false);
            var sprayFill = P.Part("WarningFill", sprayRoot, Disc(), fill, Vector3.zero, Vector3.one, false, false);
            var cone = P.Part("Mist", sprayRoot, SprayCone(), sprayMist, Vector3.zero, Vector3.one, false, false);
            var splash = P.Part("Splash", sprayRoot, Disc(), sprayMist, Vector3.zero, Vector3.one, false, false);
            foreach (var t in new[] { sprayRing, sprayFill, cone, splash }) { t.GetComponent<MeshRenderer>().receiveShadows = false; t.gameObject.SetActive(false); }
            var sprayer = sprayRoot.gameObject.AddComponent<CeilingSprayer>();
            sprayer.Configure(session, animator, sprayRing, sprayFill, cone, splash);
            progress.ConfigureSprayer(sprayer);
            var tutorial = root.gameObject.AddComponent<StageTutorial>();
            tutorial.Configure(session, commander, colony, progress);
            var traps = UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            var hud = root.gameObject.AddComponent<PhaseFiveHud>();
            hud.Configure(session, commander, colony, progress, traps, tutorial);
            hud.ConfigureCodex(CodexViewer(root.gameObject, foodArt));
            var link = new SerializedObject(commander);
            link.FindProperty("hud").objectReferenceValue = hud;
            link.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.AddComponent<GameAudio>().Configure(session, commander, colony, progress);   // Phase 6 sound

            EditorSceneManager.SaveScene(scene, StageScene);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(StageScene));
            Debug.Log("[GOKI PHASE5] ステージ生成完了: " + StageScene);
        }

        // ---------------- Title scene ----------------
        public static void GenerateTitle(StageDefinition[] stages)
        {
            GeneratedAssets.RequireOwned(TitleScene);
            var scene = EditorSceneManager.OpenScene(PhaseFourGenerator.ScenePath);
            if (!EditorSceneManager.SaveScene(scene, TitleScene)) throw new IOException("タイトルシーンを保存できません。");
            var player = UnityEngine.Object.FindFirstObjectByType<RoachMotor>().gameObject;
            var cameraObject = UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>().gameObject;
            var allies = UnityEngine.Object.FindObjectsByType<AllyRoach>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(allies, (a, b) => string.CompareOrdinal(a.name, b.name));
            var bodies = Array.ConvertAll(allies, a => a.transform);
            LoadMeshes();
            var foodArt = FoodModelIntegration.Import();
            ExterminatorModelIntegration.Import();
            DressFoods(foodArt);
            BuildCabinetNest(null, null);
            foreach (var food in UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var beacon = food.transform.Find("FoodBeacon");
                if (beacon) UnityEngine.Object.DestroyImmediate(beacon.gameObject);
                var value = food.GetComponent<FoodValue>();
                if (value) UnityEngine.Object.DestroyImmediate(value);
                UnityEngine.Object.DestroyImmediate(food);
            }
            // Nothing of the game runs here: drop the gameplay roots and components, keep the art.
            foreach (string name in new[] { "Phase2_Gameplay", "Phase4_Colony" }) Kill(GameObject.Find(name));
            Kill(UnityEngine.Object.FindFirstObjectByType<PhaseFourHud>());
            Kill(UnityEngine.Object.FindFirstObjectByType<SwarmCommander>());
            Kill(player.GetComponent<PlayerInteractor>());
            Kill(player.GetComponent<RoachMotor>());
            Kill(player.GetComponent<CharacterController>());
            Kill(cameraObject.GetComponent<ThirdPersonRoachCamera>());
            Kill(cameraObject.GetComponent<SwarmViewCamera>());
            foreach (var ally in allies) UnityEngine.Object.DestroyImmediate(ally);

            player.transform.SetPositionAndRotation(TitleHero, Quaternion.Euler(0, -58, 0));
            cameraObject.transform.SetPositionAndRotation(TitleCamera, Quaternion.LookRotation(TitleLookAt - TitleCamera));
            cameraObject.GetComponent<Camera>().fieldOfView = 50;
            // Three roaches wander loops in the middle distance, three more rest by the crumbs, the rest stay hidden.
            var walkers = new List<Transform>();
            Vector3[] loops = { new Vector3(-1.6f, 0, 0.9f), new Vector3(1.0f, 0, 1.6f), new Vector3(-3.7f, 0, 2.4f) };
            Vector3[] resting = { new Vector3(-1.9f, 0, -2.3f), new Vector3(1.6f, 0, -2.0f), new Vector3(-4.4f, 0, 0.2f) };
            for (int i = 0; i < bodies.Length; i++)
            {
                var t = bodies[i];
                t.gameObject.SetActive(i < 6);
                if (i < 3) walkers.Add(t);
                else if (i < 6) t.SetPositionAndRotation(resting[i - 3], Quaternion.Euler(0, 70 + i * 50, 0));
            }
            var root = new GameObject("Phase5_Title");
            var menu = root.AddComponent<TitleMenu>();
            menu.Configure(stages, UnityEngine.Object.FindFirstObjectByType<PolishedPresentation>());
            menu.ConfigureCodex(CodexViewer(root, foodArt));
            root.AddComponent<TitleBackdrop>().Configure(walkers.ToArray(), loops, new[] { 0.9f, 1.2f, 0.7f }, new[] { 0.45f, -0.55f, 0.35f });
            root.AddComponent<GameAudio>();   // menu sounds only
            EditorSceneManager.SaveScene(scene, TitleScene);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScene));
            Debug.Log("[GOKI PHASE5] タイトル生成完了: " + TitleScene);
        }

        private static void Kill(UnityEngine.Object item) { if (item) UnityEngine.Object.DestroyImmediate(item); }

        /// <summary>Territory: a faint ring marks each zone; a flag goes up once it is taken. Zones must be reachable from the nest.</summary>
        public static Transform[] BuildTerritory(StageDefinition stage, Transform root, SwarmCommander commander, AllyNavGrid floor = null)
        {
            var grid = floor ? floor : commander.Grid;
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + "/Art/Meshes/Phase2_Ring.asset");
            var route = new List<Vector3>();
            var zoneRing = GeneratedAssets.Material("Phase5_ZoneRing", new Color(0.42f, 0.52f, 0.16f));
            var pole = GeneratedAssets.Material("Phase5_FlagPole", new Color(0.12f, 0.1f, 0.08f));
            var cloth = GeneratedAssets.Material("Phase5_Flag", new Color(0.79f, 0.87f, 0.37f));
            cloth.EnableKeyword("_EMISSION"); cloth.SetColor("_EmissionColor", new Color(0.22f, 0.26f, 0.06f));
            var zonesRoot = GeneratedAssets.Empty("Territory", root, Vector3.zero);
            var flags = new Transform[stage.zones.Length];
            for (int i = 0; i < stage.zones.Length; i++)
            {
                var zone = stage.zones[i];
                if (!grid.Walkable(zone.centre)) throw new InvalidOperationException("縄張りの中心が床にありません: " + zone.name);
                if (!grid.FindPath(commander.NestGround, zone.centre, route)) throw new InvalidOperationException("巣から縄張りへ仲間の経路がありません: " + zone.name);
                var holder = GeneratedAssets.Empty("Zone_" + i, zonesRoot, zone.centre);
                P.Part("ZoneRing", holder, ring, zoneRing, new Vector3(0, 0.012f, 0), new Vector3(zone.radius * 2, 1, zone.radius * 2), false, false);
                var flag = GeneratedAssets.Empty("Flag", holder, Vector3.zero);
                P.Rod("Pole", flag, Vector3.zero, new Vector3(0, 1.35f, 0), 0.05f, pole);
                P.Box("Cloth", flag, new Vector3(0.24f, 1.16f, 0), new Vector3(0.46f, 0.3f, 0.03f), cloth, false);
                P.Sphere("Knob", flag, new Vector3(0, 1.38f, 0), Vector3.one * 0.08f, cloth);
                flag.gameObject.SetActive(false);
                flags[i] = flag;
            }
            return flags;
        }

        // ---------------- Food models (Blender) ----------------
        /// <summary>Every food pile gets its kind's model; the poison bait gets the greener crumb with teal pellets on top.</summary>
        public static void DressFoods(FoodModelIntegration.Set art)
        {
            foreach (var food in UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var value = food.GetComponent<FoodValue>();
                var kind = value ? value.FoodKind : FoodValue.Kind.Breadcrumb;
                FoodModelIntegration.Dress(food.transform, art.foods[(int)kind], art.material, Yaw(food.name));
            }
            foreach (var bait in UnityEngine.Object.FindObjectsByType<PoisonBait>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                FoodModelIntegration.Dress(bait.transform, art.bait, art.material, Yaw(bait.name));
                int k = 0;
                foreach (Transform part in bait.transform)
                    if (part.name == "Pellet") { part.localPosition = new Vector3(Mathf.Sin(k * 2.1f) * 0.08f, 0.115f + (k % 2) * 0.012f, Mathf.Cos(k * 2.1f) * 0.06f); k++; }
            }
        }
        private static float Yaw(string name) { int h = 0; foreach (char c in name) h = h * 31 + c; return Mathf.Abs(h % 360); }

        public static CodexViewer CodexViewer(GameObject host, FoodModelIntegration.Set art)
        {
            var viewer = host.AddComponent<CodexViewer>();
            // The five foods (by FoodValue.Kind), then the resident, the exterminator and the Stage 2 mother and son (CodexModelPage indexes).
            var family = FamilyModelIntegration.Actors.Select(a => FamilyModelIntegration.Import(a)[0]);
            var models = art.foods.Concat(new[] { ExterminatorModelIntegration.ResidentMeshes()[0],
                AssetDatabase.LoadAssetAtPath<Mesh>(BlenderModelIntegration.Root + "/Meshes/Exterminator_Detail0.asset") }).Concat(family).Concat(new[] { StageTwoGenerator.CatModel() }).ToArray();
            viewer.Configure(models, art.material, Disc(), GeneratedAssets.Material("Phase5_CodexPlinth", new Color(0.13f, 0.14f, 0.11f)));
            return viewer;
        }

        // ---------------- The nest under the sink ----------------
        /// <summary>
        /// Hollows the sink cabinet into the nest: side, back and top panels around an open floor, closed doors
        /// with plinths, and the door under the sink left ajar as the way in. Inside: the drain trap, egg cases,
        /// paper scraps, droppings and a dim warm light. With a session (the stage) the nest moves in there, the
        /// player starts inside, and the allies' floor grid is baked again. Without one (the title) it is set dressing.
        /// </summary>
        public static void BuildCabinetNest(PhaseTwoSession session, SwarmCommander commander)
        {
            var counter = GameObject.Find("KitchenCabinet").transform;
            var room = GameObject.Find("Room").transform;
            var enamel = counter.Find("CabinetBody").GetComponent<MeshRenderer>().sharedMaterial;
            var black = counter.Find("ToeKick").GetComponent<MeshRenderer>().sharedMaterial;
            var metal = counter.Find("SinkRim").GetComponent<MeshRenderer>().sharedMaterial;
            UnityEngine.Object.DestroyImmediate(counter.Find("CabinetBody").gameObject);
            UnityEngine.Object.DestroyImmediate(counter.Find("ToeKick").gameObject);
            var inside = P.Material("Phase5_CabinetInside", new Color(0.075f, 0.06f, 0.05f), 0.05f);
            var stain = P.Material("Phase5_CabinetFloor", new Color(0.12f, 0.095f, 0.07f), 0.25f);

            Solid(P.Box("CabinetSide", counter, new Vector3(-2.95f, 1.325f, 0), new Vector3(0.1f, 2.65f, 2.05f), enamel));
            Solid(P.Box("CabinetSide", counter, new Vector3(2.95f, 1.325f, 0), new Vector3(0.1f, 2.65f, 2.05f), enamel));
            Solid(P.Box("CabinetBack", counter, new Vector3(0, 1.325f, 0.975f), new Vector3(6, 2.65f, 0.1f), enamel));
            Solid(P.Box("CabinetDeck", counter, new Vector3(0, 2.6f, 0), new Vector3(6, 0.1f, 2.05f), enamel));
            Solid(P.Box("FrontRail", counter, new Vector3(0, 2.565f, -1.0f), new Vector3(6, 0.17f, 0.1f), enamel));
            P.Box("InsideBack", counter, new Vector3(0, 1.3f, 0.92f), new Vector3(5.8f, 2.5f, 0.01f), inside, false);
            P.Box("InsideTop", counter, new Vector3(0, 2.545f, -0.04f), new Vector3(5.8f, 0.01f, 1.9f), inside, false);
            for (int s = -1; s <= 1; s += 2) P.Box("InsideSide", counter, new Vector3(s * 2.895f, 1.3f, -0.04f), new Vector3(0.01f, 2.5f, 1.9f), inside, false);
            P.Box("DampFloor", counter, new Vector3(0, 0.004f, -0.04f), new Vector3(5.8f, 0.006f, 1.9f), stain, false);

            // Doors left to right; the second sits under the sink and hangs open on its left hinge.
            var doors = new List<Transform>();
            foreach (Transform t in counter) if (t.name == "FramedDoor") doors.Add(t);
            doors.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                float x = door.localPosition.x;
                if (i != 1)
                {
                    Solid(door);
                    Solid(P.Box("Plinth", counter, new Vector3(x, 0.18f, -0.95f), new Vector3(1.5f, 0.36f, 0.1f), black));
                    continue;
                }
                var hinge = GeneratedAssets.Empty("OpenDoorHinge", counter, new Vector3(x - 0.705f, 0, -1.055f));
                var leaf = new List<Transform>();
                foreach (Transform t in counter)
                    if (Mathf.Abs(t.localPosition.x - x) < 0.5f && (t.name == "FramedDoor" || t.name == "InsetPanel" || t.name == "BrassHandle")) leaf.Add(t);
                foreach (var t in leaf) t.SetParent(hinge, true);
                hinge.localRotation = Quaternion.Euler(0, DoorOpenDegrees, 0);
                door.gameObject.AddComponent<BoxCollider>();
                var obstacle = door.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();   // the resident walks around it
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one; obstacle.carving = true;
            }

            // Drain trap from the sink down to the back panel.
            P.Rod("DrainPipe", counter, new Vector3(-1.2f, 2.55f, 0), new Vector3(-1.2f, 1.55f, 0), 0.09f, metal);
            P.Rod("DrainPipe", counter, new Vector3(-1.2f, 1.55f, 0), new Vector3(-1.0f, 1.33f, 0), 0.09f, metal);
            P.Rod("DrainPipe", counter, new Vector3(-1.0f, 1.33f, 0), new Vector3(-0.8f, 1.55f, 0), 0.09f, metal);
            P.Rod("DrainPipe", counter, new Vector3(-0.8f, 1.55f, 0), new Vector3(-0.8f, 1.7f, 0), 0.09f, metal);
            P.Rod("DrainPipe", counter, new Vector3(-0.8f, 1.7f, 0), new Vector3(-0.8f, 1.7f, 0.92f), 0.09f, metal);
            // Home comforts: egg cases along the back, paper scraps for bedding, droppings.
            var ootheca = P.Material("Phase5_EggCase", new Color(0.24f, 0.11f, 0.05f), 0.45f);
            var paper = P.Material("Phase5_PaperScrap", new Color(0.62f, 0.58f, 0.48f), 0.05f);
            var dirt = P.Material("Phase5_Droppings", new Color(0.04f, 0.03f, 0.025f), 0.1f);
            Vector3[] eggs = { new Vector3(-2.5f, 0.05f, 0.7f), new Vector3(-2.3f, 0.05f, 0.75f), new Vector3(2.4f, 0.05f, 0.7f), new Vector3(1.0f, 0.05f, 0.78f), new Vector3(2.6f, 0.05f, 0.55f) };
            for (int i = 0; i < eggs.Length; i++)
                P.Sphere("EggCase", counter, eggs[i], new Vector3(0.17f, 0.085f, 0.095f), ootheca).localRotation = Quaternion.Euler(0, 20 + i * 47, 0);
            for (int i = 0; i < 7; i++)
                P.Box("PaperScrap", counter, new Vector3(-2.3f + i * 0.78f, 0.012f + (i % 2) * 0.006f, 0.45f - (i % 3) * 0.32f), new Vector3(0.42f, 0.008f, 0.3f), paper, false)
                    .localRotation = Quaternion.Euler(0, i * 53, (i % 2) * 4);
            for (int i = 0; i < 16; i++)
                P.Sphere("Dropping", counter, new Vector3(-2.6f + ((i * 37) % 52) * 0.1f, 0.015f, -0.75f + ((i * 13) % 15) * 0.1f), Vector3.one * 0.035f, dirt);
            var glow = GeneratedAssets.Empty("NestGlow", counter, new Vector3(0.4f, 1.6f, -0.1f)).gameObject.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 3.4f; glow.intensity = 0.55f; glow.color = new Color(1f, 0.72f, 0.45f); glow.shadows = LightShadows.None;

            if (!session) return;
            // The nest moves in: the old marker art goes, the safe area becomes the cabinet's inside.
            var nest = session.Nest;
            for (int i = nest.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(nest.GetChild(i).gameObject);
            Vector3 centre = counter.TransformPoint(NestLocalCentre); centre.y = 0;
            nest.position = centre;
            session.ConfigureNestArea(new Bounds(centre + Vector3.up * NestAreaSize.y * 0.5f, NestAreaSize));
            EditorUtility.SetDirty(session);
            var player = session.Player;
            player.SetPositionAndRotation(new Vector3(centre.x - 0.6f, player.position.y, centre.z - 0.1f), Quaternion.Euler(0, 180, 0));
            EditorUtility.SetDirty(player);
            var grid = PhaseThreeGenerator.BakeGrid(room, UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.InstanceID), GridPath);
            var link = new SerializedObject(commander);
            link.FindProperty("grid").objectReferenceValue = grid;
            link.ApplyModifiedPropertiesWithoutUndo();
            var allies = commander.Allies;
            for (int i = 0; i < allies.Count; i++) allies[i].transform.position = commander.NestPoint(i, 0.9f);
        }
        /// <summary>Patrol stops spread over the whole room, snapped onto the resident's NavMesh.</summary>
        private static void RoomWidePatrol(HumanAIController human)
        {
            var link = new SerializedObject(human);
            var points = link.FindProperty("patrolPoints");
            var parent = ((Transform)points.GetArrayElementAtIndex(0).objectReferenceValue).parent;
            var navigation = (UnityEngine.AI.NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            var instance = UnityEngine.AI.NavMesh.AddNavMeshData(navigation);
            try
            {
                var stops = new Transform[RoomPatrol.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    if (!UnityEngine.AI.NavMesh.SamplePosition(RoomPatrol[i], out var hit, 1.2f, UnityEngine.AI.NavMesh.AllAreas))
                        throw new InvalidOperationException("巡回地点が人間の通り道にありません: " + RoomPatrol[i]);
                    var old = i < points.arraySize ? (Transform)points.GetArrayElementAtIndex(i).objectReferenceValue : null;
                    stops[i] = old ? old : GeneratedAssets.Empty("PatrolPoint_" + i, parent, Vector3.zero);
                    stops[i].position = hit.position;
                }
                points.arraySize = stops.Length;
                for (int i = 0; i < stops.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = stops[i];
                link.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { instance.Remove(); }
        }
        private static void Solid(Transform part)
        {
            if (!part.GetComponent<Collider>()) part.gameObject.AddComponent<BoxCollider>();
            if (!part.GetComponent<ClimbableSurface>()) part.gameObject.AddComponent<ClimbableSurface>();
        }

        // ---------------- Assets ----------------
        private static void LoadMeshes()
        {
            P.BoxMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/BeveledBox.asset");
            P.SphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/SmoothLowPolySphere.asset");
            P.RodMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/EightSidedRod.asset");
        }

        // Standard shader in Fade mode: the smoke is the one translucent surface in the room.
        private static Material SmokeMaterial()
        {
            var m = GeneratedAssets.Material("Phase5_Smoke", new Color(0.76f, 0.82f, 0.74f, 0.34f));
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Mode", 2);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.18f, 0.2f, 0.17f));
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        public const string ConePath = GeneratedAssets.Root + "/Art/Meshes/Phase5_SprayCone.asset";
        // A cone along +Z from its tip (0) to a 1 m wide mouth at z = 1, open at the mouth.
        private static Mesh SprayCone()
        {
            GeneratedAssets.RequireOwned(ConePath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ConePath);
            if (mesh) return mesh;
            const int segments = 24;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vertices[i + 1] = new Vector3(Mathf.Sin(a) * 0.5f, Mathf.Cos(a) * 0.5f, 1);
                int n = (i + 1) % segments + 1;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 1; triangles[i * 6 + 2] = n;     // outside
                triangles[i * 6 + 3] = 0; triangles[i * 6 + 4] = n; triangles[i * 6 + 5] = i + 1; // inside
            }
            mesh = new Mesh { name = "Phase5_SprayCone", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, ConePath); GeneratedAssets.Mark(mesh);
            return mesh;
        }
        // Pale green translucent insecticide mist.
        private static Material SprayMistMaterial()
        {
            var m = GeneratedAssets.Material("Phase5_SprayMist", new Color(0.80f, 0.95f, 0.78f, 0.42f));
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Mode", 2);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.25f, 0.35f, 0.22f));
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        // A flat disc 1 m across (same size convention as the Phase 2 ring).
        private static Mesh Disc()
        {
            GeneratedAssets.RequireOwned(DiscPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DiscPath);
            if (mesh) return mesh;
            const int segments = 40;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.5f;
                if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            mesh = new Mesh { name = "Phase5_Disc", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, DiscPath); GeneratedAssets.Mark(mesh);
            return mesh;
        }
    }
}
