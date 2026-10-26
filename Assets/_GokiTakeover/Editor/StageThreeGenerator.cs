using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Stage 3 「町中華」 (2026-10-26, step 1 of docs/STAGE3_仕様.md): a copy of the generated Stage 1 scene whose
    /// kitchen is replaced by a two-storey Chinese diner (StageThreeBuilding.cs). The first floor lies apart on the same
    /// plane and is reached by the stairs and a duct (FloorLinks, AllyNavGrid links, NavMesh links for people). Ten new
    /// foods (Blender), ten zones, the nest under the dish room's twin sink, and the owner-cook (店主, a new Blender
    /// model) as the one human for now: the ladle swats like the slipper. Stage 1, Stage 2 and their assets are not
    /// modified.
    /// </summary>
    public static partial class StageThreeGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase5_Stage03_Diner.unity";
        public const string GridPath = GeneratedAssets.Root + "/Data/Stage03AllyGrid.asset";
        public const string NavPath = GeneratedAssets.Root + "/Data/Stage03Navigation.asset";
        public static readonly Vector3 GridOrigin = new Vector3(-7.5f, 0, -6.5f);
        public const int GridWidth = 280, GridDepth = 94;   // x -7.5 .. 62.5, z -6.5 .. 17 (both floors)
        public static readonly Rect ShopArea = new Rect(-7, -6, 70, 22.5f);
        public static readonly Vector3 ChefStart = new Vector3(3.6f, 0, 2.4f);

        public static readonly string[] ZoneNames = { "作業台の下", "コンロの下", "冷蔵庫の前", "カウンターの足元", "テーブル席の下",
            "レジの前", "流し台の前", "米袋のそば", "座卓の下", "ロッカーの前" };
        public static readonly Vector3[] ZoneCentres = { new Vector3(-2.0f, 0, -0.5f), new Vector3(-3.9f, 0, 5.0f), new Vector3(-4.6f, 0, -3.0f),
            new Vector3(12.6f, 0, 2.3f), new Vector3(15.5f, 0, -3.8f), new Vector3(24.0f, 0, -3.0f), new Vector3(-2.5f, 0, 12.6f),
            new Vector3(8.6f, 0, 12.4f), new Vector3(49.0f, 0, -2.0f), new Vector3(52.6f, 0, 13.4f) };
        public static readonly float[] ZoneRadii = { 1.4f, 1.0f, 1.2f, 1.3f, 1.2f, 1.3f, 1.4f, 1.3f, 1.2f, 1.3f };

        private static readonly (FoodValue.Kind kind, Vector3 at, bool known)[] Foods =
        {
            // 1F kitchen
            (FoodValue.Kind.Tenkasu, new Vector3(0.6f, 0.07f, 3.85f), true),          // by the fryer
            (FoodValue.Kind.Tenkasu, new Vector3(1.6f, 0.07f, 3.2f), false),
            (FoodValue.Kind.FriedRice, new Vector3(-3.2f, 0.07f, 3.6f), false),       // in front of the range
            (FoodValue.Kind.Gyoza, new Vector3(-3.4f, 2.77f, -0.7f), true),           // on the work table
            (FoodValue.Kind.Karaage, new Vector3(8.2f, 2.77f, 1.6f), false),          // on the serving table
            (FoodValue.Kind.Noodles, new Vector3(-1.0f, 0.07f, -0.2f), false),        // under the work table
            (FoodValue.Kind.FriedRice, new Vector3(4.4f, 0.07f, -2.2f), false),
            // 1F hall
            (FoodValue.Kind.DinerLeftovers, new Vector3(15.0f, 2.69f, -3.6f), false), // on a table
            (FoodValue.Kind.Gyoza, new Vector3(10.2f, 2.75f, 0.5f), false),           // on the counter
            (FoodValue.Kind.Tenkasu, new Vector3(12.6f, 0.07f, 1.2f), false),         // under the stools
            (FoodValue.Kind.FriedRice, new Vector3(21.5f, 0.07f, 0.6f), false),       // under a table
            (FoodValue.Kind.SesameBall, new Vector3(23.6f, 0.07f, -4.0f), false),     // by the register
            (FoodValue.Kind.Noodles, new Vector3(19.0f, 0.07f, -1.6f), false),
            // 1F dish room
            (FoodValue.Kind.Noodles, new Vector3(-0.5f, 0.07f, 13.4f), false),
            (FoodValue.Kind.DinerLeftovers, new Vector3(3.4f, 0.07f, 8.6f), false),   // by the slop bucket
            (FoodValue.Kind.Tenkasu, new Vector3(-4.6f, 0.07f, 9.2f), false),
            // 1F pantry
            (FoodValue.Kind.Flour, new Vector3(8.3f, 0.07f, 13.9f), false),
            (FoodValue.Kind.Flour, new Vector3(9.0f, 0.07f, 9.0f), false),
            (FoodValue.Kind.Chashu, new Vector3(14.45f, 4.11f, 8.6f), false),         // on top of the steel shelf
            (FoodValue.Kind.SesameBall, new Vector3(7.3f, 2.17f, 8.05f), false),      // on the cartons
            // 2F banquet room
            (FoodValue.Kind.Shumai, new Vector3(48.4f, 1.18f, -2.0f), false),         // on a low table
            (FoodValue.Kind.Gyoza, new Vector3(53.0f, 0.07f, 2.6f), false),
            (FoodValue.Kind.Karaage, new Vector3(55.0f, 1.18f, -1.8f), false),
            (FoodValue.Kind.SesameBall, new Vector3(58.0f, 0.07f, -4.6f), false),
            (FoodValue.Kind.DinerLeftovers, new Vector3(49.6f, 1.18f, 2.6f), false),
            // 2F staff room
            (FoodValue.Kind.DinerLeftovers, new Vector3(47.5f, 2.63f, 10.2f), false), // on the staff table
            (FoodValue.Kind.Karaage, new Vector3(45.0f, 0.07f, 13.0f), false),        // under the nap sofa
            (FoodValue.Kind.FriedRice, new Vector3(52.5f, 0.07f, 9.0f), false),
            (FoodValue.Kind.Chashu, new Vector3(54.0f, 2.58f, 8.6f), false),          // on the desk
        };
        private static readonly Vector3[] Scouts =
        {
            new Vector3(-5.5f, 0, -0.5f), new Vector3(5.5f, 0, 0), new Vector3(0.5f, 0, -4.5f), new Vector3(13.5f, 0, -4.5f), new Vector3(19.0f, 0, -1.6f),
            new Vector3(25.5f, 0, 0), new Vector3(-4.5f, 0, 11.5f), new Vector3(3.0f, 0, 12.5f), new Vector3(8.0f, 0, 11.0f), new Vector3(11.5f, 0, 9.0f),
            new Vector3(47.0f, 0, -4.0f), new Vector3(55.5f, 0, 0.5f), new Vector3(50.0f, 0, 12.5f), new Vector3(54.5f, 0, 13.0f)
        };
        private static readonly Vector3[] Respawns =
        {
            new Vector3(4.0f, 0.07f, 3.0f), new Vector3(-5.0f, 0.07f, 1.5f), new Vector3(6.0f, 0.07f, -3.6f), new Vector3(14.0f, 0.07f, -1.5f), new Vector3(24.0f, 0.07f, 1.5f),
            new Vector3(-2.0f, 0.07f, 9.5f), new Vector3(10.5f, 0.07f, 8.5f), new Vector3(51.5f, 0.07f, 0.5f), new Vector3(59.5f, 0.07f, -1.0f), new Vector3(50.5f, 0.07f, 13.0f)
        };
        private static readonly Vector3[] ChefStops =
        {
            new Vector3(3.5f, 0, 2.5f), new Vector3(5.6f, 0, -2.0f), new Vector3(-4.5f, 0, 2.4f), new Vector3(-4.3f, 0, -2.6f), new Vector3(0.5f, 0, -3.8f),
            new Vector3(13.5f, 0, -1.5f), new Vector3(19.0f, 0, -1.6f), new Vector3(24.5f, 0, 1.8f), new Vector3(-2.0f, 0, 11.5f), new Vector3(2.5f, 0, 12.6f),
            new Vector3(9.5f, 0, 10.5f), new Vector3(50.0f, 0, 0.4f), new Vector3(56.0f, 0, -3.5f), new Vector3(50.0f, 0, 11.5f)
        };

        [MenuItem("Tools/GOKI TAKEOVER/Generate Stage 3 Diner", priority = 32)]
        public static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Generate();
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Stage 3 Diner", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate()
        {
            GeneratedAssets.RequireOwned(ScenePath);
            if (!File.Exists(PhaseFiveGenerator.StageScene)) throw new InvalidOperationException("先にステージ1を生成してください。");
            PhaseFiveGenerator.Stages();
            FillDefinition(AssetDatabase.LoadAssetAtPath<StageDefinition>(PhaseFiveGenerator.StageAssets[2]));
            // Stage 2's clear screen names what it opens (it said 「飲食店」 while this was a placeholder). Once.
            var house = AssetDatabase.LoadAssetAtPath<StageDefinition>(PhaseFiveGenerator.StageAssets[1]);
            if (house && house.unlocksTitle == "飲食店") { house.unlocksTitle = "町中華"; EditorUtility.SetDirty(house); }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(PhaseFiveGenerator.StageScene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("ステージ3を保存できません。");
            P.BoxMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/BeveledBox.asset");
            P.SphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/SmoothLowPolySphere.asset");
            P.RodMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/EightSidedRod.asset");
            P.ShadowMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/ContactPlane.asset");
            LoadMaterials();

            var session = UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>();
            var commander = UnityEngine.Object.FindFirstObjectByType<SwarmCommander>();
            var colony = UnityEngine.Object.FindFirstObjectByType<ColonyManager>();
            var progress = UnityEngine.Object.FindFirstObjectByType<StageProgress>();
            var stageRoot = progress.transform;
            var room = GameObject.Find("Room").transform;

            // ---- Out with the kitchen: keep one food pile as the template, then empty the room ----
            var template = commander.Foods.First(f => f.GetComponent<FoodValue>()).gameObject;
            template.transform.SetParent(null, true);
            for (int i = room.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(room.GetChild(i).gameObject);
            foreach (var name in new[] { "Pendant", "WarmPool", "FridgeWarmFill" }) Kill(GameObject.Find("GOKI_CinematicKitchen/" + name));
            Kill(GameObject.Find("Phase5_Stage/BackDoor"));
            Kill(GameObject.Find("Phase5_Stage/Territory"));
            BuildShop(room);

            // ---- The nest under the twin sink ----
            Vector3 nestCentre = SinkCentre + new Vector3(0, 0, -0.04f);
            session.Nest.position = nestCentre;
            session.ConfigureNestArea(new Bounds(nestCentre + Vector3.up * NestAreaSize.y * 0.5f, NestAreaSize));
            session.Player.SetPositionAndRotation(new Vector3(nestCentre.x - 0.6f, session.Player.position.y, nestCentre.z - 0.1f), Quaternion.Euler(0, 180, 0));
            EditorUtility.SetDirty(session); EditorUtility.SetDirty(session.Player);
            // The sticky traps the resident set out at the start: one in the kitchen, one in the hall.
            var traps = UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.InstanceID).Where(t => t.transform.parent && t.transform.parent.name == "Phase2_Gameplay").OrderBy(t => t.name).ToArray();
            if (traps.Length >= 2) { traps[0].transform.position = new Vector3(5.2f, 0, -2.6f); traps[1].transform.position = new Vector3(13.6f, 0, -1.2f); }

            // ---- Navigation: the human NavMesh, the allies' grid with the floor links ----
            Physics.SyncTransforms();
            var navigation = BakeNavigation(room);
            var nav = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>());
            nav.FindProperty("data").objectReferenceValue = navigation;
            nav.ApplyModifiedPropertiesWithoutUndo();
            var links = FloorLinkSet();
            var grid = BakeGrid(room, UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.InstanceID).Where(t => t.transform.parent && t.transform.parent.name == "Phase2_Gameplay").ToArray(), session, links);

            // ---- Food, scouting and refill spots ----
            var foodArt = FoodModelIntegration.Import();
            var meshes = FoodModelIntegration.ImportAllKinds(foodArt);
            var foods = new List<FoodItem>(); var known = new List<bool>();
            var foodRoot = GeneratedAssets.Empty("Diner_Food", room, Vector3.zero);
            for (int i = 0; i < Foods.Length; i++)
            {
                var (kind, at, isKnown) = Foods[i];
                var copy = UnityEngine.Object.Instantiate(template, foodRoot);
                copy.name = "FoodMarker_Diner_" + i;
                copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0, 30 + i * 53, 0));
                copy.GetComponent<FoodValue>().Configure(kind);
                FoodModelIntegration.Dress(copy.transform, meshes[(int)kind], foodArt.material, (i * 137) % 360);
                foods.Add(copy.GetComponent<FoodItem>()); known.Add(isKnown);
            }
            // The day's own food (step 3): gyoza laid out while prepping, leftovers per table (hidden till a customer leaves food).
            foreach (var (kind, at, name) in BusinessFood())
            {
                var copy = UnityEngine.Object.Instantiate(template, foodRoot);
                copy.name = name;
                copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0, 30 + foods.Count * 53, 0));
                copy.GetComponent<FoodValue>().Configure(kind);
                FoodModelIntegration.Dress(copy.transform, meshes[(int)kind], foodArt.material, (foods.Count * 137) % 360);
                foods.Add(copy.GetComponent<FoodItem>()); known.Add(false);
            }
            UnityEngine.Object.DestroyImmediate(template);
            var scouts = new List<Vector3>();
            foreach (var s in Scouts)
            {
                Vector3 q = s;
                if (!grid.Walkable(q) && !grid.NearestWalkable(q, 1.5f, out q)) throw new InvalidOperationException("調査地点に到達できません: " + s);
                scouts.Add(q);
            }
            var cLink = new SerializedObject(commander);
            SetArray(cLink.FindProperty("foods"), foods.Cast<UnityEngine.Object>().ToArray());
            var knownProp = cLink.FindProperty("knownAtStart");
            knownProp.arraySize = known.Count;
            for (int i = 0; i < known.Count; i++) knownProp.GetArrayElementAtIndex(i).boolValue = known[i];
            var scoutProp = cLink.FindProperty("scoutPoints");
            scoutProp.arraySize = scouts.Count;
            for (int i = 0; i < scouts.Count; i++) scoutProp.GetArrayElementAtIndex(i).vector3Value = scouts[i];
            cLink.FindProperty("grid").objectReferenceValue = grid;
            SetArray(cLink.FindProperty("hideInSwarm"), fronts.Cast<UnityEngine.Object>().ToArray());
            SetArray(cLink.FindProperty("ceiling"), ceilings.Cast<UnityEngine.Object>().ToArray());
            cLink.ApplyModifiedPropertiesWithoutUndo();
            var iLink = new SerializedObject(session.Interactor);
            SetArray(iLink.FindProperty("foods"), foods.Cast<UnityEngine.Object>().ToArray());
            iLink.ApplyModifiedPropertiesWithoutUndo();
            var colLink = new SerializedObject(colony);
            var spots = colLink.FindProperty("respawnSpots");
            spots.arraySize = Respawns.Length;
            for (int i = 0; i < Respawns.Length; i++) spots.GetArrayElementAtIndex(i).vector3Value = Respawns[i];
            colLink.ApplyModifiedPropertiesWithoutUndo();
            foreach (var ally in commander.Allies) ally.transform.position = commander.NestPoint(ally.Slot, 0.9f);
            var overview = UnityEngine.Object.FindFirstObjectByType<SwarmViewCamera>(FindObjectsInactive.Include);
            overview.Configure(new Vector2(-6.4f, -5.4f), new Vector2(61.6f, 15.6f), commander.Settings3.swarmDistance + 2, commander.Settings3.swarmPanSpeed * 2.2f);
            EditorUtility.SetDirty(overview);

            // ---- The owner-cook (店主): the resident's skeleton and AI wearing the new Blender model ----
            var chef = session.Human;
            DressChef(chef);
            chef.ConfigureMember("店主", true, 3.5f, 1f);
            chef.transform.SetPositionAndRotation(ChefStart, Quaternion.Euler(0, 200, 0));
            EditorUtility.SetDirty(chef);
            SetPatrol(chef, ChefStops);

            // ---- The staff (step 2): the landlady and the part-timer, everyone's own attacks ----
            BuildStaff(stageRoot, session, commander, chef);

            // ---- Nest smoking (final defence) ----
            var pLink = new SerializedObject(progress);
            var nestFogger = (SmokeFogger)pLink.FindProperty("nestFogger").objectReferenceValue;
            var sink = room.Find("Diner_DishRoom/SinkCabinet");
            var smoke = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Phase5_Smoke.mat");
            var nestSmoke = P.Box("NestSmoke", sink, new Vector3(0, 1.22f, -0.04f), new Vector3(5.72f, 2.42f, 1.86f), smoke, false);
            nestSmoke.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            nestSmoke.GetComponent<MeshRenderer>().receiveShadows = false;
            nestSmoke.gameObject.SetActive(false);
            progress.ConfigureNestSmoking(nestFogger, nestSmoke.gameObject, SinkCentre + new Vector3(-0.73f, 0, -1.45f), SinkCentre + new Vector3(0.6f, 0, -2.9f), NestRefuge);

            // ---- Stage flow: the definition, zones and flags, the floor links ----
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(PhaseFiveGenerator.StageAssets[2]);   // the scene load unloaded it
            var flags = PhaseFiveGenerator.BuildTerritory(stage, stageRoot, commander, grid);
            // Zone rings above the tatami and every rug (floor markings at y >= 0.04).
            foreach (var r in stageRoot.Find("Territory").GetComponentsInChildren<Transform>(true).Where(t => t.name == "ZoneRing"))
                r.localPosition = new Vector3(r.localPosition.x, 0.045f, r.localPosition.z);
            pLink = new SerializedObject(progress);
            pLink.FindProperty("stage").objectReferenceValue = stage;
            SetArray(pLink.FindProperty("flags"), flags.Cast<UnityEngine.Object>().ToArray());
            pLink.ApplyModifiedPropertiesWithoutUndo();
            var floorLinks = stageRoot.gameObject.AddComponent<FloorLinks>();
            floorLinks.Configure(session, links, SecondFloorX);

            // ---- The day and its customers (step 3) ----
            BuildBusiness(stageRoot, session, commander, colony, progress, chef, room);

            EditorSceneManager.SaveScene(scene, ScenePath);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log("[GOKI STAGE3] 町中華を生成: " + ScenePath);
        }

        // ---------------- Definition ----------------
        public const string Description = "店主・おかみさん・バイトが働く2階建ての町中華。熱湯・お盆・ブラシに注意。";
        public const string Subtitle = "2階建ての中華料理店";
        /// <summary>Stage 3 numbers, harder than Stage 2 on every gauge. Written once (while it was still a placeholder).</summary>
        public static void FillDefinition(StageDefinition stage)
        {
            if (stage.playable && stage.zones.Length > 0)
            {
                // Set up already: keep any tuning. Once, when the staff came (step 2): the people and the write-up.
                if (stage.residents >= 3) return;
                stage.residents = 3; stage.subtitle = Subtitle; stage.description = Description;
                EditorUtility.SetDirty(stage);
                return;
            }
            stage.id = "stage03"; stage.title = "町中華"; stage.subtitle = Subtitle; stage.difficulty = 4; stage.residents = 3;
            stage.description = Description;
            stage.playable = true; stage.sceneName = Path.GetFileNameWithoutExtension(ScenePath);
            stage.unlocks = ""; stage.unlocksTitle = ""; stage.reward = ""; stage.rewardTitle = "";
            stage.zones = ZoneNames.Select((n, i) => new StageDefinition.Zone { name = n, centre = ZoneCentres[i], radius = ZoneRadii[i] }).ToArray();
            stage.foodGoal = 70; stage.fearGoal = 240; stage.holdSeconds = 18; stage.holdPresence = 3; stage.holdBonusPerRoach = 0.5f; stage.holdBonusMax = 3; stage.residentClearRate = 0.55f; stage.fearPerZone = 8;
            stage.startAllies = 0; stage.allyCorpseSeconds = 20; stage.corpseHeal = 1; stage.slipperReach = 3.6f; stage.tutorial = false;
            stage.residentHuntsAllies = true; stage.allyReactionSeconds = 0.85f; stage.allyStunSeconds = 1.5f; stage.allyWoundHealSeconds = 35;
            stage.allyAlarmRadius = 2.0f; stage.allyCollectReactionScale = 1.6f;
            stage.nestFumigations = new[] { 8f, 30f, 55f, 80f }; stage.nestFumigationWarning = 4.5f; stage.nestFumigationSeconds = 15; stage.nestSmokeDamageEvery = 1.1f;
            stage.humanDetectionScale = 1.05f; stage.humanSpeedScale = 1.1f; stage.heatArea = ShopArea; stage.familyLeavesForDefence = true;
            stage.calmSeconds = 30; stage.calmBelowFear = 25; stage.fearDecayPerSecond = 1.0f; stage.allyFearCap = 1;
            stage.finalDefenceSeconds = 100; stage.foggerInterval = 10; stage.foggerWarning = 3.2f; stage.foggerDuration = 9; stage.foggerRadius = 3.0f; stage.maxFoggers = 3;
            stage.exitPoint = ExitPoint;
            stage.parSeconds = 1500; stage.fewDiscoveries = 5; stage.someDiscoveries = 12; stage.manyAllies = 8; stage.someAllies = 5;
            EditorUtility.SetDirty(stage);
        }

        // ---------------- Floors ----------------
        /// <summary>The stairs (people and roaches) and the duct (roaches only) between the floors.</summary>
        public static FloorLinks.Link[] FloorLinkSet()
        {
            float z = StairZ;
            var stairs = new FloorLinks.Link
            {
                name = "階段", humans = true,
                a = new FloorLinks.End
                {
                    ground = new Vector3(16.3f, 0, z), hidden = new Vector3(26.3f, StepCount * StepRise, z),
                    trigger = new Bounds(new Vector3(26.2f, 6.1f, z), new Vector3(2.6f, 1.6f, 2.6f)),
                    arrive = new Vector3(15.9f, 0.05f, z), arriveYaw = 270
                },
                b = new FloorLinks.End
                {
                    ground = new Vector3(56.9f, 0, z), hidden = new Vector3(61.2f, -4.6f, z),
                    trigger = new Bounds(new Vector3(59.7f, -0.4f, z), new Vector3(4.2f, 1.0f, 2.4f)),
                    arrive = new Vector3(56.3f, 0.05f, z), arriveYaw = 270
                }
            };
            var duct = new FloorLinks.Link
            {
                name = "ダクト", humans = false,
                a = new FloorLinks.End
                {
                    ground = new Vector3(KitchenVent.x, 0, -5.5f), hidden = KitchenVent,
                    trigger = new Bounds(new Vector3(KitchenVent.x, 0.55f, -7.1f), new Vector3(1.2f, 1.1f, 1.4f)),   // well inside the duct
                    arrive = new Vector3(KitchenVent.x, 0.05f, -5.3f), arriveYaw = 0
                },
                b = new FloorLinks.End
                {
                    ground = new Vector3(StaffVent.x, 0, 15.6f), hidden = StaffVent,
                    trigger = new Bounds(new Vector3(StaffVent.x, 0.55f, 17.3f), new Vector3(1.2f, 1.1f, 1.4f)),
                    arrive = new Vector3(StaffVent.x, 0.05f, 15.4f), arriveYaw = 180
                }
            };
            return new[] { stairs, duct };
        }

        // ---------------- Navigation ----------------
        private static NavMeshData BakeNavigation(Transform room)
        {
            GeneratedAssets.RequireOwned(NavPath);
            var sources = new List<NavMeshBuildSource>();
            // The roaches' stair ramps are not for people (they take the stairs as a link).
            var markups = room.GetComponentsInChildren<Transform>(true).Where(t => t.name == RampName).Select(t => new NavMeshBuildMarkup { root = t, ignoreFromBuild = true }).ToList();
            NavMeshBuilder.CollectSources(room, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var build = NavMesh.GetSettingsByIndex(0);
            build.agentRadius = 0.42f; build.agentHeight = 4.1f; build.agentClimb = 0.18f; build.agentSlope = 40;
            build.overrideVoxelSize = true; build.voxelSize = 0.10f;
            var baked = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(new Vector3(27.5f, 3, 5.25f), new Vector3(72, 14, 24)), Vector3.zero, Quaternion.identity);
            if (!baked) throw new InvalidOperationException("町中華のNavMesh生成に失敗しました。");
            var navigation = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (navigation) { EditorUtility.CopySerialized(baked, navigation); UnityEngine.Object.DestroyImmediate(baked); }
            else { navigation = baked; AssetDatabase.CreateAsset(navigation, NavPath); GeneratedAssets.Mark(navigation); }
            EditorUtility.SetDirty(navigation);
            return navigation;
        }

        /// <summary>
        /// The allies' grid over both floors. Like PhaseThreeGenerator.BakeGrid (nothing of the room within reach of a
        /// roach standing there, sticky traps costly), plus a floor must be under the cell (the gap between the two
        /// floors and the stairwell stay blocked), plus the stair and duct links.
        /// </summary>
        private static AllyNavGrid BakeGrid(Transform room, StickyTrap[] traps, PhaseTwoSession session, FloorLinks.Link[] links)
        {
            const float cell = 0.25f;
            var data = new byte[GridWidth * GridDepth];
            Physics.SyncTransforms();
            var hits = new Collider[16];
            for (int z = 0; z < GridDepth; z++)
            for (int x = 0; x < GridWidth; x++)
            {
                var centre = GridOrigin + new Vector3((x + 0.5f) * cell, 0.5f, (z + 0.5f) * cell);
                bool floor = Physics.Raycast(new Vector3(centre.x, 0.3f, centre.z), Vector3.down, out RaycastHit below, 0.4f, ~0, QueryTriggerInteraction.Ignore)
                    && below.collider.transform.IsChildOf(room) && below.point.y > -0.05f;
                int count = Physics.OverlapSphereNonAlloc(centre, 0.44f, hits, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = !floor;
                for (int i = 0; i < count && !blocked; i++) blocked = hits[i].transform.IsChildOf(room);
                data[z * GridWidth + x] = blocked ? AllyNavGrid.Blocked : AllyNavGrid.Open;
            }
            foreach (var trap in traps)
            {
                var area = trap.GetComponent<BoxCollider>().bounds;
                area.Expand(new Vector3(0.5f, 0, 0.5f));
                for (int z = 0; z < GridDepth; z++)
                for (int x = 0; x < GridWidth; x++)
                {
                    var c = GridOrigin + new Vector3((x + 0.5f) * cell, area.center.y, (z + 0.5f) * cell);
                    if (data[z * GridWidth + x] == AllyNavGrid.Open && area.Contains(c)) data[z * GridWidth + x] = AllyNavGrid.Sticky;
                }
            }
            GeneratedAssets.RequireOwned(GridPath);
            var grid = AssetDatabase.LoadAssetAtPath<AllyNavGrid>(GridPath);
            bool created = !grid;
            if (created) grid = ScriptableObject.CreateInstance<AllyNavGrid>();
            grid.Bake(GridOrigin, cell, GridWidth, GridDepth, data);
            grid.SetLinks(links.SelectMany(l => new[] { l.a.ground, l.b.ground }).ToArray());
            if (created) { AssetDatabase.CreateAsset(grid, GridPath); GeneratedAssets.Mark(grid); }
            EditorUtility.SetDirty(grid);
            return grid;
        }

        // ---------------- The owner-cook ----------------
        private static void DressChef(HumanAIController chef)
        {
            foreach (var look in chef.GetComponentsInChildren<ExterminatorLook>(true)) UnityEngine.Object.DestroyImmediate(look);   // his own look all the way (the stage's own exterminators come later)
            var resident = ExterminatorModelIntegration.ResidentMeshes();
            var meshes = FamilyModelIntegration.Import("Chef");
            var skins = chef.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => Array.IndexOf(resident, r.sharedMesh) >= 0).ToArray();
            if (skins.Length != 3) throw new InvalidOperationException("店主: 住人のスキン（3段階）が見つかりません");
            foreach (var r in skins) { r.sharedMesh = meshes[Array.IndexOf(resident, r.sharedMesh)]; EditorUtility.SetDirty(r); }
            var skinRoot = skins[0].transform.parent;
            var animator = chef.GetComponentInChildren<BlenderHumanAnimator>();
            var leftHand = (Transform)new SerializedObject(animator).FindProperty("leftHand").objectReferenceValue;
            animator.ConfigureLance(leftHand.InverseTransformPoint(skinRoot.TransformPoint(FamilyModelIntegration.ToolTip("Chef"))));
            EditorUtility.SetDirty(animator);
        }
        /// <summary>The cook's patrol stops, snapped onto the NavMesh.</summary>
        private static void SetPatrol(HumanAIController human, Vector3[] stops, string prefix = "PatrolPoint_")
        {
            var navigation = (NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            var instance = NavMesh.AddNavMeshData(navigation);
            try
            {
                var link = new SerializedObject(human);
                var points = link.FindProperty("patrolPoints");
                var parent = human.transform.parent;
                var old = Enumerable.Range(0, points.arraySize).Select(i => (Transform)points.GetArrayElementAtIndex(i).objectReferenceValue).Where(t => t).ToList();
                var result = new Transform[stops.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    if (!NavMesh.SamplePosition(stops[i], out var hit, 1.2f, NavMesh.AllAreas)) throw new InvalidOperationException("巡回地点が人間の通り道にありません: " + stops[i]);
                    result[i] = i < old.Count ? old[i] : GeneratedAssets.Empty(prefix + i, parent, Vector3.zero);
                    result[i].position = hit.position;
                }
                for (int i = stops.Length; i < old.Count; i++) UnityEngine.Object.DestroyImmediate(old[i].gameObject);
                SetArray(points, result.Cast<UnityEngine.Object>().ToArray());
                link.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { instance.Remove(); }
        }

        // ---------------- Helpers ----------------
        private static void Kill(GameObject item) { if (item) UnityEngine.Object.DestroyImmediate(item); }
        private static void SetArray(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
