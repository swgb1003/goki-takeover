#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    // Explicit opt-in only. Exercises the same interaction, damage, sensor and attack code as gameplay.
    public sealed class PhaseTwoScenarioTest : MonoBehaviour
    {
        public string OutputFolder { get; set; }
        public event Action<bool> Completed;
        private readonly List<string> checks = new List<string>();
        private PhaseTwoSession session;
        private RoachMotor motor;
        private ThirdPersonRoachCamera cameraRig;
        private HumanAIController human;
        private NavMeshAgent agent;
        private bool performanceRun;
        [Serializable] private sealed class Performance
        {
            public string unity, cpu, gpu;
            public int width, height, frames;
            public float seconds, averageFps, percentile95Ms, maximumMs;
            public long maximumGcBytes, reservedMemoryBytes;
            public bool gcRecorderAvailable, memoryRecorderAvailable;
            public string scenario = "人間1体が巡回・感知するキッチンで30秒。主人公は巣付近を往復、視点旋回。全回収経路の性能ではない。";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-phase2-test") < 0) return;
            var runner = new GameObject("Phase2_OptIn_Test").AddComponent<PhaseTwoScenarioTest>();
            int index = Array.IndexOf(args, "-goki-output");
            runner.OutputFolder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            runner.performanceRun = true;
            Application.runInBackground = true;
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(OutputFolder);
            yield return null;
            session = FindFirstObjectByType<PhaseTwoSession>();
            motor = FindFirstObjectByType<RoachMotor>();
            human = session.Human;
            agent = human.GetComponent<NavMeshAgent>();
            cameraRig = FindFirstObjectByType<ThirdPersonRoachCamera>();
            motor.enabled = false; human.enabled = false; cameraRig.enabled = false;
            session.ResetRun();
            var suite = Checks();
            while (true)
            {
                object next;
                try { if (!suite.MoveNext()) break; next = suite.Current; }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(OutputFolder, "phase2-failure.txt"), e.ToString());
                    Debug.LogException(e); Finish(false); yield break;
                }
                yield return next;
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "phase2-gameplay.txt"), checks);
            if (performanceRun) yield return Measure();
            Finish(true);
        }
        private IEnumerator Checks()
        {
            Check(session.Hp == 3 && session.Delivered == 0 && session.IsRunning, "開始時HPと納品数");
            Check(agent.isOnNavMesh, "実行時NavMesh登録と人間の接地");
            agent.isStopped = true;
            Check(!session.Interactor.TryInteract(), "範囲外から回収できない");
            var foods = FindObjectsByType<FoodItem>(FindObjectsSortMode.None);
            var first = foods[0]; var second = foods[1];
            Teleport(first.transform.position + Vector3.right * 0.35f);
            Check(session.Interactor.TryInteract() && session.Interactor.Carried == first, "E相当でパンくずを回収");
            Check(!first.TryCarry(motor.transform), "同じ食料を二重回収しない");
            Teleport(second.transform.position + Vector3.right * 0.35f);
            Check(!session.Interactor.TryInteract() && second.State == FoodItem.FoodState.Available, "運搬上限1個");
            Teleport(session.Nest.position + Vector3.right * 0.3f);
            Check(session.Interactor.TryInteract() && session.Delivered == 1 && !session.Interactor.Carried, "巣で納品し所持を解除");
            Check(!session.Interactor.TryInteract() && session.Delivered == 1, "納品を二重加算しない");
            foreach (var food in foods)
            {
                if (food.State != FoodItem.FoodState.Available) continue;
                Teleport(food.transform.position + Vector3.right * 0.35f);
                Check(session.Interactor.TryInteract(), "残りの食料を回収");
                Teleport(session.Nest.position + Vector3.right * 0.3f);
                Check(session.Interactor.TryInteract(), "残りの食料を納品");
            }
            Check(session.State == PhaseTwoSession.RunState.Cleared && Time.timeScale == 0, "3個納品で練習クリア・時間停止");
            if (performanceRun) { yield return Capture("clear-720p.png"); }
            session.ResetRun();
            Check(session.Delivered == 0 && foods[0].State == FoodItem.FoodState.Available && foods[0].gameObject.activeSelf, "クリア後リトライで食料復元");
            Teleport(new Vector3(-0.2f, 0.03f, -2.8f));
            yield return null;
            Check(session.IsSticky && motor.SpeedMultiplier < 0.4f, "粘着シート侵入で減速");
            Teleport(new Vector3(0, 0.03f, -4.8f));
            yield return null;
            Check(!session.IsSticky && Mathf.Abs(motor.SpeedMultiplier - 1) < 0.01f, "粘着シートを出ると速度復元");
            agent.Warp(new Vector3(3.6f, 0, -0.7f));
            human.transform.rotation = Quaternion.identity;
            Teleport(new Vector3(3.6f, 0.03f, 2.3f));
            Check(human.SensePlayer(), "視野内・距離内で視認");
            Teleport(new Vector3(3.6f, 0.03f, -3.7f));
            Check(!human.SensePlayer(), "人間の背後は視野外");
            Teleport(new Vector3(-2.25f, 0.03f, -0.9f));
            Check(!human.HasLineOfSight(), "机の下は天板が視線を遮る");
            Teleport(new Vector3(3.6f, 0.03f, 2.3f));
            for (int i = 0; i < 10; i++) human.Tick(0.1f);
            Check(human.State == HumanAIController.HumanState.Chase && session.Discoveries == 1, "発見ゲージが満ちると追跡");
            Teleport(new Vector3(-2.25f, 0.03f, -0.9f));
            for (int i = 0; i < 8; i++) human.Tick(0.1f);
            Check(human.State == HumanAIController.HumanState.Search, "遮蔽で見失い捜索へ");
            for (int i = 0; i < 55; i++) human.Tick(0.1f);
            Check(human.State == HumanAIController.HumanState.Routine, "捜索時間切れで巡回へ");
            session.ResetRun();
            agent.isStopped = true;
            Teleport(new Vector3(3.6f, 0.03f, 2.3f));
            agent.Warp(new Vector3(3.6f, 0, -0.7f));
            human.transform.rotation = Quaternion.identity;
            var patrolStart = human.transform.position;
            agent.isStopped = false;
            human.enabled = true;
            yield return new WaitForSeconds(1.3f);
            Check(Vector3.Distance(human.transform.position, patrolStart) > 0.2f, "NavMeshAgentが実時間で移動");
            human.enabled = false;
            // Reset into a deterministic attack position, wait out spawn protection and attack cooldown.
            session.ResetRun();
            agent.isStopped = true;
            agent.Warp(new Vector3(3.6f, 0, -0.7f));
            human.transform.rotation = Quaternion.identity;
            Teleport(new Vector3(3.6f, 0.03f, 0.7f));
            yield return new WaitForSeconds(1.7f);
            for (int i = 0; i < 10 && human.State != HumanAIController.HumanState.Attack; i++) human.Tick(0.1f);
            Check(human.State == HumanAIController.HumanState.Attack && session.Hp == 3, "スリッパは予兆中にダメージなし");
            if (performanceRun) { cameraRig.Follow(0); yield return Capture("attack-720p.png"); }
            Teleport(new Vector3(4.9f, 0.03f, 0.7f));
            for (int i = 0; i < 16; i++) human.Tick(0.1f);
            Check(session.Hp == 3 && human.State != HumanAIController.HumanState.Attack, "予兆の枠外へ退避すると回避");
            // Inside the old 0.85 radius but beside the sole: only a real crush hurts.
            yield return new WaitForSeconds(2);
            agent.Warp(new Vector3(3.6f, 0, -0.7f));
            human.transform.rotation = Quaternion.identity;
            Teleport(new Vector3(3.6f, 0.03f, 0.7f));
            for (int i = 0; i < 10 && human.State != HumanAIController.HumanState.Attack; i++) human.Tick(0.1f);
            Vector3 beside = human.AttackPoint + Vector3.Cross(Vector3.up, human.StrikeForward) * 0.6f;
            Teleport(beside);
            for (int i = 0; i < 16; i++) human.Tick(0.1f);
            Check(session.Hp == 3 && !human.IsUnderSlipper(beside), "スリッパの横0.6なら潰されずダメージなし");
            agent.isStopped = true;
            yield return new WaitForSeconds(2);
            agent.Warp(new Vector3(3.6f, 0, -0.7f));
            Teleport(new Vector3(3.6f, 0.03f, 0.7f));
            human.transform.rotation = Quaternion.identity;
            var slam = FindFirstObjectByType<BlenderHumanAnimator>();
            bool raised = false, landedFlat = slam == null, earlyDamage = false;
            int landings = 0, crushes = 0;
            System.Action<Vector3, bool> count = (point, crushed) => { landings++; if (crushed) crushes++; };
            human.SlipperLanding += count;
            for (int i = 0; i < 80 && session.Hp == 3; i++)
            {
                human.Tick(0.03f);
                earlyDamage |= session.Hp < 3 && !human.SlipperLanded;
                yield return null;
                if (!slam || human.State != HumanAIController.HumanState.Attack) continue;
                if (human.AttackTime > session.Settings.attackWindup * 0.5f && human.AttackTime < session.Settings.attackWindup * 0.62f)
                    raised |= slam.TreadCentreWorld.y > 2.2f;
                if (human.SlipperLanded && !landedFlat)
                {
                    Vector3 miss = Vector3.ProjectOnPlane(slam.TreadCentreWorld - human.AttackPoint, Vector3.up);
                    float rest = human.StrikeSurfaceHeight;
                    landedFlat = miss.magnitude < 0.12f && Mathf.Abs(slam.TreadCentreWorld.y - rest) < 0.06f && slam.TreadNormalWorld.y < -0.8f;
                    if (!landedFlat) Debug.LogWarning("[PHASE2 PLAY] sole " + slam.TreadCentreWorld + " target " + human.AttackPoint + " normal " + slam.TreadNormalWorld);
                }
            }
            human.SlipperLanding -= count;
            Check(session.Hp == 2 && landings == 1 && crushes == 1 && !earlyDamage, "スリッパの靴底が叩き潰した瞬間だけHPが1減る");
            if (slam) Check(raised && landedFlat, "スリッパを頭上へ振りかぶり靴底を標的へ平らに叩きつける");
            for (int i = 0; i < 25; i++) human.Tick(0.03f);
            Check(!session.TryDamage(1) && session.Hp == 2, "被弾直後の無敵時間で連続ヒットを抑止");
            session.Pause();
            Check(Time.timeScale == 0 && !motor.MovementEnabled, "ポーズで時間と操作を停止");
            session.Resume();
            Check(Time.timeScale == 1 && session.IsRunning, "ポーズ解除で復帰");
            for (int i = 0; i < 2; i++) { yield return new WaitForSeconds(1.7f); session.TryDamage(1); }
            Check(session.Hp == 0 && session.State == PhaseTwoSession.RunState.Dead && Time.timeScale == 0, "HP0で死亡画面・時間停止");
            Check(!session.TryDamage(1) && !session.Interactor.TryInteract(), "死亡後は被弾・回収を停止");
            if (performanceRun) yield return Capture("death-720p.png");
            session.ResetRun();
            Check(session.Hp == 3 && session.Delivered == 0 && !session.Interactor.Carried && Time.timeScale == 1 && human.State == HumanAIController.HumanState.Routine, "死亡後リトライでHP・所持・納品・人間を初期化");
            Check(Vector3.Distance(session.Player.position, session.Nest.position) < 1.2f && !session.TryDamage(1), "巣の安全地帯から再開");
            // The resident walks into a roach pinned against a wall: it must stay inside the room.
            agent.isStopped = true;
            Teleport(new Vector3(6.38f, 0.03f, -1.5f));
            for (int i = 0; i < 4; i++) { motor.SimulateMove(Vector2.zero, false, 1 / 60f); yield return null; }
            for (float x = 5.3f; x <= 6.7f; x += 0.05f)
            {
                agent.Warp(new Vector3(x, 0, -1.5f));
                Physics.SyncTransforms();
                for (int i = 0; i < 2; i++) motor.SimulateMove(Vector2.zero, false, 1 / 60f);
            }
            var pinned = motor.transform.position;
            Check(pinned.x < 6.6f && pinned.y > -0.2f && !motor.IsEmbedded, "壁際で人間に押し込まれても壁を抜けない");
            agent.Warp(new Vector3(4.2f, 0, -1.5f));
            for (int i = 0; i < 30; i++) { motor.MovementEnabled = true; motor.SimulateMove(InputToward(Vector3.left), true, 1 / 60f); yield return null; }
            Check(motor.transform.position.x < pinned.x - 0.3f, "押し込まれた後も自力で脱出できる");
            // Something still tunnels the roach into furniture: it returns to the last clear spot.
            Teleport(new Vector3(3.6f, 0.03f, 2.3f));
            for (int i = 0; i < 4; i++) { motor.SimulateMove(Vector2.zero, false, 1 / 60f); yield return null; }
            int recoveries = motor.Recoveries;
            Teleport(new Vector3(1.35f, 0.03f, 4.8f));
            motor.SimulateMove(Vector2.zero, false, 1 / 60f);
            Check(!motor.IsEmbedded && (motor.Recoveries > recoveries || motor.transform.position.z < 3.7f), "家具の内部に入り込んでも安全位置へ復帰");
            var body = motor.GetComponent<CharacterController>();
            body.enabled = false; motor.transform.position = new Vector3(3.6f, -3f, 2.3f); body.enabled = true; Physics.SyncTransforms();
            motor.SimulateMove(Vector2.zero, false, 1 / 60f);
            Check(motor.transform.position.y > -0.2f, "床の下へ落ちても安全位置へ復帰");
        }
        private IEnumerator Measure()
        {
            session.ResetRun();
            human.enabled = true;
            cameraRig.enabled = false;
            motor.MovementEnabled = true;
            yield return new WaitForSecondsRealtime(3);
            yield return Capture("arrival-720p.png");
            var times = new List<float>(3000);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            float seconds = 0; long maxGc = 0;
            while (seconds < 30)
            {
                if (!session.IsRunning) session.Resume();
                float dt = Time.unscaledDeltaTime;
                motor.MovementEnabled = true;
                Vector3 goal = Mathf.Repeat(seconds, 4) < 2 ? new Vector3(-5.8f, 0, -4.6f) : new Vector3(-5.5f, 0, -4.0f);
                Vector3 direction = Vector3.ProjectOnPlane(goal - motor.transform.position, Vector3.up);
                Vector3 forward = Vector3.ProjectOnPlane(cameraRig.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector2 input = direction.magnitude < 0.08f ? Vector2.zero : new Vector2(Vector3.Dot(direction.normalized, right), Vector3.Dot(direction.normalized, forward));
                motor.SimulateMove(input, false, dt);
                cameraRig.RotateView(new Vector2(dt * 2, 0));
                cameraRig.Follow(dt);
                times.Add(dt); seconds += dt;
                if (gc.Valid) maxGc = Math.Max(maxGc, gc.LastValue);
                yield return null;
            }
            times.Sort();
            var result = new Performance { unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height, frames = times.Count, seconds = seconds, averageFps = times.Count / seconds,
                percentile95Ms = times[Mathf.CeilToInt(times.Count * 0.95f) - 1] * 1000, maximumMs = times[times.Count - 1] * 1000,
                maximumGcBytes = maxGc, reservedMemoryBytes = memory.Valid ? memory.LastValue : -1,
                gcRecorderAvailable = gc.Valid, memoryRecorderAvailable = memory.Valid };
            File.WriteAllText(Path.Combine(OutputFolder, "phase2-performance.json"), JsonUtility.ToJson(result, true));
            gc.Dispose(); memory.Dispose();
            yield return Capture("gameplay-720p.png");
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name));
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException(description); checks.Add("PASS: " + description); Debug.Log("[PHASE2 PLAY] PASS: " + description); }
        private Vector2 InputToward(Vector3 direction)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraRig.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
        }
        private void Teleport(Vector3 position)
        {
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = 0.03f;
            motor.transform.position = position;
            controller.enabled = true; Physics.SyncTransforms();
        }
        private void Finish(bool passed)
        {
            if (Completed != null) Completed(passed);
            else Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
