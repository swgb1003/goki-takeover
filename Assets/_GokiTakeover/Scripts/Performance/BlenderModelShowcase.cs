#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    // Opt-in screenshot mode; absent from the normal release build.
    public sealed class BlenderModelShowcase : MonoBehaviour
    {
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var args=Environment.GetCommandLineArgs();if(Application.isEditor||Array.IndexOf(args,"-goki-model-showcase")<0)return;
            int n=Array.IndexOf(args,"-goki-output");var capture=new GameObject("Model_Showcase_OptIn").AddComponent<BlenderModelShowcase>();
            capture.output=n>=0&&n+1<args.Length?args[n+1]:Application.persistentDataPath;Application.runInBackground=true;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1.5f);
            var session=FindFirstObjectByType<PhaseTwoSession>();session.Pause();
            var motor=FindFirstObjectByType<RoachMotor>();motor.enabled=false;motor.GetComponent<CharacterController>().enabled=false;
            var human=session.Human;human.enabled=false;human.GetComponent<NavMeshAgent>().enabled=false;
            var follow=FindFirstObjectByType<ThirdPersonRoachCamera>();follow.enabled=false;
            foreach(var animator in FindObjectsByType<BlenderRoachAnimator>(FindObjectsSortMode.None))animator.enabled=false;
            foreach(var animator in FindObjectsByType<CockroachVisualController>(FindObjectsSortMode.None))animator.enabled=false;
            foreach(var animator in FindObjectsByType<BlenderHumanAnimator>(FindObjectsSortMode.None))animator.enabled=false;
            FindFirstObjectByType<PhaseTwoHud>().enabled=false;
            var camera=Camera.main;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.085f,.092f);
            camera.fieldOfView=38;camera.farClipPlane=30;
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.22f,.24f,.26f);
            var light=new GameObject("PreviewKey").AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.None;light.intensity=1.0f;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(35,160,0);
            var fill=new GameObject("PreviewFill").AddComponent<Light>();fill.type=LightType.Directional;fill.shadows=LightShadows.None;fill.intensity=.45f;fill.cullingMask=1<<31;fill.transform.rotation=Quaternion.Euler(20,-70,0);
            foreach(var actor in new[]{motor.transform,human.transform})
            {
                actor.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var child in actor.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                bool roach=actor==motor.transform;
                camera.transform.position=roach?new Vector3(.95f,.74f,1.2f):new Vector3(3.2f,2.6f,6.4f);
                camera.transform.LookAt(roach?new Vector3(0,.15f,.10f):new Vector3(0,2.02f,.05f));
                var skinners=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach(var group in actor.GetComponentsInChildren<LODGroup>())group.ForceLOD(0);
                for(int before=1;before>=0;before--)
                {
                    foreach(var renderer in actor.GetComponentsInChildren<MeshRenderer>())renderer.enabled=before==1&&renderer.name!="ContactShadow";
                    foreach(var r in skinners)r.enabled=before==0&&r.name=="Detail_0";
                    yield return new WaitForEndOfFrame();
                    CaptureCamera(camera,Path.Combine(output,(roach?"roach":"human")+(before==1?"-before.png":"-after.png")));
                    yield return new WaitForSecondsRealtime(.2f);
                }
                foreach(var child in actor.GetComponentsInChildren<Transform>(true))child.gameObject.layer=roach?2:0;
            }
            Application.Quit(0);
        }
        public static void CaptureCamera(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);Destroy(pixels);}
        }
    }
}
#endif
