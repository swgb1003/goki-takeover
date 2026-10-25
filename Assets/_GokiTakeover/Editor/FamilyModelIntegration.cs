using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Imports the Stage 2 family (Tools/Blender/build_family_models.py -> Source/Mother.json, Source/Son.json) as three
    /// skinned Mesh LODs each on the resident's skeleton, like the exterminator: the bone list must match the resident's
    /// and the bind poses are the resident's, so a copy of the resident's skin can wear them. The held tool's tip
    /// (vacuum head, net) comes along as nozzleTip in model space.
    /// </summary>
    public static class FamilyModelIntegration
    {
        public static readonly string[] Actors = { "Mother", "Son" };
        public static string SourcePath(string actor) => BlenderModelIntegration.Root + "/Source/" + actor + ".json";

        [Serializable] private class Extras { public Vector3 nozzleTip; }
        public static Vector3 ToolTip(string actor)
        {
            var tip = JsonUtility.FromJson<Extras>(File.ReadAllText(SourcePath(actor))).nozzleTip;
            if (tip.sqrMagnitude < 1e-6f) throw new InvalidDataException(actor + ".json has no nozzleTip");
            return tip;
        }

        public static Mesh[] Import(string actor)
        {
            var data = JsonUtility.FromJson<BlenderModelIntegration.Model>(File.ReadAllText(SourcePath(actor)));
            var resident = JsonUtility.FromJson<BlenderModelIntegration.Model>(File.ReadAllText(BlenderModelIntegration.Root + "/Source/Resident.json"));
            if (data.version != 1 || data.levels.Length != 3) throw new InvalidDataException("Unknown family schema: " + SourcePath(actor));
            if (!data.bones.Select(b => b.name + "/" + b.parent).SequenceEqual(resident.bones.Select(b => b.name + "/" + b.parent)))
                throw new InvalidDataException(actor + " must use the resident's skeleton.");
            var residentMeshes = ExterminatorModelIntegration.ResidentMeshes();
            var result = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                var d = data.levels[i]; string path = BlenderModelIntegration.Root + "/Meshes/" + d.name + ".asset";
                GeneratedAssets.RequireOwned(path);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool created = !mesh;
                if (created) mesh = new Mesh(); else mesh.Clear(false);
                mesh.name = d.name; mesh.indexFormat = d.positions.Length < 65536 ? IndexFormat.UInt16 : IndexFormat.UInt32;
                mesh.vertices = d.positions; mesh.normals = d.normals; mesh.colors = d.colors; mesh.uv = d.uv; mesh.triangles = d.triangles;
                mesh.bindposes = residentMeshes[i].bindposes;
                mesh.boneWeights = d.weights.Select(w => new BoneWeight { boneIndex0 = w.bone0, boneIndex1 = w.bone1, weight0 = w.weight0, weight1 = w.weight1 }).ToArray();
                mesh.RecalculateBounds();
                if (created) { AssetDatabase.CreateAsset(mesh, path); GeneratedAssets.Mark(mesh); }
                else EditorUtility.SetDirty(mesh);
                result[i] = mesh;
            }
            return result;
        }
    }
}
