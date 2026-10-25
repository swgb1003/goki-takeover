using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    public static class BlenderModelIntegration
    {
        public const string Root=GeneratedAssets.Root+"/Art/BlenderModels";
        public const string ScenePath=GeneratedAssets.Root+"/Scenes/Phase2_Kitchen_BlenderModels.unity";
        public const string NormalBuild="Build/GokiTakeover_BlenderModels/GokiTakeover.exe";
        public const string ValidationBuild="Build/GokiTakeover_BlenderModels_Validation/GokiTakeover.exe";
        [Serializable] public class BoneData {public string name,parent;public Vector3 position;}
        [Serializable] public class WeightData {public int bone0,bone1;public float weight0,weight1;}
        [Serializable] public class Level {public string name;public Vector3[] positions,normals;public Color[] colors;public Vector2[] uv;public int[] triangles;public WeightData[] weights;}
        [Serializable] public class Model {public int version;public string actor;public BoneData[] bones;public Level[] levels;}
        public sealed class Skin {public GameObject root;public Transform[] bones;public SkinnedMeshRenderer[] renderers;public LODGroup lod;}

        [MenuItem("Tools/GOKI TAKEOVER/Open Blender Model Kitchen",priority=23)]
        public static void Open() { if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else Generate(); }
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Blender Model Kitchen",priority=24)]
        public static void Rebuild() {if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())Generate();}
        [MenuItem("Tools/GOKI TAKEOVER/Build Blender Models Windows 720p",priority=34)]
        public static void Build()=>PhaseOneBuild.BuildScene(ScenePath,NormalBuild,BuildOptions.None);
        public static void BuildBoth(){PhaseOneBuild.BuildScene(ScenePath,ValidationBuild,BuildOptions.Development);Build();}
        [MenuItem("Tools/GOKI TAKEOVER/Open Blender Model Kitchen",true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Blender Model Kitchen",true)]
        [MenuItem("Tools/GOKI TAKEOVER/Build Blender Models Windows 720p",true)]
        private static bool CanRun()=>!EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate()
        {
            GeneratedAssets.RequireOwned(ScenePath);
            var scene=EditorSceneManager.OpenScene(PhaseTwoGenerator.ScenePath);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException(ScenePath);
            var motor=UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
            if(PrefabUtility.IsPartOfPrefabInstance(motor.gameObject))PrefabUtility.UnpackPrefabInstance(motor.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var legacy=motor.GetComponentInChildren<CockroachVisualController>();
            // Keep the original motion joints and collider, hide only their old rendered geometry.
            foreach(var r in motor.GetComponentsInChildren<Renderer>())if(r.gameObject.name!="ContactShadow")r.enabled=false;
            var roach=CreateSkin("Cockroach",legacy.transform);
            var bindings=new BlenderRoachAnimator.LegBinding[6];
            for(int i=0;i<6;i++)
            {
                var l=legacy.Legs[i];string tag=(l.side<0?"L":"R")+(i%3);
                var upper=BlenderHumanAnimator.Find(roach.bones,"Upper_"+tag);var lower=BlenderHumanAnimator.Find(roach.bones,"Lower_"+tag);
                bindings[i]=new BlenderRoachAnimator.LegBinding {upper=upper,lower=lower,hipSource=l.hip,kneeSource=l.knee,toeSource=l.toe,
                    upperRestDirection=roach.root.transform.InverseTransformVector(lower.position-upper.position),
                    lowerRestDirection=roach.root.transform.InverseTransformVector(l.toe.position-l.knee.position)};
            }
            roach.root.AddComponent<BlenderRoachAnimator>().Configure(roach.root.transform,BlenderHumanAnimator.Find(roach.bones,"Body"),legacy.transform.Find("BodyPivot"),
                new[]{BlenderHumanAnimator.Find(roach.bones,"Antenna_L"),BlenderHumanAnimator.Find(roach.bones,"Antenna_R")},legacy.Antennae,bindings);
            foreach(Transform t in roach.root.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
            var human=UnityEngine.Object.FindFirstObjectByType<HumanAIController>();
            foreach(var r in human.GetComponentsInChildren<Renderer>())r.enabled=false;
            var resident=CreateSkin("Resident",human.transform);
            resident.root.AddComponent<BlenderHumanAnimator>().Configure(human,resident.bones,human.transform.Find("SlipperWeapon"));
            EditorSceneManager.SaveScene(scene,ScenePath);GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));AssetDatabase.SaveAssets();
            Debug.Log("[BLENDER MODELS] Imported authored meshes, 3 LODs, one skin/material per actor. Scene: "+ScenePath);
        }

        public static Skin CreateSkin(string actor,Transform parent)
        {
            string path=Root+"/Source/"+actor+".json";
            var data=JsonUtility.FromJson<Model>(File.ReadAllText(path));
            if(data.version!=1||data.levels.Length!=3)throw new InvalidDataException("Unknown Blender mesh schema: "+path);
            GeneratedAssets.Folder(Root+"/Meshes");GeneratedAssets.Folder(Root+"/Materials");GeneratedAssets.Folder(GeneratedAssets.Root+"/Prefabs");
            var root=new GameObject(actor+"_BlenderSkin");root.transform.SetParent(parent,false);
            var bones=new Transform[data.bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                var b=data.bones[i];var bone=new GameObject(b.name).transform;
                int parentIndex=Array.FindIndex(data.bones,x=>x.name==b.parent);
                bone.SetParent(parentIndex<0?root.transform:bones[parentIndex],false);
                bone.localPosition=b.position-(parentIndex<0?Vector3.zero:data.bones[parentIndex].position);bones[i]=bone;
            }
            var material=SurfaceMaterial();var renderers=new SkinnedMeshRenderer[3];
            var bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
            for(int i=0;i<3;i++)
            {
                var d=data.levels[i];string meshPath=Root+"/Meshes/"+d.name+".asset";GeneratedAssets.RequireOwned(meshPath);
                // Refill the native Mesh directly: CopySerialized can retain stale native
                // vertex/index buffers when the regenerated topology changes size.
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                bool created=!mesh;
                if(created)mesh=new Mesh();else mesh.Clear(false);
                mesh.name=d.name;mesh.indexFormat=d.positions.Length<65536?IndexFormat.UInt16:IndexFormat.UInt32;
                mesh.vertices=d.positions;mesh.normals=d.normals;mesh.colors=d.colors;mesh.uv=d.uv;mesh.triangles=d.triangles;mesh.bindposes=bindposes;
                mesh.boneWeights=d.weights.Select(w=>new BoneWeight {boneIndex0=w.bone0,boneIndex1=w.bone1,weight0=w.weight0,weight1=w.weight1}).ToArray();mesh.RecalculateBounds();
                if(created){AssetDatabase.CreateAsset(mesh,meshPath);GeneratedAssets.Mark(mesh);}
                else EditorUtility.SetDirty(mesh);
                var node=new GameObject("Detail_"+i);node.transform.SetParent(root.transform,false);var r=node.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh=mesh;r.bones=bones;r.rootBone=bones[0];r.sharedMaterial=material;r.quality=i<2?SkinQuality.Bone2:SkinQuality.Bone1;
                r.updateWhenOffscreen=false;r.skinnedMotionVectors=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                r.shadowCastingMode=i<2?ShadowCastingMode.On:ShadowCastingMode.Off;
                var bounds=mesh.bounds;bounds.Expand(actor=="Resident"?new Vector3(1.3f,1.4f,1.7f):new Vector3(.65f,.5f,.65f));r.localBounds=bounds;renderers[i]=r;
            }
            var lod=root.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;lod.animateCrossFading=false;
            lod.SetLODs(new[]{new LOD(actor=="Resident"?.25f:.17f,new Renderer[]{renderers[0]}),new LOD(.07f,new Renderer[]{renderers[1]}),new LOD(.012f,new Renderer[]{renderers[2]})});lod.RecalculateBounds();
            string prefabPath=GeneratedAssets.Root+"/Prefabs/"+actor+"_BlenderArt.prefab";GeneratedAssets.RequireOwned(prefabPath);
            GeneratedAssets.Mark(PrefabUtility.SaveAsPrefabAsset(root,prefabPath));
            return new Skin{root=root,bones=bones,renderers=renderers,lod=lod};
        }
        private static Material SurfaceMaterial()
        {
            string path=Root+"/Materials/AnatomySurface.mat";GeneratedAssets.RequireOwned(path);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find("GokiTakeover/Blender Vertex Surface");if(!shader)throw new InvalidOperationException("Vertex surface shader missing");
            if(!m){m=new Material(shader){name="AnatomySurface"};AssetDatabase.CreateAsset(m,path);GeneratedAssets.Mark(m);}
            m.shader=shader;m.color=Color.white;EditorUtility.SetDirty(m);return m;
        }
    }
}
