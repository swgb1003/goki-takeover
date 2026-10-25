using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GokiTakeover.Editor
{
    public static class KitchenStageGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase1_Kitchen_Test.unity";

        [MenuItem("Tools/GOKI TAKEOVER/Generate Kitchen Test Scene", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Generated Kitchen Scene", true)]
        private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/GOKI TAKEOVER/Generate Kitchen Test Scene", priority = 10)]
        public static void GenerateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
                else Generate();
                Debug.Log("[GOKI] Phase 1テストシーンを開きました。Playで開始します。既存シーンがあれば再利用します。");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Generated Kitchen Scene", priority = 11)]
        public static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try { Generate(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static void Generate()
        {
            GeneratedAssets.Folder(GeneratedAssets.Root + "/Scenes");
            GeneratedAssets.RequireOwned(ScenePath);
            GameObject prefab = CockroachGeneratorEditor.Generate();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var stage = new GameObject("GOKI_Phase1_Kitchen");
            Transform environment = GeneratedAssets.Empty("Environment", stage.transform, Vector3.zero);
            var wall = GeneratedAssets.Material("KitchenWall", new Color(0.34f, 0.39f, 0.39f));
            var wood = GeneratedAssets.Material("KitchenWood", new Color(0.37f, 0.18f, 0.075f));
            var oak = GeneratedAssets.Material("KitchenOak", new Color(0.59f, 0.34f, 0.14f));
            var cream = GeneratedAssets.Material("KitchenCream", new Color(0.77f, 0.75f, 0.61f));
            var dark = GeneratedAssets.Material("KitchenDark", new Color(0.10f, 0.12f, 0.12f));
            var red = GeneratedAssets.Material("KitchenRed", new Color(0.56f, 0.16f, 0.09f));
            var green = GeneratedAssets.Material("MarkerLime", new Color(0.72f, 0.85f, 0.12f));
            var bread = GeneratedAssets.Material("MarkerBread", new Color(0.98f, 0.66f, 0.25f));
            var blue = GeneratedAssets.Material("MarkerHuman", new Color(0.23f, 0.43f, 0.61f));
            var floorA = GeneratedAssets.Material("KitchenTileA", new Color(0.48f, 0.39f, 0.27f));
            var floorB = GeneratedAssets.Material("KitchenTileB", new Color(0.57f, 0.47f, 0.33f));

            // Single floor collider; visual tile cubes are static and have no individual physics.
            Cube("Floor", environment, new Vector3(0, -0.15f, 0), new Vector3(14, 0.3f, 12), floorA);
            Transform tiles = GeneratedAssets.Empty("FloorTiles", environment, Vector3.zero);
            for (int x = 0; x < 14; x++)
            for (int z = 0; z < 12; z++)
                Cube("Tile_" + x + "_" + z, tiles, new Vector3(x - 6.5f, -0.0125f, z - 5.5f),
                    new Vector3(0.975f, 0.035f, 0.975f), (x + z) % 2 == 0 ? floorA : floorB, false);
            Cube("Wall_Back", environment, new Vector3(0, 2.7f, 6.1f), new Vector3(14.4f, 5.4f, 0.2f), wall);
            Cube("Wall_Front", environment, new Vector3(0, 2.7f, -6.1f), new Vector3(14.4f, 5.4f, 0.2f), wall);
            Cube("Wall_Left", environment, new Vector3(-7.1f, 2.7f, 0), new Vector3(0.2f, 5.4f, 12), wall);
            Cube("Wall_Right", environment, new Vector3(7.1f, 2.7f, 0), new Vector3(0.2f, 5.4f, 12), wall);
            foreach (float z in new[] { -5.93f, 5.93f })
                Cube("Skirting", environment, new Vector3(0, 0.18f, z), new Vector3(14, 0.36f, 0.10f), wood);

            Transform fridge = GeneratedAssets.Empty("Refrigerator", environment, new Vector3(-4.8f, 0, 4.65f));
            Cube("Body", fridge, new Vector3(0, 2.3f, 0), new Vector3(2.3f, 4.3f, 2.0f), cream);
            Cube("DoorSeam", fridge, new Vector3(0, 3.0f, -1.015f), new Vector3(2.15f, 0.045f, 0.04f), dark, false);
            Cube("Handle", fridge, new Vector3(0.75f, 2.3f, -1.08f), new Vector3(0.08f, 0.65f, 0.09f), dark, false);
            Cube("Note", fridge, new Vector3(-0.4f, 3.6f, -1.03f), new Vector3(0.48f, 0.48f, 0.025f), bread, false);

            Transform counter = GeneratedAssets.Empty("Counter", environment, new Vector3(1.35f, 0, 4.8f));
            Cube("Cabinet", counter, new Vector3(0, 1.45f, 0), new Vector3(6.0f, 2.4f, 2.1f), wood);
            Cube("Worktop", counter, new Vector3(0, 2.75f, 0), new Vector3(6.2f, 0.22f, 2.3f), cream);
            for (int i = 0; i < 4; i++)
            {
                float x = -2.25f + i * 1.5f;
                Cube("Door", counter, new Vector3(x, 1.5f, -1.075f), new Vector3(1.42f, 2.1f, 0.05f), oak, false);
                Cube("Handle", counter, new Vector3(x + 0.45f, 2.0f, -1.12f), new Vector3(0.08f, 0.28f, 0.06f), dark, false);
            }
            Cube("Sink", counter, new Vector3(-1.2f, 2.88f, 0), new Vector3(1.6f, 0.04f, 1.35f), dark, false);
            Cube("Tap", counter, new Vector3(-1.2f, 3.15f, 0.65f), new Vector3(0.1f, 0.6f, 0.1f), cream, false);
            Cube("Cooktop", counter, new Vector3(1.65f, 2.89f, 0), new Vector3(1.8f, 0.05f, 1.5f), dark, false);
            for (int i = 0; i < 2; i++)
                Cube("Burner", counter, new Vector3(1.2f + i * 0.9f, 2.93f, 0), new Vector3(0.55f, 0.035f, 0.55f), red, false);
            Cube("UpperCabinet", counter, new Vector3(0, 4.5f, 0.5f), new Vector3(5.9f, 1.1f, 1.0f), oak);

            Transform table = GeneratedAssets.Empty("Table", environment, new Vector3(-2.25f, 0, -0.9f));
            Cube("Top", table, new Vector3(0, 2.7f, 0), new Vector3(3.8f, 0.22f, 2.7f), oak);
            FourLegs(table, 1.55f, 1.0f, 2.6f, 0.18f, wood);
            Cube("Runner", table, new Vector3(0, 2.825f, 0), new Vector3(1.2f, 0.025f, 2.68f), red, false);
            Cube("Plate", table, new Vector3(1.0f, 2.85f, 0), new Vector3(0.95f, 0.05f, 0.95f), cream, false);
            Chair(environment, new Vector3(-2.25f, 0, -3.2f), 0, wood, oak);
            Chair(environment, new Vector3(-4.85f, 0, -0.9f), 90, wood, oak);

            Transform shelf = GeneratedAssets.Empty("Shelf", environment, new Vector3(5.85f, 0, 2.2f));
            for (int i = 0; i < 4; i++)
                Cube("ShelfBoard_" + i, shelf, new Vector3(0, 0.45f + i * 1.15f, 0), new Vector3(1.5f, 0.12f, 3.2f), oak);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Cube("ShelfPost", shelf, new Vector3(x * 0.67f, 2, z * 1.45f), new Vector3(0.13f, 4, 0.13f), wood);
            for (int i = 0; i < 9; i++)
                Cube("Storage", shelf, new Vector3(0, 0.76f + (i / 3) * 1.15f, (i % 3 - 1) * 0.9f),
                    new Vector3(0.75f, 0.5f, 0.65f), i % 2 == 0 ? cream : red, false);

            Transform markers = GeneratedAssets.Empty("Markers_NoGameplay", stage.transform, Vector3.zero);
            Vector3[] foodPositions = { new Vector3(-0.5f, 0.12f, 1.5f), new Vector3(3.6f, 0.12f, 3.2f), new Vector3(-4.5f, 0.12f, -3.8f) };
            for (int i = 0; i < foodPositions.Length; i++)
            {
                Transform marker = GeneratedAssets.Empty("FoodMarker_" + (i + 1), markers, foodPositions[i]);
                for (int j = 0; j < 3; j++)
                    Cube("Crumb", marker, new Vector3(j * 0.13f - 0.13f, j * 0.018f, j % 2 * 0.1f), Vector3.one * (0.17f - j * 0.025f), bread, false);
                Cube("MarkerBase", marker, new Vector3(0, -0.10f, 0), new Vector3(0.6f, 0.025f, 0.6f), green, false);
            }
            Transform human = GeneratedAssets.Empty("HumanMarker_Static", markers, new Vector3(3.6f, 0, -1.5f));
            Cube("Foot_L", human, new Vector3(-0.28f, 0.12f, 0.10f), new Vector3(0.45f, 0.24f, 0.9f), dark);
            Cube("Foot_R", human, new Vector3(0.28f, 0.12f, 0.10f), new Vector3(0.45f, 0.24f, 0.9f), dark);
            Cube("Leg_L", human, new Vector3(-0.28f, 1.3f, -0.05f), new Vector3(0.36f, 2.4f, 0.4f), blue);
            Cube("Leg_R", human, new Vector3(0.28f, 1.3f, -0.05f), new Vector3(0.36f, 2.4f, 0.4f), blue);
            Cube("Torso", human, new Vector3(0, 3.0f, -0.05f), new Vector3(1.0f, 1.25f, 0.6f), blue);
            Cube("HeadMarker", human, new Vector3(0, 4.0f, -0.05f), new Vector3(0.6f, 0.7f, 0.6f), cream);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.52f, 0.43f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            var light = new GameObject("KitchenSun").AddComponent<Light>();
            light.transform.SetParent(stage.transform);
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.83f, 0.60f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = new Vector3(-1.0f, 0.04f, -4.3f);
            var camera = new GameObject("GOKI_ThirdPersonCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 62;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 40;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<ThirdPersonRoachCamera>().Configure(player.transform);
            var motor = player.GetComponent<RoachMotor>();
            motor.Configure(camera.transform, player.GetComponentInChildren<CockroachVisualController>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            stage.AddComponent<PhaseOneHud>().Configure(motor);
            stage.AddComponent<PhaseOneSession>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("[GOKI] テストシーンを保存できません。");
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            AssetDatabase.SaveAssets();
        }

        private static void Chair(Transform parent, Vector3 position, float rotation, Material wood, Material oak)
        {
            Transform chair = GeneratedAssets.Empty("Chair", parent, position);
            chair.localRotation = Quaternion.Euler(0, rotation, 0);
            Cube("Seat", chair, new Vector3(0, 1.55f, 0), new Vector3(1.3f, 0.16f, 1.3f), oak);
            FourLegs(chair, 0.5f, 0.5f, 1.5f, 0.13f, wood);
            Cube("Back", chair, new Vector3(0, 2.25f, -0.55f), new Vector3(1.3f, 1.3f, 0.13f), wood);
        }
        private static void FourLegs(Transform parent, float x, float z, float height, float thickness, Material material)
        {
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
                Cube("Leg", parent, new Vector3(a * x, height * 0.5f, b * z), new Vector3(thickness, height, thickness), material);
        }
        private static Transform Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (!collision) UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.isStatic = true;
            return cube.transform;
        }
    }
}
