#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace GokiTakeover
{
    // Explicit opt-in only (Editor verification or "-goki-phase3-test" on a Development build).
    // Drives the real motor, camera, allies and commander code with scripted input.
    public sealed class PhaseThreeScenarioTest : MonoBehaviour
    {
        public string OutputFolder { get; set; }
        public event Action<bool> Completed;
        private readonly List<string> checks = new List<string>();
        private PhaseTwoSession session;
        private RoachMotor motor;
        private ThirdPersonRoachCamera cameraRig;
        private SwarmViewCamera overview;
        private SwarmCommander commander;
        private HumanAIController human;
        private NavMeshAgent agent;
        private bool performanceRun;
        private const float Step = 1 / 60f;
        [Serializable] private sealed class Performance
        {
            public string unity, cpu, gpu;
            public int width, height, frames, allies;
            public float seconds, averageFps, percentile95Ms, maximumMs, thirdPersonAverageFps, swarmAverageFps;
            public long maximumGcBytes, reservedMemoryBytes;
            public bool gcRecorderAvailable, memoryRecorderAvailable;
            public string scenario = "30秒: 0-10秒 三人称で壁登り→天井、10-20秒 軍団モードで全仲間に回収指示・人間巡回、20-30秒 三人称に戻り床を移動（仲間は回収継続）。";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-phase3-test") < 0) return;
            var runner = new GameObject("Phase3_OptIn_Test").AddComponent<PhaseThreeScenarioTest>();
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
            motor = session.Motor;
            human = session.Human;
            agent = human.GetComponent<NavMeshAgent>();
            cameraRig = session.FollowCamera;
            overview = FindFirstObjectByType<SwarmViewCamera>(FindObjectsInactive.Include);
            commander = FindFirstObjectByType<SwarmCommander>();
            motor.enabled = false; human.enabled = false; cameraRig.enabled = false;
            session.ResetRun();
            cameraRig.enabled = false;
            agent.isStopped = true;
            var suite = Checks();
            var stack = new Stack<IEnumerator>();
            stack.Push(suite);
            while (stack.Count > 0)
            {
                object next;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(OutputFolder, "phase3-failure.txt"), string.Join("\n", checks) + "\nFAIL: " + e);
                    Debug.LogException(e); Finish(false); yield break;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "phase3-gameplay.txt"), checks);
            if (performanceRun) yield return Measure();
            Finish(true);
        }

        private IEnumerator Checks()
        {
            Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && overview && overview.gameObject == cameraRig.gameObject, "描画カメラは1台、軍団視点は同じカメラの別コントローラー");
            Check(FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1 && motor.ClimbEnabled, "主人公1匹・壁登り有効");
            int pool = commander.Allies.Count, active = commander.Settings3.allyCount;
            int enabled = 0; foreach (var a in commander.Allies) if (a.gameObject.activeSelf) enabled++;
            Check(pool == 9 && enabled == active && active >= 4 && active <= 9, "仲間プール9匹のうち設定数" + active + "匹だけ有効");
            Check(session.DeliveryGoal == commander.Settings3.deliveryGoal && commander.AvailableFoodSpots == 4, "納品目標" + session.DeliveryGoal + "個・開始時に見えている食料4か所");

            // ---- Climbing: floor -> wall -> ceiling -> drop ----
            int recoveries = motor.Recoveries;
            Place(new Vector3(5.9f, 0.02f, -2.5f), 90);
            yield return Drive(Vector2.zero, 0.2f);
            yield return Drive(Vector2.up, 2f, () => motor.IsClimbing);
            Check(motor.IsClimbing && motor.SurfaceUp.x < -0.9f, "壁へ進み続けると右の壁に張りつく（法線 " + motor.SurfaceUp.ToString("F2") + "）");
            bool embedded = false;
            yield return Drive(Vector2.up, 9f, () => { embedded |= motor.IsEmbedded; return motor.SurfaceUp.y < -0.9f; });
            Check(motor.IsClimbing && motor.SurfaceUp.y < -0.9f && Mathf.Abs(motor.BodyCentre.y - 4.885f) < 0.08f, "壁を登りきると天井へ移る（高さ " + motor.BodyCentre.y.ToString("F2") + "）");
            Check(!embedded && motor.Recoveries == recoveries, "壁・角・天井で壁の中に埋まらない");
            Vector3 before = motor.BodyCentre;
            yield return Drive(Vector2.up, 1f);
            Check(motor.IsClimbing && Mathf.Abs(motor.BodyCentre.y - before.y) < 0.05f && Flat(motor.BodyCentre - before).magnitude > 0.8f, "天井を逆さまに歩ける");
            Vector3 eye = cameraRig.transform.position;
            Check(eye.y > 0.05f && eye.y < 5.4f && eye.x < 7 && eye.x > -7, "天井移動中もカメラは室内に留まる");
            if (performanceRun) yield return Capture("ceiling-720p.png");
            motor.Detach();
            yield return Drive(Vector2.zero, 3f, () => !motor.IsClimbing && motor.transform.position.y < 0.1f);
            yield return Drive(Vector2.zero, 1f);
            var align = motor.transform.Find("SurfaceAlign");
            Check(!motor.IsClimbing && motor.transform.position.y < 0.1f && Quaternion.Angle(align.localRotation, Quaternion.identity) < 6f, "Spaceで天井から離れて床へ着地し、体の傾きが戻る");

            // Down the wall back to the floor.
            Place(new Vector3(5.9f, 0.02f, -2.5f), 90);
            yield return Drive(Vector2.up, 2f, () => motor.IsClimbing);
            yield return Drive(Vector2.up, 0.8f);
            float high = motor.BodyCentre.y;
            yield return Drive(Vector2.down, 4f, () => !motor.IsClimbing);
            Check(high > 1.2f && !motor.IsClimbing && motor.transform.position.y < 0.1f && !motor.IsEmbedded, "壁を下って床へ戻る");

            // Not climbable: the refrigerator side.
            Place(new Vector3(-2.75f, 0.02f, 4.6f), -90);
            yield return Drive(Vector2.up, 1.2f);
            Check(!motor.IsClimbing, "冷蔵庫など指定外の面は登らない");

            // Cabinet front -> over the worktop lip -> standing on the worktop; take the food; drop down.
            Place(new Vector3(2.6f, 0.02f, 2.9f), 0);
            yield return Drive(Vector2.up, 2f, () => motor.IsClimbing);
            Check(motor.IsClimbing, "食器棚の正面に張りつく");
            embedded = false;
            yield return Drive(Vector2.up, 8f, () => { embedded |= motor.IsEmbedded; return !motor.IsClimbing; });
            yield return Drive(Vector2.zero, 0.3f);
            float top = motor.transform.position.y;
            Check(!motor.IsClimbing && top > 2.8f && top < 2.95f && !embedded, "天板の縁を回り込んで天板の上に立つ（高さ " + top.ToString("F2") + "）");
            Check(!session.PlayerStrikeable, "天板の上はスリッパが届かない");
            var worktopFood = commander.Foods[3];
            Place(worktopFood.transform.position + new Vector3(-0.45f, 0, 0), 90, worktopFood.transform.position.y - 0.065f);
            Check(session.Interactor.TryInteract() && session.Interactor.Carried == worktopFood, "天板の上の食料を回収");
            Place(new Vector3(3.2f, 2.88f, 4.0f), 180, 2.88f);
            yield return Drive(Vector2.up, 3f, () => motor.transform.position.y < 0.1f);
            Check(motor.transform.position.y < 0.1f && session.Interactor.Carried == worktopFood && !motor.IsEmbedded, "食料を持ったまま天板から床へ降りる");
            Place(session.Nest.position + Vector3.right * 0.3f, 0);
            Check(session.Interactor.TryInteract() && session.Delivered == 1, "高所の食料を巣へ納品");

            // The resident cannot slam a roach that is on a wall.
            session.ResetRun(); cameraRig.enabled = false; agent.isStopped = true;
            yield return new WaitForSeconds(1.7f);
            Place(new Vector3(5.9f, 0.02f, -2.5f), 90);
            yield return Drive(Vector2.up, 2f, () => motor.IsClimbing);
            yield return Drive(Vector2.up, 0.25f);
            agent.Warp(new Vector3(4.9f, 0, -2.5f)); human.transform.rotation = Quaternion.Euler(0, 90, 0);
            bool attacked = false;
            for (int i = 0; i < 40; i++) { human.Tick(0.1f); attacked |= human.State == HumanAIController.HumanState.Attack; }
            Check(human.State == HumanAIController.HumanState.Chase && !attacked && session.Hp == 3, "壁にいる間は発見されても叩かれない");
            motor.Detach();
            yield return Drive(Vector2.zero, 2f, () => !motor.IsClimbing && motor.transform.position.y < 0.1f);
            Place(new Vector3(5.9f, 0.02f, -2.5f), 90);
            yield return Drive(Vector2.up, 2f, () => motor.IsClimbing);
            session.ResetRun(); cameraRig.enabled = false; agent.isStopped = true;
            Check(!motor.IsClimbing && Vector3.Distance(session.Player.position, session.Nest.position) < 1.2f && motor.GetComponent<CharacterController>().enabled, "壁に張りついたままリトライしても巣の床から再開");

            // ---- Tab: one camera, world state kept ----
            commander.enabled = false;
            var positions = new Vector3[pool];
            for (int i = 0; i < pool; i++) positions[i] = commander.Allies[i].Position;
            Vector3 player = motor.transform.position, resident = human.transform.position;
            int hp = session.Hp, delivered = session.Delivered;
            for (int i = 0; i < 11; i++)
            {
                commander.SetMode(commander.Mode == SwarmCommander.ViewMode.Swarm ? SwarmCommander.ViewMode.ThirdPerson : SwarmCommander.ViewMode.Swarm);
                yield return null;
                Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && cameraRig.enabled != overview.enabled, "Tab切替" + (i + 1) + "回目: カメラ1台・有効な視点は片方だけ");
            }
            bool alliesKept = true;
            for (int i = 0; i < pool; i++) alliesKept &= (commander.Allies[i].Position - positions[i]).sqrMagnitude < 1e-6f;
            Check((motor.transform.position - player).sqrMagnitude < 1e-6f && session.Hp == hp && session.Delivered == delivered,
                "連続切替でも主人公の位置・HP・納品数を維持（移動 " + (motor.transform.position - player).magnitude.ToString("F3") + "m）");
            Check(alliesKept && Flat(human.transform.position - resident).magnitude < 0.05f, "連続切替でも仲間・人間の位置を維持（人間 " + Flat(human.transform.position - resident).magnitude.ToString("F3") + "m）");
            Check(FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<AllyRoach>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 9,
                "連続切替でキャラクターを複製しない");
            Check(commander.Mode == SwarmCommander.ViewMode.Swarm && overview.enabled && !cameraRig.enabled && Camera.main.transform.forward.y < -0.7f, "軍団モードは見下ろし視点");
            var room = GameObject.Find("Room").transform;
            var ceiling = room.Find("Ceiling").GetComponent<Renderer>();
            var front = room.Find("Wall_Front").GetComponent<Renderer>();
            Check(!ceiling.enabled && front.shadowCastingMode == ShadowCastingMode.ShadowsOnly, "軍団モードでは天井と手前の壁を隠す（壁の影は維持）");
            Check(Time.timeScale == 1 && session.IsRunning && Cursor.lockState == CursorLockMode.None && Cursor.visible, "軍団モードでも時間は止まらず、マウスで指示できる");
            commander.enabled = true;
            commander.SelectOnly(2);
            Check(commander.SelectedCount == 1 && commander.IsSelected(2), "クリック相当で仲間を1匹選択");
            Vector3 target = new Vector3(0, 0, -2);
            var view = overview.Camera;
            commander.PointOrder(view, view.WorldToScreenPoint(target));
            Check(commander.Allies[2].State == AllyRoach.AllyState.GoToTarget && Flat(commander.RallyPoint - target).magnitude < 0.3f && commander.Allies[1].State == AllyRoach.AllyState.FollowLeader,
                "右クリック相当で選択中の仲間だけが目標へ向かう（目標 " + commander.RallyPoint.ToString("F2") + "）");
            Vector3 start = commander.Allies[2].Position;
            yield return new WaitForSeconds(1f);
            Check(Flat(commander.Allies[2].Position - start).magnitude > 0.5f, "軍団モード中も仲間は実時間で移動");
            commander.SelectAll();
            Check(commander.SelectedCount == active, "Q相当で全員選択");
            commander.PointOrder(view, view.WorldToScreenPoint(commander.Foods[1].transform.position));
            Check(commander.Allies[0].State == AllyRoach.AllyState.CollectFood && commander.LastOrder == AllyRoach.Order.Collect, "食料を右クリックすると回収指示");
            commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
            Check(cameraRig.enabled && !overview.enabled && ceiling.enabled && front.shadowCastingMode == ShadowCastingMode.On, "通常モードへ戻ると三人称カメラ・天井・壁を復元");
            cameraRig.enabled = false;

            // ---- Orders: collect, explore, collect again until the goal ----
            session.ResetRun(); cameraRig.enabled = false; agent.isStopped = true;
            commander.enabled = false;
            Check(commander.Command(AllyRoach.Order.Collect) == active, "通常視点の1キー相当で全仲間に回収指示");
            yield return Simulate(160, () => commander.AllyDeliveries >= 3 && NoneCarrying());
            Check(commander.AllyDeliveries == 3 && session.Delivered == 3 && commander.Foods[0].State == FoodItem.FoodState.Delivered
                && commander.Foods[1].State == FoodItem.FoodState.Delivered && commander.Foods[2].State == FoodItem.FoodState.Delivered, "仲間が床の食料3個を回収・納品（1個につき1回だけ加算）");
            Check(commander.Foods[3].State == FoodItem.FoodState.Available && !IsTargeted(commander.Foods[3]), "仲間は登れないので天板の食料は狙わない");
            yield return Simulate(25, () => AllIn(AllyRoach.AllyState.Idle));
            Check(AllIn(AllyRoach.AllyState.Idle) && commander.Notice.Contains("高い所"), "回収できる食料が尽きたら巣へ戻って待機し、理由を表示");
            Check(commander.Command(AllyRoach.Order.Explore) == active, "探索を指示");
            yield return Simulate(200, () => commander.ScoutedCount == commander.ScoutPoints.Count);
            bool allFound = true;
            for (int i = 4; i < commander.Foods.Count; i++) allFound &= commander.IsDiscovered(i) && commander.Foods[i].transform.Find("FoodBeacon").gameObject.activeSelf;
            Check(commander.ScoutedCount == commander.ScoutPoints.Count && allFound, "探索で調査地点" + commander.ScoutPoints.Count + "か所を調べ、隠れた食料4個を発見");
            yield return Simulate(30, () => AllIn(AllyRoach.AllyState.Idle));
            Check(AllIn(AllyRoach.AllyState.Idle), "探索完了後は巣に戻って待機");
            commander.Command(AllyRoach.Order.Collect);
            yield return Simulate(200, () => session.State == PhaseTwoSession.RunState.Cleared);
            Check(session.State == PhaseTwoSession.RunState.Cleared && session.Delivered == session.DeliveryGoal, "発見した食料も回収し、目標" + session.DeliveryGoal + "個でクリア");
            if (performanceRun) yield return Capture("clear-720p.png");

            // ---- Failure cases must end, not loop ----
            session.ResetRun(); cameraRig.enabled = false; agent.isStopped = true;
            commander.enabled = false;
            var ally = commander.Allies[0];
            ally.SetOrder(AllyRoach.Order.Move, new Vector3(20, 0, 20));
            yield return Simulate(1, () => ally.State == AllyRoach.AllyState.Idle);
            Check(ally.State == AllyRoach.AllyState.Idle, "部屋の外など到達不能な目標は諦めて待機");
            ally.SetOrder(AllyRoach.Order.Move, new Vector3(1.35f, 0, 4.8f));
            yield return Simulate(20, () => ally.State == AllyRoach.AllyState.Idle);
            Check(ally.State == AllyRoach.AllyState.Idle && commander.Grid.Walkable(ally.Position) && ally.Position.z < 3.7f, "家具の中を指定したら手前の床で止まる");

            // ---- Survival beats orders: flee the telegraphed slipper; a slow ally is crushed and respawns ----
            yield return new WaitForSeconds(1.7f);
            agent.Warp(new Vector3(3.6f, 0, -0.7f)); human.transform.rotation = Quaternion.identity;
            Place(new Vector3(3.6f, 0.02f, 0.7f), 0);
            for (int i = 0; i < 12 && human.State != HumanAIController.HumanState.Attack; i++) human.Tick(0.1f);
            Check(human.State == HumanAIController.HumanState.Attack, "人間がスリッパの予兆を開始");
            Place(new Vector3(4.8f, 0.02f, -3.5f), 0);
            var runner = commander.Allies[0]; var slow = commander.Allies[1];
            Vector3 right = Vector3.Cross(Vector3.up, human.StrikeForward);
            runner.SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            runner.transform.position = human.AttackPoint + right * 0.15f - Vector3.up * human.AttackPoint.y;
            slow.transform.position = human.AttackPoint - Vector3.up * human.AttackPoint.y;
            bool fled = false;
            for (int i = 0; i < 60 && !human.SlipperLanded; i++)
            {
                human.Tick(0.03f);
                runner.Tick(0.03f);
                fled |= runner.State == AllyRoach.AllyState.FleeHazard;
            }
            Check(fled && runner.State != AllyRoach.AllyState.Disabled && !human.IsUnderSlipper(runner.Position), "予兆の枠内の仲間は命令より回避を優先して逃げる");
            Check(slow.State == AllyRoach.AllyState.Disabled && commander.AllyLosses == 1 && commander.Notice.Contains("スリッパ"), "逃げ遅れた仲間は潰されて行動不能・通知");
            yield return new WaitForSeconds(commander.Settings3.allyRespawnSeconds + 0.3f);
            slow.Tick(0.02f);
            Check(slow.Active && Vector3.Distance(slow.Position, commander.NestGround) < 2.5f, commander.Settings3.allyRespawnSeconds.ToString("F0") + "秒後に巣で復活");
            session.ResetRun(); cameraRig.enabled = false;
            commander.enabled = true;
            Check(commander.AllyLosses == 0 && commander.Mode == SwarmCommander.ViewMode.ThirdPerson && commander.AvailableFoodSpots == 4, "リトライで仲間・発見状態・視点を初期化");
        }

        // ---------- helpers ----------
        private void Place(Vector3 position, float yaw, float y = float.NaN)
        {
            motor.ResetSurface();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = float.IsNaN(y) ? 0.02f : y;
            motor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSafePosition();
            cameraRig.ResetView();
        }

        private IEnumerator Drive(Vector2 input, float seconds, Func<bool> until = null)
        {
            int steps = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                motor.MovementEnabled = true;   // the session re-derives it from cursor lock every frame
                motor.SimulateMove(input, false, Step);
                cameraRig.Follow(Step);
                if (until != null && until()) yield break;
                if (i % 12 == 11) yield return null;
            }
        }

        // Fixed-step ally simulation (commander disabled), so long errands are tested quickly and repeatably.
        private IEnumerator Simulate(float seconds, Func<bool> until)
        {
            const float dt = 0.05f;
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                foreach (var a in commander.Allies) a.Tick(dt);
                if (until()) yield break;
                if (i % 40 == 39) yield return null;
            }
        }

        private bool NoneCarrying() { foreach (var a in commander.Allies) if (a.Carried) return false; return true; }
        private bool IsTargeted(FoodItem food) { foreach (var a in commander.Allies) if (a.TargetFood == food || a.Carried == food) return true; return false; }
        private bool AllIn(AllyRoach.AllyState state)
        {
            foreach (var a in commander.Allies) if (a.gameObject.activeSelf && a.State != state) return false;
            return true;
        }

        private IEnumerator Measure()
        {
            session.ResetRun();
            commander.enabled = true;
            human.enabled = true;
            agent.isStopped = false;
            cameraRig.enabled = false;
            yield return new WaitForSecondsRealtime(2);
            Place(new Vector3(5.4f, 0.02f, -2.5f), 90);
            var times = new List<float>(3000);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            float seconds = 0, tpsTime = 0, swarmTime = 0; int tpsFrames = 0, swarmFrames = 0; long maxGc = 0;
            bool wallShot = false, swarmShot = false, alliesShot = false;
            int skip = 0;   // frames right after a screenshot are not gameplay frames
            while (seconds < 30)
            {
                if (session.State == PhaseTwoSession.RunState.Paused) session.Resume();
                if (!session.IsRunning) { session.ResetRun(); cameraRig.enabled = false; }
                float dt = Time.unscaledDeltaTime;
                motor.MovementEnabled = true;
                if (seconds < 10)
                {
                    motor.SimulateMove(Vector2.up, false, dt);
                    cameraRig.Follow(dt);
                    tpsTime += dt; tpsFrames++;
                    if (!wallShot && motor.IsClimbing && motor.BodyCentre.y > 2.2f) { wallShot = true; skip = 2; yield return Capture("wall-720p.png"); }
                }
                else if (seconds < 20)
                {
                    if (commander.Mode != SwarmCommander.ViewMode.Swarm)
                    {
                        motor.Detach();
                        commander.SetMode(SwarmCommander.ViewMode.Swarm);
                        commander.SelectAll();
                        commander.Command(AllyRoach.Order.Collect);
                    }
                    motor.SimulateMove(Vector2.zero, false, dt);
                    swarmTime += dt; swarmFrames++;
                    if (!swarmShot && seconds > 15) { swarmShot = true; skip = 2; yield return Capture("swarm-720p.png"); }
                }
                else
                {
                    if (commander.Mode != SwarmCommander.ViewMode.ThirdPerson) { commander.SetMode(SwarmCommander.ViewMode.ThirdPerson); cameraRig.enabled = false; }
                    motor.SimulateMove(new Vector2(0.4f, 1), false, dt);
                    cameraRig.RotateView(new Vector2(dt * 2, 0));
                    cameraRig.Follow(dt);
                    tpsTime += dt; tpsFrames++;
                    if (!alliesShot && seconds > 24) { alliesShot = true; skip = 2; yield return Capture("allies-720p.png"); }
                }
                if (skip > 0) skip--; else times.Add(dt);
                seconds += dt;
                if (gc.Valid) maxGc = Math.Max(maxGc, gc.LastValue);
                yield return null;
            }
            times.Sort();
            var result = new Performance
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height, frames = times.Count, seconds = seconds, averageFps = times.Count / seconds,
                percentile95Ms = times[Mathf.CeilToInt(times.Count * 0.95f) - 1] * 1000, maximumMs = times[times.Count - 1] * 1000,
                thirdPersonAverageFps = tpsFrames / Mathf.Max(tpsTime, 0.001f), swarmAverageFps = swarmFrames / Mathf.Max(swarmTime, 0.001f),
                allies = commander.Settings3.allyCount, maximumGcBytes = maxGc, reservedMemoryBytes = memory.Valid ? memory.LastValue : -1,
                gcRecorderAvailable = gc.Valid, memoryRecorderAvailable = memory.Valid
            };
            File.WriteAllText(Path.Combine(OutputFolder, "phase3-performance.json"), JsonUtility.ToJson(result, true));
            gc.Dispose(); memory.Dispose();
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name));
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException(description); checks.Add("PASS: " + description); Debug.Log("[PHASE3 PLAY] PASS: " + description); }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        private void Finish(bool passed)
        {
            if (Completed != null) Completed(passed);
            else Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
