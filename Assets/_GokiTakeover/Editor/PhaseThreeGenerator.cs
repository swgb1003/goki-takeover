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
    /// Phase 3 scene: a copy of the Blender-model kitchen plus a ceiling, climbable surfaces, more food
    /// (one on the worktop, four hidden), a pool of nine allies with a baked floor grid, and the
    /// tactics view on the existing camera. The Phase 2 scenes and their assets are not modified.
    /// </summary>
    public static class PhaseThreeGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase3_Kitchen_Swarm.unity";
        public const string SettingsPath = GeneratedAssets.Root + "/Data/PhaseThreeSettings.asset";
        public const string GridPath = GeneratedAssets.Root + "/Data/PhaseThreeAllyGrid.asset";
        public const string NormalBuild = "Build/GokiTakeover_Phase3/GokiTakeover.exe";
        public const string ValidationBuild = "Build/GokiTakeover_Phase3_Validation/GokiTakeover.exe";
        public const int AllyPool = 9;
        public static readonly Vector3 WorktopFood = new Vector3(3.6f, 2.935f, 4.2f);
        public static readonly Vector3[] HiddenFoods =
            { new Vector3(6.2f, 0.07f, -5.2f), new Vector3(-6.2f, 0.07f, 1.6f), new Vector3(0.6f, 0.07f, -5.3f), new Vector3(-3.2f, 0.07f, 2.6f) };
        public static readonly Vector3[] ScoutPoints =
        {
            new Vector3(5.8f, 0, -4.8f), new Vector3(5.6f, 0, 0.3f), new Vector3(3.0f, 0, 2.8f), new Vector3(-0.6f, 0, 2.8f),
            new Vector3(-5.6f, 0, 2.2f), new Vector3(-5.4f, 0, -1.0f), new Vector3(-2.2f, 0, -4.9f), new Vector3(1.6f, 0, -4.6f)
        };
        private static readonly string[] Climbable = { "Wall_Back", "Wall_Front", "Wall_Left", "Wall_Right", "Ceiling", "CabinetBody", "StoneWorktop", "ToeKick" };

        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 3 Swarm", priority = 25)]
        public static void OpenMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Generate();
        }
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 3 Swarm", priority = 26)]
        public static void RebuildMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Generate(); }
        [MenuItem("Tools/GOKI TAKEOVER/Build Phase 3 Windows 720p", priority = 35)]
        public static void Build() => PhaseOneBuild.BuildScene(ScenePath, NormalBuild, BuildOptions.None);
        public static void BuildBoth() { PhaseOneBuild.BuildScene(ScenePath, ValidationBuild, BuildOptions.Development); Build(); }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Phase 3 Swarm", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Phase 3 Swarm", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Build Phase 3 Windows 720p", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate()
        {
            GeneratedAssets.RequireOwned(ScenePath);
            GeneratedAssets.RequireOwned(GridPath);
            if (!File.Exists(BlenderModelIntegration.ScenePath)) throw new InvalidOperationException("先にBlenderモデル版キッチンを生成してください。");
            var scene = EditorSceneManager.OpenScene(BlenderModelIntegration.ScenePath);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Phase 3シーンを保存できません。");
            P.BoxMesh = AssetDatabase.LoadAssetAtPath<Mesh>(P.Root + "/Meshes/BeveledBox.asset");
            var settings = AssetDatabase.LoadAssetAtPath<PhaseThreeSettings>(SettingsPath);
            if (!settings)
            {
                GeneratedAssets.RequireOwned(SettingsPath);
                settings = ScriptableObject.CreateInstance<PhaseThreeSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath); GeneratedAssets.Mark(settings);
            }
            var session = UnityEngine.Object.FindFirstObjectByType<PhaseTwoSession>();
            var motor = UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
            var follow = UnityEngine.Object.FindFirstObjectByType<ThirdPersonRoachCamera>();
            var interactor = motor.GetComponent<PlayerInteractor>();
            var room = GameObject.Find("Room").transform;
            UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindFirstObjectByType<PhaseTwoHud>());

            // Ceiling: closes the room for climbing. It never casts a shadow (the key light would black out
            // the kitchen) and the tactics view hides it.
            var plaster = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/MidnightPlaster.mat");
            var ceiling = P.Box("Ceiling", room, new Vector3(0, 5.5f, 0), new Vector3(14.4f, 0.2f, 12.48f), plaster);
            var ceilingRenderer = ceiling.GetComponent<MeshRenderer>();
            ceilingRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ceiling.gameObject.isStatic = true;
            foreach (var t in room.GetComponentsInChildren<Transform>())
                if (Array.IndexOf(Climbable, t.name) >= 0 && t.GetComponent<Collider>()) t.gameObject.AddComponent<ClimbableSurface>();

            // Climbing pivot: the controller stays upright (it is a sphere); only this pivot tilts the body.
            var controller = motor.GetComponent<CharacterController>();
            float half = controller.center.y;
            var align = new GameObject("SurfaceAlign").transform;
            align.SetParent(motor.transform, false);
            align.localPosition = new Vector3(0, half, 0);
            var children = new List<Transform>();
            foreach (Transform child in motor.transform) if (child != align) children.Add(child);
            foreach (var child in children) child.SetParent(align, true);
            align.gameObject.layer = 2;
            motor.ConfigureClimbing(align);
            EditorUtility.SetDirty(motor);

            // Food: the three crumbs, one on the worktop (known: a reason to climb) and four to scout.
            var template = GameObject.Find("FoodMarker_0");
            var foods = new List<FoodItem>();
            var known = new List<bool>();
            for (int i = 0; i < 3; i++) { foods.Add(GameObject.Find("FoodMarker_" + i).GetComponent<FoodItem>()); known.Add(true); }
            var extra = new List<Vector3> { WorktopFood }; extra.AddRange(HiddenFoods);
            for (int i = 0; i < extra.Count; i++)
            {
                var copy = UnityEngine.Object.Instantiate(template, template.transform.parent);
                copy.name = "FoodMarker_" + (3 + i);
                copy.transform.SetPositionAndRotation(extra[i], Quaternion.Euler(0, 40 + i * 67, 0));
                foods.Add(copy.GetComponent<FoodItem>());
                known.Add(i == 0);
            }
            interactor.Configure(session, foods.ToArray(), motor.transform.Find("SurfaceAlign/FoodCarryAnchor"));
            EditorUtility.SetDirty(interactor);

            var grid = BakeGrid(room, UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.InstanceID));
            var scouts = new Vector3[ScoutPoints.Length];
            for (int i = 0; i < scouts.Length; i++)
            {
                if (!grid.Walkable(ScoutPoints[i]) && !grid.NearestWalkable(ScoutPoints[i], 1.5f, out scouts[i]))
                    throw new InvalidOperationException("調査地点に到達できません: " + ScoutPoints[i]);
                if (grid.Walkable(ScoutPoints[i])) scouts[i] = ScoutPoints[i];
            }

            var root = new GameObject("Phase3_Swarm").transform;
            var allies = new AllyRoach[AllyPool];
            var nest = session.Nest.position;
            for (int i = 0; i < AllyPool; i++)
            {
                var ally = new GameObject("Ally_" + i).transform;
                ally.SetParent(root, false);
                ally.position = new Vector3(nest.x + Mathf.Sin(i) * 1.2f, 0, nest.z + Mathf.Cos(i) * 1.2f);
                var body = UnityEngine.Object.Instantiate(align.gameObject, ally).transform;
                body.name = "Body";
                float size = 0.8f + 0.15f * ((i * 7) % 9) / 8f;   // size variation; the shared material stays one draw setup
                body.localPosition = new Vector3(0, half * size, 0);
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one * size;
                var oldAnchor = body.Find("FoodCarryAnchor");
                if (oldAnchor) UnityEngine.Object.DestroyImmediate(oldAnchor.gameObject);
                foreach (var c in body.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                var anchor = GeneratedAssets.Empty("CarryAnchor", body, new Vector3(0, 0.27f - half, 0.48f));
                foreach (var t in ally.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
                allies[i] = ally.gameObject.AddComponent<AllyRoach>();
                allies[i].Configure(body.GetComponentInChildren<CockroachVisualController>(), anchor, body);
                ally.gameObject.SetActive(i < settings.allyCount);
            }

            var cameraObject = follow.gameObject;
            var overview = cameraObject.GetComponent<SwarmViewCamera>();
            if (!overview) overview = cameraObject.AddComponent<SwarmViewCamera>();
            overview.Configure(new Vector2(-6.4f, -5.4f), new Vector2(6.4f, 5.4f), settings.swarmDistance, settings.swarmPanSpeed);
            overview.enabled = false;
            var hud = root.gameObject.AddComponent<PhaseThreeHud>();
            var commander = root.gameObject.AddComponent<SwarmCommander>();
            var front = room.Find("Wall_Front").GetComponent<Renderer>();
            commander.Configure(session, settings, grid, allies, foods.ToArray(), known.ToArray(), scouts, overview,
                new[] { front }, new Renderer[] { ceilingRenderer }, hud);
            hud.Configure(session, commander, UnityEngine.Object.FindObjectsByType<StickyTrap>(FindObjectsSortMode.InstanceID));
            EditorSceneManager.SaveScene(scene, ScenePath);
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            AssetDatabase.SaveAssets();
            Debug.Log("[GOKI PHASE3] シーン生成完了: " + ScenePath);
        }

        /// <summary>Floor cells a roach-sized sphere fits in, tested against the room's own colliders.</summary>
        public static AllyNavGrid BakeGrid(Transform room, StickyTrap[] traps, string path = GridPath) => BakeGrid(room, traps, path, new Vector3(-7, 0, -6), 56, 48);
        /// <summary>The same over any floor plan (Stage 2: the kitchen and the living room): origin corner, cells across and deep.</summary>
        public static AllyNavGrid BakeGrid(Transform room, StickyTrap[] traps, string path, Vector3 origin, int width, int depth)
        {
            const float cell = 0.25f;
            var data = new byte[width * depth];
            Physics.SyncTransforms();
            var hits = new Collider[16];
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                var centre = origin + new Vector3((x + 0.5f) * cell, 0.5f, (z + 0.5f) * cell);
                int count = Physics.OverlapSphereNonAlloc(centre, 0.44f, hits, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < count && !blocked; i++) blocked = hits[i].transform.IsChildOf(room);
                data[z * width + x] = blocked ? AllyNavGrid.Blocked : AllyNavGrid.Open;
            }
            foreach (var trap in traps)
            {
                var area = trap.GetComponent<BoxCollider>().bounds;
                area.Expand(new Vector3(0.5f, 0, 0.5f));
                for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    var c = origin + new Vector3((x + 0.5f) * cell, area.center.y, (z + 0.5f) * cell);
                    if (area.Contains(c) && data[z * width + x] == AllyNavGrid.Open) data[z * width + x] = AllyNavGrid.Sticky;
                }
            }
            GeneratedAssets.RequireOwned(path);
            var grid = AssetDatabase.LoadAssetAtPath<AllyNavGrid>(path);
            if (!grid)
            {
                grid = ScriptableObject.CreateInstance<AllyNavGrid>();
                grid.Bake(origin, cell, width, depth, data);
                AssetDatabase.CreateAsset(grid, path); GeneratedAssets.Mark(grid);
            }
            else grid.Bake(origin, cell, width, depth, data);
            EditorUtility.SetDirty(grid);
            return grid;
        }
    }
}
