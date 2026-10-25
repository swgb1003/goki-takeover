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
    // Explicit opt-in only (Editor verification or "-goki-phase5-test" on a Development build). Starts on the
    // title and walks the whole flow through the real screens' actions: title -> stage select -> sortie ->
    // briefing -> takeover gauges -> final defence -> clear -> unlock -> title -> continue from the nest record.
    // Settings and the save go to separate test files.
    public sealed class PhaseFiveScenarioTest : MonoBehaviour
    {
        public string OutputFolder { get; set; }
        public event Action<bool> Completed;
        private readonly List<string> checks = new List<string>();
        private bool performanceRun;
        private TitleMenu menu;
        private PhaseTwoSession session;
        private RoachMotor motor;
        private ThirdPersonRoachCamera cameraRig;
        private SwarmCommander commander;
        private ColonyManager colony;
        private HumanReaction reaction;
        private HumanAIController human;
        private NavMeshAgent agent;
        private StageProgress progress;
        private StageTutorial tutorial;
        [Serializable] private sealed class Performance
        {
            public string unity, cpu, gpu;
            public int width, height, frames, allies, foggers, successors, resets;
            public float seconds, averageFps, percentile95Ms, maximumMs, maximumAtSecond;
            public long maximumGcBytes, reservedMemoryBytes;
            // Phase 6 profiler counters (per frame): main thread CPU time, draw calls, batches, set-pass calls, triangles.
            public string quality = "中";
            public float mainThreadAverageMs, mainThread95Ms;
            public int drawCallsAverage, drawCallsMaximum, batchesAverage, setPassAverage, trianglesAverage;
            public long systemUsedMemoryBytes;
            public string phase, events = "";
            public string scenario = "30秒: 最終防衛中（燻煙剤・人間はパニック）、仲間8匹で開始（死ぬと戻らない）。0-10秒 三人称で移動、10-20秒 軍団モードで防衛指示、20-30秒 三人称で移動。";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Isolate()
        {
            string folder = null;
#if UNITY_EDITOR
            folder = UnityEditor.SessionState.GetString("GokiTakeover.Phase5.Isolate", "");
#endif
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-goki-output");
            if (Array.IndexOf(args, "-goki-phase5-test") >= 0 && index >= 0 && index + 1 < args.Length) folder = args[index + 1];
            if (string.IsNullOrEmpty(folder)) return;
            GameSettings.PathOverride = Path.Combine(folder, "settings-test.json");
            SaveData.PathOverride = Path.Combine(folder, "save-test.json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-phase5-test") < 0) return;
            var runner = new GameObject("Phase5_OptIn_Test").AddComponent<PhaseFiveScenarioTest>();
            int index = Array.IndexOf(args, "-goki-output");
            runner.OutputFolder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            runner.performanceRun = true;
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(OutputFolder);
            foreach (var f in new[] { SaveData.FilePath, SaveData.FilePath + ".bak", GameSettings.FilePath, GameSettings.FilePath + ".bak" }) if (File.Exists(f)) File.Delete(f);
            yield return null;
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
                    File.WriteAllText(Path.Combine(OutputFolder, "phase5-failure.txt"), string.Join("\n", checks) + "\nFAIL: " + e);
                    Debug.LogException(e); Finish(false); yield break;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "phase5-gameplay.txt"), checks);
            if (performanceRun) yield return Measure();
            Finish(true);
        }

        private IEnumerator Checks()
        {
            // ---- Title ----
            yield return WaitFor(() => FindFirstObjectByType<TitleMenu>(), 10);
            menu = FindFirstObjectByType<TitleMenu>();
            menu.Reload();   // the title read the save before this test cleared it
            Check(menu && !FindFirstObjectByType<PhaseTwoSession>() && !FindFirstObjectByType<HumanAIController>() && FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1,
                "タイトル: メニューだけでゲーム処理なし・カメラ1台");
            Check(FindFirstObjectByType<GameAudio>() && FindFirstObjectByType<AudioListener>(), "タイトル: メニューの効果音");
            Check(menu.Current == TitleMenu.Page.Title && !menu.CanContinue && menu.StageCount == 3, "初回: タイトル画面・「つづきから」は無効・ステージ3つ");
            File.WriteAllText(SaveData.FilePath, "{ broken");
            var broken = SaveData.Load();
            Check(broken.IsUnlocked("stage01") && !broken.IsUnlocked("stage02") && SaveData.LastLoadMessage != null && File.Exists(SaveData.FilePath + ".bak"),
                "壊れたセーブは .bak に退避して新しいセーブで始める（例外なし）");
            File.Delete(SaveData.FilePath);
            menu.Reload();
            if (performanceRun) yield return Capture("title-720p.png");
            menu.Open(TitleMenu.Page.StageSelect);
            menu.Select(1);
            Check(menu.Current == TitleMenu.Page.StageSelect && !menu.Sortie(false), "ステージ選択: 未解放の「一軒家」には出撃できない");
            menu.Select(0);
            if (performanceRun) yield return Capture("stageselect-720p.png");
            Check(menu.Sortie(false), "ステージ1へ出撃");
            yield return EnterStage();

            // ---- Briefing ----
            Check(progress.Current == StageProgress.Phase.Briefing && session.State == PhaseTwoSession.RunState.Paused, "出撃前にミッション説明（ゲームは停止）");
            Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && FindFirstObjectByType<PhaseFiveHud>() && !FindFirstObjectByType<PhaseFourHud>(), "ステージ: カメラ1台・Phase 5 HUD");
            if (performanceRun) yield return Capture("briefing-720p.png");
            progress.Begin();
            Freeze();
            yield return null; yield return null;
            Check(progress.Current == StageProgress.Phase.Invasion && session.IsRunning, "出撃: 侵略開始");
            var save = SaveData.Load();
            Check(save.HasCheckpoint("stage01") && save.checkpoint.allies == 0 && save.checkpoint.food == 0 && colony.Allies == 0, "ステージ開始時のチェックポイントを保存（主人公1匹・仲間0匹で開始）");
            Check(progress.FoodGauge == 0 && progress.TerritoryGauge == 0 && progress.FearGauge == 0 && progress.ZoneCount == 4, "3ゲージは0%から・縄張り4区域");
            Check(tutorial && tutorial.Active && tutorial.Current == StageTutorial.Step.Move, "初めての出撃でチュートリアル開始（1 / " + StageTutorial.StepCount + " 移動）");
            Check(commander.Command(AllyRoach.Order.Capture) == 0 && commander.Notice.Contains("仲間がいません"), "仲間がいない時の命令は「巣で増やそう」と案内");

            // ---- The nest under the sink (2026-10-24 revision) ----
            Check(session.HasNestArea && session.InNest(session.Nest.position + Vector3.up * 0.1f) && session.InSafeNest && session.Interactor.AtNest,
                "巣は流し台下の収納の中: 主人公は巣の中から出撃（安全地帯・納品できる）");
            var slots = new List<Vector3>();
            for (int i = 0; i < commander.Allies.Count; i++) slots.Add(commander.NestPoint(i, 0.9f));
            float spacing = float.MaxValue;
            for (int i = 0; i < slots.Count; i++) for (int j = i + 1; j < slots.Count; j++) spacing = Mathf.Min(spacing, Flat(slots[i] - slots[j]).magnitude);
            Check(slots.TrueForAll(p => session.InNest(p + Vector3.up * 0.1f) && commander.Grid.Walkable(p)) && spacing >= 0.45f,
                "巣の中に仲間" + slots.Count + "匹＋主人公の待機場所（すべて収納の中・間隔 " + spacing.ToString("F2") + "m 以上）");
            var exitPath = new List<Vector3>();
            Check(commander.Grid.FindPath(commander.NestGround, new Vector3(0.9f, 0, 2.0f), exitPath) && !session.InNest(new Vector3(0.9f, 0.1f, 2.0f)), "少し開いた扉から部屋へ出入りできる");
            var hinge = GameObject.Find("KitchenCabinet/OpenDoorHinge");
            Check(hinge && Quaternion.Angle(hinge.transform.localRotation, Quaternion.identity) > 30 && !GameObject.Find("NestBeacon") && !GameObject.Find("NestRing"),
                "流し台下の扉が開いている。光る目印の立方体・輪はなし");
            bool dressed = true;
            foreach (var f in commander.Foods)
            {
                var model = f.transform.Find("FoodModel");
                var value = f.GetComponent<FoodValue>();
                var mesh = model ? model.GetComponent<MeshFilter>().sharedMesh : null;
                dressed &= mesh && mesh.name == "Food_" + (value ? value.FoodKind : FoodValue.Kind.Breadcrumb) && !f.transform.Find("Crumb");
            }
            Check(dressed, "食料はすべてBlenderで作ったモデル（種類ごと: パンくず・米粒・クッキー・骨付きチキン・バナナの皮）");
            var viewer = FindFirstObjectByType<CodexViewer>();
            Check(viewer && viewer.Count == 10 && !viewer.Rendering && FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "図鑑の回転台は開くまでカメラを作らない");
            viewer.Show((int)FoodValue.Kind.Leftovers, false);
            float yaw = viewer.Yaw;
            viewer.Drag(new Vector2(-100, 0));
            Check(viewer.Rendering && Mathf.Abs(viewer.Yaw - yaw - 45) < 0.01f, "図鑑: ドラッグでモデルが回る");
            yield return new WaitForSecondsRealtime(0.4f);
            Check(!viewer.Rendering, "図鑑を閉じると回転台のカメラは止まる");

            // ---- Food gauge (tutorial: move, pick up, deliver) ----
            var foods = commander.Foods;
            Place(foods[0].transform.position + Vector3.right * 0.35f);
            session.Interactor.TryInteract();
            yield return null;
            Check(session.Interactor.Carried && tutorial.Current == StageTutorial.Step.Deliver, "チュートリアル: 移動→食料を拾うと「巣へ運ぼう」へ進む");
            if (performanceRun) yield return Capture("tutorial-720p.png");
            Place(session.Nest.position + Vector3.right * 0.3f);
            session.Interactor.TryInteract();
            yield return null;
            Check(colony.TotalCollected == 1 && Mathf.Approximately(progress.FoodGauge, 1f / progress.Stage.foodGoal), "納品で食料確保ゲージが上がる（1 / " + progress.Stage.foodGoal + "）");
            yield return null;
            var audio = FindFirstObjectByType<GameAudio>();
            Check(audio && audio.Played >= 2 && audio.LastPlayed == GameAudio.Sfx.Deliver, "効果音: 拾う・納品で鳴る（合成音、" + (audio ? audio.Played : 0) + "回）");
            Check(tutorial.Current == StageTutorial.Step.Breed, "チュートリアル: 納品すると「仲間を増やそう」へ進む");
            Check(SaveData.Load().checkpoint.totalCollected == 1, "納品すると自動で記録（巣を開かなくても「つづきから」に残る）");
            if (performanceRun)
            {
                var hudView = FindFirstObjectByType<PhaseFiveHud>();
                session.Pause(); hudView.OpenCodex(2); CodexModelPage.Foods.selected = 0;
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("codex-food-720p.png");
                CodexModelPage.Foods.selected = 3;   // not met yet: a silhouette
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture("codex-silhouette-720p.png");
                session.Resume();
                yield return null;
            }

            // ---- First ally (tutorial: breed, tactics view, capture order) ----
            colony.OpenNestMenu();
            colony.DebugGrant(2);
            if (performanceRun) yield return Capture("tutorial-nest-720p.png");
            Check(colony.TryBreed() && colony.Allies == 1, "巣で最初の仲間を産む（1匹）");
            colony.CloseNestMenu();
            yield return null;
            Check(tutorial.Current == StageTutorial.Step.OpenSwarm, "チュートリアル: 仲間が生まれると「軍団モードを開こう」へ進む");
            commander.SetMode(SwarmCommander.ViewMode.Swarm);
            yield return null;
            Check(tutorial.Current == StageTutorial.Step.Capture, "チュートリアル: 軍団モードを開くと「占拠を命じよう」（5 を押す）へ進む");
            if (performanceRun) yield return Capture("tutorial-swarm-720p.png");
            var first = commander.Allies[0];
            Check(commander.Command(AllyRoach.Order.Capture) == 1 && first.CurrentOrder == AllyRoach.Order.Capture && first.CaptureZone >= 0, "5 占拠: 選んだ仲間に占拠を指示");
            int z = first.CaptureZone;
            float nearest = float.MaxValue;
            for (int i = 0; i < progress.ZoneCount; i++) nearest = Mathf.Min(nearest, Flat(progress.Stage.zones[i].centre - first.Position).magnitude);
            Check(Mathf.Approximately(Flat(progress.Stage.zones[z].centre - first.Position).magnitude, nearest) && commander.Notice.Contains("あと1匹"),
                "1匹なら一番近い区域へ。「あと1匹必要 — あなたも区域に入ろう」と案内");
            yield return null;
            Check(tutorial.Current == StageTutorial.Step.Hold, "チュートリアル: 占拠を命じると「区域を占領しよう」へ進む");
            var zone = progress.Stage.zones[z];
            agent.Warp(new Vector3(4.5f, 0, -3.5f));
            commander.enabled = true;   // let the ally walk there
            yield return WaitFor(() => first.State == AllyRoach.AllyState.Idle && Flat(first.Position - zone.centre).magnitude <= zone.radius, 25);
            Check(Flat(first.Position - zone.centre).magnitude <= zone.radius && first.Label == "占拠中", "仲間は自分で区域へ歩き、中にとどまる（" + zone.name + "）");
            yield return new WaitForSeconds(1.1f);
            Check(progress.ZonePresence(z) == 1 && progress.ZoneProgress(z) == 0, "仲間1匹だけでは占領は進まない（2匹必要）");
            Place(zone.centre + new Vector3(0.5f, 0, -0.4f));
            yield return new WaitForSeconds(1.1f);
            Check(progress.ZonePresence(z) >= 2 && progress.ZoneProgress(z) > 0 && !progress.ZoneCaptured(z), "あなたが入って2匹になると占領が進む（" + Mathf.RoundToInt(progress.ZoneProgress(z) * 100) + "%）");
            float fearBefore = reaction.FearGained;
            progress.DebugCapture(z);
            yield return new WaitForSeconds(0.6f);
            Check(progress.ZoneCaptured(z) && progress.CapturedZones == 1 && reaction.FearGained >= fearBefore + progress.Stage.fearPerZone - 0.01f, "占領完了: 旗が立ち、人間の恐怖が増える"
                + (progress.ZoneCaptured(z) && reaction.FearGained >= fearBefore + progress.Stage.fearPerZone - 0.01f ? "" : "（占領 " + progress.CapturedZones + "・在区域 " + progress.ZonePresence(z)
                + "・進行 " + progress.ZoneProgress(z).ToString("F2") + "・恐怖 " + fearBefore.ToString("F1") + "→" + reaction.FearGained.ToString("F1") + "）"));
            var flag = GameObject.Find("Phase5_Stage/Territory/Zone_" + z + "/Flag");
            Check(flag && flag.activeSelf, "占領した区域に旗を表示");
            Check(first.CurrentOrder == AllyRoach.Order.Capture && first.CaptureZone >= 0 && first.CaptureZone != z && !progress.ZoneCaptured(first.CaptureZone), "占領した仲間は次の区域へ向かう");
            Check(tutorial.Current == StageTutorial.Step.Territory && tutorial.Active, "ガイド: 1区域を占領すると次の目標「残りの区域も占領」へ進む");
            Freeze();
            commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
            if (performanceRun) yield return Capture("guide-territory-720p.png");

            // ---- Capture with several allies: groups of two, nearest zones ----
            colony.OpenNestMenu();
            colony.DebugGrant(9);
            colony.TryBreed(); colony.TryBreed(); colony.TryBreed();
            colony.CloseNestMenu();
            Check(colony.Allies == 4, "仲間4匹");
            Check(commander.Command(AllyRoach.Order.Capture) == 4, "通常モードの 5 占拠は全員へ");
            var counts = new Dictionary<int, int>();
            foreach (var a in commander.Allies)
                if (a.Alive) counts[a.CaptureZone] = counts.TryGetValue(a.CaptureZone, out int n) ? n + 1 : 1;
            Check(counts.Count == 2 && !counts.ContainsKey(z) && !counts.ContainsKey(-1) && Array.TrueForAll(new List<int>(counts.Values).ToArray(), v => v == 2),
                "4匹は2匹ずつ、まだ占領していない2区域に分かれる");
            Check(progress.ZoneAt(zone.centre) == z && progress.ZoneAt(new Vector3(0, 0, -5.5f)) == -1, "右クリックの区域判定（区域の中だけ）");

            int other = (z + 1) % progress.ZoneCount;
            var zoneB = progress.Stage.zones[other];
            agent.Warp(zoneB.centre);
            commander.Allies[1].transform.position = zoneB.centre + new Vector3(0.3f, 0, 0);
            commander.Allies[2].transform.position = zoneB.centre + new Vector3(-0.3f, 0, 0);
            yield return new WaitForSeconds(0.8f);
            Check(progress.ZoneContested(other) && progress.ZoneProgress(other) == 0, "人間がいる区域は占領が進まない");
            agent.Warp(new Vector3(4.5f, 0, -3.5f));
            Check(!progress.AllGaugesFull && progress.Current == StageProgress.Phase.Invasion, "ゲージがそろうまで最終防衛は始まらない");

            // ---- The slipper reaches the worktop and low walls ----
            yield return SlipperReach();

            // ---- Fear gauge ----
            float gauge = progress.FearGauge;
            reaction.AddFear(20);
            Check(progress.FearGauge > gauge, "恐怖度が加わると人間の恐怖ゲージが上がる（減衰しても戻らない累積）");

            // ---- Nest record ----
            colony.OpenNestMenu();
            colony.DebugGrant(6);
            colony.TryBreed();
            colony.CloseNestMenu();
            save = SaveData.Load();
            Check(save.checkpoint.allies == 5 && save.checkpoint.zones[z] >= progress.Stage.holdSeconds && save.checkpoint.totalCollected == 1, "巣を閉じると進行を記録（仲間5匹・占領1区域・食料）");

            // ---- Final defence ----
            colony.Restore(colony.Food, progress.Stage.foodGoal, colony.NestLevel, colony.UpgradeLevels, colony.Allies, colony.UpgradesBought, colony.Born);
            for (int i = 0; i < progress.ZoneCount; i++) progress.DebugCapture(i);
            yield return null;
            Check(tutorial.Active && tutorial.Current == StageTutorial.Step.Fear && progress.TerritoryGauge >= 1 && progress.FoodGauge >= 1,
                "ガイド: 縄張りと食料がそろうと「人間を怖がらせる」へ（そろった目標は飛ばす）");
            progress.DebugAddFear(progress.Stage.fearGoal);
            yield return WaitFor(() => progress.Current == StageProgress.Phase.FinalDefence, 3);
            Check(progress.Current == StageProgress.Phase.FinalDefence && progress.AllGaugesFull && progress.DefenceRemaining > 50, "3ゲージ100%で最終防衛（駆除業者）が始まる");
            yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Panic, "最終防衛中の人間はパニック");
            var look = FindFirstObjectByType<ExterminatorLook>();
            Check(look && look.Showing && human.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.name.StartsWith("Exterminator"), "最終防衛: 白い防護服とマスクの駆除業者が現れる");
            session.AddMaxHp(6);
            var open = new Vector3(1.6f, 0, -2.2f);
            Place(open);
            yield return WaitFor(() => Array.Exists(progress.Foggers, f => f.Active), 5);
            var fogger = Array.Find(progress.Foggers, f => f.Active);
            Check(fogger && fogger.Current == SmokeFogger.Phase.Warning && Flat(fogger.transform.position - open).magnitude < 0.8f, "最初の燻煙剤は主人公の足元に、まず橙の輪で予告");
            Check(Flat(fogger.transform.position - commander.NestGround).magnitude >= progress.Stage.foggerRadius + 1.3f, "通常の燻煙剤は巣を覆わない（巣を燻すのは業者の別の行動）");
            Check(commander.Threatened(fogger.transform.position + new Vector3(0.5f, 0, 0.3f), out Vector3 escape) && Flat(escape - fogger.transform.position).magnitude > fogger.Radius && commander.Grid.Walkable(escape),
                "仲間は命令より先に輪の外の床へ逃げる");
            if (performanceRun) yield return Capture("fogger-720p.png");
            int hp = session.Hp;
            var victim = commander.Allies[3];
            int before = colony.Allies;
            yield return WaitFor(() => fogger.Current == SmokeFogger.Phase.Smoking, 6);
            Check(session.Hp == hp, "予告中はダメージなし");
            victim.transform.position = fogger.transform.position + new Vector3(0.4f, 0, 0);
            Place(fogger.transform.position + new Vector3(-0.3f, 0, 0.2f));
            yield return new WaitForSeconds(0.5f);
            Check(session.Hp < hp && progress.SmokeHits >= 1, "煙の中では生命力が減る");
            Check(victim.State == AllyRoach.AllyState.Disabled && progress.SmokeLosses >= 1, "煙の中の仲間もやられる");
            Check(victim.IsCorpse && colony.Allies == before - 1 && commander.AllyDeaths >= 1, "やられた仲間は復活せず死体が残り、仲間の数から減る");
            Check(tutorial.Active && tutorial.Current == StageTutorial.Step.Defence && !SaveData.Load().guideDone, "ガイド: 最終防衛中は燻煙剤の避け方を表示");
            Check(!fogger.Contains(fogger.transform.position + Vector3.up * 2.9f) && !fogger.Contains(fogger.transform.position + new Vector3(fogger.Radius + 0.3f, 0.3f, 0)),
                "煙は輪の外と天板の高さ（2.9m）には届かない");
            if (performanceRun) yield return Capture("smoke-720p.png");

            // ---- The exterminator smokes the nest: hiding there no longer works ----
            Place(session.Nest.position + new Vector3(-0.6f, 0, 0));
            AllyRoach hider = null;
            foreach (var a in commander.Allies) if (a.Alive && a.Active && !a.Stunned) { hider = a; break; }
            if (hider) { hider.transform.position = commander.NestPoint(hider.Slot, 0.9f); hider.SetOrder(AllyRoach.Order.Defend, Vector3.zero); }
            bool ticking = commander.enabled;
            commander.enabled = true;   // the allies act on their own here: they have to leave the nest themselves
            if (progress.NestFumigation == StageProgress.Fumigation.None) progress.DebugFumigateNest();
            Check(progress.NestFumigation != StageProgress.Fumigation.None && progress.Stage.nestFumigations.Length >= 2, "最終防衛中、駆除業者は巣（流し台下）も燻しに来る（" + progress.Stage.nestFumigations.Length + "回）");
            yield return WaitFor(() => progress.NestFumigation == StageProgress.Fumigation.Warning, 6);
            Check(progress.NestFumigation == StageProgress.Fumigation.Warning && progress.NestFogger.Active && commander.NestSmoked && session.InSafeNest,
                "扉の前に燻煙剤を置き、" + progress.Stage.nestFumigationWarning.ToString("F0") + "秒の予告（予告中はまだ安全）");
            Check(commander.Threatened(session.Nest.position, out Vector3 way) && !session.InNest(way + Vector3.up * 0.1f), "予告が出ると巣の中の仲間は自分で外へ逃げる");
            Check(!session.InNest(commander.NestPoint(0, 0.9f) + Vector3.up * 0.1f), "燻されている間、仲間の帰り先は巣の外の避難場所（テーブルの下）");
            if (performanceRun) yield return Capture("nest-fumigation-720p.png");
            yield return WaitFor(() => progress.NestFumigation == StageProgress.Fumigation.Smoke, progress.Stage.nestFumigationWarning + 1);
            int beforeSmoke = session.Hp;
            yield return new WaitForSeconds(progress.Stage.nestSmokeDamageEvery + 1.8f);
            Check(session.NestSmoked && !session.InSafeNest && !session.Interactor.AtNest && session.Hp < beforeSmoke, "煙が満ちた巣の中ではダメージを受け、巣は使えない（安全地帯ではない）");
            Place(open + new Vector3(0, 0, 5f));
            progress.DebugEndFumigation();
            Check(!session.NestSmoked && !commander.NestSmoked && progress.NestFumigation == StageProgress.Fumigation.None, "煙が晴れると巣はまた安全地帯に戻る");
            Check(hider == null || !hider.IsDead, "予告の間に仲間は自分で巣から逃げ出し、煙にやられない");
            commander.enabled = ticking;

            // ---- The ceiling is no hiding place either: the backpack sprayer ----
            var sprayer = progress.Sprayer;
            var lanceArm = human.GetComponentInChildren<BlenderHumanAnimator>();
            NavMesh.SamplePosition(new Vector3(2.8f, 0, -2.8f), out NavMeshHit under, 1.5f, NavMesh.AllAreas);
            agent.Warp(under.position);
            PlaceExact(new Vector3(2.0f, 5.0f, -1.2f));
            motor.BeginClimb(Vector3.down);
            Check(sprayer && motor.IsClimbing && !session.PlayerStrikeable && sprayer.PlayerHigh, "天井に張り付く（スリッパは届かない）");
            yield return WaitFor(() => sprayer.Current == CeilingSprayer.Phase.Aim, CeilingSprayer.NoticeSeconds + 7);
            Check(sprayer.Current == CeilingSprayer.Phase.Aim && sprayer.Inside(motor.FootPoint) && lanceArm.SprayAim > 0.9f,
                "天井に" + CeilingSprayer.NoticeSeconds.ToString("F0") + "秒いると、業者が背中のタンクの噴霧器を上に向け、天井に橙の円で予告");
            if (performanceRun)
            {
                // The exterminator seen from open floor, lance raised at the ceiling; then the warning on the ceiling.
                yield return new WaitForSeconds(0.45f);
                var eye = cameraRig.transform; bool follow = cameraRig.enabled;
                cameraRig.enabled = false;
                eye.position = new Vector3(0.2f, 1.6f, -5.2f);
                eye.rotation = Quaternion.LookRotation(human.transform.position + Vector3.up * 2.8f - eye.position);
                yield return Capture("exterminator-aim-720p.png");
                cameraRig.enabled = follow; cameraRig.ResetView();
                yield return Capture("ceiling-spray-aim-720p.png");
            }
            int beforeSpray = session.Hp, sprayHits = sprayer.Hits;
            yield return WaitFor(() => sprayer.Current == CeilingSprayer.Phase.Spray, CeilingSprayer.AimSeconds + 1);
            yield return new WaitForSeconds(0.35f);
            if (performanceRun) yield return Capture("ceiling-spray-720p.png");
            yield return WaitFor(() => sprayer.Current != CeilingSprayer.Phase.Spray, CeilingSprayer.SpraySeconds + 1);
            Check(sprayer.Hits > sprayHits && session.Hp < beforeSpray && lanceArm.AimWeight > 0.5f, "円の中で噴霧を浴びるとダメージ（腕を上げてノズルを向ける）");
            yield return WaitFor(() => sprayer.Current == CeilingSprayer.Phase.Aim, CeilingSprayer.CooldownSeconds + CeilingSprayer.NoticeSeconds + 7);
            motor.transform.position += new Vector3(CeilingSprayer.Radius + 1.2f, 0, 0);   // crawl out of the ring
            Physics.SyncTransforms();
            sprayHits = sprayer.Hits;
            yield return WaitFor(() => sprayer.Current == CeilingSprayer.Phase.Recover || sprayer.Current == CeilingSprayer.Phase.Idle, CeilingSprayer.AimSeconds + CeilingSprayer.SpraySeconds + 1);
            Check(sprayer.Shots >= 2 && sprayer.Hits == sprayHits, "予告の円の外へ這って逃げればかわせる");
            motor.ResetSurface();
            Place(open + new Vector3(0, 0, 5f));
            yield return new WaitForSeconds(CeilingSprayer.RecoverSeconds + 0.2f);
            Check(sprayer.Current == CeilingSprayer.Phase.Idle && !human.Suspended && lanceArm.SprayAim == 0, "噴霧の後、業者はまた動き出す");

            // ---- Eating a fallen ally ----
            Place(open + new Vector3(0, 0, 5f));   // well outside the smoke
            yield return new WaitForSeconds(1.8f);
            var fallen = commander.Allies[4];
            fallen.transform.position = Flat(motor.FootPoint) + new Vector3(0.45f, 0, 0);
            fallen.Crush(8);
            int wounded = session.Hp;
            Check(wounded < session.MaximumHp && progress.NearbyCorpse == fallen, "そばの死体を E で食べられる（生命力 " + wounded + " / " + session.MaximumHp + "）");
            Check(progress.TryEatCorpse() && session.Hp == wounded + progress.Stage.corpseHeal && !fallen.gameObject.activeSelf && progress.CorpsesEaten == 1,
                "死体を食べると生命力 +" + progress.Stage.corpseHeal + "、死体は消える");

            // ---- Defeat: a successor takes over ----
            int allies = colony.Allies;
            session.TryDamage(999);
            Check(session.State == PhaseTwoSession.RunState.Dead && progress.CanSucceed, "やられた: 仲間がいれば後継に交代できる"
                + (session.State == PhaseTwoSession.RunState.Dead && progress.CanSucceed ? "" : "（状態 " + session.State + "・仲間 " + colony.Allies + "）"));
            if (performanceRun) yield return Capture("defeat-720p.png");
            Check(progress.Succeed() && session.IsRunning && colony.Allies == allies - 1 && session.Hp == session.MaximumHp && progress.Successors == 1
                && progress.Current == StageProgress.Phase.FinalDefence, "後継: 仲間1匹と引き換えに巣から再開、最終防衛は続く");

            // ---- Survive -> the resident moves out -> clear ----
            progress.DebugFinishDefence();
            yield return WaitFor(() => progress.Current == StageProgress.Phase.Retreat, 2);
            Check(progress.Current == StageProgress.Phase.Retreat && !Array.Exists(progress.Foggers, f => f.Active), "耐えきると燻煙剤が止み、人間が逃げ出す（反応段階5 撤退）");
            yield return null;
            Check(tutorial.Active && tutorial.Current == StageTutorial.Step.Done, "ガイド: 耐えきると「乗っ取り成功！」");
            yield return WaitFor(() => session.State == PhaseTwoSession.RunState.Cleared, 12);
            Check(session.State == PhaseTwoSession.RunState.Cleared && progress.Current == StageProgress.Phase.Cleared && progress.ResidentHidden,
                "人間が部屋から出ていき、ステージクリア");
            save = SaveData.Load();
            var record = save.Record("stage01");
            Check(progress.Rating.Length == 1 && "ABC".Contains(progress.Rating) && record != null && record.clears == 1 && record.bestRating == progress.Rating,
                "評価 " + progress.Rating + "（" + progress.RatingPoints + "点）を記録");
            Check(save.IsUnlocked("stage02") && save.HasReward("skin_gold") && progress.NewStageUnlocked && progress.NewRewardUnlocked && !save.HasCheckpoint("stage01"),
                "クリアで次のステージと見た目を解放、チェックポイントは消去");
            Check(!tutorial.Active && save.guideDone && save.tutorialDone, "ガイド: クリアで終了し、次からは出ない（記録）");
            if (performanceRun) yield return Capture("clear-720p.png");

            // ---- Replay and a fresh checkpoint ----
            progress.RestartStage();
            Freeze();
            yield return null; yield return null;
            save = SaveData.Load();
            Check(progress.Current == StageProgress.Phase.Invasion && progress.CapturedZones == 0 && progress.FoodGauge == 0 && progress.FearGauge == 0
                && !flag.activeSelf && !progress.ResidentHidden && save.HasCheckpoint("stage01") && save.checkpoint.allies == 0 && colony.Allies == 0 && !tutorial.Active,
                "リプレイ: ゲージ・旗・人間・仲間を初期化し、開始時チェックポイントを保存（チュートリアルは出ない）");
            Check(!FindFirstObjectByType<ExterminatorLook>().Showing, "リプレイ: 人間は住人の姿に戻る");
            tutorial.Show();
            yield return null;
            Check(tutorial.Active && tutorial.Current == StageTutorial.Step.Move && !SaveData.Load().guideDone, "ポーズの「ガイドを表示する」で最初の手順から再表示");
            tutorial.Skip();
            Check(!tutorial.Active && SaveData.Load().guideDone, "「ガイドを閉じる」で非表示（記録）");
            progress.DebugCapture(2);
            yield return new WaitForSeconds(0.6f);
            colony.OpenNestMenu();
            colony.DebugGrant(9);
            colony.TryBreed(); colony.TryBreed();
            session.Discoveries = 2;
            colony.CloseNestMenu();
            int food = colony.Food;
            float gained = reaction.FearGained;

            // ---- Back to the title, continue from the nest record ----
            GameFlow.ToTitle();
            yield return WaitFor(() => FindFirstObjectByType<TitleMenu>() && !FindFirstObjectByType<StageProgress>(), 10);
            menu = FindFirstObjectByType<TitleMenu>();
            Check(menu.Current == TitleMenu.Page.Title && menu.CanContinue, "タイトルに戻ると「つづきから」が有効");
            menu.Open(TitleMenu.Page.StageSelect);
            menu.Select(0);
            Check(!menu.Sortie(false) && menu.FreshArmed && SaveData.Load().HasCheckpoint("stage01"), "記録がある時の「最初から出撃」はもう一度押して確認（うっかり記録を消さない）");
            if (performanceRun) yield return Capture("stageselect-continue-720p.png");
            menu.Back();
            Check(menu.Continue(), "つづきから");
            yield return EnterStage();
            Check(progress.Current == StageProgress.Phase.Briefing && GameFlow.ResumeRequested, "再開もミッション説明から");
            progress.Begin();
            Freeze();
            yield return null;
            Check(colony.Allies == 2 && colony.Food == food && progress.ZoneCaptured(2) && progress.CapturedZones == 1 && session.Discoveries == 2
                && Mathf.Abs(reaction.FearGained - gained) < 0.5f && GameObject.Find("Phase5_Stage/Territory/Zone_2/Flag"),
                "巣の記録から復元: 仲間2匹・食料・占領区域・発見回数・恐怖");

            // ---- The resident hunts allies too ----
            yield return AllyHunt();

            // ---- No ally left: back to the record ----
            yield return new WaitForSeconds(1.8f);
            while (commander.RemoveAlly()) { }

            // ---- The patrol covers the whole room (no allies left to chase; the player hides in the nest) ----
            Place(session.Nest.position);
            reaction.enabled = false;
            human.ResetHuman();
            human.enabled = true;
            var stops = new HashSet<int>();
            float left = float.MaxValue, right = float.MinValue;
            for (float t = 0; t < 32 && (stops.Count < 5 || left > -3 || right < 3); t += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                if (human.State == HumanAIController.HumanState.Routine) stops.Add(human.Waypoint);
                left = Mathf.Min(left, human.transform.position.x); right = Mathf.Max(right, human.transform.position.x);
            }
            Check(human.PatrolRandom && stops.Count >= 5 && left < -3 && right > 3, "人間は部屋全体を巡回する（巡回地点 " + stops.Count + "か所・x " + left.ToString("F1") + "〜" + right.ToString("F1") + "）");
            human.enabled = false;
            agent.isStopped = true;
            reaction.enabled = true;
            yield return null; yield return null;
            Check(SaveData.Load().checkpoint.allies == 0, "自動記録は仲間を失ったことも残す（巣へ戻らなくても最新の状態）");
            Place(new Vector3(1.6f, 0, 2.8f));   // away from the nest, where no damage is taken
            session.TryDamage(999);
            Check(session.State == PhaseTwoSession.RunState.Dead && !progress.CanSucceed && progress.HasCheckpoint, "仲間がいないと後継はなく、記録から再開を選べる");
            var last = SaveData.Load().checkpoint;
            Check(progress.RestoreCheckpoint() && session.IsRunning && colony.Allies == last.allies && colony.TotalCollected == last.totalCollected, "記録から再開（最新の自動記録）");

            // ---- Stage select after the clear ----
            GameFlow.ToTitle(GameFlow.TitlePage.StageSelect);
            yield return WaitFor(() => FindFirstObjectByType<TitleMenu>() && !FindFirstObjectByType<StageProgress>(), 10);
            menu = FindFirstObjectByType<TitleMenu>();
            menu.Select(1);
            Check(menu.Current == TitleMenu.Page.StageSelect && menu.Save.IsUnlocked("stage02") && menu.IsOpen(menu.SelectedStage),
                "クリア後: 「一軒家」が解放され出撃できる");
            menu.Select(0);
            menu.ChooseSkin("skin_gold");
            Check(SaveData.Load().skin == "skin_gold", "解放した見た目を選んで保存");
            if (performanceRun) yield return Capture("stageselect-unlocked-720p.png");
            menu.Open(TitleMenu.Page.Settings);
            menu.Options.mouseSensitivity = 3.25f;
            menu.Options.Save();
            Check(GameSettings.Load().mouseSensitivity == 3.25f, "タイトルの設定も保存");
            Check(new GameSettings().fullscreen && JsonUtility.FromJson<GameSettings>("{\"version\":1}").fullscreen && GameSettings.Load().fullscreen,
                "画面は全画面が既定（以前の設定ファイルでも全画面）");
            menu.Options.mouseSensitivity = 2f; menu.Options.Save();
            menu.Back();
        }

        private IEnumerator EnterStage()
        {
            yield return WaitFor(() => { var p = FindFirstObjectByType<StageProgress>(); return p && p.Current == StageProgress.Phase.Briefing && !FindFirstObjectByType<TitleMenu>(); }, 20);
            yield return null;
            progress = FindFirstObjectByType<StageProgress>();
            session = FindFirstObjectByType<PhaseTwoSession>();
            motor = session.Motor;
            human = session.Human;
            agent = human.GetComponent<NavMeshAgent>();
            cameraRig = session.FollowCamera;
            commander = FindFirstObjectByType<SwarmCommander>();
            colony = FindFirstObjectByType<ColonyManager>();
            reaction = colony.Reaction;
            tutorial = FindFirstObjectByType<StageTutorial>();
            Check(progress && session, "ステージを読み込み");
        }

        // The scripted checks place roaches by hand: hold the player, the allies and the resident still.
        private void Freeze()
        {
            motor.enabled = false; human.enabled = false; cameraRig.enabled = false; commander.enabled = false;
            agent.isStopped = true;
        }

        private void Place(Vector3 position)
        {
            motor.ResetSurface();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = 0.02f;
            motor.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSafePosition();
            cameraRig.ResetView();
        }

        private IEnumerator Measure()
        {
            GameFlow.ToTitle(GameFlow.TitlePage.StageSelect);
            yield return WaitFor(() => FindFirstObjectByType<TitleMenu>() && !FindFirstObjectByType<StageProgress>(), 10);
            menu = FindFirstObjectByType<TitleMenu>();
            menu.Select(0);
            menu.Sortie(false, true);
            yield return EnterStage();
            progress.Begin();
            cameraRig.enabled = false;
            yield return null;
            colony.OpenNestMenu();
            colony.DebugGrant(60);
            for (int i = 0; i < 6; i++) colony.TryBreed();
            colony.TryExpand(); colony.TryBreed(); colony.TryBreed();
            yield return Capture("nest-720p.png");
            colony.CloseNestMenu();
            // The nest under the sink with the whole colony home, then the way in seen from the room.
            yield return new WaitForSeconds(1.5f);
            Place(session.Nest.position + new Vector3(-1.2f, 0, 0.1f));
            motor.transform.rotation = Quaternion.Euler(0, 90, 0);
            cameraRig.ResetView();
            yield return Capture("nest-inside-720p.png");
            Place(new Vector3(1.6f, 0, 1.4f));
            motor.transform.rotation = Quaternion.Euler(0, -20, 0);
            cameraRig.ResetView();
            yield return Capture("nest-door-720p.png");
            // Every food page of the encyclopedia, all met.
            foreach (var id in new[] { "food_Rice", "food_Sweets", "food_Leftovers", "food_Garbage" }) colony.Learn(id);
            session.Pause();
            FindFirstObjectByType<PhaseFiveHud>().OpenCodex(2);
            for (int k = 0; k < CodexModelPage.Foods.pages.Length; k++)
            {
                CodexModelPage.Foods.selected = k;
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Capture("codex-" + (FoodValue.Kind)CodexModelPage.Foods.pages[k].model + "-720p.png");
            }
            FindFirstObjectByType<PhaseFiveHud>().OpenCodex(1);   // the people
            colony.Learn("fogger");
            for (int k = 0; k < CodexModelPage.Humans.pages.Length; k++)
            {
                CodexModelPage.Humans.selected = k;
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Capture("codex-human" + k + "-720p.png");
            }
            session.Resume();
            colony.Restore(colony.Food, progress.Stage.foodGoal, colony.NestLevel, colony.UpgradeLevels, colony.Allies, colony.UpgradesBought, colony.Born);
            for (int i = 0; i < 3; i++) progress.DebugCapture(i);
            progress.DebugAddFear(progress.Stage.fearGoal);
            yield return new WaitForSeconds(0.6f);
            // TPS screenshot with three of four zones held, before the last one triggers the final defence.
            Place(progress.Stage.zones[0].centre + new Vector3(1.6f, 0, -1.8f));
            cameraRig.ResetView();
            yield return Capture("tps-720p.png");
            progress.DebugCapture(3);
            yield return WaitFor(() => progress.Current == StageProgress.Phase.FinalDefence, 3);
            session.AddMaxHp(20);   // the run measures frame time; being caught must not end the run
            Place(new Vector3(1.0f, 0.02f, -2.6f));
            yield return new WaitForSecondsRealtime(1);
            var times = new List<float>(3000);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            var counters = new Counters();
            float seconds = 0, worst = 0, worstAt = 0; long maxGc = 0; int skip = 0, resets = 0, armed = progress.FoggerWaves;
            string events = "";
            bool swarmShot = false;
            while (seconds < 30)
            {
                if (session.State == PhaseTwoSession.RunState.Paused) session.Resume();
                if (session.State == PhaseTwoSession.RunState.Dead) { events += "successor @" + seconds.ToString("F1") + "; "; if (!progress.Succeed()) { progress.RestartStage(); resets++; } }
                float dt = Time.unscaledDeltaTime;
                motor.MovementEnabled = true;
                if (seconds < 10 || seconds >= 20)
                {
                    if (commander.Mode != SwarmCommander.ViewMode.ThirdPerson) commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
                    cameraRig.enabled = false;
                    motor.SimulateMove(seconds < 10 ? new Vector2(0.3f, 0.7f) : new Vector2(-0.4f, 1), false, dt);
                    cameraRig.RotateView(new Vector2(dt * 1.5f, 0));
                    cameraRig.Follow(dt);
                }
                else
                {
                    if (commander.Mode != SwarmCommander.ViewMode.Swarm)
                    {
                        motor.Detach();
                        commander.SetMode(SwarmCommander.ViewMode.Swarm);
                        commander.SelectAll();
                        commander.Command(AllyRoach.Order.Defend);
                    }
                    motor.SimulateMove(Vector2.zero, false, dt);
                    if (!swarmShot && seconds > 15) { swarmShot = true; skip = 2; yield return Capture("swarm-720p.png"); }
                }
                if (skip > 0) skip--;
                else
                {
                    if (times.Count > 0 && dt > worst) { worst = dt; worstAt = seconds; }
                    if (times.Count > 0 && dt > 0.1f)
                        events += "slow " + (dt * 1000).ToString("F0") + "ms @" + seconds.ToString("F1") + " (human " + human.State + " up " + human.StrikeUp.ToString("F1") + " climb " + motor.IsClimbing
                            + " allies " + colony.Allies + " deaths " + commander.AllyDeaths + " hp " + session.Hp + " mode " + commander.Mode + "); ";
                    times.Add(dt);
                    if (gc.Valid) maxGc = Math.Max(maxGc, gc.LastValue);
                    counters.Sample();
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
                allies = colony.Allies, foggers = progress.FoggerWaves - armed, successors = progress.Successors, resets = resets, phase = progress.Current.ToString(),
                maximumGcBytes = maxGc, events = events, reservedMemoryBytes = memory.Valid ? memory.LastValue : -1
            };
            counters.Fill(result);
            File.WriteAllText(Path.Combine(OutputFolder, "phase5-performance.json"), JsonUtility.ToJson(result, true));
            gc.Dispose(); memory.Dispose(); counters.Dispose();
            yield return LowQuality();
        }

        // The same final defence for 20 more seconds in third person on the 低 setting (no shadows, no MSAA).
        private IEnumerator LowQuality()
        {
            colony.Options.quality = 0;
            colony.ApplyOptions();
            if (commander.Mode != SwarmCommander.ViewMode.ThirdPerson) commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
            if (progress.Current != StageProgress.Phase.FinalDefence) { for (int i = 0; i < progress.ZoneCount; i++) progress.DebugCapture(i); }
            yield return new WaitForSecondsRealtime(1);
            var times = new List<float>(2000);
            var counters = new Counters();
            float seconds = 0;
            while (seconds < 20)
            {
                if (session.State == PhaseTwoSession.RunState.Paused) session.Resume();
                if (session.State == PhaseTwoSession.RunState.Dead && !progress.Succeed()) progress.RestartStage();
                float dt = Time.unscaledDeltaTime;
                motor.MovementEnabled = true;
                cameraRig.enabled = false;
                motor.SimulateMove(new Vector2(0.35f * Mathf.Sin(seconds), 0.8f), seconds > 10, dt);
                cameraRig.RotateView(new Vector2(dt * 1.2f, 0));
                cameraRig.Follow(dt);
                if (times.Count > 0 || seconds > 0) times.Add(dt);
                counters.Sample();
                seconds += dt;
                yield return null;
            }
            yield return Capture("lowquality-720p.png");
            times.Sort();
            var result = new Performance
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, quality = "低",
                width = Screen.width, height = Screen.height, frames = times.Count, seconds = seconds, averageFps = times.Count / seconds,
                percentile95Ms = times[Mathf.CeilToInt(times.Count * 0.95f) - 1] * 1000, maximumMs = times[times.Count - 1] * 1000,
                allies = colony.Allies, phase = progress.Current.ToString(),
                scenario = "20秒: 画質「低」、最終防衛中に三人称で移動（後半は走る）。"
            };
            counters.Fill(result);
            File.WriteAllText(Path.Combine(OutputFolder, "phase5-performance-low.json"), JsonUtility.ToJson(result, true));
            counters.Dispose();
            colony.Options.quality = 1;
            colony.ApplyOptions();
        }

        private sealed class Counters : IDisposable
        {
            private ProfilerRecorder main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            private ProfilerRecorder draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            private ProfilerRecorder batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            private ProfilerRecorder setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            private ProfilerRecorder triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            private ProfilerRecorder system = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            private readonly List<float> mainMs = new List<float>(3000);
            private long drawSum, batchSum, passSum, triSum; private int drawMax, samples;
            public void Sample()
            {
                if (main.Valid && main.LastValue > 0) mainMs.Add(main.LastValue / 1e6f);
                if (!draws.Valid) return;
                samples++;
                drawSum += draws.LastValue; drawMax = Math.Max(drawMax, (int)draws.LastValue);
                batchSum += batches.Valid ? batches.LastValue : 0; passSum += setPass.Valid ? setPass.LastValue : 0; triSum += triangles.Valid ? triangles.LastValue : 0;
            }
            public void Fill(Performance r)
            {
                if (mainMs.Count > 0)
                {
                    float sum = 0; foreach (float v in mainMs) sum += v;
                    r.mainThreadAverageMs = sum / mainMs.Count;
                    mainMs.Sort();
                    r.mainThread95Ms = mainMs[Mathf.CeilToInt(mainMs.Count * 0.95f) - 1];
                }
                if (samples > 0)
                {
                    r.drawCallsAverage = (int)(drawSum / samples); r.drawCallsMaximum = drawMax;
                    r.batchesAverage = (int)(batchSum / samples); r.setPassAverage = (int)(passSum / samples); r.trianglesAverage = (int)(triSum / samples);
                }
                r.systemUsedMemoryBytes = system.Valid ? system.LastValue : -1;
            }
            public void Dispose() { main.Dispose(); draws.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose(); system.Dispose(); }
        }

        // Table top and a low cabinet side are hit; a high wall is out of reach; an ally on the floor beside the
        // table is not hit by a slap on the table top.
        private IEnumerator SlipperReach()
        {
            reaction.enabled = false;
            human.Suspended = false;
            var settings = session.Settings;
            // Table top, near its right edge (top face 2.8).
            PlaceExact(new Vector3(-0.6f, 2.82f, -0.9f));
            Check(session.PlayerStrikeable && !motor.IsClimbing, "台（テーブルの天板）の上も届く高さ");
            var below = commander.Allies[0];
            below.transform.position = new Vector3(-0.15f, 0, -0.9f);
            yield return Strike(new Vector3(0.9f, 0, -0.9f), "slipper-table-720p.png");
            Check(human.StrikeUp.y > 0.99f && human.AttackPoint.y > 2.7f, "天板の上を狙って叩く（狙い " + human.AttackPoint.y.ToString("F2") + "m）");
            Check(lastStrikeHit, "天板の上の主人公にスリッパが当たる");
            Check(below.Alive && below.State != AllyRoach.AllyState.Disabled, "天板を叩いても、そばの床の仲間には当たらない");

            // Low on the cabinet side (front face z 3.775, climbable).
            PlaceExact(new Vector3(1.0f, 1.3f, 3.5f));
            Check(motor.BeginClimb(Vector3.back), "食器棚の側面に張りつく");
            motor.transform.position += new Vector3(0, 0, 3.775f - motor.FootPoint.z);
            Physics.SyncTransforms();
            Check(motor.IsClimbing && session.PlayerStrikeable && motor.FootPoint.y < progress.Stage.slipperReach, "壁の低い所（" + motor.FootPoint.y.ToString("F1") + "m）も届く");
            yield return Strike(new Vector3(1.0f, 0, 2.35f), "slipper-wall-720p.png");
            Check(human.StrikeUp.z < -0.9f, "壁の面に向けて叩く");
            Check(lastStrikeHit, "壁の主人公にスリッパが当たる");

            // High on the back wall beside the cabinet, and the ceiling: out of reach.
            PlaceExact(new Vector3(5.5f, progress.Stage.slipperReach + 0.6f, 5.7f));
            motor.BeginClimb(Vector3.back);
            Check(motor.IsClimbing && !session.PlayerStrikeable, "高い壁（" + motor.FootPoint.y.ToString("F1") + "m）は届かない");
            motor.ResetSurface();
            PlaceExact(new Vector3(5.5f, 5.0f, 2f));
            motor.BeginClimb(Vector3.down);
            Check(!session.PlayerStrikeable, "天井は届かない");
            motor.ResetSurface();

            human.enabled = false;
            agent.isStopped = true;
            agent.Warp(new Vector3(4.5f, 0, -3.5f));
            reaction.enabled = true;
            below.transform.position = commander.FormationPoint(commander.NestGround, 0, 0, 0.9f);   // back home, clear of later smoke
            Place(new Vector3(1.6f, 0, -2.2f));
            yield return new WaitForSeconds(1.8f);   // out of the hit's invulnerability
        }

        // An ally alone on the open floor while the player waits at the nest: the resident spots it, swings at it
        // (the ally reacts a moment late) and crushes it. Allies are frozen, so it does not run.
        private IEnumerator AllyHunt()
        {
            reaction.enabled = false;
            human.Suspended = false;
            Place(commander.NestGround);
            AllyRoach prey = null, friend = null;
            foreach (var a in commander.Allies) if (a.Alive && a.Active) { if (!prey) prey = a; else if (!friend) friend = a; }
            Check(prey != null && human.ExtraPrey == (IHumanPrey)commander && !human.SensePlayer(), "人間は仲間も獲物として探す（主人公は巣の中で見えない）");
            Vector3 spot = new Vector3(1.6f, 0, -2.2f);
            prey.Spawn(spot, 0);
            if (friend) { friend.Spawn(spot + new Vector3(-1.2f, 0, 0.7f), 0); friend.SetOrder(AllyRoach.Order.Defend, Vector3.zero); }
            NavMesh.SamplePosition(spot + new Vector3(1.4f, 0, -0.6f), out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
            agent.Warp(hit.position);
            agent.isStopped = true;
            human.transform.rotation = Quaternion.LookRotation(Flat(spot - human.transform.position));
            int discoveries = session.Discoveries, deaths = commander.AllyDeaths, hp = session.Hp;
            human.enabled = true;
            yield return WaitFor(() => human.State == HumanAIController.HumanState.Attack, 8);
            Check(human.State == HumanAIController.HumanState.Attack && human.Prey == prey.Slot && !human.TargetIsPlayer,
                "床の仲間を見つけて追い、スリッパを振りかぶる" + (human.State == HumanAIController.HumanState.Attack ? "" : "（state " + human.State + " det " + human.Detection.ToString("F2")
                + " sense " + human.SensePrey(prey.Position) + " human " + human.transform.position + "）"));
            Check(commander.Threatened(prey.Position, out _) && !commander.Threatened(prey, out _) && human.AttackTime < commander.AllyReactionSeconds * 0.6f,
                "狙われた仲間はすぐには逃げない（反応の遅れ " + (commander.AllyReactionSeconds * 0.6f).ToString("F2") + "〜" + (commander.AllyReactionSeconds * 1.4f).ToString("F2") + "秒）");
            if (friend)
                Check(commander.Threatened(friend, out Vector3 scatter) && Flat(scatter - human.AttackPoint).magnitude > commander.AllyAlarmRadius,
                    "警報: 仲間が狙われると、そばの仲間は固まらずにすぐ散る（半径 " + commander.AllyAlarmRadius.ToString("F1") + "m の外へ）");
            if (performanceRun) { yield return new WaitForSeconds(session.Settings.attackWindup * 0.5f); yield return Capture("slipper-ally-720p.png"); }
            yield return WaitFor(() => human.SlipperLanded, 3);
            yield return null;
            Check(prey.Stunned && prey.Wounded && !prey.IsDead && commander.AllyDeaths == deaths && commander.AllyFlips >= 1,
                "1回目のスリッパでは仲間はひっくり返るだけ（" + commander.AllyStunSeconds.ToString("F0") + "秒、やられない）");
            human.enabled = false;
            agent.isStopped = true;
            commander.enabled = true;   // allies tick again: the flipped one gets back up
            if (performanceRun) yield return Capture("ally-flipped-720p.png");
            yield return WaitFor(() => !prey.Stunned, commander.AllyStunSeconds + 1);
            Check(!prey.Stunned && prey.Wounded && prey.Alive && prey.Label != "ひっくり返り", "起き上がって命令に戻る（傷は残る）");

            // F: a tap calls the allies in view only.
            Place(new Vector3(0.1f, 0, -2.4f));   // open floor: nothing between the camera and the ally ahead
            motor.transform.rotation = Quaternion.Euler(0, 180, 0);
            cameraRig.ResetView();
            prey.transform.position = new Vector3(0.1f, 0, -4.5f); prey.SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            if (friend) { friend.transform.position = new Vector3(0.1f, 0, 1.6f); friend.SetOrder(AllyRoach.Order.Defend, Vector3.zero); }
            yield return null;
            var eye = cameraRig.GetComponent<Camera>();
            Physics.Linecast(eye.transform.position, prey.Position + Vector3.up * 0.3f, out RaycastHit block, ~(1 << 2), QueryTriggerInteraction.Ignore);
            string why = " (cam " + eye.transform.position + " vp " + eye.WorldToViewportPoint(prey.Position + Vector3.up * 0.3f) + " block " + (block.collider ? block.collider.name : "-")
                + " prey " + prey.Position + " friend " + (friend ? friend.Position.ToString() : "-") + ")";
            Check(commander.CallVisible() >= 1 && prey.CurrentOrder == AllyRoach.Order.Follow && (!friend || friend.CurrentOrder == AllyRoach.Order.Defend),
                "F: 画面に見えている仲間だけを呼ぶ（後ろの仲間は今の命令のまま）" + (prey.CurrentOrder == AllyRoach.Order.Follow && (!friend || friend.CurrentOrder == AllyRoach.Order.Defend) ? "" : why));
            prey.transform.position = commander.NestPoint(prey.Slot, 0.9f);
            yield return null; yield return null;
            Check(!prey.Wounded, "巣に入ると傷が治る");
            prey.transform.position = spot;
            Check(!prey.Hit(commander.AllyStunSeconds, commander.AllyWoundHealSeconds, 0) && prey.Hit(commander.AllyStunSeconds, commander.AllyWoundHealSeconds, 0)
                && prey.IsCorpse && commander.AllyDeaths == deaths + 1, "ひっくり返っている間（傷があるうち）にもう一度叩かれるとやられ、死体になる");
            commander.enabled = false;
            Check(session.Discoveries == discoveries && session.Hp == hp, "仲間が見つかっても主人公の発見回数・生命力は変わらない");
            Check(commander.CollectReactionScale > 1.2f, "回収中の仲間は警報を聞かず、スリッパへの反応も " + commander.CollectReactionScale.ToString("F1") + "倍遅い");

            // Losing sight: the resident searches on the move instead of standing still.
            Place(session.Nest.position);   // hidden in the nest
            NavMesh.SamplePosition(new Vector3(2.2f, 0, -2.6f), out NavMeshHit from, 1.5f, NavMesh.AllAreas);
            agent.Warp(from.position);
            agent.isStopped = false;
            human.enabled = true;
            human.DebugSearch(new Vector3(0.8f, 0, -1.6f));
            float moved = 0, still = 0, longest = 0;
            float stillAt = 0; string stillState = "";
            Vector3 was = human.transform.position;
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForSeconds(0.1f);
                float step = Flat(human.transform.position - was).magnitude; was = human.transform.position;
                moved += step;
                // Only the search counts: a glimpse of an ally on the way starts a short chase of its own.
                still = step < 0.02f && human.State == HumanAIController.HumanState.Search ? still + 0.1f : 0;
                if (still > longest) { longest = still; stillAt = (i + 1) * 0.1f; stillState = human.State + (agent.hasPath ? " path " + agent.remainingDistance.ToString("F1") : " nopath") + (agent.isStopped ? " stopped" : ""); }
            }
            Check(moved > 2f && longest < 1.3f, "見失った後は立ち止まらず、最後に見た辺りを歩き回って探す（4秒で " + moved.ToString("F1") + "m、最長停止 " + longest.ToString("F1") + "秒 @" + stillAt.ToString("F1") + " " + stillState + "）");
            human.enabled = false;
            agent.isStopped = true;
            agent.Warp(new Vector3(4.5f, 0, -3.5f));
            reaction.enabled = true;
        }

        private bool lastStrikeHit;
        // The resident stands at "from" facing the player, notices, swings, and the sole lands.
        private IEnumerator Strike(Vector3 from, string shot)
        {
            yield return new WaitForSeconds(1.7f);   // invulnerability from an earlier hit
            NavMesh.SamplePosition(from, out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
            agent.Warp(hit.position);
            agent.isStopped = true;   // stand and look instead of walking the patrol; the strike steps in by itself
            Vector3 look = Flat(session.PlayerStrikePoint - human.transform.position);
            human.transform.rotation = Quaternion.LookRotation(look);
            int hp = session.Hp;
            human.enabled = true;
            yield return WaitFor(() => human.State == HumanAIController.HumanState.Attack, 8);
            Check(human.State == HumanAIController.HumanState.Attack, "人間が気づいてスリッパを振りかぶる" + (human.State == HumanAIController.HumanState.Attack ? "" :
                "（state " + human.State + " det " + human.Detection.ToString("F2") + " see " + human.CanSeePlayer + " sense " + human.SensePlayer() + " los " + human.HasLineOfSight()
                + " strikeable " + session.PlayerStrikeable + " human " + human.transform.position + " player " + session.PlayerStrikePoint + " nav " + agent.isOnNavMesh + " susp " + human.Suspended + "）"));
            if (performanceRun) { yield return new WaitForSeconds(session.Settings.attackWindup * 0.5f); yield return Capture(shot); }
            yield return WaitFor(() => human.SlipperLanded, 3);
            yield return null;
            lastStrikeHit = session.Hp < hp;
            yield return WaitFor(() => human.State != HumanAIController.HumanState.Attack, 3);
            human.enabled = false;
            agent.isStopped = true;
        }

        private void PlaceExact(Vector3 position)
        {
            motor.ResetSurface();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            motor.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSafePosition();
            cameraRig.ResetView();
        }

        private IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name));
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException(description); checks.Add("PASS: " + description); Debug.Log("[PHASE5 PLAY] PASS: " + description); }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        private void Finish(bool passed)
        {
            if (Completed != null) Completed(passed);
            else Application.Quit(passed ? 0 : 1);
            Destroy(gameObject);
        }
    }
}
#endif
