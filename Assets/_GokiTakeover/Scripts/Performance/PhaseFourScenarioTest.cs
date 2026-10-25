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
    // Explicit opt-in only (Editor verification or "-goki-phase4-test" on a Development build).
    // Uses the real colony, reaction, hazard and HUD code; settings go to a separate test file.
    public sealed class PhaseFourScenarioTest : MonoBehaviour
    {
        public string OutputFolder { get; set; }
        public event Action<bool> Completed;
        private readonly List<string> checks = new List<string>();
        private PhaseTwoSession session;
        private RoachMotor motor;
        private ThirdPersonRoachCamera cameraRig;
        private SwarmCommander commander;
        private ColonyManager colony;
        private HumanReaction reaction;
        private HumanAIController human;
        private NavMeshAgent agent;
        private bool performanceRun;
        private const float Step = 1 / 60f;
        [Serializable] private sealed class Performance
        {
            public string unity, cpu, gpu;
            public int width, height, frames, allies, trapsPlaced, resets, damageTaken;
            public float seconds, averageFps, percentile95Ms, maximumMs, maximumAtSecond;
            public long maximumGcBytes, reservedMemoryBytes;
            public string reactionLevel, events = "";
            public string scenario = "30秒: 仲間8匹・人間はパニック（罠設置・スプレーあり）。0-10秒 三人称で人間に近づく、10-20秒 軍団モードで回収指示、20-30秒 三人称で移動。";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void IsolateSettings()
        {
#if UNITY_EDITOR
            string path = UnityEditor.SessionState.GetString("GokiTakeover.Phase4.SettingsPath", "");
            if (!string.IsNullOrEmpty(path)) GameSettings.PathOverride = path;
#endif
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-goki-output");
            if (Array.IndexOf(args, "-goki-phase4-test") >= 0 && index >= 0 && index + 1 < args.Length)
                GameSettings.PathOverride = Path.Combine(args[index + 1], "settings-test.json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-phase4-test") < 0) return;
            var runner = new GameObject("Phase4_OptIn_Test").AddComponent<PhaseFourScenarioTest>();
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
            commander = FindFirstObjectByType<SwarmCommander>();
            colony = FindFirstObjectByType<ColonyManager>();
            reaction = colony.Reaction;
            motor.enabled = false; human.enabled = false;
            Restart();
            var stack = new Stack<IEnumerator>();
            stack.Push(Checks());
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
                    File.WriteAllText(Path.Combine(OutputFolder, "phase4-failure.txt"), string.Join("\n", checks) + "\nFAIL: " + e);
                    Debug.LogException(e); Finish(false); yield break;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "phase4-gameplay.txt"), checks);
            if (performanceRun) yield return Measure();
            Finish(true);
        }

        private void Restart()
        {
            session.ResetRun();
            cameraRig.enabled = false;
            commander.enabled = false;
            agent.isStopped = true;
        }

        private IEnumerator Checks()
        {
            var settings = colony.Settings;
            Check(colony.Allies == settings.startAllies && colony.Food == 0 && colony.NestLevel == 1 && colony.AllyCap == 6, "開始時: 仲間" + settings.startAllies + "匹・食料0・巣Lv1（上限6匹）");
            Check(reaction.CurrentLevel == HumanReaction.Level.Peace && human.SpeedScale == 1 && session.MaximumHp == 3, "開始時: 人間は「平和」、生命力3");
            Check(session.DeliveryGoal == int.MaxValue, "このフェーズは納品数ではクリアにならない（仲間の数で判定）");

            // ---- Deliveries become colony food, by kind ----
            var foods = commander.Foods;
            Place(foods[0].transform.position + Vector3.right * 0.35f, 0);
            Check(session.Interactor.TryInteract(), "パンくずを回収");
            Place(session.Nest.position + Vector3.right * 0.3f, 0);
            Check(session.Interactor.TryInteract(), "巣で納品");
            yield return null;
            Check(colony.Food == 1 && colony.TotalCollected == 1, "パンくず納品で食料+1");
            yield return null;
            Check(colony.Food == 1, "同じ納品を二重に数えない");
            Place(foods[3].transform.position + new Vector3(-0.45f, 0, 0), 90, foods[3].transform.position.y - 0.065f);
            Check(session.Interactor.TryInteract() && FoodValue.AmountOf(foods[3]) == 2, "天板のお菓子（+2）を回収");
            Place(session.Nest.position + Vector3.right * 0.3f, 0);
            session.Interactor.TryInteract();
            yield return null;
            Check(colony.Food == 3 && colony.Options.Knows("food_Sweets"), "お菓子の納品で食料+2、図鑑に記録");
            Vector3 home = foods[0].transform.position;
            colony.DebugRespawnDue();
            yield return null; yield return null;
            Check(foods[0].State == FoodItem.FoodState.Available && foods[0].gameObject.activeSelf && Vector3.Distance(foods[0].transform.position, session.Player.position) >= 3f,
                "納品した食料は時間がたつと別の場所に補充される（" + foods[0].transform.position.ToString("F1") + "）");

            // ---- Nest: breeding, refusing without food, upgrades, expansion ----
            colony.OpenNestMenu();
            Check(session.State == PhaseTwoSession.RunState.Paused && colony.NestMenuOpen, "巣メニューを開くとゲームが止まる");
            Check(colony.TryBreed() && colony.Allies == 5 && colony.Food == 0, "食料3で仲間が1匹増える");
            Check(!colony.TryBreed() && colony.Allies == 5 && colony.Food == 0 && colony.Message.Contains("足りません"), "食料不足では増やさず、消費もせず理由を表示");
            Check(!colony.TryUpgrade(0) && colony.LevelAt(0) == 0 && colony.Food == 0, "食料不足では強化しない");
            colony.DebugGrant(60);
            Check(colony.TryUpgrade(0) && colony.Food == 55 && Mathf.Approximately(motor.UpgradeSpeed, 1.08f) && Mathf.Approximately(commander.AllySpeedScale, 1.08f), "移動速度Lv1: 主人公・仲間が8%速く（食料-5）");
            Check(colony.TryUpgrade(2) && colony.Food == 49 && Mathf.Approximately(human.DetectionTimeScale, 1.2f), "隠密性Lv1: 発見までの時間+20%（食料-6）");
            Check(colony.TryUpgrade(4) && session.MaximumHp == 4 && session.Hp == 4, "耐久力Lv1: 生命力の上限と現在値+1");
            Check(colony.TryUpgrade(1) && session.CarryingSpeed > session.Settings.carryingSpeed, "運搬力Lv1: 運搬中の減速が小さくなる");
            Check(colony.TryUpgrade(3) && colony.BreedCost == 2, "繁殖力Lv1: 増員費用 3→2");
            int food = colony.Food;
            Check(!colony.TryUpgrade(3) && colony.Food == food && colony.Message.Contains("最大"), "最大レベルの強化は消費せず理由を表示");
            Check(colony.TryUpgrade(0) && colony.CostAt(0) == settings.upgrades[0].CostAt(2), "強化は段階ごとに費用が上がる");
            Check(colony.TryBreed() && colony.Allies == 6, "仲間6匹（巣Lv1の上限）");
            food = colony.Food;
            Check(!colony.TryBreed() && colony.Allies == 6 && colony.Food == food && colony.Message.Contains("拡張"), "上限では増やさず、巣の拡張を促す");
            Check(colony.TryExpand() && colony.NestLevel == 2 && colony.AllyCap == 9, "巣を拡張すると上限9匹");
            Check(!colony.TryExpand(), "巣はLv2が最大");
            Check(colony.TryBreed() && colony.TryBreed() && colony.Allies == 8 && session.State == PhaseTwoSession.RunState.Paused, "仲間8匹（まだクリアではない）");
            colony.CloseNestMenu();
            Check(session.IsRunning && !colony.NestMenuOpen, "巣メニューを閉じると再開");
            colony.OpenNestMenu();
            Check(colony.TryBreed() && colony.Allies == 9 && session.State == PhaseTwoSession.RunState.Cleared && !colony.NestMenuOpen, "仲間9匹でコロニー完成（クリア）");
            if (performanceRun) yield return Capture("clear-720p.png");
            Restart();
            Check(colony.Allies == 4 && colony.Food == 0 && colony.NestLevel == 1 && colony.LevelAt(0) == 0 && motor.UpgradeSpeed == 1 && session.MaximumHp == 3
                && Mathf.Approximately(session.CarryingSpeed, session.Settings.carryingSpeed) && colony.BreedCost == 3, "リトライで食料・仲間・巣・強化を初期化");

            // ---- Reaction levels ----
            session.Discoveries = 1;
            yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Noticed && Mathf.Approximately(human.SpeedScale, 1.1f) && reaction.BubbleVisible, "初めて見つかると「発見」: 人間が速くなり吹き出しを表示");
            session.Discoveries = 3;
            yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Alert && Mathf.Approximately(human.DetectionTimeScale, 0.75f), "3回見つかると「警戒」: 見つけるのが速くなる");
            reaction.AddFear(100);
            yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Panic && reaction.HighestLevel == HumanReaction.Level.Panic, "恐怖度が上がると「パニック」");
            reaction.AddFear(-60);
            yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Alert, "恐怖度が下がるとパニックは収まるが「警戒」のまま");

            // ---- Rule-based countermeasures: bait first, then a sticky sheet, where roaches were seen ----
            var spot = new Vector3(2.0f, 0, -4.5f);
            reaction.RecordSighting(spot, 5);
            agent.isStopped = false;
            Check(reaction.BeginPlacement() && reaction.CurrentAction == HumanReaction.Activity.Walking && human.Suspended, "警戒した人間が目撃の多い場所へ罠を置きに行く");
            yield return WaitUntil(() => reaction.TrapsPlaced == 1, 20);
            PoisonBait bait = null;
            foreach (var b in reaction.Baits) if (b.IsArmed) bait = b;
            Check(bait && Flat(bait.transform.position - spot).magnitude < 1.6f && !bait.Identified && !commander.IsScouted(bait.ScoutIndex), "1つ目は毒餌（未判別）を目撃地点の近くに設置し、その区域を未調査に戻す");
            Check(reaction.CurrentAction == HumanReaction.Activity.None && !human.Suspended, "設置後は人間の通常AIに制御を返す");
            var spot2 = new Vector3(5.0f, 0, -3.8f);
            reaction.RecordSighting(spot2, 5);
            Check(reaction.BeginPlacement(), "2つ目の設置へ（同じ区域は続けて選ばない）");
            yield return WaitUntil(() => reaction.TrapsPlaced == 2, 20);
            StickyTrap placedTrap = null;
            foreach (var t in reaction.PlacedTraps) if (t.gameObject.activeSelf) placedTrap = t;
            Check(placedTrap && Flat(placedTrap.transform.position - spot2).magnitude < 2.5f && commander.Grid.IsSticky(placedTrap.transform.position), "2つ目は粘着シート。仲間の経路では高コスト地帯になる");
            Place(placedTrap.transform.position, 0);
            yield return null;
            Check(session.IsSticky && colony.Options.Knows("sticky"), "置かれた粘着シートでも主人公が減速、図鑑に記録");
            Place(session.Nest.position + Vector3.right * 0.3f, 0);
            var path = new List<Vector3>();
            Check(commander.Grid.FindPath(commander.NestGround, new Vector3(5.8f, 0, -4.8f), path), "罠を置いても仲間の通り道は塞がれない");

            // ---- Poison bait: identified by allies, harmful when taken ----
            var sniffer = commander.Allies[0];
            sniffer.transform.position = bait.transform.position + new Vector3(0.8f, 0, 0) - Vector3.up * bait.transform.position.y;
            yield return new WaitForSeconds(0.35f);
            Check(bait.Identified && colony.Options.Knows("bait") && commander.Notice.Contains("毒餌"), "仲間が近づくと毒餌を見破り、通知・図鑑に記録");
            var trap2 = reaction.Place(new Vector3(-2.2f, 0, -5.0f)) as PoisonBait;
            Check(trap2 && !trap2.Identified, "未判別の毒餌");
            yield return new WaitForSeconds(1.7f);
            int hp = session.Hp;
            Place(trap2.transform.position + Vector3.right * 0.4f, 0);
            Check(!session.Interactor.NearbyFood && reaction.NearbyBait(session.Player.position, session.Settings.interactionRange) == trap2, "毒餌は食料として扱われず、E の対象になる");
            reaction.EatBait(trap2);
            Check(session.Hp == hp - 1 && !trap2.IsArmed, "毒餌を拾うと生命力-1、毒餌は消える");

            // ---- Insecticide spray in panic ----
            reaction.AddFear(100);
            yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Panic, "パニックに戻す");
            session.AddMaxHp(2);   // keep the roach alive through a possible double hit
            yield return new WaitForSeconds(1.7f);
            agent.Warp(new Vector3(3.6f, 0, -0.7f)); human.transform.rotation = Quaternion.identity;
            Place(new Vector3(3.6f, 0.02f, 1.9f), 0);
            for (int i = 0; i < 12; i++) human.Tick(0.1f);
            Check(human.State == HumanAIController.HumanState.Chase && human.CanSeePlayer, "人間が中距離で主人公を追跡");
            var victim = commander.Allies[1];
            victim.transform.position = new Vector3(3.75f, 0, 1.3f);
            hp = session.Hp;
            yield return null;
            var spray = reaction.Spray;
            Check(reaction.CurrentAction == HumanReaction.Activity.SprayWindup && spray.Telegraphing && human.Suspended, "パニック中の人間が殺虫スプレーを構え、扇形の予兆を表示");
            // The screenshot itself takes about half a second, which is already most of the windup.
            if (performanceRun) yield return Capture("spray-720p.png");
            else yield return new WaitForSeconds(0.45f);
            Check(session.Hp == hp && reaction.CurrentAction == HumanReaction.Activity.SprayWindup, "予兆中はダメージなし");
            yield return WaitUntil(() => reaction.CurrentAction == HumanReaction.Activity.None, 5);
            Check(session.Hp < hp && session.Hp >= hp - 2 && reaction.SprayHits >= 1 && colony.Options.Knows("spray"), "スプレーを浴びると生命力が減る（無敵時間で連続ヒットは抑制）・図鑑に記録");
            Check(victim.State == AllyRoach.AllyState.Disabled, "扇の中の仲間もやられる");
            Check(!human.Suspended && !spray.Spraying && !spray.Telegraphing, "噴射後は人間の通常AIに戻る");
            spray.Telegraph(new Vector3(5.5f, 2.2f, -2f), Vector3.right, 3.8f, 24f);
            bool open = spray.Hits(new Vector3(6.5f, 0.3f, -2f), null), walled = spray.Hits(new Vector3(7.6f, 0.3f, -2f), null);
            bool side = spray.Hits(new Vector3(5.6f, 0.3f, 0.5f), null);
            spray.Stop();
            Check(open && !walled && !side, "スプレーは扇の外と壁の向こうには届かない");

            // ---- Seeing many allies scares the resident ----
            Restart();
            agent.Warp(new Vector3(3.6f, 0, -0.7f)); human.transform.rotation = Quaternion.identity;
            Vector3[] seen = { new Vector3(3.6f, 0, 1.5f), new Vector3(3.2f, 0, 2.0f), new Vector3(4.0f, 0, 2.5f) };
            for (int i = 0; i < seen.Length; i++) commander.Allies[i].transform.position = seen[i];
            yield return new WaitForSeconds(1.2f);
            Check(reaction.VisibleAllies >= 2 && reaction.Fear > 2, "人間に見られた仲間の数だけ恐怖度が上がる（恐怖度 " + reaction.Fear.ToString("F1") + "）");

            // ---- Settings and encyclopedia persistence ----
            colony.LoadOptions();
            Check(colony.Options.Knows("spray") && colony.Options.Knows("bait"), "図鑑の記録はファイルに保存され、再読込で残る");
            colony.Options.mouseSensitivity = 3.5f;
            colony.Options.cameraShake = false;
            colony.SaveOptions();
            Check(Mathf.Approximately(cameraRig.Sensitivity, 3.5f) && cameraRig.ShakeScale == 0 && GameSettings.Load().mouseSensitivity == 3.5f && File.Exists(GameSettings.FilePath),
                "設定（マウス感度・画面の揺れ）を即時反映して保存");
            colony.Options.quality = 0; colony.SaveOptions();
            bool low = QualitySettings.shadows == ShadowQuality.Disable && QualitySettings.antiAliasing == 0;
            colony.Options.quality = 1; colony.SaveOptions();
            Check(low && QualitySettings.shadows != ShadowQuality.Disable, "画質 低（影・AAなし）／中（キッチンの影あり）を切替");
            File.WriteAllText(GameSettings.FilePath, "{ not json");
            var broken = GameSettings.Load();
            Check(broken.mouseSensitivity == 2f && GameSettings.LastLoadMessage != null && File.Exists(GameSettings.FilePath + ".bak"), "壊れた設定ファイルは .bak に退避して初期設定で起動（例外なし）");
            colony.Options.mouseSensitivity = 2f; colony.Options.cameraShake = true; colony.SaveOptions();

            // ---- Reset clears the resident's history and the placed hazards ----
            Vector3 trapAt = placedTrap.transform.position;
            Restart();
            bool cleared = reaction.CurrentLevel == HumanReaction.Level.Peace && reaction.Fear == 0 && reaction.TrapsPlaced == 0 && !commander.Grid.IsSticky(trapAt);
            foreach (var b in reaction.Baits) cleared &= !b.IsArmed;
            foreach (var t in reaction.PlacedTraps) cleared &= !t.gameObject.activeSelf;
            Check(cleared, "リトライで人間の反応・恐怖度・置かれた罠と毒餌を初期化");
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
        private IEnumerator WaitUntil(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Measure()
        {
            Restart();
            commander.enabled = true;
            colony.DebugGrant(40);
            colony.OpenNestMenu();
            colony.TryUpgrade(0); colony.TryUpgrade(2);
            colony.TryBreed(); colony.TryBreed(); colony.TryExpand(); colony.TryBreed(); colony.TryBreed();
            yield return Capture("nest-720p.png");
            colony.CloseNestMenu();
            session.Pause();
            var page = typeof(PhaseFourHud).GetField("page", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hud = FindFirstObjectByType<PhaseFourHud>();
            page.SetValue(hud, Enum.ToObject(page.FieldType, 1));
            yield return Capture("settings-720p.png");
            page.SetValue(hud, Enum.ToObject(page.FieldType, 2));
            yield return Capture("codex-720p.png");
            session.Resume();
            reaction.AddFear(100);
            reaction.Place(new Vector3(1.0f, 0, -4.6f));
            reaction.Place(new Vector3(4.8f, 0, -3.6f));
            human.enabled = true;
            agent.isStopped = false;
            session.AddMaxHp(20);   // the run measures frame time; being caught must not reset the colony
            int startHp = session.Hp, resets = 0;
            Place(new Vector3(-1.0f, 0.02f, -3.6f), 60);
            yield return new WaitForSecondsRealtime(1);
            var times = new List<float>(3000);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            float seconds = 0, worst = 0, worstAt = 0; long maxGc = 0; int skip = 0;
            string events = "";
            bool tpsShot = false, swarmShot = false;
            while (seconds < 30)
            {
                if (session.State == PhaseTwoSession.RunState.Paused) session.Resume();
                if (!session.IsRunning)
                {
                    events += "reset " + session.State + " hp" + session.Hp + " @" + seconds.ToString("F1") + "; ";
                    session.ResetRun(); cameraRig.enabled = false; reaction.AddFear(100); resets++;
                }
                float dt = Time.unscaledDeltaTime;
                motor.MovementEnabled = true;
                if (seconds < 10 || seconds >= 20)
                {
                    if (commander.Mode != SwarmCommander.ViewMode.ThirdPerson) { commander.SetMode(SwarmCommander.ViewMode.ThirdPerson); }
                    cameraRig.enabled = false;
                    motor.SimulateMove(seconds < 10 ? new Vector2(0, 0.6f) : new Vector2(0.4f, 1), false, dt);
                    cameraRig.RotateView(new Vector2(seconds < 10 ? 0 : dt * 2, 0));
                    cameraRig.Follow(dt);
                    if (!tpsShot && seconds > 4) { tpsShot = true; skip = 2; yield return Capture("tps-720p.png"); }
                }
                else
                {
                    if (commander.Mode != SwarmCommander.ViewMode.Swarm)
                    {
                        motor.Detach();
                        commander.SetMode(SwarmCommander.ViewMode.Swarm);
                        commander.SelectAll();
                    }
                    else if (commander.LastOrder != AllyRoach.Order.Collect && seconds > 10.6f) commander.Command(AllyRoach.Order.Collect);
                    motor.SimulateMove(Vector2.zero, false, dt);
                    if (!swarmShot && seconds > 15) { swarmShot = true; skip = 2; yield return Capture("swarm-720p.png"); }
                }
                if (skip > 0) skip--;
                else
                {
                    if (times.Count > 0 && dt > worst) { worst = dt; worstAt = seconds; }
                    times.Add(dt);
                    if (gc.Valid) maxGc = Math.Max(maxGc, gc.LastValue);
                    if (gc.Valid && gc.LastValue > 100000) events += "gc " + gc.LastValue + " @" + seconds.ToString("F1") + "; ";
                }
                seconds += dt;
                yield return null;
            }
            times.Sort();
            var result = new Performance
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height, frames = times.Count, seconds = seconds, averageFps = times.Count / seconds,
                percentile95Ms = times[Mathf.CeilToInt(times.Count * 0.95f) - 1] * 1000, maximumMs = times[times.Count - 1] * 1000, maximumAtSecond = worstAt,
                allies = colony.Allies, trapsPlaced = reaction.TrapsPlaced, resets = resets, damageTaken = startHp - session.Hp, reactionLevel = HumanReaction.LevelName(reaction.CurrentLevel),
                maximumGcBytes = maxGc, events = events, reservedMemoryBytes = memory.Valid ? memory.LastValue : -1
            };
            File.WriteAllText(Path.Combine(OutputFolder, "phase4-performance.json"), JsonUtility.ToJson(result, true));
            gc.Dispose(); memory.Dispose();
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name));
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException(description); checks.Add("PASS: " + description); Debug.Log("[PHASE4 PLAY] PASS: " + description); }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        private void Finish(bool passed)
        {
            if (Completed != null) Completed(passed);
            else Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
