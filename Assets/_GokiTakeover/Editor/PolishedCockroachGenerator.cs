using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    public static class PolishedCockroachGenerator
    {
        public const string PrefabPath = GeneratedAssets.Root + "/Prefabs/Cockroach_Player_Polished.prefab";

        public static GameObject Generate()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(CockroachGeneratorEditor.PrefabPath))
                throw new System.InvalidOperationException("[GOKI] 先にGenerate Cockroachを実行してください。");
            GeneratedAssets.RequireOwned(PrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(CockroachGeneratorEditor.PrefabPath);
            try
            {
                var shell = PolishedAssets.Material("Roach_AmberShell", new Color(0.31f, 0.105f, 0.028f), 0.53f, 0.08f);
                var belly = PolishedAssets.Material("Roach_DarkAbdomen", new Color(0.12f, 0.047f, 0.022f), 0.24f);
                var limbs = PolishedAssets.Material("Roach_Legs", new Color(0.23f, 0.075f, 0.025f), 0.38f);
                var eyes = PolishedAssets.Material("Roach_ObsidianEyes", new Color(0.018f, 0.012f, 0.008f), 0.82f);
                var highlight = PolishedAssets.Material("Roach_EyeGlint", new Color(0.85f, 0.74f, 0.50f), 0.6f);
                var etching = PolishedAssets.Material("Roach_ShellRidges", new Color(0.36f, 0.14f, 0.038f), 0.43f);
                var strokes = new System.Collections.Generic.List<CombineInstance>();
                Transform visual = root.transform.Find("Visual");
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    bool rod = filter.sharedMesh.name == "RoachRod";
                    filter.sharedMesh = rod ? PolishedAssets.RodMesh : PolishedAssets.SphereMesh;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    string original = renderer.sharedMaterial.name;
                    renderer.sharedMaterial = original == "RoachShell" ? shell : original == "RoachBody" ? belly
                        : original == "RoachEyes" ? eyes : limbs;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                Transform body = visual.Find("BodyPivot");
                Shape(body.Find("Abdomen"), new Vector3(0, 0.27f, -0.16f), new Vector3(0.74f, 0.34f, 1.30f));
                Shape(body.Find("Thorax"), new Vector3(0, 0.40f, 0.35f), new Vector3(0.70f, 0.31f, 0.48f));
                Shape(body.Find("Head"), new Vector3(0, 0.35f, 0.63f), new Vector3(0.46f, 0.32f, 0.43f));
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    Transform cover = body.Find("Shells/" + (side < 0 ? "Shell_L" : "Shell_R"));
                    Shape(cover, Vector3.zero, Vector3.one);
                    cover.localRotation = Quaternion.identity;
                    cover.GetComponent<MeshFilter>().sharedMesh = PolishedAssets.Wing(side);
                    for (int ridge = -1; ridge <= 1; ridge++)
                    {
                        Vector3 previous = Vector3.zero;
                        for (int segment = 0; segment < 5; segment++)
                        {
                            Vector3 point = PolishedAssets.WingSurface(side, 0.18f+(ridge+1)*0.27f, 0.15f+segment*0.175f) + Vector3.up*0.012f;
                            if (segment > 0)
                                strokes.Add(new CombineInstance { mesh = PolishedAssets.RodMesh,
                                    transform = Matrix4x4.TRS((previous+point)*0.5f,Quaternion.FromToRotation(Vector3.up,point-previous),
                                        new Vector3(0.010f,(point-previous).magnitude*0.5f,0.010f)) });
                            previous = point;
                        }
                    }
                    Shape(body.Find(side < 0 ? "Eye_L" : "Eye_R"), new Vector3(side * 0.175f, 0.41f, 0.765f), Vector3.one * 0.105f);
                    PolishedAssets.Sphere("EyeGlint", body, new Vector3(side * 0.17f, 0.438f, 0.803f), Vector3.one * 0.026f, highlight);
                    PolishedAssets.Rod("Palp", body, new Vector3(side * 0.08f, 0.27f, 0.80f), new Vector3(side * 0.16f, 0.25f, 0.92f), 0.028f, limbs);
                }
                string ridgePath = PolishedAssets.Root + "/Meshes/ShellRidges.asset";
                GeneratedAssets.RequireOwned(ridgePath);
                var ridgeMesh = new Mesh { name = "ShellRidges" };
                ridgeMesh.CombineMeshes(strokes.ToArray(), true, true);
                Mesh savedRidges = AssetDatabase.LoadAssetAtPath<Mesh>(ridgePath);
                if (savedRidges)
                {
                    EditorUtility.CopySerialized(ridgeMesh, savedRidges);
                    Object.DestroyImmediate(ridgeMesh);
                    EditorUtility.SetDirty(savedRidges);
                }
                else
                {
                    savedRidges = ridgeMesh;
                    AssetDatabase.CreateAsset(savedRidges, ridgePath);
                    GeneratedAssets.Mark(savedRidges);
                }
                PolishedAssets.Part("WingRidges", body, savedRidges, etching, Vector3.zero, Vector3.one, false, false);
                var controller = visual.GetComponent<CockroachVisualController>();
                controller.ConfigureLegWidth(0.065f, 0.043f);
                foreach (var leg in controller.Legs)
                    PolishedAssets.Sphere("Joint", leg.knee, Vector3.zero, Vector3.one * 0.072f, limbs);
                foreach (var antenna in controller.Antennae)
                {
                    int side = antenna.name.EndsWith("L") ? -1 : 1;
                    antenna.localPosition = new Vector3(side * 0.13f, 0.46f, 0.77f);
                    foreach (Transform child in antenna.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                    Vector3 start = Vector3.zero;
                    for (int segment = 0; segment < 8; segment++)
                    {
                        Vector3 end = start + new Vector3(side * 0.045f, 0.09f - segment * 0.015f, 0.12f);
                        PolishedAssets.Rod("Segment_" + segment, antenna, start, end, 0.024f - segment * 0.002f, limbs);
                        start = end;
                    }
                }
                PolishedAssets.Shadow("ContactShadow", root.transform, new Vector3(0, 0.018f, -0.04f), new Vector2(0.85f, 0.98f),
                    PolishedAssets.ShadowMaterial("RoachContact", 0.58f));
                foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new System.InvalidOperationException("[GOKI] 改善版Prefabの保存に失敗しました。");
                GeneratedAssets.Mark(prefab);
                return prefab;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Shape(Transform target, Vector3 position, Vector3 scale)
        {
            if (!target) throw new System.InvalidOperationException("[GOKI] 元Prefabの部位が不足しています。");
            target.localPosition = position;
            target.localScale = scale;
        }
    }
}
