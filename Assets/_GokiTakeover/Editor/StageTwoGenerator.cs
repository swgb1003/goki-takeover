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
    /// Stage 2 「一軒家」 (2026-10-25): a copy of the generated Stage 1 scene grown into a family house. The kitchen's back
    /// door becomes a doorway into a living room (sofa on tall legs, coffee table, TV board, bookshelf, the son's toy
    /// corner with his bug cage, a door out to the hall). Three people live here: the father (the Stage 1 resident:
    /// slipper, traps, spray; he calls the exterminator), the mother (new model: slipper and a vacuum that sucks roaches
    /// in) and the son (new model, scaled down: a bug net that holds the player and cages allies). Six zones across both
    /// rooms, more food, a bigger floor grid and human NavMesh. Stage 1 and its assets are not modified.
    /// Later the same day: a bedroom and the son's room behind them (four rooms, eight zones) and the family's daily
    /// life (StageTwoLifeGenerator.cs: the rooms, the activity spots, HouseholdLife).
    /// </summary>
    public static partial class StageTwoGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase5_Stage02_House.unity";
        public const string GridPath = GeneratedAssets.Root + "/Data/Stage02AllyGrid.asset";
        public const string NavPath = GeneratedAssets.Root + "/Data/Stage02Navigation.asset";
        public const string FanPath = GeneratedAssets.Root + "/Art/Meshes/Stage02_VacuumFan.asset";
        public const string CatGridPath = GeneratedAssets.Root + "/Data/Stage02CatGrid.asset";
        public static readonly Vector3 CatBed = new Vector3(9.3f, 0, 4.6f);
        // The living room: x 7.24 .. 21.24 (the kitchen's right wall is shared), z -6 .. 6, same ceiling.
        public const float LivingMinX = 7.24f, LivingMaxX = 21.24f, DoorZMin = -5.0f, DoorZMax = -2.0f, DoorHeight = 4.4f;
        public static readonly Vector3 GridOrigin = new Vector3(-7, 0, -6);
        public const int GridWidth = 114, GridDepth = 90;   // x -7 .. 21.5, z -6 .. 16.5 (all four rooms)
        public static readonly Vector3 ExitPoint = new Vector3(20.5f, 0, -0.9f);
        public static readonly Vector3 CagePosition = new Vector3(19.6f, 0, -1.6f);
        public static readonly string[] ZoneNames = { "テーブルの下", "冷蔵庫の前", "流し台の前", "ソファの下", "テレビの前", "おもちゃ箱のそば", "ベッドの下", "勉強机の下" };
        public static readonly Vector3[] ZoneCentres = { new Vector3(-2.3f, 0, -0.9f), new Vector3(-3.9f, 0, 2.7f), new Vector3(-0.9f, 0, 3.0f),
            new Vector3(15.5f, 0, 4.45f), new Vector3(15.5f, 0, -3.0f), new Vector3(18.2f, 0, -2.4f), new Vector3(-1.0f, 0, 12.6f), new Vector3(12.0f, 0, 15.0f) };
        public static readonly float[] ZoneRadii = { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f, 1.3f, 1.5f, 1.3f };
        public static readonly Rect HouseArea = new Rect(-7, -6, 28.4f, 22.4f);
        // New food in the living room: kind, position (high ones are reached by climbing), known from the start.
        private static readonly (FoodValue.Kind kind, Vector3 at, bool known)[] LivingFoods =
        {
            (FoodValue.Kind.Sweets, new Vector3(15.0f, 1.94f, 1.6f), true),     // on the coffee table
            (FoodValue.Kind.Sweets, new Vector3(16.8f, 0.07f, 4.5f), false),    // under the sofa
            (FoodValue.Kind.Breadcrumb, new Vector3(17.3f, 0.07f, -3.9f), false),
            (FoodValue.Kind.Leftovers, new Vector3(12.4f, 0.07f, -4.1f), false),
            (FoodValue.Kind.Garbage, new Vector3(9.0f, 0.07f, -4.7f), false),   // by the bin
            (FoodValue.Kind.Rice, new Vector3(19.2f, 0.07f, 4.6f), false),
            (FoodValue.Kind.Leftovers, new Vector3(13.6f, 1.47f, -5.2f), false),// on the TV board
            (FoodValue.Kind.Sweets, new Vector3(20.5f, 4.87f, 2.5f), false),    // on top of the bookshelf
            (FoodValue.Kind.Breadcrumb, new Vector3(10.0f, 0.07f, 1.0f), false),
        };
        private static readonly Vector3[] LivingScouts =
        {
            new Vector3(9.2f, 0, 3.4f), new Vector3(11.4f, 0, -1.0f), new Vector3(15.5f, 0, 4.4f), new Vector3(19.6f, 0, 5.0f),
            new Vector3(19.4f, 0, -0.4f), new Vector3(17.6f, 0, -5.2f), new Vector3(12.0f, 0, -4.6f), new Vector3(15.5f, 0, -1.4f)
        };
        private static readonly Vector3[] LivingRespawns =
        {
            new Vector3(10.2f, 0.07f, 3.8f), new Vector3(12.0f, 0.07f, -2.6f), new Vector3(18.6f, 0.07f, 3.0f), new Vector3(16.2f, 0.07f, -2.0f), new Vector3(20.2f, 0.07f, -4.8f)
        };
        // Patrol stops (snapped onto the human NavMesh). Father: the whole house. Mother: mostly the kitchen. Son: mostly the living room.
        private static readonly Vector3[] LivingStops =
        {
            new Vector3(9.4f, 0, 2.0f), new Vector3(11.2f, 0, -2.6f), new Vector3(13.2f, 0, -1.2f), new Vector3(18.9f, 0, 2.0f),
            new Vector3(18.6f, 0, -2.8f), new Vector3(11.6f, 0, 2.6f), new Vector3(16.0f, 0, -1.2f)
        };
        private static readonly Vector3[] KitchenStops =
        {
            new Vector3(3.6f, 0, -3.8f), new Vector3(5.2f, 0, -0.8f), new Vector3(4.2f, 0, 2.9f), new Vector3(-1.8f, 0, 2.6f),
            new Vector3(-5.9f, 0, 0.4f), new Vector3(-5.4f, 0, -3.9f), new Vector3(0.6f, 0, -3.4f)
        };
        private static Material plasterWarm, oakFloor, walnut, oak, charcoal, enamel, ivory, metal;

        [MenuItem("Tools/GOKI TAKEOVER/Generate Stage 2 House", priority = 31)]
        public static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Generate(PhaseFiveGenerator.Stages()[1]);
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Stage 2 House", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate(StageDefinition stage)
        {
            GeneratedAssets.RequireOwned(ScenePath);
            if (!File.Exists(PhaseFiveGenerator.StageScene)) throw new InvalidOperationException("先にステージ1を生成してください。");
            // Opening a scene unloads assets nothing references: take the definition fresh from disk.
            stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(PhaseFiveGenerator.StageAssets[1]);
            FillDefinition(stage);
            var scene = EditorSceneManager.OpenScene(PhaseFiveGenerator.StageScene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("ステージ2を保存できません。");
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

            // ---- The house ----
            Kill(GameObject.Find("Phase5_Stage/BackDoor"));
            Kill(GameObject.Find("Phase5_Stage/Territory"));
            var living = GeneratedAssets.Empty("LivingRoom", room, Vector3.zero);
            var renderers = BuildRooms(room, living);
            Furnish(living);
            var north = BuildNorthRooms(room);   // the bedroom and the son's room
            renderers = (renderers.front.Concat(north.hide).ToArray(), renderers.ceiling.Concat(north.ceiling).ToArray());
            foreach (var root in new[] { living, north.bedroom, north.kids })
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

            // ---- Navigation: the human NavMesh and the allies' floor grid over both rooms ----
            Physics.SyncTransforms();
            var navigation = BakeNavigation(room);
            Assign(new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()), "data", navigation);
            var traps = UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.InstanceID);
            var grid = PhaseThreeGenerator.BakeGrid(room, traps, GridPath, GridOrigin, GridWidth, GridDepth);

            // ---- Food, scouting, respawn spots ----
            var foods = new List<FoodItem>(commander.Foods);
            var cLink = new SerializedObject(commander);
            var knownProp = cLink.FindProperty("knownAtStart");
            var known = Enumerable.Range(0, knownProp.arraySize).Select(i => knownProp.GetArrayElementAtIndex(i).boolValue).ToList();
            FoodItem AddFood(FoodValue.Kind kind, Vector3 at, bool isKnown, string prefix)
            {
                var template = foods.First(f => f.GetComponent<FoodValue>() && f.GetComponent<FoodValue>().FoodKind == kind);
                var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                copy.name = prefix + foods.Count;
                copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0, 30 + foods.Count * 53, 0));
                foods.Add(copy.GetComponent<FoodItem>());
                known.Add(isKnown);
                return foods[foods.Count - 1];
            }
            foreach (var (kind, at, isKnown) in LivingFoods.Concat(NorthFoods)) AddFood(kind, at, isKnown, "FoodMarker_House_");
            // Hidden until the family drops them: crumbs while the mother cooks, leftovers after dinner.
            var crumbFoods = LifeCrumbs.Select(c => AddFood(c.kind, c.at, false, "FoodMarker_Life_")).ToArray();
            var leftoverFoods = LifeLeftovers.Select(c => AddFood(c.kind, c.at, false, "FoodMarker_Life_")).ToArray();
            var scouts = new List<Vector3>(commander.ScoutPoints);
            foreach (var p in LivingScouts.Concat(NorthScouts))
            {
                Vector3 q = p;
                if (!grid.Walkable(q) && !grid.NearestWalkable(q, 1.5f, out q)) throw new InvalidOperationException("リビングの調査地点に到達できません: " + p);
                scouts.Add(q);
            }
            SetArray(cLink.FindProperty("foods"), foods.Cast<UnityEngine.Object>().ToArray());
            knownProp.arraySize = known.Count;
            for (int i = 0; i < known.Count; i++) knownProp.GetArrayElementAtIndex(i).boolValue = known[i];
            var scoutProp = cLink.FindProperty("scoutPoints");
            scoutProp.arraySize = scouts.Count;
            for (int i = 0; i < scouts.Count; i++) scoutProp.GetArrayElementAtIndex(i).vector3Value = scouts[i];
            cLink.FindProperty("grid").objectReferenceValue = grid;
            AppendArray(cLink.FindProperty("hideInSwarm"), renderers.front);
            AppendArray(cLink.FindProperty("ceiling"), renderers.ceiling);
            cLink.ApplyModifiedPropertiesWithoutUndo();
            var iLink = new SerializedObject(session.Interactor);
            SetArray(iLink.FindProperty("foods"), foods.Cast<UnityEngine.Object>().ToArray());
            iLink.ApplyModifiedPropertiesWithoutUndo();
            var colLink = new SerializedObject(colony);
            var spots = colLink.FindProperty("respawnSpots");
            foreach (var p in LivingRespawns.Concat(NorthRespawns))
            {
                spots.arraySize++;
                spots.GetArrayElementAtIndex(spots.arraySize - 1).vector3Value = p;
            }
            colLink.ApplyModifiedPropertiesWithoutUndo();
            var overview = UnityEngine.Object.FindFirstObjectByType<SwarmViewCamera>(FindObjectsInactive.Include);
            overview.Configure(new Vector2(-6.4f, -5.4f), new Vector2(20.6f, 15.6f), commander.Settings3.swarmDistance + 2, commander.Settings3.swarmPanSpeed * 1.6f);
            EditorUtility.SetDirty(overview);

            // ---- The household ----
            var father = session.Human;
            father.ConfigureMember("父", true, 3.5f, 1f);
            EditorUtility.SetDirty(father);
            SetPatrol(father, KitchenStops.Concat(LivingStops).Concat(BedroomStops).ToArray(), "PatrolPoint_");
            var mother = CopyMember(father, "Human_Mother", new Vector3(10.8f, 0, 0.6f), 180, "Mother", 1f);
            var son = CopyMember(father, "Human_Son", new Vector3(18.2f, 0, 0.2f), 250, "Son", 0.72f);
            mother.ConfigureMember("母", true, 3.5f, 1.05f);
            son.ConfigureMember("息子", false, 2.55f, 1.25f);
            SetPatrol(mother, KitchenStops.Take(6).Concat(LivingStops.Take(4)).Concat(BedroomStops.Take(2)).ToArray(), "MotherStop_");
            SetPatrol(son, LivingStops.Concat(KidStops).Concat(new[] { new Vector3(4.0f, 0, -3.0f), new Vector3(1.0f, 0, 2.6f) }).ToArray(), "SonStop_");
            session.ConfigureFamily(new[] { mother, son });
            EditorUtility.SetDirty(session);

            // The mother's vacuum: an orange fan on the floor and the air drawn into the head.
            var warning = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Phase2_Warning.mat");
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + "/Art/Meshes/Phase2_Ring.asset");
            var disc = AssetDatabase.LoadAssetAtPath<Mesh>(PhaseFiveGenerator.DiscPath);
            var cone = AssetDatabase.LoadAssetAtPath<Mesh>(PhaseFiveGenerator.ConePath);
            var fx = GeneratedAssets.Empty("HouseholdAttacks", stageRoot, Vector3.zero);
            var fan = P.Part("VacuumFan", fx, VacuumFan(), FanMaterial(), Vector3.zero, Vector3.one, false, false);
            P.Part("Edge", fan, VacuumFanEdge(), warning, new Vector3(0, 0.004f, 0), Vector3.one, false, false).GetComponent<MeshRenderer>().receiveShadows = false;
            var air = P.Part("VacuumAir", fx, cone, Translucent("Stage02_VacuumAir", new Color(0.86f, 0.9f, 0.92f, 0.28f), new Color(0.18f, 0.2f, 0.22f)), Vector3.zero, Vector3.one, false, false);
            foreach (var t in new[] { fan, air }) { t.GetComponent<MeshRenderer>().receiveShadows = false; t.gameObject.SetActive(false); }
            var vacuum = mother.gameObject.AddComponent<VacuumAttack>();
            vacuum.Configure(session, commander, mother, mother.GetComponentInChildren<BlenderHumanAnimator>(), "mother");
            vacuum.ConfigureVisuals(fan, air);
            // The son's net: the ring and filling disc, the net over a caught roach, and his bug cage.
            var fill = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Phase5_FoggerFill.mat");
            var netRing = P.Part("NetRing", fx, ring, warning, Vector3.zero, Vector3.one, false, false);
            var netFill = P.Part("NetFill", fx, disc, fill, Vector3.zero, Vector3.one, false, false);
            var cover = GeneratedAssets.Empty("NetCover", fx, Vector3.zero);
            var netMaterial = Translucent("Stage02_NetCover", new Color(0.9f, 0.95f, 0.88f, 0.45f), new Color(0.2f, 0.22f, 0.2f));
            P.Sphere("Net", cover, new Vector3(0, 0.12f, 0), new Vector3(1.9f, 0.62f, 1.9f), netMaterial).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            P.Part("Hoop", cover, ring, GeneratedAssets.Material("Stage02_NetHoop", new Color(0.92f, 0.78f, 0.1f)), new Vector3(0, 0.03f, 0), new Vector3(1.95f, 1, 1.95f), false, false);
            foreach (var t in new[] { netRing, netFill, cover }) t.gameObject.SetActive(false);
            var cage = BuildCage(living, session, commander);
            var net = son.gameObject.AddComponent<BugNetAttack>();
            net.Configure(session, commander, son, son.GetComponentInChildren<BlenderHumanAnimator>(), "son");
            net.ConfigureVisuals(netRing, netFill, cover, cage);

            // ---- The house cat ----
            BuildCatBed(living);
            Physics.SyncTransforms();
            var catGrid = BakeCatGrid(room, session);
            var catRoot = GeneratedAssets.Empty("Pet_Cat", stageRoot, CatBed);
            var catSkin = BlenderModelIntegration.CreateSkin("Cat", catRoot);
            foreach (var t in catRoot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;   // never blocks sight lines
            var catRing = P.Part("CatRing", fx, ring, warning, Vector3.zero, Vector3.one, false, false);
            var catFill = P.Part("CatFill", fx, disc, fill, Vector3.zero, Vector3.one, false, false);
            catRing.gameObject.SetActive(false); catFill.gameObject.SetActive(false);
            var catBody = catSkin.root.AddComponent<CatAnimator>();
            var cat = catRoot.gameObject.AddComponent<PetCat>();
            catBody.Configure(cat, catSkin.bones);
            cat.Configure(session, commander, progress, catGrid, catBody, catRing, catFill, catSkin.root.transform, CatBed);
            var catLink = new SerializedObject(cat);
            catLink.FindProperty("area").rectValue = new Rect(-6.5f, -5.5f, 27.5f, 21.5f);   // all four rooms
            catLink.ApplyModifiedPropertiesWithoutUndo();

            // ---- Exit door to the hall (the family evacuates and the father leaves by it) ----
            var door = GeneratedAssets.Material("Phase5_Door", new Color(0.34f, 0.21f, 0.12f));
            var frame = GeneratedAssets.Material("Phase5_DoorFrame", new Color(0.78f, 0.74f, 0.64f));
            var exit = GeneratedAssets.Empty("HallDoor", stageRoot, new Vector3(LivingMaxX - 0.03f, 0, ExitPoint.z));
            P.Box("Panel", exit, new Vector3(0, 2.15f, 0), new Vector3(0.05f, 4.3f, 1.9f), door, false);
            P.Box("FrameLeft", exit, new Vector3(-0.01f, 2.2f, -1.0f), new Vector3(0.08f, 4.4f, 0.12f), frame, false);
            P.Box("FrameRight", exit, new Vector3(-0.01f, 2.2f, 1.0f), new Vector3(0.08f, 4.4f, 0.12f), frame, false);
            P.Box("FrameTop", exit, new Vector3(-0.01f, 4.38f, 0), new Vector3(0.08f, 0.12f, 2.12f), frame, false);
            P.Sphere("Handle", exit, new Vector3(-0.08f, 2.0f, -0.72f), Vector3.one * 0.14f, frame);

            // ---- Stage flow: the Stage 2 definition, its zones and flags ----
            stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(PhaseFiveGenerator.StageAssets[1]);   // the scene load unloaded it again
            var flags = PhaseFiveGenerator.BuildTerritory(stage, stageRoot, commander, grid);
            var pLink = new SerializedObject(progress);
            pLink.FindProperty("stage").objectReferenceValue = stage;
            SetArray(pLink.FindProperty("flags"), flags.Cast<UnityEngine.Object>().ToArray());
            pLink.ApplyModifiedPropertiesWithoutUndo();

            // ---- Daily life: what the family does while the house is calm ----
            BuildLife(stageRoot, session, progress, colony, father, mother, son, crumbFoods, leftoverFoods);

            EditorSceneManager.SaveScene(scene, ScenePath);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log("[GOKI STAGE2] 一軒家を生成: " + ScenePath);
        }

        // ---------------- Definition ----------------
        /// <summary>Stage 2 numbers, harder than Stage 1 on every gauge. Written once (when it was still a placeholder).</summary>
        public static void FillDefinition(StageDefinition stage)
        {
            if (stage.playable && stage.zones.Length > 0)
            {
                // Already set up: keep any tuning. Once, when the bedroom and the son's room were added (2026-10-25):
                // their two zones, the heat map over the whole house and the household calming down again.
                if (stage.zones.Length >= ZoneNames.Length) return;
                stage.zones = stage.zones.Concat(Enumerable.Range(stage.zones.Length, ZoneNames.Length - stage.zones.Length)
                    .Select(i => new StageDefinition.Zone { name = ZoneNames[i], centre = ZoneCentres[i], radius = ZoneRadii[i] })).ToArray();
                stage.heatArea = HouseArea; stage.calmSeconds = 60;
                stage.description = Description;
                EditorUtility.SetDirty(stage);
                return;
            }
            stage.id = "stage02"; stage.title = "一軒家"; stage.subtitle = "家族と猫の住む一軒家（父・母・息子・ミケ）"; stage.difficulty = 3; stage.residents = 3;
            stage.description = Description;
            stage.playable = true; stage.sceneName = Path.GetFileNameWithoutExtension(ScenePath);
            stage.unlocks = "stage03"; stage.unlocksTitle = "飲食店"; stage.reward = ""; stage.rewardTitle = "";
            stage.zones = ZoneNames.Select((n, i) => new StageDefinition.Zone { name = n, centre = ZoneCentres[i], radius = ZoneRadii[i] }).ToArray();
            stage.foodGoal = 45; stage.fearGoal = 170; stage.holdSeconds = 15; stage.holdPresence = 3; stage.residentClearRate = 0.5f; stage.fearPerZone = 9;
            stage.startAllies = 0; stage.allyCorpseSeconds = 20; stage.corpseHeal = 1; stage.slipperReach = 3.6f; stage.tutorial = false;
            stage.residentHuntsAllies = true; stage.allyReactionSeconds = 0.85f; stage.allyStunSeconds = 1.6f; stage.allyWoundHealSeconds = 35;
            stage.allyAlarmRadius = 2.0f; stage.allyCollectReactionScale = 1.6f;
            stage.nestFumigations = new[] { 8f, 30f, 55f }; stage.nestFumigationWarning = 4.5f; stage.nestFumigationSeconds = 15; stage.nestSmokeDamageEvery = 1.1f;
            stage.humanDetectionScale = 1.1f; stage.humanSpeedScale = 1.08f; stage.heatArea = HouseArea; stage.familyLeavesForDefence = true; stage.calmSeconds = 60;
            stage.finalDefenceSeconds = 80; stage.foggerInterval = 11; stage.foggerWarning = 3.2f; stage.foggerDuration = 9; stage.foggerRadius = 3.0f; stage.maxFoggers = 3;
            stage.exitPoint = ExitPoint;
            stage.parSeconds = 1080; stage.fewDiscoveries = 4; stage.someDiscoveries = 10; stage.manyAllies = 8; stage.someAllies = 5;
            EditorUtility.SetDirty(stage);
        }

        public const string Description = "父・母・息子と猫が暮らす4部屋の一軒家。掃除機・網・猫に注意。";

        // ---------------- Rooms ----------------
        private static void LoadMaterials()
        {
            Material Load(string n) => AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/" + n + ".mat");
            walnut = Load("Walnut"); oak = Load("WarmOak"); charcoal = Load("Charcoal"); enamel = Load("DeepTealEnamel"); ivory = Load("WarmCeramic"); metal = Load("BrushedBrass");
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(P.Root + "/Textures/WalnutGrain.asset");
            plasterWarm = P.Material("Stage02_LivingPlaster", new Color(0.44f, 0.39f, 0.32f), 0.08f);
            oakFloor = P.Material("Stage02_OakFloor", new Color(0.74f, 0.58f, 0.40f), 0.38f, 0, wood, new Vector2(5, 7));
        }

        /// <summary>Doorway in the shared wall, the living room shell and ceiling. Returns the renderers the tactics view hides.</summary>
        private static (Renderer[] front, Renderer[] ceiling) BuildRooms(Transform room, Transform living)
        {
            var kitchenPlaster = room.Find("Wall_Right").GetComponent<MeshRenderer>().sharedMaterial;
            Kill(room.Find("Wall_Right").gameObject);
            foreach (Transform t in room.Cast<Transform>().ToArray())
                if (t.name == "Skirting" && t.localPosition.x > 6.5f) Kill(t.gameObject);
            // Shared wall: two pieces and a lintel around the doorway (z DoorZMin .. DoorZMax).
            float zA = (-6.24f + DoorZMin) / 2, zB = (DoorZMax + 6.24f) / 2;
            Climb(P.Box("Wall_Right", room, new Vector3(7.12f, 2.7f, zA), new Vector3(0.24f, 5.4f, DoorZMin + 6.24f), kitchenPlaster));
            Climb(P.Box("Wall_Right", room, new Vector3(7.12f, 2.7f, zB), new Vector3(0.24f, 5.4f, 6.24f - DoorZMax), kitchenPlaster));
            Climb(P.Box("Wall_Right", room, new Vector3(7.12f, (DoorHeight + 5.4f) / 2, (DoorZMin + DoorZMax) / 2), new Vector3(0.24f, 5.4f - DoorHeight, DoorZMax - DoorZMin), kitchenPlaster));
            // The living side wears the living room's colour.
            P.Box("LivingFacing", living, new Vector3(7.245f, 2.7f, zA), new Vector3(0.01f, 5.4f, DoorZMin + 6.0f), plasterWarm, false);
            P.Box("LivingFacing", living, new Vector3(7.245f, 2.7f, zB), new Vector3(0.01f, 5.4f, 6.0f - DoorZMax), plasterWarm, false);
            P.Box("LivingFacing", living, new Vector3(7.245f, (DoorHeight + 5.4f) / 2, (DoorZMin + DoorZMax) / 2), new Vector3(0.01f, 5.4f - DoorHeight, DoorZMax - DoorZMin), plasterWarm, false);
            var trim = oak;
            foreach (float x in new[] { 6.98f, 7.27f })
            {
                P.Box("DoorTrim", living, new Vector3(x, DoorHeight / 2, DoorZMin - 0.05f), new Vector3(0.06f, DoorHeight, 0.12f), trim, false);
                P.Box("DoorTrim", living, new Vector3(x, DoorHeight / 2, DoorZMax + 0.05f), new Vector3(0.06f, DoorHeight, 0.12f), trim, false);
                P.Box("DoorTrim", living, new Vector3(x, DoorHeight + 0.05f, (DoorZMin + DoorZMax) / 2), new Vector3(0.06f, 0.12f, DoorZMax - DoorZMin + 0.2f), trim, false);
            }
            P.Box("Skirting", room, new Vector3(6.96f, 0.15f, (-6 + DoorZMin) / 2), new Vector3(0.08f, 0.3f, DoorZMin + 6), walnut, false);
            P.Box("Skirting", room, new Vector3(6.96f, 0.15f, (DoorZMax + 6) / 2), new Vector3(0.08f, 0.3f, 6 - DoorZMax), walnut, false);
            P.Box("Threshold", living, new Vector3(7.12f, 0.012f, (DoorZMin + DoorZMax) / 2), new Vector3(0.3f, 0.02f, DoorZMax - DoorZMin), oak, false);

            float cx = (LivingMinX + LivingMaxX) / 2, w = LivingMaxX - LivingMinX;
            P.Box("Floor", living, new Vector3(cx, -0.15f, 0), new Vector3(w + 0.1f, 0.3f, 12), oakFloor);
            WallWithDoor("Wall_Back", living, 7.0f, LivingMaxX + 0.24f, KidDoorXMin, KidDoorXMax, 6.12f, true, plasterWarm);
            var front = Climb(P.Box("Wall_Front", living, new Vector3(cx, 2.7f, -6.12f), new Vector3(w + 0.24f, 5.4f, 0.24f), plasterWarm));
            Climb(P.Box("Wall_Right", living, new Vector3(LivingMaxX + 0.12f, 2.7f, 0), new Vector3(0.24f, 5.4f, 12.48f), plasterWarm));
            var ceiling = Climb(P.Box("Ceiling", living, new Vector3(cx, 5.5f, 0), new Vector3(w + 0.48f, 0.2f, 12.48f), plasterWarm));
            ceiling.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            P.Box("Skirting", living, new Vector3(cx, 0.15f, -5.96f), new Vector3(w, 0.3f, 0.08f), walnut, false);
            Skirting(living, LivingMinX, LivingMaxX, KidDoorXMin, KidDoorXMax, 5.96f, true);
            P.Box("Skirting", living, new Vector3(LivingMaxX - 0.04f, 0.15f, 0), new Vector3(0.08f, 0.3f, 12), walnut, false);
            P.Box("Skirting", living, new Vector3(7.28f, 0.15f, (-6 + DoorZMin) / 2), new Vector3(0.08f, 0.3f, DoorZMin + 6), walnut, false);
            P.Box("Skirting", living, new Vector3(7.28f, 0.15f, (DoorZMax + 6) / 2), new Vector3(0.08f, 0.3f, 6 - DoorZMax), walnut, false);
            return (new Renderer[] { front.GetComponent<Renderer>() }, new Renderer[] { ceiling.GetComponent<Renderer>() });
        }

        private static void Furnish(Transform living)
        {
            var shadow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/FurnitureContact.mat");
            // Sofa on tall legs: roaches hide underneath (the slipper cannot reach under it).
            var fabric = P.Material("Stage02_SofaFabric", new Color(0.17f, 0.25f, 0.28f), 0.06f);
            var cushion = P.Material("Stage02_Cushion", new Color(0.68f, 0.46f, 0.16f), 0.06f);
            var sofa = GeneratedAssets.Empty("Sofa", living, new Vector3(15.5f, 0, 4.75f));
            Climb(P.Box("Seat", sofa, new Vector3(0, 1.5f, -0.1f), new Vector3(5.2f, 0.62f, 2.0f), fabric));
            Climb(P.Box("Back", sofa, new Vector3(0, 2.5f, 0.82f), new Vector3(5.2f, 1.9f, 0.5f), fabric));
            for (int s = -1; s <= 1; s += 2) Climb(P.Box("Arm", sofa, new Vector3(s * 2.75f, 2.05f, -0.05f), new Vector3(0.45f, 1.1f, 2.15f), fabric));
            for (int i = 0; i < 3; i++) P.Box("SeatCushion", sofa, new Vector3(-1.65f + i * 1.65f, 1.88f, -0.2f), new Vector3(1.6f, 0.2f, 1.75f), fabric, false);
            P.Box("Pillow", sofa, new Vector3(-1.7f, 2.45f, 0.42f), new Vector3(1.0f, 0.9f, 0.3f), cushion, false).localRotation = Quaternion.Euler(-12, 8, 6);
            P.Box("Pillow", sofa, new Vector3(1.9f, 2.4f, 0.42f), new Vector3(0.9f, 0.85f, 0.3f), cushion, false).localRotation = Quaternion.Euler(-12, -10, -5);
            foreach (var at in new[] { new Vector3(-2.5f, 0, -0.95f), new Vector3(2.5f, 0, -0.95f), new Vector3(-2.5f, 0, 0.9f), new Vector3(2.5f, 0, 0.9f) })
            {
                P.Rod("SofaLeg", sofa, at, at + Vector3.up * 1.2f, 0.16f, walnut);
                P.Box("LegCollider", sofa, at + Vector3.up * 0.6f, new Vector3(0.16f, 1.2f, 0.16f), walnut).GetComponent<MeshRenderer>().enabled = false;
            }
            P.Shadow("SofaAO", living, new Vector3(15.5f, 0.006f, 4.7f), new Vector2(6.0f, 2.8f), shadow);

            // Coffee table on a checked rug.
            var rug = P.Material("Stage02_Rug", new Color(0.92f, 0.86f, 0.8f), 0.04f, 0, AssetDatabase.LoadAssetAtPath<Texture2D>(P.Root + "/Textures/WovenCheck.asset"), new Vector2(3, 2));
            P.Box("Rug", living, new Vector3(15.5f, 0.012f, 1.0f), new Vector3(7.6f, 0.02f, 5.4f), rug, false);
            var table = GeneratedAssets.Empty("CoffeeTable", living, new Vector3(15.5f, 0, 1.4f));
            Climb(P.Box("Top", table, new Vector3(0, 1.78f, 0), new Vector3(3.6f, 0.18f, 2.0f), oak));
            foreach (var at in new[] { new Vector3(-1.6f, 0, -0.8f), new Vector3(1.6f, 0, -0.8f), new Vector3(-1.6f, 0, 0.8f), new Vector3(1.6f, 0, 0.8f) })
            {
                P.Rod("TableLeg", table, at, at + Vector3.up * 1.7f, 0.15f, walnut);
                P.Box("LegCollider", table, at + Vector3.up * 0.85f, new Vector3(0.15f, 1.7f, 0.15f), walnut).GetComponent<MeshRenderer>().enabled = false;
            }
            P.Sphere("SnackBowl", table, new Vector3(0.9f, 1.95f, -0.2f), new Vector3(0.75f, 0.18f, 0.75f), ivory);
            P.Box("Remote", table, new Vector3(-1.0f, 1.9f, 0.4f), new Vector3(0.18f, 0.06f, 0.55f), charcoal, false).localRotation = Quaternion.Euler(0, 25, 0);

            // TV board against the front wall, the TV glowing toward the sofa.
            var board = GeneratedAssets.Empty("TvBoard", living, new Vector3(15.5f, 0, -5.25f));
            Climb(P.Box("Cabinet", board, new Vector3(0, 0.7f, 0), new Vector3(5.2f, 1.4f, 1.3f), walnut));
            for (int i = 0; i < 3; i++) P.Box("Drawer", board, new Vector3(-1.7f + i * 1.7f, 0.7f, 0.66f), new Vector3(1.55f, 1.15f, 0.04f), oak, false);
            var screen = P.Material("Stage02_TvScreen", new Color(0.05f, 0.08f, 0.12f), 0.85f, 0, null, null, new Color(0.10f, 0.22f, 0.38f));
            Climb(P.Box("TvBody", board, new Vector3(0, 2.75f, -0.15f), new Vector3(4.4f, 2.5f, 0.16f), charcoal));
            P.Box("TvScreen", board, new Vector3(0, 2.78f, -0.06f), new Vector3(4.2f, 2.3f, 0.02f), screen, false);
            P.Box("TvStand", board, new Vector3(0, 1.48f, -0.15f), new Vector3(1.2f, 0.16f, 0.6f), charcoal, false);
            var tvLight = GeneratedAssets.Empty("TvGlow", living, new Vector3(15.5f, 2.6f, -4.0f)).gameObject.AddComponent<Light>();
            tvLight.type = LightType.Point; tvLight.color = new Color(0.55f, 0.72f, 1f); tvLight.intensity = 1.3f; tvLight.range = 6.5f; tvLight.shadows = LightShadows.None; tvLight.renderMode = LightRenderMode.ForceVertex;

            // Bookshelf on the right wall (climbable, food on top).
            var shelf = GeneratedAssets.Empty("Bookshelf", living, new Vector3(20.55f, 0, 2.5f));
            Climb(P.Box("Body", shelf, new Vector3(0, 2.4f, 0), new Vector3(1.2f, 4.8f, 3.0f), walnut));
            Color[] spines = { new Color(0.55f, 0.12f, 0.1f), new Color(0.12f, 0.24f, 0.45f), new Color(0.75f, 0.62f, 0.3f), new Color(0.2f, 0.36f, 0.2f), new Color(0.82f, 0.8f, 0.74f) };
            var bookMats = spines.Select((c, i) => P.Material("Stage02_Book" + i, c, 0.12f)).ToArray();
            for (int row = 0; row < 4; row++)
                for (int b = 0; b < 9; b++)
                {
                    float h = 0.62f + ((b * 7 + row * 3) % 5) * 0.06f;
                    P.Box("Book", shelf, new Vector3(-0.62f, 0.25f + row * 1.15f + h / 2, -1.25f + b * 0.29f), new Vector3(0.06f, h, 0.24f), bookMats[(b + row * 2) % bookMats.Length], false);
                }

            // The son's corner: toy box, blocks, a ball, a play mat.
            var red = P.Material("Stage02_ToyRed", new Color(0.78f, 0.12f, 0.1f), 0.5f);
            var blue = P.Material("Stage02_ToyBlue", new Color(0.12f, 0.32f, 0.72f), 0.5f);
            var yellow = P.Material("Stage02_ToyYellow", new Color(0.92f, 0.72f, 0.1f), 0.5f);
            P.Box("PlayMat", living, new Vector3(18.4f, 0.03f, -3.0f), new Vector3(4.0f, 0.02f, 3.4f), P.Material("Stage02_PlayMat", new Color(0.35f, 0.62f, 0.55f), 0.1f), false);
            var toybox = GeneratedAssets.Empty("ToyBox", living, new Vector3(19.4f, 0, -4.9f));
            Climb(P.Box("Box", toybox, new Vector3(0, 0.65f, 0), new Vector3(2.0f, 1.3f, 1.3f), blue));
            P.Box("Lid", toybox, new Vector3(0, 1.34f, -0.1f), new Vector3(2.05f, 0.1f, 1.35f), yellow, false).localRotation = Quaternion.Euler(-14, 0, 0);
            P.Box("Block", living, new Vector3(17.2f, 0.18f, -2.0f), Vector3.one * 0.36f, red, false).localRotation = Quaternion.Euler(0, 20, 0);
            P.Box("Block", living, new Vector3(17.5f, 0.18f, -1.6f), Vector3.one * 0.36f, yellow, false).localRotation = Quaternion.Euler(0, 55, 0);
            P.Box("Block", living, new Vector3(17.35f, 0.54f, -1.85f), Vector3.one * 0.36f, blue, false).localRotation = Quaternion.Euler(0, 35, 0);
            P.Sphere("Ball", living, new Vector3(16.6f, 0.4f, -4.4f), Vector3.one * 0.8f, red);

            // Bin by the doorway, plant and floor lamp, window with curtains above the sofa, a ceiling lamp.
            var bin = GeneratedAssets.Empty("TrashBin", living, new Vector3(8.2f, 0, -5.3f));
            Climb(P.Rod("Bin", bin, Vector3.zero, new Vector3(0, 1.2f, 0), 0.9f, P.Material("Stage02_Bin", new Color(0.62f, 0.64f, 0.6f), 0.4f, 0.3f)));
            var lamp = GeneratedAssets.Empty("FloorLamp", living, new Vector3(8.4f, 0, 5.2f));
            P.Rod("Pole", lamp, Vector3.zero, new Vector3(0, 3.6f, 0), 0.08f, metal);
            P.Rod("Base", lamp, Vector3.zero, new Vector3(0, 0.06f, 0), 0.7f, charcoal);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/LampGlow.mat");
            P.Sphere("Shade", lamp, new Vector3(0, 3.7f, 0), new Vector3(0.9f, 0.6f, 0.9f), glow);
            var lampLight = GeneratedAssets.Empty("LampLight", lamp, new Vector3(0, 3.4f, -0.4f)).gameObject.AddComponent<Light>();
            lampLight.type = LightType.Point; lampLight.color = new Color(1f, 0.66f, 0.34f); lampLight.intensity = 1.5f; lampLight.range = 5.5f; lampLight.shadows = LightShadows.None; lampLight.renderMode = LightRenderMode.ForceVertex;
            var pot = GeneratedAssets.Empty("BigPlant", living, new Vector3(20.6f, 0, -0.0f + 5.3f));
            P.Rod("Pot", pot, Vector3.zero, new Vector3(0, 1.0f, 0), 0.8f, AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/Terracotta.mat"));
            var leaf = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/HerbLeaves.mat");
            for (int i = 0; i < 9; i++)
            {
                Vector3 end = new Vector3(Mathf.Sin(i * 2.4f) * 0.6f, 2.0f + (i % 3) * 0.45f, Mathf.Cos(i * 2.4f) * 0.6f);
                P.Rod("Stem", pot, new Vector3(0, 1.0f, 0), end, 0.04f, leaf);
                P.Sphere("Leaf", pot, end, new Vector3(0.35f, 0.08f, 0.8f), leaf).localRotation = Quaternion.Euler(20, i * 137, 25);
            }
            Picture(living, new Vector3(15.5f, 4.0f, 5.97f), 0, new Color(0.46f, 0.62f, 0.72f), new Color(0.32f, 0.46f, 0.26f));
            P.Sphere("CeilingLamp", living, new Vector3(15.0f, 5.33f, 0.4f), new Vector3(1.6f, 0.18f, 1.6f), glow);
            var main = GeneratedAssets.Empty("LivingLight", living, new Vector3(15.0f, 4.4f, 0.4f)).gameObject.AddComponent<Light>();
            main.type = LightType.Point; main.color = new Color(1f, 0.74f, 0.46f); main.intensity = 2.8f; main.range = 11f; main.shadows = LightShadows.None; main.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>The son's bug cage: a green frame with mesh sides and up to nine prisoners wriggling inside.</summary>
        private static BugCage BuildCage(Transform living, PhaseTwoSession session, SwarmCommander commander)
        {
            var cageRoot = GeneratedAssets.Empty("BugCage", living, CagePosition);
            cageRoot.localRotation = Quaternion.Euler(0, -90, 0);   // the open side faces the room (west)
            var frame = P.Material("Stage02_CageFrame", new Color(0.18f, 0.55f, 0.24f), 0.55f);
            var mesh = Translucent("Stage02_CageMesh", new Color(0.72f, 0.86f, 0.7f, 0.32f), new Color(0.1f, 0.14f, 0.1f));
            var body = P.Box("CageBody", cageRoot, new Vector3(0, 0.55f, 0), new Vector3(1.5f, 1.1f, 1.0f), mesh, false);
            body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var solid = cageRoot.gameObject.AddComponent<BoxCollider>();
            solid.center = new Vector3(0, 0.55f, 0); solid.size = new Vector3(1.5f, 1.1f, 1.0f);
            P.Box("Base", cageRoot, new Vector3(0, 0.05f, 0), new Vector3(1.56f, 0.1f, 1.06f), frame, false);
            P.Box("Lid", cageRoot, new Vector3(0, 1.12f, 0), new Vector3(1.56f, 0.1f, 1.06f), frame, false);
            P.Rod("Handle", cageRoot, new Vector3(-0.5f, 1.17f, 0), new Vector3(0.5f, 1.17f, 0), 0.07f, frame);
            foreach (var c in new[] { new Vector3(-0.75f, 0, -0.5f), new Vector3(0.75f, 0, -0.5f), new Vector3(-0.75f, 0, 0.5f), new Vector3(0.75f, 0, 0.5f) })
                P.Rod("Post", cageRoot, c, c + Vector3.up * 1.12f, 0.06f, frame);
            var shell = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/Roach_AmberShell.mat");
            var legs = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/Roach_Legs.mat");
            var prisoners = new Transform[9];
            for (int i = 0; i < prisoners.Length; i++)
            {
                var bug = GeneratedAssets.Empty("Prisoner_" + i, cageRoot, new Vector3(-0.5f + (i % 3) * 0.5f, 0.16f + (i / 3) * 0.02f, -0.28f + (i / 3) * 0.28f));
                P.Sphere("Body", bug, Vector3.zero, new Vector3(0.18f, 0.08f, 0.32f), shell);
                for (int s = -1; s <= 1; s += 2) P.Rod("Antenna", bug, new Vector3(s * 0.03f, 0.02f, 0.15f), new Vector3(s * 0.12f, 0.05f, 0.35f), 0.012f, legs);
                prisoners[i] = bug;
            }
            var cage = cageRoot.gameObject.AddComponent<BugCage>();
            cage.Configure(session, commander, prisoners);
            return cage;
        }

        /// <summary>A round cushion bed beside the sofa with a food bowl: where the cat naps.</summary>
        private static void BuildCatBed(Transform living)
        {
            var cushion = P.Material("Stage02_CatBed", new Color(0.55f, 0.24f, 0.22f), 0.05f);
            var inner = P.Material("Stage02_CatBedInner", new Color(0.86f, 0.8f, 0.7f), 0.05f);
            var bed = GeneratedAssets.Empty("CatBed", living, CatBed);
            P.Rod("Rim", bed, Vector3.zero, new Vector3(0, 0.32f, 0), 2.2f, cushion);
            P.Rod("Cushion", bed, new Vector3(0, 0.02f, 0), new Vector3(0, 0.36f, 0), 1.8f, inner);
            P.Sphere("Bowl", living, CatBed + new Vector3(-1.3f, 0.12f, -0.9f), new Vector3(0.7f, 0.24f, 0.7f), P.Material("Stage02_CatBowl", new Color(0.2f, 0.42f, 0.7f), 0.6f));
        }
        /// <summary>
        /// The cat's own floor: a cat-sized probe (radius 0.55 at height 1.0) instead of a roach's, so it cannot go under the
        /// sofa or the coffee table, and never into the nest (the hollow cabinet), whatever fits.
        /// </summary>
        public static AllyNavGrid BakeCatGrid(Transform room, PhaseTwoSession session)
        {
            const float cell = 0.25f;
            var data = new byte[GridWidth * GridDepth];
            var hits = new Collider[16];
            for (int z = 0; z < GridDepth; z++)
            for (int x = 0; x < GridWidth; x++)
            {
                var centre = GridOrigin + new Vector3((x + 0.5f) * cell, 1.0f, (z + 0.5f) * cell);
                int count = Physics.OverlapSphereNonAlloc(centre, 0.55f, hits, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = session.InNest(new Vector3(centre.x, 0.1f, centre.z));
                for (int i = 0; i < count && !blocked; i++) blocked = hits[i].transform.IsChildOf(room);
                data[z * GridWidth + x] = blocked ? AllyNavGrid.Blocked : AllyNavGrid.Open;
            }
            GeneratedAssets.RequireOwned(CatGridPath);
            var grid = AssetDatabase.LoadAssetAtPath<AllyNavGrid>(CatGridPath);
            bool created = !grid;
            if (created) grid = ScriptableObject.CreateInstance<AllyNavGrid>();
            grid.Bake(GridOrigin, cell, GridWidth, GridDepth, data);
            if (created) { AssetDatabase.CreateAsset(grid, CatGridPath); GeneratedAssets.Mark(grid); }
            EditorUtility.SetDirty(grid);
            return grid;
        }
        /// <summary>The cat's LOD0 mesh for the encyclopedia (imported once if the stage has not been generated yet).</summary>
        public static Mesh CatModel()
        {
            string path = BlenderModelIntegration.Root + "/Meshes/Cat_Detail0.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) return mesh;
            var skin = BlenderModelIntegration.CreateSkin("Cat", null);
            UnityEngine.Object.DestroyImmediate(skin.root);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        // ---------------- Navigation ----------------
        private static NavMeshData BakeNavigation(Transform room)
        {
            GeneratedAssets.RequireOwned(NavPath);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(room, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var build = NavMesh.GetSettingsByIndex(0);
            build.agentRadius = 0.42f; build.agentHeight = 4.1f; build.agentClimb = 0.18f; build.agentSlope = 40;
            build.overrideVoxelSize = true; build.voxelSize = 0.10f;
            var baked = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(new Vector3(7.2f, 2, 5.1f), new Vector3(30, 6, 24)), Vector3.zero, Quaternion.identity);
            if (!baked) throw new InvalidOperationException("一軒家のNavMesh生成に失敗しました。");
            var navigation = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (navigation) { EditorUtility.CopySerialized(baked, navigation); UnityEngine.Object.DestroyImmediate(baked); }
            else { navigation = baked; AssetDatabase.CreateAsset(navigation, NavPath); GeneratedAssets.Mark(navigation); }
            EditorUtility.SetDirty(navigation);
            return navigation;
        }

        // ---------------- The household ----------------
        /// <summary>A copy of the resident (same skeleton, animator and AI) wearing another family member's meshes.</summary>
        private static HumanAIController CopyMember(HumanAIController father, string name, Vector3 at, float yaw, string actor, float scale)
        {
            var copy = UnityEngine.Object.Instantiate(father.gameObject, father.transform.parent);
            copy.name = name;
            copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var member = copy.GetComponent<HumanAIController>();
            foreach (var look in copy.GetComponentsInChildren<ExterminatorLook>(true)) UnityEngine.Object.DestroyImmediate(look);
            var can = copy.transform.Find("SprayCan");
            if (can) UnityEngine.Object.DestroyImmediate(can.gameObject);
            var resident = ExterminatorModelIntegration.ResidentMeshes();
            var meshes = FamilyModelIntegration.Import(actor);
            var skins = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => Array.IndexOf(resident, r.sharedMesh) >= 0).ToArray();
            if (skins.Length != 3) throw new InvalidOperationException(name + ": 住人のスキン（3段階）が見つかりません");
            foreach (var r in skins) { r.sharedMesh = meshes[Array.IndexOf(resident, r.sharedMesh)]; EditorUtility.SetDirty(r); }
            var skinRoot = skins[0].transform.parent;
            skinRoot.localScale = Vector3.one * scale;
            if (scale < 1)
            {
                var capsule = copy.GetComponent<CapsuleCollider>();
                capsule.height = 4.1f * scale; capsule.center = new Vector3(0, 2.05f * scale, 0);
                copy.GetComponent<NavMeshAgent>().height = 4.1f * scale;
            }
            // A slipper warning ring of its own.
            var link = new SerializedObject(member);
            var ring = (Transform)link.FindProperty("warning").objectReferenceValue;
            var ownRing = UnityEngine.Object.Instantiate(ring.gameObject, ring.parent);
            ownRing.name = "SlipperAttackWarning_" + actor;
            ownRing.SetActive(false);
            link.FindProperty("warning").objectReferenceValue = ownRing.transform;
            link.ApplyModifiedPropertiesWithoutUndo();
            // The tool in the left hand (vacuum head, net) aims like the exterminator's lance.
            var animator = copy.GetComponentInChildren<BlenderHumanAnimator>();
            var leftHand = (Transform)new SerializedObject(animator).FindProperty("leftHand").objectReferenceValue;
            animator.ConfigureLance(leftHand.InverseTransformPoint(skinRoot.TransformPoint(FamilyModelIntegration.ToolTip(actor))));
            EditorUtility.SetDirty(animator);
            return member;
        }

        private static void SetPatrol(HumanAIController human, Vector3[] stops, string prefix)
        {
            WithNavMesh(() =>
            {
                var link = new SerializedObject(human);
                var points = link.FindProperty("patrolPoints");
                var parent = human.transform.parent;
                var result = new Transform[stops.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    if (!NavMesh.SamplePosition(stops[i], out var hit, 1.2f, NavMesh.AllAreas)) throw new InvalidOperationException("巡回地点が人間の通り道にありません: " + stops[i]);
                    var old = prefix == "PatrolPoint_" && i < points.arraySize ? (Transform)points.GetArrayElementAtIndex(i).objectReferenceValue : null;
                    result[i] = old ? old : GeneratedAssets.Empty(prefix + i, parent, Vector3.zero);
                    result[i].position = hit.position;
                }
                SetArray(points, result.Cast<UnityEngine.Object>().ToArray());
                link.ApplyModifiedPropertiesWithoutUndo();
            });
        }
        /// <summary>Runs with the house's human NavMesh loaded (for sampling points on it).</summary>
        private static void WithNavMesh(Action work)
        {
            var navigation = (NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            var instance = NavMesh.AddNavMeshData(navigation);
            try { work(); }
            finally { instance.Remove(); }
        }

        // ---------------- Meshes and materials ----------------
        private static Mesh VacuumFan()
        {
            GeneratedAssets.RequireOwned(FanPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FanPath);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = "Stage02_VacuumFan" }; else mesh.Clear();
            const int segments = 18;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-VacuumAttack.HalfAngle, VacuumAttack.HalfAngle, i / (float)segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            mesh.vertices = vertices; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (created) { AssetDatabase.CreateAsset(mesh, FanPath); GeneratedAssets.Mark(mesh); }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
        public const string FanEdgePath = GeneratedAssets.Root + "/Art/Meshes/Stage02_VacuumFanEdge.asset";
        // The fan's outline (both sides and the far arc) as thin flat strips, in the same unit space as the fan.
        private static Mesh VacuumFanEdge()
        {
            GeneratedAssets.RequireOwned(FanEdgePath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FanEdgePath);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = "Stage02_VacuumFanEdge" }; else mesh.Clear();
            const float w = 0.014f; const int segments = 18;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Strip(Vector3 a, Vector3 b)
            {
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * w;
                int o = vertices.Count;
                vertices.Add(a - side); vertices.Add(a + side); vertices.Add(b + side); vertices.Add(b - side);
                triangles.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });   // facing up
            }
            Vector3 At(float t) { float a = Mathf.Lerp(-VacuumAttack.HalfAngle, VacuumAttack.HalfAngle, t) * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }
            Strip(Vector3.zero, At(0)); Strip(Vector3.zero, At(1));
            for (int i = 0; i < segments; i++) Strip(At(i / (float)segments), At((i + 1) / (float)segments));
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (created) { AssetDatabase.CreateAsset(mesh, FanEdgePath); GeneratedAssets.Mark(mesh); }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
        private static Material FanMaterial() => Translucent("Stage02_VacuumFan", new Color(1f, 0.45f, 0.1f, 0.5f), new Color(0.6f, 0.24f, 0.05f));
        private static Material Translucent(string name, Color color, Color emission)
        {
            var m = GeneratedAssets.Material(name, color);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Mode", 2);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------- Helpers ----------------
        private static Transform Climb(Transform part)
        {
            if (!part.GetComponent<Collider>()) part.gameObject.AddComponent<BoxCollider>();
            if (!part.GetComponent<ClimbableSurface>()) part.gameObject.AddComponent<ClimbableSurface>();
            return part;
        }
        private static void Kill(GameObject item) { if (item) UnityEngine.Object.DestroyImmediate(item); }
        private static bool Assign(SerializedObject link, string property, UnityEngine.Object value)
        { link.FindProperty(property).objectReferenceValue = value; link.ApplyModifiedPropertiesWithoutUndo(); return true; }
        private static void SetArray(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static void AppendArray(SerializedProperty array, Renderer[] values)
        {
            foreach (var v in values) { array.arraySize++; array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = v; }
        }
    }
}
