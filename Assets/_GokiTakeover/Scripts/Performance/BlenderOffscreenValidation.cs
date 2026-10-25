#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace GokiTakeover
{
    // Explicit 720p D3D render target for unattended validation. Not in release builds.
    [DefaultExecutionOrder(300)]
    public sealed class BlenderOffscreenValidation : MonoBehaviour
    {
        Camera view;RenderTexture target;string output;int frames;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var args=Environment.GetCommandLineArgs();if(Application.isEditor||Array.IndexOf(args,"-goki-render-offscreen")<0)return;
            int i=Array.IndexOf(args,"-goki-output");var helper=new GameObject("Offscreen_Validation_Only").AddComponent<BlenderOffscreenValidation>();
            helper.output=i>=0&&i+1<args.Length?args[i+1]:Application.persistentDataPath;
        }
        void Start()
        {
            view=Camera.main;target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();
            view.targetTexture=target;view.enabled=false;
            Debug.Log("[MODEL VALIDATION] 1280x720 explicit offscreen D3D rendering, one Camera.Render per frame. Window presentation/IMGUI not profiled.");
        }
        void LateUpdate()
        {
            if(!view)return;view.Render();
            if(++frames==5)Capture("offscreen-arrival.png");
        }
        void Capture(string name)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;Destroy(pixels);}
        }
        void OnApplicationQuit(){if(target&&target.IsCreated())Capture("offscreen-gameplay.png");}
        void OnDestroy(){if(view)view.targetTexture=null;if(target){target.Release();Destroy(target);}}
    }
}
#endif
