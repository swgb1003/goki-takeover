#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Opt-in development-player measurement; normal launch never creates this component.</summary>
    public sealed class PhaseOneSmokeTest : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string unity, cpu, gpu, operatingSystem;
            public int memoryMB, width, height, frames;
            public float measuredSeconds, averageFps, percentile95FrameMs, maximumFrameMs;
            public long maximumGcBytes, totalReservedMemoryBytes;
            public bool gcRecorderAvailable, memoryRecorderAvailable;
            public string route = "30秒の自動歩行・ダッシュ・カメラ旋回。起動直後3秒とPNG撮影フレームを除外。";
        }
        private readonly List<float> frameTimes = new List<float>(5000);
        private RoachMotor motor;
        private ThirdPersonRoachCamera followCamera;
        private float startTime, measuredSeconds;
        private bool capturing, captured, completed;
        private long maximumGc;
        private string outputFolder;
        private ProfilerRecorder gcRecorder, memoryRecorder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "-goki-smoke-test") < 0 || Application.isEditor) return;
            var runner = new GameObject("GOKI_OptIn_DevelopmentSmokeTest").AddComponent<PhaseOneSmokeTest>();
            int outputIndex = Array.IndexOf(arguments, "-goki-output");
            runner.outputFolder = outputIndex >= 0 && outputIndex + 1 < arguments.Length ? arguments[outputIndex + 1]
                : Path.Combine(Application.persistentDataPath, "Phase1SmokeTest");
            Directory.CreateDirectory(runner.outputFolder);
        }

        private IEnumerator Start()
        {
            motor = FindFirstObjectByType<RoachMotor>();
            followCamera = FindFirstObjectByType<ThirdPersonRoachCamera>();
            if (!motor || !followCamera)
            {
                Debug.LogError("[GOKI SMOKE] 試験シーンの参照不足です。");
                Application.Quit(2);
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            motor.enabled = false;
            followCamera.enabled = false;
            var hud = FindFirstObjectByType<PhaseOneHud>();
            if (hud) hud.ShowCursorPrompt = false;
            var polishedHud = FindFirstObjectByType<PolishedHud>();
            if (polishedHud) polishedHud.ShowCursorPrompt = false;
            Application.runInBackground = true;
            if (polishedHud)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(outputFolder, "phase1-arrival-720p.png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            startTime = Time.realtimeSinceStartup;
            while (!completed)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                float routeTime = elapsed % 10f;
                Vector2 input = routeTime < 3 ? Vector2.up : routeTime < 5 ? Vector2.right : routeTime < 8 ? Vector2.down : Vector2.left;
                motor.SimulateMove(input, routeTime >= 5, Time.unscaledDeltaTime);
                followCamera.RotateView(new Vector2(4f * Time.unscaledDeltaTime, 0));
                followCamera.Follow(Time.unscaledDeltaTime);
                if (elapsed > 3 && !capturing)
                {
                    float frame = Time.unscaledDeltaTime;
                    frameTimes.Add(frame);
                    measuredSeconds += frame;
                    if (gcRecorder.Valid) maximumGc = Math.Max(maximumGc, gcRecorder.LastValue);
                }
                if (measuredSeconds >= 30) completed = true;
                yield return null;
            }
            Result result = new Result
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                operatingSystem = SystemInfo.operatingSystem, memoryMB = SystemInfo.systemMemorySize,
                width = Screen.width, height = Screen.height, frames = frameTimes.Count,
                measuredSeconds = measuredSeconds, averageFps = frameTimes.Count / measuredSeconds,
                maximumGcBytes = maximumGc, gcRecorderAvailable = gcRecorder.Valid,
                memoryRecorderAvailable = memoryRecorder.Valid,
                totalReservedMemoryBytes = memoryRecorder.Valid ? memoryRecorder.LastValue : -1
            };
            frameTimes.Sort();
            result.percentile95FrameMs = frameTimes[Mathf.Clamp(Mathf.CeilToInt(frameTimes.Count * 0.95f) - 1, 0, frameTimes.Count - 1)] * 1000;
            result.maximumFrameMs = frameTimes[frameTimes.Count - 1] * 1000;
            File.WriteAllText(Path.Combine(outputFolder, "phase1-performance.json"), JsonUtility.ToJson(result, true));
            Debug.Log("[GOKI SMOKE] 結果: " + JsonUtility.ToJson(result));
            Application.Quit(0);
        }

        private void Update()
        {
            // Capture independently of the measurement coroutine, once after warmup.
            if (startTime > 0 && !captured && Time.realtimeSinceStartup - startTime > 3.2f)
                StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            capturing = true;
            captured = true;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outputFolder, "phase1-player-720p.png"));
            // Disk IO / readback is outside the timed sample and cannot inflate the reported spike.
            yield return new WaitForSecondsRealtime(0.5f);
            capturing = false;
        }

        private void OnDestroy()
        {
            gcRecorder.Dispose();
            memoryRecorder.Dispose();
        }
    }
}
#endif
