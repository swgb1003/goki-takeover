using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    internal static class GeneratedAssets
    {
        internal const string Root = "Assets/_GokiTakeover";
        internal const string Label = "GokiTakeover.Generated";
        internal static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        internal static void RequireOwned(string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing && Array.IndexOf(AssetDatabase.GetLabels(existing), Label) < 0)
                throw new InvalidOperationException("[GOKI] 生成物以外の既存アセットを保護しました: " + path);
        }

        internal static void Mark(UnityEngine.Object asset)
        {
            var labels = new List<string>(AssetDatabase.GetLabels(asset));
            if (!labels.Contains(Label)) labels.Add(Label);
            AssetDatabase.SetLabels(asset, labels.ToArray());
        }

        internal static Material Material(string name, Color color)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            Folder(Root + "/Art/Materials");
            RequireOwned(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = GraphicsSettings.currentRenderPipeline ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("[GOKI] 対応するシェーダーがありません。Built-inまたはURPを確認してください。");
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
                Mark(material);
            }
            material.shader = shader;
            material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static Mesh LowPolySphere()
        {
            string path = Root + "/Art/Meshes/RoachSphere.asset";
            Folder(Root + "/Art/Meshes");
            RequireOwned(path);
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached) return cached;
            var vertices = new List<Vector3>();
            const int rings = 6, sides = 10;
            for (int ring = 0; ring < rings; ring++)
            for (int side = 0; side < sides; side++)
            {
                Vector3 a = SpherePoint(ring, side, rings, sides);
                Vector3 b = SpherePoint(ring, side + 1, rings, sides);
                Vector3 c = SpherePoint(ring + 1, side, rings, sides);
                Vector3 d = SpherePoint(ring + 1, side + 1, rings, sides);
                if (ring > 0) { vertices.Add(a); vertices.Add(b); vertices.Add(c); }
                if (ring < rings - 1) { vertices.Add(b); vertices.Add(d); vertices.Add(c); }
            }
            return SaveFlatMesh(vertices, "RoachSphere", path);
        }

        private static Vector3 SpherePoint(int ring, int side, int rings, int sides)
        {
            float latitude = Mathf.PI * ring / rings;
            float longitude = Mathf.PI * 2 * side / sides;
            return new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude),
                Mathf.Sin(latitude) * Mathf.Sin(longitude)) * 0.5f;
        }

        internal static Mesh LowPolyRod()
        {
            string path = Root + "/Art/Meshes/RoachRod.asset";
            Folder(Root + "/Art/Meshes");
            RequireOwned(path);
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached) return cached;
            var vertices = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3, b = (i + 1) * Mathf.PI / 3;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.5f, -1, Mathf.Sin(a) * 0.5f);
                Vector3 q = new Vector3(Mathf.Cos(b) * 0.5f, -1, Mathf.Sin(b) * 0.5f);
                Vector3 r = p + Vector3.up * 2, s = q + Vector3.up * 2;
                vertices.Add(p); vertices.Add(r); vertices.Add(q);
                vertices.Add(q); vertices.Add(r); vertices.Add(s);
                vertices.Add(Vector3.down); vertices.Add(p); vertices.Add(q);
                vertices.Add(Vector3.up); vertices.Add(s); vertices.Add(r);
            }
            return SaveFlatMesh(vertices, "RoachRod", path);
        }

        private static Mesh SaveFlatMesh(List<Vector3> vertices, string name, string path)
        {
            var mesh = new Mesh { name = name };
            int[] indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetVertices(vertices);
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            Mark(mesh);
            return mesh;
        }

        internal static Transform Empty(string name, Transform parent, Vector3 position)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.localPosition = position;
            return result;
        }

        internal static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            Transform part = Empty(name, parent, position);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            part.localScale = scale;
            return part;
        }

        internal static Transform Rod(string name, Transform parent, Mesh mesh, Material material, Vector3 start, Vector3 end, float width)
        {
            Transform part = Part(name, parent, mesh, material, (start + end) * 0.5f,
                new Vector3(width, Vector3.Distance(start, end) * 0.5f, width));
            part.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            return part;
        }
    }
}
