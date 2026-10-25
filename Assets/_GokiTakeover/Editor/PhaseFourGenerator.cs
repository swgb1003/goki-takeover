using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Phase 4 scene: a copy of the Phase 3 kitchen plus food kinds and values, the colony (stock, nest,
    /// breeding, upgrades, respawning food), the resident's reaction levels with pooled sticky sheets and
    /// poison bait, the insecticide spray, and the Phase 4 HUD. Earlier scenes and assets are not modified.
    /// </summary>
    public static class PhaseFourGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase4_Kitchen_Colony.unity";
        public const string SettingsPath = GeneratedAssets.Root + "/Data/ColonySettings.asset";
        public const string FanPath = GeneratedAssets.Root + "/Art/Meshes/Phase4_SprayFan.asset";
        public const string NormalBuild = "Build/GokiTakeover_Phase4/GokiTakeover.exe";
        public const string ValidationBuild = "Build/GokiTakeover_Phase4_Validation/GokiTakeover.exe";
        public const int TrapPool = 3, BaitPool = 3;
        // Kinds for FoodMarker_0..7 (three crumbs, worktop, four hidden).
        public static readonly FoodValue.Kind[] Kinds =
        {
            FoodValue.Kind.Breadcrumb, FoodValue.Kind.Breadcrumb, FoodValue.Kind.Breadcrumb, FoodValue.Kind.Sweets,
            FoodValue.Kind.Leftovers, FoodValue.Kind.Rice, FoodValue.Kind.Garbage, FoodValue.Kind.Sweets
        };
        public static readonly Vector3[] RespawnCandidates =
        {
            new Vector3(-3.4f, 0.07f, -5.0f), new Vector3(1.0f, 0.07f, -0.6f), new Vector3(4.8f, 0.07f, -2.6f), new Vector3(5.6f, 0.07f, -0.2f),
            new Vector3(2.6f, 0.07f, 3.0f), new Vector3(-1.8f, 0.07f, 3.0f), new Vector3(-4.4f, 0.07f, 1.2f), new Vector3(-0.9f, 0.07f, -0.4f),
            new Vector3(6.3f, 0.07f, 4.8f), new Vector3(3.2f, 0.07f, -5.2f), new Vector3(-6.2f, 0.07f, -0.6f),
            new Vector3(0.2f, 2.935f, 4.3f), new Vector3(3.9f, 2.935f, 5.2f)
        };

        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 4 Colony", priority = 27)]
        public static void OpenMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Generate();
        }
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 4 Colony", priority = 28)]
        public static void RebuildMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Generate(); }
        [MenuItem("Tools/GOKI TAKEOVER/Build Phase 4 Windows 720p", priority = 36)]
        public static void Build() => PhaseOneBuild.BuildScene(ScenePath, NormalBuild, BuildOptions.None);
        public static void BuildValidation() => PhaseOneBuild.BuildScene(ScenePath, ValidationBuild, BuildOptions.Development);
        public static void BuildBoth() { BuildValidation(); Build(); }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 4 Colony", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 4 Colony", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Build Phase 4 Windows 720p", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate()
        {
            GeneratedAssets.RequireOwned(ScenePath);
            if (!File.Exists(PhaseThreeGenerator.ScenePath)) throw new InvalidOperationException("先にPhase 3シーンを生成してください。");
            var scene = EditorSceneManager.OpenScene(PhaseThreeGenerator.ScenePath);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Phase 4シーンを保存できません。");
            P.BoxMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/BeveledBox.asset");
            P.SphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/SmoothLowPolySphere.asset");
            P.RodMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/EightSidedRod.asset");
            var settings = AssetDatabase.LoadAssetAtPath<ColonySettings>(SettingsPath);
            if (!settings)
            {
                GeneratedAssets.RequireOwned(SettingsPath);
                settings = ScriptableObject.CreateInstance<ColonySettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath); GeneratedAssets.Mark(settings);
            }
            var session = UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>();
            var commander = UnityEngine.Object.FindFirstObjectByType<SwarmCommander>();
            var human = session.Human;
            UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindFirstObjectByType<PhaseThreeHud>());

            // Food kinds: colour and size tell them apart; values follow spec 5.1.
            var tints = new Dictionary<FoodValue.Kind, Material>
            {
                { FoodValue.Kind.Rice, GeneratedAssets.Material("Phase4_Rice", new Color(0.86f, 0.84f, 0.74f)) },
                { FoodValue.Kind.Sweets, GeneratedAssets.Material("Phase4_Sweets", new Color(0.86f, 0.42f, 0.36f)) },
                { FoodValue.Kind.Leftovers, GeneratedAssets.Material("Phase4_Leftovers", new Color(0.55f, 0.27f, 0.10f)) },
                { FoodValue.Kind.Garbage, GeneratedAssets.Material("Phase4_Garbage", new Color(0.36f, 0.38f, 0.16f)) },
            };
            var scales = new Dictionary<FoodValue.Kind, float> { { FoodValue.Kind.Rice, 0.7f }, { FoodValue.Kind.Sweets, 1.05f }, { FoodValue.Kind.Leftovers, 1.35f }, { FoodValue.Kind.Garbage, 1.15f } };
            for (int i = 0; i < commander.Foods.Count; i++)
            {
                var food = commander.Foods[i];
                var kind = Kinds[Mathf.Min(i, Kinds.Length - 1)];
                food.gameObject.AddComponent<FoodValue>().Configure(kind);
                if (!tints.ContainsKey(kind)) continue;
                foreach (Transform part in food.transform)
                    if (part.name == "Crumb") { part.GetComponent<MeshRenderer>().sharedMaterial = tints[kind]; part.localScale *= scales[kind]; }
            }

            var root = new GameObject("Phase4_Colony").transform;
            var hazards = GeneratedAssets.Empty("Countermeasures", root, Vector3.zero);
            var sourceTrap = GameObject.Find("StickyTrap_0");
            var trapPool = new StickyTrap[TrapPool];
            for (int i = 0; i < TrapPool; i++)
            {
                var copy = UnityEngine.Object.Instantiate(sourceTrap, hazards);
                copy.name = "PlacedStickyTrap_" + i;
                copy.SetActive(false);
                trapPool[i] = copy.GetComponent<StickyTrap>();
            }

            // Poison bait: crumbs a shade greener, with teal pellets as the tell; an orange ring once identified.
            var baitMaterial = GeneratedAssets.Material("Phase4_Bait", new Color(0.55f, 0.40f, 0.17f));
            var pellet = GeneratedAssets.Material("Phase4_BaitPellet", new Color(0.12f, 0.62f, 0.58f));
            var warning = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Phase2_Warning.mat");
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + "/Art/Meshes/Phase2_Ring.asset");
            var template = GameObject.Find("FoodMarker_0");
            var baitPool = new PoisonBait[BaitPool];
            for (int i = 0; i < BaitPool; i++)
            {
                var copy = UnityEngine.Object.Instantiate(template, hazards);
                copy.name = "PoisonBait_" + i;
                UnityEngine.Object.DestroyImmediate(copy.GetComponent<FoodValue>());
                UnityEngine.Object.DestroyImmediate(copy.GetComponent<FoodItem>());
                var beacon = copy.transform.Find("FoodBeacon");
                if (beacon) UnityEngine.Object.DestroyImmediate(beacon.gameObject);
                foreach (Transform part in copy.transform) if (part.name == "Crumb") part.GetComponent<MeshRenderer>().sharedMaterial = baitMaterial;
                for (int k = 0; k < 4; k++)
                    P.Sphere("Pellet", copy.transform, new Vector3(Mathf.Sin(k * 1.7f) * 0.2f, 0.05f, Mathf.Cos(k * 1.7f) * 0.17f), Vector3.one * 0.07f, pellet);
                var marker = P.Part("IdentifiedRing", copy.transform, ring, warning, new Vector3(0, -0.05f, 0), new Vector3(1.5f, 1, 1.5f));
                marker.gameObject.SetActive(false);
                var bait = copy.AddComponent<PoisonBait>();
                bait.Configure(marker.gameObject);
                copy.SetActive(false);
                baitPool[i] = bait;
            }

            // Spray: a floor fan for the warning and the mist, and a can held at chest height.
            var mist = GeneratedAssets.Material("Phase4_Mist", new Color(0.78f, 0.90f, 0.82f));
            mist.EnableKeyword("_EMISSION"); mist.SetColor("_EmissionColor", new Color(0.35f, 0.45f, 0.4f));
            var sprayRoot = GeneratedAssets.Empty("InsecticideSpray", hazards, Vector3.zero);
            var fan = P.Part("SprayFan", sprayRoot, FanMesh(settings.sprayHalfAngle), warning, Vector3.zero, Vector3.one, false, false);
            fan.gameObject.SetActive(false);
            var canMaterial = GeneratedAssets.Material("Phase4_SprayCan", new Color(0.78f, 0.12f, 0.10f));
            var can = GeneratedAssets.Empty("SprayCan", human.transform, new Vector3(0.38f, 2.35f, 0.5f));
            P.Rod("Can", can, Vector3.zero, new Vector3(0, 0.42f, 0), 0.16f, canMaterial);
            P.Rod("Nozzle", can, new Vector3(0, 0.42f, 0), new Vector3(0, 0.5f, 0.05f), 0.06f, mist);
            can.gameObject.SetActive(false);
            var spray = sprayRoot.gameObject.AddComponent<InsecticideSpray>();
            spray.Configure(fan.GetComponent<MeshRenderer>(), warning, mist, can.gameObject);

            var reaction = root.gameObject.AddComponent<HumanReaction>();
            reaction.Configure(session, commander, settings, spray, trapPool, baitPool);
            var grid = commander.Grid;
            var spots = new List<Vector3>();
            var route = new List<Vector3>();
            // Floor spots must be free, away from the nest and reachable by allies (no closed-off pockets).
            foreach (var c in RespawnCandidates)
                if (c.y > 0.5f || (grid.Walkable(c) && !grid.IsSticky(c) && Vector3.Distance(new Vector3(c.x, 0, c.z), commander.NestGround) > 2.6f
                    && grid.FindPath(commander.NestGround, c, route))) spots.Add(c);
            var colony = root.gameObject.AddComponent<ColonyManager>();
            colony.Configure(session, commander, reaction, settings, spots.ToArray(), UnityEngine.Object.FindFirstObjectByType<PolishedPresentation>());

            var allTraps = new List<StickyTrap>(UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID));
            allTraps.AddRange(trapPool);
            session.Configure(session.Settings, session.Motor, session.Interactor, session.FollowCamera, human, session.Nest, allTraps.ToArray());
            EditorUtility.SetDirty(session);
            var hud = root.gameObject.AddComponent<PhaseFourHud>();
            hud.Configure(session, commander, colony, allTraps.ToArray());
            var link = new SerializedObject(commander);
            link.FindProperty("hud").objectReferenceValue = hud;
            link.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            AssetDatabase.SaveAssets();
            Debug.Log("[GOKI PHASE4] シーン生成完了: " + ScenePath + " / 補充地点 " + spots.Count);
        }

        private static Mesh FanMesh(float halfAngle)
        {
            GeneratedAssets.RequireOwned(FanPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FanPath);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = "Phase4_SprayFan" }; else mesh.Clear();
            const int segments = 16;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            mesh.vertices = vertices; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (created) { AssetDatabase.CreateAsset(mesh, FanPath); GeneratedAssets.Mark(mesh); }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
