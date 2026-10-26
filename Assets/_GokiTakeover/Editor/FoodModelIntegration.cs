using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Imports the Blender-authored foods (Tools/Blender/build_food_models.py -> Source/Food.json) as native Mesh
    /// assets, one per FoodValue.Kind plus the poison bait's look-alike, drawn with the shared vertex-colour
    /// surface material (rgb = albedo, a = smoothness). Regeneration refills the same assets, keeping their GUIDs.
    /// </summary>
    public static class FoodModelIntegration
    {
        public const string SourcePath = BlenderModelIntegration.Root + "/Source/Food.json";
        public const string MaterialPath = BlenderModelIntegration.Root + "/Materials/FoodSurface.mat";
        public static readonly string[] Names = { "Food_Breadcrumb", "Food_Rice", "Food_Sweets", "Food_Leftovers", "Food_Garbage" };   // by FoodValue.Kind
        public const string BaitName = "Food_Bait";
        [Serializable] private class Model { public string name; public Vector3[] positions, normals; public Color[] colors; public Vector2[] uv; public int[] triangles; }
        [Serializable] private class Library { public int version; public Model[] models; }

        public sealed class Set { public Mesh[] foods; public Mesh bait; public Material material; }

        // Stage 3 (町中華): ten more foods from Tools/Blender/build_diner_food_models.py -> Source/DinerFood.json,
        // by FoodValue.Kind from Tenkasu on. Same material.
        public const string DinerSourcePath = BlenderModelIntegration.Root + "/Source/DinerFood.json";
        public static readonly string[] DinerNames = { "Food_Tenkasu", "Food_Noodles", "Food_FriedRice", "Food_Gyoza", "Food_Karaage",
            "Food_SesameBall", "Food_DinerLeftovers", "Food_Flour", "Food_Chashu", "Food_Shumai" };
        /// <summary>A mesh for every FoodValue.Kind: the first five from Food.json, the diner ten from DinerFood.json.</summary>
        public static Mesh[] ImportAllKinds(Set first)
        {
            var data = JsonUtility.FromJson<Library>(File.ReadAllText(DinerSourcePath));
            if (data.version != 1 || data.models == null) throw new InvalidDataException("Unknown food mesh schema: " + DinerSourcePath);
            return first.foods.Concat(DinerNames.Select(n => MeshFor(data, n))).ToArray();
        }

        public static Set Import()
        {
            var data = JsonUtility.FromJson<Library>(File.ReadAllText(SourcePath));
            if (data.version != 1 || data.models == null) throw new InvalidDataException("Unknown food mesh schema: " + SourcePath);
            GeneratedAssets.Folder(BlenderModelIntegration.Root + "/Meshes"); GeneratedAssets.Folder(BlenderModelIntegration.Root + "/Materials");
            var set = new Set { foods = new Mesh[Names.Length] };
            for (int i = 0; i < Names.Length; i++) set.foods[i] = MeshFor(data, Names[i]);
            set.bait = MeshFor(data, BaitName);
            set.material = SurfaceMaterial();
            return set;
        }

        private static Mesh MeshFor(Library data, string name)
        {
            var d = Array.Find(data.models, m => m.name == name);
            if (d == null) throw new InvalidDataException("Food model missing: " + name);
            string path = BlenderModelIntegration.Root + "/Meshes/" + name + ".asset";
            GeneratedAssets.RequireOwned(path);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = !mesh;
            if (created) mesh = new Mesh(); else mesh.Clear(false);
            mesh.name = name;
            mesh.indexFormat = d.positions.Length < 65536 ? IndexFormat.UInt16 : IndexFormat.UInt32;
            mesh.vertices = d.positions; mesh.normals = d.normals; mesh.colors = d.colors; mesh.uv = d.uv; mesh.triangles = d.triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            if (created) { AssetDatabase.CreateAsset(mesh, path); GeneratedAssets.Mark(mesh); }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Its own asset (same shader as the characters) so the character material stays untouched.
        private static Material SurfaceMaterial()
        {
            GeneratedAssets.RequireOwned(MaterialPath);
            var shader = Shader.Find("GokiTakeover/Blender Vertex Surface");
            if (!shader) throw new InvalidOperationException("Vertex surface shader missing");
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!m) { m = new Material(shader) { name = "FoodSurface" }; AssetDatabase.CreateAsset(m, MaterialPath); GeneratedAssets.Mark(m); }
            m.shader = shader; m.color = Color.white; m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Swaps a food pile's placeholder crumbs for the authored model (floor contact at the pile's base).</summary>
        public static Transform Dress(Transform pile, Mesh mesh, Material material, float yaw)
        {
            for (int i = pile.childCount - 1; i >= 0; i--)
                if (pile.GetChild(i).name == "Crumb") UnityEngine.Object.DestroyImmediate(pile.GetChild(i).gameObject);
            var old = pile.Find("FoodModel");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var model = new GameObject("FoodModel").transform;
            model.SetParent(pile, false);
            model.localPosition = new Vector3(0, -0.068f, 0);
            model.localRotation = Quaternion.Euler(0, yaw, 0);
            model.gameObject.layer = pile.gameObject.layer;
            model.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = model.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // The soft contact shadow under the pile follows the model's footprint.
            var contact = pile.Find("CrumbAO");
            if (contact)
            {
                var size = mesh.bounds.size;
                contact.localScale = new Vector3(size.x * 1.15f, contact.localScale.y, size.z * 1.15f);
                contact.localRotation = model.localRotation;
            }
            return model;
        }
    }
}
