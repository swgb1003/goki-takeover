using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GokiTakeover.Editor
{
    public static class CockroachGeneratorEditor
    {
        public const string PrefabPath = GeneratedAssets.Root + "/Prefabs/Cockroach_Player.prefab";
        public const string AppearancePath = GeneratedAssets.Root + "/Data/CockroachAppearance.asset";

        [MenuItem("Tools/GOKI TAKEOVER/Generate Cockroach", true)]
        private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/GOKI TAKEOVER/Generate Cockroach", priority = 0)]
        public static void GenerateMenu()
        {
            try
            {
                var prefab = Generate();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("[GOKI] ゴキブリPrefabを生成しました。同じ生成アセットを更新し、シーンに重複配置しません: " + PrefabPath);
            }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }

        public static GameObject Generate()
        {
            GeneratedAssets.Folder(GeneratedAssets.Root + "/Data");
            GeneratedAssets.Folder(GeneratedAssets.Root + "/Prefabs");
            GeneratedAssets.RequireOwned(PrefabPath);
            var appearance = AssetDatabase.LoadAssetAtPath<CockroachAppearance>(AppearancePath);
            if (!appearance && AssetDatabase.LoadMainAssetAtPath(AppearancePath))
                throw new System.InvalidOperationException("[GOKI] 外観設定パスの既存アセットは型が異なります: " + AppearancePath);
            if (!appearance)
            {
                appearance = ScriptableObject.CreateInstance<CockroachAppearance>();
                AssetDatabase.CreateAsset(appearance, AppearancePath);
            }
            var bodyMat = GeneratedAssets.Material("RoachBody", appearance.BodyColor);
            var shellMat = GeneratedAssets.Material("RoachShell", appearance.ShellColor);
            var limbMat = GeneratedAssets.Material("RoachLimbs", appearance.LimbColor);
            var eyeMat = GeneratedAssets.Material("RoachEyes", appearance.EyeColor);
            Mesh sphere = GeneratedAssets.LowPolySphere();
            Mesh rod = GeneratedAssets.LowPolyRod();
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("CockroachRoot");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                root.layer = 2; // Built-in Ignore Raycast, only camera obstruction casts exclude it.
                Transform visualRoot = GeneratedAssets.Empty("Visual", root.transform, Vector3.zero);
                visualRoot.localScale = Vector3.one * appearance.ModelSize;
                Transform bodyPivot = GeneratedAssets.Empty("BodyPivot", visualRoot, Vector3.zero);
                GeneratedAssets.Part("Abdomen", bodyPivot, sphere, bodyMat, new Vector3(0, 0.30f, -0.15f), new Vector3(0.70f, 0.37f, 1.18f));
                GeneratedAssets.Part("Head", bodyPivot, sphere, bodyMat, new Vector3(0, 0.36f, 0.54f), new Vector3(0.45f, 0.32f, 0.40f));
                GeneratedAssets.Part("Thorax", bodyPivot, sphere, shellMat, new Vector3(0, 0.40f, 0.30f), new Vector3(0.65f, 0.28f, 0.43f));
                Transform shells = GeneratedAssets.Empty("Shells", bodyPivot, Vector3.zero);
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    Transform shell = GeneratedAssets.Part(i == 0 ? "Shell_L" : "Shell_R", shells, sphere, shellMat,
                        new Vector3(side * 0.17f, 0.42f, -0.16f), new Vector3(0.37f, 0.22f, 1.12f));
                    shell.localRotation = Quaternion.Euler(0, side * 3, side * -8);
                    GeneratedAssets.Part(i == 0 ? "Eye_L" : "Eye_R", bodyPivot, sphere, eyeMat,
                        new Vector3(side * 0.16f, 0.43f, 0.675f), Vector3.one * 0.115f);
                }
                var legData = new CockroachVisualController.Leg[6];
                Transform legs = GeneratedAssets.Empty("Legs", visualRoot, Vector3.zero);
                int index = 0;
                for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                {
                    Vector3 hipPosition = new Vector3(side * 0.25f, 0.28f, 0.32f - row * 0.37f);
                    Vector3 foot = new Vector3(side * (row == 1 ? 0.87f : 0.75f), 0.025f, 0.60f - row * 0.65f);
                    Vector3 kneePosition = Vector3.Lerp(hipPosition, foot, 0.5f) + new Vector3(side * 0.12f, 0.18f, 0);
                    Transform hip = GeneratedAssets.Empty((side < 0 ? "L_" : "R_") + (row + 1) + "_Hip", legs, hipPosition);
                    Transform knee = GeneratedAssets.Empty("Knee", hip, kneePosition - hipPosition);
                    Transform upper = GeneratedAssets.Rod("Upper", hip, rod, limbMat, Vector3.zero, kneePosition - hipPosition, 0.045f);
                    Transform lower = GeneratedAssets.Rod("Lower", knee, rod, limbMat, Vector3.zero, foot - kneePosition, 0.030f);
                    Transform toe = GeneratedAssets.Empty("Toe", knee, foot - kneePosition);
                    GeneratedAssets.Part("Tip", toe, sphere, eyeMat, Vector3.zero, new Vector3(0.065f, 0.035f, 0.10f));
                    legData[index++] = new CockroachVisualController.Leg
                    {
                        hip = hip, knee = knee, upper = upper, lower = lower, toe = toe,
                        restFoot = foot, side = side, tripod = (row + (side > 0 ? 1 : 0)) % 2
                    };
                }
                Transform antennaGroup = GeneratedAssets.Empty("Antennae", bodyPivot, Vector3.zero);
                var antennae = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    Transform pivot = GeneratedAssets.Empty(i == 0 ? "Antenna_L" : "Antenna_R", antennaGroup,
                        new Vector3(side * 0.13f, 0.48f, 0.66f));
                    antennae[i] = pivot;
                    Vector3 point = Vector3.zero;
                    // Four connected, tapered low-poly segments, extending beyond the head.
                    for (int segment = 0; segment < 4; segment++)
                    {
                        Vector3 next = point + new Vector3(side * (0.07f + segment * 0.015f), 0.15f - segment * 0.04f, 0.21f);
                        GeneratedAssets.Rod("Segment_" + (segment + 1), pivot, rod, limbMat, point, next, 0.025f - segment * 0.004f);
                        point = next;
                    }
                }
                var visual = visualRoot.gameObject.AddComponent<CockroachVisualController>();
                visual.Configure(bodyPivot, antennae, legData);
                foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
                var controller = root.AddComponent<CharacterController>();
                // Conservative capsule encloses the abdomen AND leg stride; antenna tips are decorative.
                float radius = appearance.ModelSize * 1.12f;
                controller.radius = radius;
                controller.height = radius * 2;
                controller.center = new Vector3(0, radius, 0);
                controller.skinWidth = 0.012f;
                controller.stepOffset = 0.035f;
                controller.slopeLimit = 45;
                controller.minMoveDistance = 0;
                root.AddComponent<RoachMotor>().Configure(null, visual);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new System.InvalidOperationException("[GOKI] Prefab保存に失敗しました: " + PrefabPath);
                GeneratedAssets.Mark(prefab);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
