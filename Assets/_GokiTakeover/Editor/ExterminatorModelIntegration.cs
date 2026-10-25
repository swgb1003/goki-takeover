using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Imports the exterminator (Tools/Blender/build_exterminator_model.py -> Source/Exterminator.json) as three
    /// skinned Mesh LODs on the resident's skeleton: the bone list must match the resident's, and the bind poses
    /// are copied from the resident's meshes so the same SkinnedMeshRenderers can show either one.
    /// </summary>
    public static class ExterminatorModelIntegration
    {
        public const string SourcePath = BlenderModelIntegration.Root + "/Source/Exterminator.json";

        [Serializable] private class Extras { public Vector3 nozzleTip; }
        /// <summary>The spray lance's tip in model space (the skin root's space), at rest.</summary>
        public static Vector3 NozzleTip()
        {
            var tip = JsonUtility.FromJson<Extras>(File.ReadAllText(SourcePath)).nozzleTip;
            if (tip.sqrMagnitude < 1e-6f) throw new InvalidDataException("Exterminator.json has no nozzleTip");
            return tip;
        }

        public static Mesh[] ResidentMeshes() =>
            Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>(BlenderModelIntegration.Root + "/Meshes/Resident_Detail" + i + ".asset")).ToArray();

        public static Mesh[] Import()
        {
            var data = JsonUtility.FromJson<BlenderModelIntegration.Model>(File.ReadAllText(SourcePath));
            var resident = JsonUtility.FromJson<BlenderModelIntegration.Model>(File.ReadAllText(BlenderModelIntegration.Root + "/Source/Resident.json"));
            if (data.version != 1 || data.levels.Length != 3) throw new InvalidDataException("Unknown exterminator schema: " + SourcePath);
            if (!data.bones.Select(b => b.name + "/" + b.parent).SequenceEqual(resident.bones.Select(b => b.name + "/" + b.parent)))
                throw new InvalidDataException("The exterminator must use the resident's skeleton.");
            var residentMeshes = ResidentMeshes();
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
