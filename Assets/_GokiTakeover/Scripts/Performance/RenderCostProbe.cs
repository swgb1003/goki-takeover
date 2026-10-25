#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace GokiTakeover
{
    // Explicit opt-in only ("-goki-render-probe" on a Development build). Why does the frame rate drop when the
    // view changes? Enters stage 1 with eight allies, holds the game still, and for each rendering setup times
    // the same fixed views (third person facing four ways, the tactics view) with the frame cap off, so the
    // numbers show the real cost of each view. Writes render-probe.txt (and .json) to -goki-output.
    public sealed class RenderCostProbe : MonoBehaviour
    {
        private string output;
        private PhaseTwoSession session;
        private SwarmCommander commander;
        private ColonyManager colony;
        private StageProgress progress;
        private ThirdPersonRoachCamera cameraRig;
        private Light warm;
        private readonly StringBuilder table = new StringBuilder();
        private readonly List<string> rows = new List<string>();
        private Dictionary<Renderer, bool> shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-render-probe") < 0) return;
            var probe = new GameObject("RenderCostProbe").AddComponent<RenderCostProbe>();
            int index = Array.IndexOf(args, "-goki-output");
            probe.output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(output);
            yield return Wait(() => FindFirstObjectByType<TitleMenu>(), 15);
            var menu = FindFirstObjectByType<TitleMenu>();
            menu.Select(0);
            menu.Sortie(false);
            yield return Wait(() => { var p = FindFirstObjectByType<StageProgress>(); return p && p.Current == StageProgress.Phase.Briefing; }, 20);
            yield return null;
            progress = FindFirstObjectByType<StageProgress>();
            session = FindFirstObjectByType<PhaseTwoSession>();
            commander = FindFirstObjectByType<SwarmCommander>();
            colony = FindFirstObjectByType<ColonyManager>();
            cameraRig = session.FollowCamera;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.name == "WarmPool") warm = l;
            // "-goki-warmup": prepare every shader variant up front, to compare against preparing them on first sight.
            string warmup = "なし";
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-goki-warmup") >= 0)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Shader.WarmupAllShaders();
                warmup = "あり（" + watch.ElapsedMilliseconds + "ms）";
            }
            progress.Begin();
            yield return null;
            var tutorial = FindFirstObjectByType<StageTutorial>();
            if (tutorial) tutorial.Finish();
            colony.OpenNestMenu();
            colony.DebugGrant(60);
            for (int i = 0; i < 6; i++) colony.TryBreed();
            colony.TryExpand(); colony.TryBreed(); colony.TryBreed();
            colony.CloseNestMenu();
            // Hold the resident away and still; the allies gather around the player in the middle of the room.
            var human = session.Human;
            human.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(5.5f, 0, -4.5f));
            human.enabled = false;
            colony.Reaction.enabled = false;
            session.Motor.enabled = false;
            Place(new Vector3(0.6f, 0.02f, -1.4f));
            yield return new WaitForSecondsRealtime(3);   // allies walk into formation
            commander.enabled = false;

            table.AppendLine("GOKI TAKEOVER 描画コスト計測  " + DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            table.AppendLine(SystemInfo.processorType + " / " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType + " / " + Screen.width + "x" + Screen.height);
            table.AppendLine("フレーム上限なし（vSync・60fps上限を外して計測）。各2秒の平均。GPU時間は対応環境のみ。仲間 " + colony.Allies + "匹。シェーダーの事前準備: " + warmup);
            table.AppendLine();
            table.AppendLine("設定 | 視点 | FPS | フレームms | CPUメインms | GPUms | 描画コール | バッチ | SetPass | 影の描画物 | 三角形");
            string[] setups = { "中（現状）", "低（現状）", "中・点光源を頂点ライトに", "中・影なし", "中・MSAAなし", "中・仲間を非表示", "低・点光源を頂点ライトに" };
            for (int s = 0; s < setups.Length; s++)
            {
                Apply(s);
                yield return Views(setups[s], s == 0);
            }
            // Like real play (60 fps cap): repeated Tab switches and a continuous mouse turn, on 中 and 低.
            table.AppendLine();
            table.AppendLine("視点を変える操作（実プレイと同じ60fps上限）。HUDのFPS表示と同じく0.5秒ごとの平均FPSの最小値と、最も遅い1フレーム");
            table.AppendLine("設定 | 操作 | 0.5秒平均FPSの最小 | 全体の平均FPS | 50ms超のフレーム数 | 最遅フレームms | 最遅フレームの内訳");
            for (int q = 0; q < 2; q++)
            {
                Apply(q);
                Application.targetFrameRate = 60;
                string label = q == 0 ? "中（現状）" : "低（現状）";
                yield return Operation(label, "止まって見ているだけ（4秒）", 4f, null);
                yield return Operation(label, "Tabで視点切り替え（1.5秒ごと×6回）", 9f, t => { if (Mathf.Repeat(t, 1.5f) < Time.unscaledDeltaTime) commander.SetMode(commander.Mode == SwarmCommander.ViewMode.Swarm ? SwarmCommander.ViewMode.ThirdPerson : SwarmCommander.ViewMode.Swarm); });
                if (commander.Mode != SwarmCommander.ViewMode.ThirdPerson) commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
                cameraRig.enabled = false;
                yield return Operation(label, "マウスで回し続ける（毎秒150度・6秒）", 6f, t => { cameraRig.RotateView(new Vector2(150f * Time.unscaledDeltaTime / cameraRig.Sensitivity, 0)); cameraRig.Follow(Time.unscaledDeltaTime); });
            }
            Apply(0);
            File.WriteAllText(Path.Combine(output, "render-probe.txt"), table.ToString());
            File.WriteAllText(Path.Combine(output, "render-probe.json"), "[\n" + string.Join(",\n", rows) + "\n]\n");
            Application.Quit(0);
        }

        private void Apply(int setup)
        {
            colony.Options.quality = setup == 1 || setup == 6 ? 0 : 1;
            colony.ApplyOptions();
            if (warm) warm.renderMode = setup == 2 || setup == 6 ? LightRenderMode.ForceVertex : LightRenderMode.ForcePixel;
            if (setup == 3) QualitySettings.shadows = ShadowQuality.Disable;
            if (setup == 4) QualitySettings.antiAliasing = 0;
            if (shown == null)
            {
                shown = new Dictionary<Renderer, bool>();
                foreach (var ally in commander.Allies) foreach (var r in ally.GetComponentsInChildren<Renderer>(true)) shown[r] = r.enabled;
            }
            foreach (var pair in shown) pair.Key.enabled = setup != 5 && pair.Value;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }

        private IEnumerator Views(string setup, bool shots)
        {
            float[] yaws = { 0, 90, 180, 270 };
            string[] names = { "三人称・奥（流し台）向き", "三人称・右（勝手口）向き", "三人称・手前向き", "三人称・左（冷蔵庫）向き" };
            if (commander.Mode != SwarmCommander.ViewMode.ThirdPerson) commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
            cameraRig.enabled = false;
            for (int v = 0; v < yaws.Length; v++)
            {
                cameraRig.ResetView();
                cameraRig.RotateView(new Vector2((yaws[v] - session.Player.eulerAngles.y) / cameraRig.Sensitivity, 0));
                cameraRig.Follow(0);
                yield return Sample(setup, names[v]);
                if (shots) yield return Shot("probe-view" + v + ".png");
            }
            commander.SetMode(SwarmCommander.ViewMode.Swarm);
            yield return Sample(setup, "軍団モード（俯瞰）");
            if (shots) yield return Shot("probe-swarm.png");
            commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
            cameraRig.enabled = false;
        }

        private IEnumerator Sample(string setup, string view)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var passes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var timings = new FrameTiming[1];
            double frame = 0, cpu = 0, gpu = 0; long d = 0, b = 0, p = 0, c = 0, t = 0; int n = 0, gpuN = 0;
            float clock = 0;
            yield return null;
            while (clock < 2f)
            {
                float dt = Time.unscaledDeltaTime;
                clock += dt;
                frame += dt * 1000; n++;
                if (main.Valid) cpu += main.LastValue / 1e6;
                d += draws.LastValue; b += batches.LastValue; p += passes.LastValue; c += casters.LastValue; t += tris.LastValue;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) { gpu += timings[0].gpuFrameTime; gpuN++; }
                yield return null;
            }
            main.Dispose(); draws.Dispose(); batches.Dispose(); passes.Dispose(); casters.Dispose(); tris.Dispose();
            float ms = (float)(frame / n);
            string gpuText = gpuN > 0 ? (gpu / gpuN).ToString("F1") : "―";
            table.AppendLine(setup + " | " + view + " | " + (1000 / ms).ToString("F0") + " | " + ms.ToString("F1") + " | " + (cpu / n).ToString("F1") + " | " + gpuText
                + " | " + d / n + " | " + b / n + " | " + p / n + " | " + c / n + " | " + t / n);
            rows.Add("  {\"setup\":\"" + setup + "\",\"view\":\"" + view + "\",\"fps\":" + (1000 / ms).ToString("F1") + ",\"frameMs\":" + ms.ToString("F2") + ",\"cpuMainMs\":" + (cpu / n).ToString("F2")
                + ",\"gpuMs\":" + (gpuN > 0 ? (gpu / gpuN).ToString("F2") : "null") + ",\"drawCalls\":" + d / n + ",\"batches\":" + b / n + ",\"setPass\":" + p / n + ",\"shadowCasters\":" + c / n + ",\"triangles\":" + t / n + "}");
        }

        private static readonly string[] Markers =
        {
            "PlayerLoop", "Camera.Render", "Shadows.RenderShadowmap", "Culling", "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForGfxCommandsFromMainThread",
            "GC.Collect", "GUI.Repaint", "GUIUtility.ProcessEvent", "BehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PostLateUpdate.UpdateAllSkinnedMeshes",
            "Physics.Processing", "Shader.CreateGPUProgram", "CreateGPUProgram", "Shader.Parse", "Loading.ReadObject", "Gfx.PresentFrame"
        };
        // Runs an operation every frame for some seconds; reports the worst 0.5 s window and the slowest frame with
        // the profiler markers that took the most time in it.
        private IEnumerator Operation(string setup, string name, float seconds, Action<float> step)
        {
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var recorders = new List<KeyValuePair<string, ProfilerRecorder>>();
            foreach (var h in available)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (Array.IndexOf(Markers, d.Name) >= 0) recorders.Add(new KeyValuePair<string, ProfilerRecorder>(d.Name, new ProfilerRecorder(h, 1)));
            }
            foreach (var r in recorders) r.Value.Start();
            yield return new WaitForSecondsRealtime(0.5f);
            float t = 0, window = 0, worst = 0, minFps = float.MaxValue; int frames = 0, windowFrames = 0, slow = 0;
            string detail = "";
            while (t < seconds)
            {
                step?.Invoke(t);
                yield return null;
                float dt = Time.unscaledDeltaTime;
                t += dt; window += dt; frames++; windowFrames++;
                if (dt > 0.05f) slow++;
                if (dt > worst)
                {
                    worst = dt;
                    var parts = new List<KeyValuePair<string, double>>();
                    foreach (var r in recorders) if (r.Value.Valid && r.Value.Count > 0) parts.Add(new KeyValuePair<string, double>(r.Key, r.Value.LastValue / 1e6));
                    parts.Sort((a, b) => b.Value.CompareTo(a.Value));
                    var sb = new StringBuilder();
                    for (int i = 0; i < parts.Count && i < 6; i++) if (parts[i].Value >= 0.5) sb.Append(parts[i].Key).Append(' ').Append(parts[i].Value.ToString("F1")).Append("ms  ");
                    detail = sb.ToString();
                }
                if (window >= 0.5f) { minFps = Mathf.Min(minFps, windowFrames / window); window = 0; windowFrames = 0; }
            }
            foreach (var r in recorders) r.Value.Dispose();
            table.AppendLine(setup + " | " + name + " | " + minFps.ToString("F0") + " | " + (frames / t).ToString("F0") + " | " + slow + " | " + (worst * 1000).ToString("F0") + " | " + detail);
            rows.Add("  {\"setup\":\"" + setup + "\",\"operation\":\"" + name + "\",\"minWindowFps\":" + minFps.ToString("F1") + ",\"averageFps\":" + (frames / t).ToString("F1") + ",\"worstMs\":" + (worst * 1000).ToString("F1") + ",\"worstFrame\":\"" + detail.Trim() + "\"}");
        }

        private void Place(Vector3 position)
        {
            var motor = session.Motor;
            motor.ResetSurface();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            motor.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name));
            yield return new WaitForSecondsRealtime(0.4f);
        }
        private static IEnumerator Wait(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
#endif
