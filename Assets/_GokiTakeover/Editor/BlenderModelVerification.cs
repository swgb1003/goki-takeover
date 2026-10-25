using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GokiTakeover.Editor
{
    [InitializeOnLoad]
    public static class BlenderModelVerification
    {
        const string Active="Goki.Blender.Verify";
        static int checks;
        static bool buildOnSuccess;
        static BlenderModelVerification(){EditorApplication.update+=Poll;}
        public static void GenerateVerifyAndBuild(){buildOnSuccess=true;GenerateAndVerify();}
        public static void GenerateAndVerify()
        {
            Directory.CreateDirectory("docs/Validation/BlenderModels");
            try
            {
                string oldScene=Hash(PhaseTwoGenerator.ScenePath),oldPrefab=Hash(PolishedCockroachGenerator.PrefabPath);
                BlenderModelIntegration.Generate();
                string guid=AssetDatabase.AssetPathToGUID(BlenderModelIntegration.ScenePath);
                int assets=AssetDatabase.FindAssets("",new[]{BlenderModelIntegration.Root}).Length;
                BlenderModelIntegration.Generate();
                Check(oldScene==Hash(PhaseTwoGenerator.ScenePath)&&oldPrefab==Hash(PolishedCockroachGenerator.PrefabPath),"既存Phase2シーン・旧Prefabを保全");
                Check(guid==AssetDatabase.AssetPathToGUID(BlenderModelIntegration.ScenePath),"再生成でもシーンGUID維持");
                Check(assets==AssetDatabase.FindAssets("",new[]{BlenderModelIntegration.Root}).Length,"再生成でアセット重複なし");
                var groups=UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
                Check(groups.Length==2,"両モデルに3段階LOD");
                foreach(var group in groups)
                {
                    var lods=group.GetLODs();Check(lods.Length==3&&group.fadeMode==LODFadeMode.None,group.name+" LOD交差描画をしない");
                    int prior=int.MaxValue;
                    foreach(var lod in lods)
                    {
                        Check(lod.renderers.Length==1,"LODあたり描画スキン1個");
                        var skin=(SkinnedMeshRenderer)lod.renderers[0];var mesh=skin.sharedMesh;
                        int tris=mesh.triangles.Length/3;Check(tris<12000&&tris<prior,skin.name+" 三角形予算・段階削減 "+tris);prior=tris;
                        Check(skin.sharedMaterials.Length==1&&skin.bones.Length<=24,"材質1・骨24本以内");
                        Check(!skin.updateWhenOffscreen&&!skin.skinnedMotionVectors,"画面外更新とモーションベクトルを無効化");
                        Check(mesh.vertexCount==mesh.boneWeights.Length&&mesh.colors.Length==mesh.vertexCount,"頂点・カラー・スキンウェイト整合");
                        Check(mesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1-1)<.001f),"全頂点のウェイト合計1");
                        Check(mesh.triangles.All(i=>i>=0&&i<mesh.vertexCount),"メッシュ頂点インデックスが有効");
                        var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;int front=0,back=0;
                        for(int t=0;t<indices.Length;t+=3){int a=indices[t],b=indices[t+1],c=indices[t+2];var cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);if(cross.sqrMagnitude<1e-12f)continue;if(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])>=0)front++;else back++;}
                        Check(front>back*20,"表面の頂点順と外向き法線が一致 "+front+"/"+(front+back));
                    }
                }
                var player=UnityEngine.Object.FindFirstObjectByType<RoachMotor>();
                Check(player.GetComponentsInChildren<Collider>().Length==1,"主人公の当たり判定は既存の1個だけ");
                Check(player.GetComponentInChildren<CockroachVisualController>().Legs.Length==6,"既存6脚IKを保持");
                Check(UnityEngine.Object.FindObjectsByType<HumanAIController>(FindObjectsSortMode.None).Length==1,"人間AI1体を維持");
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"ゲームカメラ1台を維持");
                Check(UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None).Length==3,"食料3個を維持");
                AssetDatabase.SaveAssets();
                File.WriteAllText("docs/Validation/BlenderModels/editmode.txt","PASS: "+checks+" checks\n");
                if(buildOnSuccess)BlenderModelIntegration.BuildBoth();
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void VerifyPlayMode()
        {
            EditorSceneManager.OpenScene(BlenderModelIntegration.ScenePath);
            SessionState.SetBool(Active,true);SessionState.SetString(Active+".Time",DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
        }
        static void Poll()
        {
            if(!SessionState.GetBool(Active,false))return;
            if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Active+".Time",""))).TotalSeconds>120){SessionState.SetBool(Active,false);EditorApplication.Exit(1);return;}
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.timeSinceLevelLoad<.5f)return;
            SessionState.SetBool(Active,false);
            try
            {
                var legacy=UnityEngine.Object.FindFirstObjectByType<CockroachVisualController>();
                legacy.SetMotion(1,true);legacy.Animate(.07f,1);
                var sync=legacy.GetComponentInChildren<BlenderRoachAnimator>();sync.SendMessage("LateUpdate");
                var skin=sync.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);
                Check(baked.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)),"実行時スキニングに非有限頂点なし");UnityEngine.Object.Destroy(baked);
                Check(Vector3.Distance(sync.transform.Find("Root/Upper_L0").position,legacy.Legs[0].hip.position)<.001f,"新しい脚が既存IKの股関節に追従");
                Check(Vector3.Distance(sync.transform.Find("Root/Upper_L0/Lower_L0").position,legacy.Legs[0].knee.position)<.001f,"新しい下腿が既存IKの膝に追従");
                File.WriteAllText("docs/Validation/BlenderModels/skin-playmode.txt","PASS: "+checks+" skin checks\n");
                var runner=new GameObject("Blender_Model_Regression").AddComponent<PhaseTwoScenarioTest>();runner.OutputFolder="docs/Validation/BlenderModels";
                runner.Completed+=pass=>{EditorApplication.isPlaying=false;EditorApplication.Exit(pass?0:1);};
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;Debug.Log("[BLENDER VERIFY] PASS: "+text);}
    }
}
