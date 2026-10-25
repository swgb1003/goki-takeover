using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    public static class PhaseTwoGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase2_Kitchen_Infiltration.unity";
        public const string SettingsPath = GeneratedAssets.Root + "/Data/PhaseTwoSettings.asset";
        public const string NavPath = GeneratedAssets.Root + "/Data/PhaseTwoNavigation.asset";
        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 2 Infiltration", priority = 20)]
        public static void OpenMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Generate();
        }
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 2 Infiltration", priority = 21)]
        public static void RebuildMenu()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Generate(); }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 2 Infiltration", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 2 Infiltration", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Build Phase 2 Windows 720p", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem("Tools/GOKI TAKEOVER/Build Phase 2 Windows 720p", priority = 32)]
        public static void Build() => PhaseOneBuild.BuildScene(ScenePath, "Build/GokiTakeover_Phase2/GokiTakeover.exe", BuildOptions.None);
        public static void BuildValidation() => PhaseOneBuild.BuildScene(ScenePath, "Build/GokiTakeover_Phase2_Validation/GokiTakeover.exe", BuildOptions.Development);
        public static void BuildBoth() { BuildValidation(); Build(); }

        public static void Generate()
        {
            GeneratedAssets.RequireOwned(ScenePath);
            GeneratedAssets.RequireOwned(NavPath);
            if (!File.Exists(PolishedKitchenGenerator.ScenePath)) throw new InvalidOperationException("先に改善版キッチンを生成してください。");
            var scene = EditorSceneManager.OpenScene(PolishedKitchenGenerator.ScenePath);
            // Copy the saved scene; never rerun Phase 1 asset generators or overwrite their data.
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Phase 2シーンを保存できません。");
            var oldHud = UnityEngine.Object.FindFirstObjectByType<PolishedHud>();
            UnityEngine.Object.DestroyImmediate(oldHud);
            var marker = GameObject.Find("HumanMarker_StaticSlipper");
            if (marker) UnityEngine.Object.DestroyImmediate(marker);
            P.BoxMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/BeveledBox.asset");
            P.SphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/SmoothLowPolySphere.asset");
            P.RodMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/EightSidedRod.asset");
            var root = new GameObject("Phase2_Gameplay").transform;
            var session = root.gameObject.AddComponent<PhaseTwoSession>();
            var settings = AssetDatabase.LoadAssetAtPath<PhaseTwoSettings>(SettingsPath);
            if (!settings)
            {
                GeneratedAssets.RequireOwned(SettingsPath);
                settings = ScriptableObject.CreateInstance<PhaseTwoSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath); GeneratedAssets.Mark(settings);
            }
            var lime = Material("Lime", new Color(0.61f, 0.80f, 0.16f));
            var orange = Material("Warning", new Color(1, 0.34f, 0.045f));
            var dark = Material("Dark", new Color(0.035f, 0.045f, 0.04f));
            var teal = Material("Clothes", new Color(0.15f, 0.34f, 0.32f));
            var skin = Material("Skin", new Color(0.70f, 0.48f, 0.30f));
            var glue = Material("Glue", new Color(0.54f, 0.32f, 0.075f));
            Transform nest = GeneratedAssets.Empty("Nest_SafeHome", root, new Vector3(-6, 0, -4.5f));
            P.Box("NestOpening", nest, new Vector3(-0.92f, 0.22f, 0), new Vector3(0.10f, 0.44f, 0.9f), dark, false);
            P.Box("NestRoof", nest, new Vector3(-0.78f, 0.42f, 0), new Vector3(0.30f, 0.06f, 0.95f), dark, false);
            P.Part("NestRing", nest, RingMesh(), lime, new Vector3(0, 0.018f, 0), new Vector3(2.2f, 1, 2.2f));
            P.Box("NestBeacon", nest, new Vector3(-0.75f, 0.60f, 0), Vector3.one * 0.13f, lime, false).localRotation = Quaternion.Euler(20, 45, 20);
            var motor = UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
            var follow = UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>();
            motor.transform.SetPositionAndRotation(new Vector3(-5.2f, 0.04f, -4.3f), Quaternion.Euler(0, 45, 0));
            var interactor = motor.gameObject.AddComponent<PlayerInteractor>();
            Transform carry = GeneratedAssets.Empty("FoodCarryAnchor", motor.transform, new Vector3(0, 0.27f, 0.48f));
            var foods = new FoodItem[3];
            for (int i = 0; i < 3; i++)
            {
                var food = GameObject.Find("FoodMarker_" + i);
                foreach (Transform part in food.GetComponentsInChildren<Transform>()) part.gameObject.isStatic = false;
                var contact = food.transform.Find("CrumbAO");
                if (contact) UnityEngine.Object.DestroyImmediate(contact.gameObject);
                foods[i] = food.AddComponent<FoodItem>();
                P.Box("FoodBeacon", food.transform, new Vector3(0, 0.40f, 0), Vector3.one * 0.10f, lime, false).localRotation = Quaternion.Euler(20, 45, 20);
            }
            interactor.Configure(session, foods, carry);
            var traps = new StickyTrap[2];
            Vector3[] trapPositions = { new Vector3(2.4f, 0, 1.6f), new Vector3(-0.2f, 0, -2.8f) };
            for (int i = 0; i < traps.Length; i++)
            {
                var trap = GeneratedAssets.Empty("StickyTrap_" + i, root, trapPositions[i]);
                P.Box("Cardboard", trap, new Vector3(0, 0.014f, 0), new Vector3(1.3f, 0.028f, 1), dark, false);
                P.Box("StickySurface", trap, new Vector3(0, 0.033f, 0), new Vector3(1.16f, 0.015f, 0.86f), glue, false);
                for (int j = -1; j <= 1; j++) P.Box("WarningStripe", trap, new Vector3(j * 0.4f, 0.035f, -0.46f), new Vector3(0.20f, 0.02f, 0.06f), orange, false);
                var area = trap.gameObject.AddComponent<BoxCollider>();
                area.isTrigger = true; area.center = new Vector3(0, 0.25f, 0); area.size = new Vector3(1.3f, 0.6f, 1);
                traps[i] = trap.gameObject.AddComponent<StickyTrap>();
            }
            var room = GameObject.Find("Room").transform;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(room, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var build = NavMesh.GetSettingsByIndex(0);
            build.agentRadius = 0.42f; build.agentHeight = 4.1f; build.agentClimb = 0.18f; build.agentSlope = 40;
            build.overrideVoxelSize = true; build.voxelSize = 0.10f;
            var baked = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(new Vector3(0, 2, 0), new Vector3(16, 6, 14)), Vector3.zero, Quaternion.identity);
            if (!baked) throw new InvalidOperationException("人間のNavMesh生成に失敗しました。");
            var navigation = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (navigation) { EditorUtility.CopySerialized(baked, navigation); UnityEngine.Object.DestroyImmediate(baked); }
            else { navigation = baked; AssetDatabase.CreateAsset(navigation, NavPath); GeneratedAssets.Mark(navigation); }
            EditorUtility.SetDirty(navigation);
            root.gameObject.AddComponent<KitchenNavigation>().Configure(navigation);
            var humanRoot = GeneratedAssets.Empty("Human_Resident", root, new Vector3(3.6f, 0, -0.7f));
            var agent = humanRoot.gameObject.AddComponent<NavMeshAgent>();
            // Native agents may activate before script OnEnable when a scene loads. Enable after navigation registration instead.
            agent.enabled = false;
            agent.radius = 0.42f; agent.height = 4.1f; agent.speed = settings.patrolSpeed;
            agent.angularSpeed = 240; agent.acceleration = 7; agent.stoppingDistance = 0.22f;
            agent.baseOffset = 0; agent.autoBraking = true;
            var capsule = humanRoot.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 2.05f, 0); capsule.height = 4.1f; capsule.radius = 0.42f;
            P.Sphere("Torso", humanRoot, new Vector3(0, 2.6f, 0), new Vector3(1.15f, 1.6f, 0.70f), teal);
            P.Sphere("Head", humanRoot, new Vector3(0, 3.75f, 0), new Vector3(0.70f, 0.8f, 0.68f), skin);
            P.Sphere("Hair", humanRoot, new Vector3(0, 4.0f, -0.045f), new Vector3(0.73f, 0.38f, 0.7f), dark);
            var left = Leg(humanRoot, -0.30f, dark, teal);
            var right = Leg(humanRoot, 0.30f, dark, teal);
            P.Rod("LeftArm", humanRoot, new Vector3(-0.6f, 3.1f, 0), new Vector3(-0.7f, 2.0f, 0.2f), 0.23f, skin);
            P.Rod("RightArm", humanRoot, new Vector3(0.6f, 3.1f, 0), new Vector3(0.75f, 2.1f, 0.35f), 0.23f, skin);
            var slipper = GeneratedAssets.Empty("SlipperWeapon", humanRoot, new Vector3(0.78f, 2.0f, 0.40f));
            P.Sphere("Sole", slipper, Vector3.zero, new Vector3(0.52f, 0.12f, 1.1f), dark);
            P.Sphere("Upper", slipper, new Vector3(0, 0.13f, 0.2f), new Vector3(0.51f, 0.25f, 0.6f), orange);
            var warning = P.Part("SlipperAttackWarning", root, RingMesh(), orange, Vector3.zero, Vector3.one);
            warning.gameObject.SetActive(false);
            Vector3[] route = { new Vector3(3.6f, 0, -3.8f), new Vector3(4.1f, 0, 3.5f), new Vector3(0.5f, 0, 2.6f), new Vector3(0.4f, 0, -3.8f) };
            var points = new Transform[route.Length];
            for (int i = 0; i < route.Length; i++) points[i] = GeneratedAssets.Empty("PatrolPoint_" + i, root, route[i]);
            var human = humanRoot.gameObject.AddComponent<HumanAIController>();
            human.Configure(session, points, warning, slipper, left, right);
            session.Configure(settings, motor, interactor, follow, human, nest, traps);
            root.gameObject.AddComponent<PhaseTwoHud>().Configure(session);
            EditorSceneManager.SaveScene(scene, ScenePath);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            AssetDatabase.SaveAssets();
            Debug.Log("[GOKI PHASE2] シーン生成完了: " + ScenePath);
        }
        private static Transform Leg(Transform parent, float x, Material dark, Material cloth)
        {
            var pivot = GeneratedAssets.Empty(x < 0 ? "LeftLeg" : "RightLeg", parent, new Vector3(x, 1.8f, 0));
            P.Box("TrouserLeg", pivot, new Vector3(0, -0.8f, 0), new Vector3(0.36f, 1.7f, 0.40f), cloth, false);
            P.Sphere("FootSlipper", pivot, new Vector3(0, -1.67f, 0.17f), new Vector3(0.48f, 0.25f, 0.88f), dark);
            return pivot;
        }
        private static Material Material(string name, Color color)
        {
            var result = GeneratedAssets.Material("Phase2_" + name, color);
            if (name == "Lime" || name == "Warning") { result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor", color * 0.55f); }
            return result;
        }
        private static Mesh RingMesh()
        {
            string path = GeneratedAssets.Root + "/Art/Meshes/Phase2_Ring.asset";
            GeneratedAssets.RequireOwned(path);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) return mesh;
            var vertices = new Vector3[96]; var triangles = new int[288];
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Vector3 direction = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                vertices[i * 2] = direction * 0.5f; vertices[i * 2 + 1] = direction * 0.43f;
                int k = i * 6, n = (i * 2 + 2) % 96;
                triangles[k] = i * 2; triangles[k + 1] = n; triangles[k + 2] = i * 2 + 1;
                triangles[k + 3] = n; triangles[k + 4] = n + 1; triangles[k + 5] = i * 2 + 1;
            }
            mesh = new Mesh { name = "Phase2_Ring", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); GeneratedAssets.Mark(mesh); return mesh;
        }
    }
}
