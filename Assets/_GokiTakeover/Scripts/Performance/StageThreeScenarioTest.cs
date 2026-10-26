#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GokiTakeover
{
    // Stage 3 「町中華」 (2026-10-26, step 1). Explicit opt-in only: the Editor verification plays the Stage 3 scene
    // directly; a Development build with "-goki-stage3-test" starts on the title, unlocks Stage 3 in a test save and
    // sorties there. Checks the two-storey diner, the stairs and the duct (the player walks them, allies path through
    // them, the cook takes the stairs), the diner foods, the final defence and the clear. Settings and the save go to
    // separate test files.
    public sealed class StageThreeScenarioTest : MonoBehaviour
    {
        public string OutputFolder { get; set; }
        public event Action<bool> Completed;
        private readonly List<string> checks = new List<string>();
        private bool playerRun;
        private PhaseTwoSession session;
        private RoachMotor motor;
        private ThirdPersonRoachCamera cameraRig;
        private SwarmCommander commander;
        private ColonyManager colony;
        private StageProgress progress;
        private FloorLinks floors;
        private HumanAIController chef, okami, baito;
        private Camera view;
        private static readonly FieldInfo PatrolField = typeof(HumanAIController).GetField("patrolPoints", BindingFlags.NonPublic | BindingFlags.Instance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Isolate()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-goki-output");
            if (Array.IndexOf(args, "-goki-stage3-test") < 0 || index < 0 || index + 1 >= args.Length) return;
            GameSettings.PathOverride = Path.Combine(args[index + 1], "settings-test.json");
            SaveData.PathOverride = Path.Combine(args[index + 1], "save-test.json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-stage3-test") < 0) return;
            var runner = new GameObject("Stage3_OptIn_Test").AddComponent<StageThreeScenarioTest>();
            int index = Array.IndexOf(args, "-goki-output");
            runner.OutputFolder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            runner.playerRun = true;
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(OutputFolder);
            foreach (var f in new[] { SaveData.FilePath, SaveData.FilePath + ".bak" }) if (File.Exists(f)) File.Delete(f);
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
                    Time.timeScale = 1;
                    File.WriteAllText(Path.Combine(OutputFolder, "stage3-failure.txt"), string.Join("\n", checks) + "\nFAIL: " + e);
                    Debug.LogException(e); Finish(false); yield break;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "stage3-gameplay.txt"), checks);
            Finish(true);
        }

        private IEnumerator Checks()
        {
            // ---- Into the diner (from the title in a player run) ----
            var menu = FindFirstObjectByType<TitleMenu>();
            if (menu)
            {
                foreach (var f in new[] { SaveData.FilePath, SaveData.FilePath + ".bak" }) if (File.Exists(f)) File.Delete(f);
                var save = SaveData.Load();
                save.unlocked = new[] { "stage01", "stage02", "stage03" }; save.guideDone = true;
                save.Save();
                menu.Reload();
                // The paper title (案2b): the red-pen loop follows the focus, a press plays out before the page changes.
                menu.Focus = 0;
                yield return WaitForReal(0.4f);
                if (playerRun) { yield return Capture("title-720p.png"); yield return CleanShot("clean-title-720p.png"); }
                menu.Press(1);
                Check(menu.Current == TitleMenu.Page.Title && !menu.Pressing, "タイトル: 記録がない時の「つづきから」は押しても進まない（首を振る）");
                menu.Focus = 2;
                yield return WaitForReal(0.4f);
                if (playerRun) yield return Capture("title-focus-720p.png");
                menu.Press(2);
                Check(menu.Pressing && menu.Current == TitleMenu.Page.Title, "タイトル: 押した直後は押す動きを見せてから進む（" + menu.Current + "）");
                yield return WaitForReal(0.07f);
                if (playerRun) yield return Capture("title-press-720p.png");
                yield return WaitFor(() => menu.Current == TitleMenu.Page.Settings, 2);
                Check(menu.Current == TitleMenu.Page.Settings && !menu.Pressing, "タイトル: 押す動きのあとで「設定」が開く（" + menu.Current + "）");
                menu.Back();
                menu.Focus = 0;
                menu.Open(TitleMenu.Page.StageSelect);
                menu.Select(2);
                Check(menu.IsOpen(menu.SelectedStage) && menu.SelectedStage.id == "stage03" && menu.SelectedStage.title == "町中華", "ステージ選択: ステージ2クリア後は「町中華」に出撃できる");
                if (playerRun) yield return Capture("stage3-select-720p.png");
                Check(menu.Sortie(false), "ステージ3へ出撃");
            }
            yield return WaitFor(() => { var p = FindFirstObjectByType<StageProgress>(); return p && p.Current == StageProgress.Phase.Briefing && !FindFirstObjectByType<TitleMenu>(); }, 20);
            yield return null;
            progress = FindFirstObjectByType<StageProgress>();
            session = FindFirstObjectByType<PhaseTwoSession>();
            motor = session.Motor; cameraRig = session.FollowCamera; view = cameraRig.GetComponent<Camera>();
            commander = FindFirstObjectByType<SwarmCommander>();
            colony = FindFirstObjectByType<ColonyManager>();
            floors = FindFirstObjectByType<FloorLinks>();
            chef = session.Human;
            var stage = progress.Stage;
            Check(stage.id == "stage03" && progress.Current == StageProgress.Phase.Briefing && session.State == PhaseTwoSession.RunState.Paused, "町中華: 出撃前のミッション説明");
            if (playerRun) yield return Capture("stage3-briefing-720p.png");
            progress.Begin();
            yield return null; yield return null;
            Check(progress.Current == StageProgress.Phase.Invasion && session.IsRunning && progress.ZoneCount == 10 && colony.Allies == stage.startAllies, "侵略開始: 縄張り10区域（2階建て）・仲間 " + stage.startAllies + "匹");
            okami = session.Family[0]; baito = session.Family[1];
            Check(session.HumanCount == 3 && chef.Label == "店主" && chef.Slaps && chef.WeaponName == "お玉" && okami.Label == "おかみさん" && okami.Slaps && okami.WeaponName == "お盆" && okami.StrikeBox
                && baito.Label == "バイト" && !baito.Slaps && ReferenceEquals(chef.Floors, floors) && ReferenceEquals(baito.Floors, floors) && !chef.Agent.autoTraverseOffMeshLink,
                "人間は店主（お玉）・おかみさん（お盆）・バイト（叩かない）の3人、みな階段は自分で上り下りする");
            Check(floors.FloorOf(session.Player.position) == 1 && session.InSafeNest, "主人公は1Fの巣（洗い場のシンク下）から");

            FreezeChef();
            session.AddMaxHp(20);
            // The day runs by itself; the checks before BusinessChecks keep it still (no customers walking in on them).
            FindFirstObjectByType<DinerBusiness>().enabled = false;
            FindFirstObjectByType<DinerCustomers>().Empty();
            yield return null;

            // ---- The rooms (pictures for the eye) ----
            if (playerRun)
            {
                PlaceExact(session.Nest.position + Vector3.up * 0.1f);
                yield return Shot(new Vector3(6.6f, 3.2f, -5.0f), new Vector3(-3.0f, 1.4f, 3.0f), "stage3-kitchen-720p.png");
                yield return Shot(new Vector3(26.4f, 3.6f, -5.2f), new Vector3(15.0f, 1.4f, 2.0f), "stage3-hall-720p.png");
                yield return Shot(new Vector3(4.2f, 2.8f, 7.2f), new Vector3(-3.0f, 0.8f, 15.0f), "stage3-dishroom-720p.png");
                yield return Shot(new Vector3(5.8f, 3.0f, 6.9f), new Vector3(12.5f, 1.4f, 13.0f), "stage3-pantry-720p.png");
                yield return Shot(new Vector3(45.0f, 3.2f, -5.3f), new Vector3(55.0f, 0.6f, 2.0f), "stage3-banquet-720p.png");
                yield return Shot(new Vector3(55.4f, 3.4f, 6.8f), new Vector3(46.0f, 1.0f, 14.0f), "stage3-staff-720p.png");
                // The cook up close.
                PlaceHuman(chef, new Vector3(3.6f, 0, 0.4f), 180);
                yield return null;
                yield return Shot(new Vector3(3.6f, 3.0f, -3.6f), new Vector3(3.6f, 2.4f, 0.4f), "stage3-chef-720p.png");
                PlaceHuman(chef, StageThreeStart, 200);
                // Food on the floor by the fryer.
                PlaceExact(new Vector3(1.4f, 0.02f, 2.2f));
                yield return Shot(new Vector3(1.6f, 0.9f, 1.4f), new Vector3(0.4f, 0.1f, 3.9f), "stage3-food-720p.png", "clean-food-720p.png");
            }

            // ---- The diner food ----
            var foods = commander.Foods;
            Check(foods.Count == 38 && foods.All(f => FoodValue.IsDiner(f.GetComponent<FoodValue>().FoodKind)), "食料は町中華の10種（" + foods.Count + "か所、食べ残し・仕込みの分を含む）");
            var tenkasu = foods.First(f => f.GetComponent<FoodValue>().FoodKind == FoodValue.Kind.Tenkasu && f.transform.position.y < 0.5f);
            PlaceExact(tenkasu.transform.position + new Vector3(0, 0.0f, -0.45f));
            yield return null;
            Check(session.Interactor.NearbyFood == tenkasu && FoodValue.NameOf(tenkasu) == "天かす" && session.Interactor.TryInteract() && session.Interactor.Carried == tenkasu, "天かすを拾う（E 回収）");
            int collected = colony.TotalCollected;
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);
            yield return null;
            Check(session.Interactor.TryInteract() && !session.Interactor.Carried, "巣（シンク下）で E 納品");
            yield return null; yield return null;   // the colony counts it on its own update
            Check(colony.TotalCollected == collected + 1, "納品した天かすで食料 +1（" + collected + " → " + colony.TotalCollected + "）");

            // ---- Up the stairs on foot ----
            float z = floors.Links[0].a.ground.z;
            PlaceFacing(new Vector3(15.6f, 0.02f, z), 90);
            int trips = floors.Trips;
            bool shotOnStairs = false;
            float end = Time.realtimeSinceStartup + 20;
            while (floors.FloorOf(motor.transform.position) == 1 && !floors.Travelling && Time.realtimeSinceStartup < end)
            {
                Walk();
                if (playerRun && !shotOnStairs && motor.transform.position.y > 2.2f) { shotOnStairs = true; yield return Capture("stage3-stairs-720p.png"); }
                yield return null;
            }
            yield return WaitFor(() => !floors.Travelling, 2);
            Check(floors.FloorOf(motor.transform.position) == 2 && floors.Trips == trips + 1 && floors.FloorName(motor.transform.position) == "2F" && motor.transform.position.y < 0.5f,
                "階段を歩いて登ると暗転して2Fの宴会座敷へ（" + motor.transform.position.ToString("F1") + "）");
            yield return null;
            if (playerRun) { cameraRig.ResetView(); yield return Capture("stage3-2f-arrive-720p.png"); yield return CleanShot("clean-2f-720p.png"); }
            Check(floors.Hint(motor.transform.position) == "階段を下りると 1F へ", "2Fの階段の降り口で「階段を下りると 1F へ」");

            // ---- Down again ----
            PlaceFacing(new Vector3(56.4f, 0.02f, z), 90);
            trips = floors.Trips; int recoveries = motor.Recoveries;
            end = Time.realtimeSinceStartup + 12;

            while (floors.FloorOf(motor.transform.position) == 2 && !floors.Travelling && Time.realtimeSinceStartup < end) { Walk(); yield return null; }
            yield return WaitFor(() => !floors.Travelling, 2);
            Check(floors.FloorOf(motor.transform.position) == 1 && floors.Trips == trips + 1 && Flat(motor.transform.position - floors.Links[0].a.arrive).magnitude < 0.8f && motor.Recoveries == recoveries,
                "2Fの降り口へ進むと1Fの階段の下に出る（落下の巻き戻しなし・" + motor.transform.position.ToString("F1") + "・移動" + (floors.Trips - trips) + "回・巻き戻し" + (motor.Recoveries - recoveries) + "回）");
            // Stepping out of the arrival spot does not send the player straight back.
            for (int i = 0; i < 30; i++) { Walk(); yield return null; }
            Check(floors.Trips == trips + 1 && floors.FloorOf(motor.transform.position) == 1, "着いた所から歩き出しても戻されない");

            // ---- The duct (roaches only) ----
            var duct = floors.Links[1];
            PlaceFacing(new Vector3(duct.a.ground.x, 0.02f, -5.2f), 180);
            Check(floors.Hint(motor.transform.position) == "ダクトに入ると 2F へ（ゴキブリだけの道）", "ダクトの前で「ダクトに入ると 2F へ」");
            if (playerRun) { yield return null; yield return Capture("stage3-duct-720p.png"); }
            trips = floors.Trips;
            end = Time.realtimeSinceStartup + 8;
            while (floors.FloorOf(motor.transform.position) == 1 && !floors.Travelling && Time.realtimeSinceStartup < end) { Walk(); yield return null; }
            yield return WaitFor(() => !floors.Travelling, 2);
            Check(floors.FloorOf(motor.transform.position) == 2 && Flat(motor.transform.position - duct.b.arrive).magnitude < 0.8f, "厨房の壁の金網からダクトに入ると2Fの休憩室へ（" + motor.transform.position.ToString("F2") + (motor.IsClimbing ? "・壁を登っている" : "") + "）");
            PlaceFacing(new Vector3(duct.b.ground.x, 0.02f, 15.3f), 0);
            end = Time.realtimeSinceStartup + 8;
            while (floors.FloorOf(motor.transform.position) == 2 && !floors.Travelling && Time.realtimeSinceStartup < end) { Walk(); yield return null; }
            yield return WaitFor(() => !floors.Travelling, 2);
            Check(floors.FloorOf(motor.transform.position) == 1 && Flat(motor.transform.position - duct.a.arrive).magnitude < 0.8f, "休憩室のダクトから厨房へ戻れる");

            // ---- Allies find their way between the floors ----
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);
            var ally = commander.AddAlly();
            Time.timeScale = 3;
            ally.SetOrder(AllyRoach.Order.Move, new Vector3(50.0f, 0, 0.4f));
            yield return WaitFor(() => ally.State == AllyRoach.AllyState.Idle && Flat(ally.Position - new Vector3(50.0f, 0, 0.4f)).magnitude < 3f, 25);
            Check(floors.FloorOf(ally.Position) == 2 && Flat(ally.Position - new Vector3(50.0f, 0, 0.4f)).magnitude < 2.0f, "仲間は巣から2Fの宴会座敷まで歩いて行く（" + ally.Position.ToString("F1") + "）");
            ally.SetOrder(AllyRoach.Order.Move, new Vector3(5.2f, 0, 0.4f));
            yield return WaitFor(() => ally.State == AllyRoach.AllyState.Idle && Flat(ally.Position - new Vector3(5.2f, 0, 0.4f)).magnitude < 3f, 25);
            Check(floors.FloorOf(ally.Position) == 1 && Flat(ally.Position - new Vector3(5.2f, 0, 0.4f)).magnitude < 2.0f, "2Fから1Fの厨房へも戻れる（" + ally.Position.ToString("F1") + "）");
            int zone = Array.FindIndex(stage.zones, zz => zz.name == "ロッカーの前");
            ally.SetCapture(stage.zones[zone].centre, zone);
            yield return WaitFor(() => progress.ZoneAt(ally.Position) == zone, 25);
            Check(progress.ZoneAt(ally.Position) == zone, "2Fの「ロッカーの前」へ占拠に行ける");
            Time.timeScale = 1;
            ally.gameObject.SetActive(false);

            // ---- The cook takes the stairs ----
            var upstairs = new GameObject("TestStop_2F").transform;
            upstairs.position = new Vector3(50.0f, 0, 0.4f);
            var patrol = (Transform[])PatrolField.GetValue(chef);
            chef.enabled = true; chef.Agent.isStopped = false;
            chef.ResetHuman();
            PatrolField.SetValue(chef, new[] { upstairs });
            Time.timeScale = 3;
            bool onStairs = false;
            end = Time.realtimeSinceStartup + 30;
            while (floors.FloorOf(chef.transform.position) == 1 && Time.realtimeSinceStartup < end) { onStairs |= chef.OnStairs; yield return null; }
            yield return WaitFor(() => !chef.OnStairs, 4);
            Check(onStairs && floors.FloorOf(chef.transform.position) == 2 && chef.Agent.isOnNavMesh && !chef.OnStairs, "店主は階段を上って2Fへ行く（" + chef.transform.position.ToString("F1") + "）");
            yield return WaitFor(() => Flat(chef.transform.position - upstairs.position).magnitude < 1.5f, 12);
            Check(Flat(chef.transform.position - upstairs.position).magnitude < 1.5f, "2Fの巡回地点まで歩く");
            upstairs.position = ChefStartStop;
            end = Time.realtimeSinceStartup + 30;
            while (floors.FloorOf(chef.transform.position) == 2 && Time.realtimeSinceStartup < end) yield return null;
            yield return WaitFor(() => !chef.OnStairs, 4);
            Check(floors.FloorOf(chef.transform.position) == 1 && chef.Agent.isOnNavMesh, "店主は階段を下りて1Fへ戻る");
            Time.timeScale = 1;
            PatrolField.SetValue(chef, patrol);
            Destroy(upstairs.gameObject);
            FreezeChef();

            yield return StaffChecks();
            yield return BusinessChecks();

            // ---- Final defence and the clear: the landlady and the part-timer leave ----
            colony.DebugGrant(0);
            for (int i = 0; i < progress.ZoneCount; i++) progress.DebugCapture(i);
            yield return WaitForReal(0.4f);
            progress.DebugAddFear(1000);
            PlaceExact(new Vector3(-2.0f, 0.02f, -0.5f));
            colony.Restore(colony.Food, stage.foodGoal, colony.NestLevel, colony.UpgradeLevels, colony.Allies, colony.UpgradesBought, colony.Born);
            session.AddMaxHp(60);   // the smoke must not end the run while the staff walk out (it is not what is tested here)
            foreach (var h in new[] { chef, okami, baito }) { h.enabled = true; h.Agent.isStopped = false; }
            yield return WaitFor(() => progress.Current == StageProgress.Phase.FinalDefence, 3);
            Check(progress.Current == StageProgress.Phase.FinalDefence, "3ゲージ満タンで最終防衛");
            Check(okami.Suspended && baito.Suspended && progress.Attacks.All(a => !a.Allowed), "おかみさんとバイトは攻撃をやめて裏口から避難を始める");
            okami.Agent.Warp(stage.exitPoint + Vector3.right * 0.4f); baito.Agent.Warp(stage.exitPoint + Vector3.right * 0.6f);
            yield return WaitFor(() => progress.FamilyGone, 14);
            Check(progress.FamilyGone && !session.HumanPresent(1) && !session.HumanPresent(2), "おかみさんとバイトは店から出ていく（残るのは店主）");
            progress.DebugFinishDefence();
            yield return WaitFor(() => progress.Current == StageProgress.Phase.Cleared, 25);
            Check(progress.Current == StageProgress.Phase.Cleared && session.State == PhaseTwoSession.RunState.Cleared, "最終防衛を耐えると店主は裏口から出ていき、ステージクリア（" + progress.Current + "・" + session.State + "・生命力 " + session.Hp + "・店主 " + chef.transform.position.ToString("F1") + (chef.Suspended ? "・止まっている" : "") + (chef.OnStairs ? "・階段" : "") + "・残り " + (chef.Agent.isOnNavMesh ? chef.Agent.remainingDistance.ToString("F1") : "NavMesh外") + "）");
            var result = SaveData.Load();
            Check(result.Record("stage03") != null, "クリアの記録（ステージ3）");
            if (playerRun) yield return Capture("stage3-clear-720p.png");
            progress.RestartStage();
            yield return null; yield return null;
            Check(floors.FloorOf(session.Player.position) == 1 && session.InSafeNest && !floors.Travelling && floors.FloorOf(chef.transform.position) == 1, "リプレイで主人公は1Fの巣・店主は厨房に戻る");
            Check(session.HumanPresent(1) && session.HumanPresent(2) && progress.Attacks.All(a => a.Allowed) && FindFirstObjectByType<WetFloor>().Count == 0 && !session.Slowed,
                "リプレイでおかみさんとバイトも店に戻り、濡れた床は乾く");
            Check(FindFirstObjectByType<DinerCustomers>().Inside == 0 && FindFirstObjectByType<DinerBusiness>().Current == DinerBusiness.Phase.Prep && !FindFirstObjectByType<DinerBusiness>().Dark, "リプレイで客はいなくなり、仕込みから始まる");
        }

        // Step 2: the staff's attacks, each started on the spot (DebugAim / DebugStrike) with everyone else frozen.
        private IEnumerator StaffChecks()
        {
            FreezeChef();
            var hot = chef.GetComponent<HotWaterAttack>();
            var spray = okami.GetComponent<AlcoholSprayAttack>();
            var brush = baito.GetComponent<DeckBrushAttack>();
            var mop = baito.GetComponent<MopDuty>();
            var wet = FindFirstObjectByType<WetFloor>();
            Check(hot && spray && brush && mop && wet && progress.Attacks.Count == 3, "店主に熱湯・おかみさんにアルコールスプレー・バイトにデッキブラシとモップがけ（攻撃3つ）");
            Vector3 space = new Vector3(6.5f, 0, -0.8f);   // open kitchen floor, clear of the sticky trap

            // ---- The cook's hot water ----
            PlaceHuman(chef, space + new Vector3(0, 0, 3.4f), 180);
            Place(space);
            var helper = commander.AddAlly();
            helper.Spawn(space + new Vector3(0, 0, 1.2f), 0); helper.SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            hot.DebugAim(space);
            yield return null;
            Check(hot.Aiming && hot.OnStrip(motor.FootPoint) && chef.Suspended, "熱湯: 床に橙の帯で予告（店主はその場で片手鍋を傾ける）");
            Vector3 escape;
            Check(hot.Threatens(helper.Position, out escape) && !hot.OnStrip(escape), "帯の上の仲間は帯の横へ逃げる");
            commander.enabled = false;   // allies hold still (the commander ticks them)
            int hp = session.Hp;
            if (playerRun) { yield return WaitForReal(HotWaterAttack.AimSeconds * 0.5f); yield return Shot(space + new Vector3(3.0f, 2.6f, -2.8f), space + new Vector3(-0.4f, 0.2f, 1.6f), "stage3-hotwater-aim-720p.png", "clean-hotwater-720p.png"); }
            yield return WaitFor(() => hot.Hot, 3);
            yield return WaitFor(() => hot.Current == HotWaterAttack.Phase.Pool, 2);
            if (playerRun) yield return Shot(space + new Vector3(3.0f, 2.6f, -2.8f), space + new Vector3(-0.4f, 0.2f, 1.6f), "stage3-hotwater-flow-720p.png");
            Check(hot.PlayerHits == 1 && session.Hp == hp - 1, "帯の上にいると熱湯でダメージ（" + hp + " → " + session.Hp + "）");
            Check(hot.AllyHits == 1 && helper.Stunned, "帯の上の仲間は熱湯でひっくり返る");
            commander.enabled = true;
            yield return WaitFor(() => hot.Current == HotWaterAttack.Phase.Idle, 4);
            Check(!chef.Suspended, "熱湯の後、店主は動き出す");
            yield return WaitForReal(1.4f);   // past the invulnerability
            hp = session.Hp;
            hot.DebugAim(space);
            Place(space + new Vector3(HotWaterAttack.Width * 0.5f + 0.9f, 0, 0));
            yield return WaitFor(() => hot.Current == HotWaterAttack.Phase.Recover, 5);
            Check(session.Hp == hp, "帯から横へ出ていれば熱湯は当たらない");
            yield return WaitFor(() => hot.Current == HotWaterAttack.Phase.Idle, 3);

            // ---- The landlady's tray: a wide box ----
            PlaceHuman(okami, space + new Vector3(0, 0, 1.9f), 180);
            Place(space);
            yield return WaitForReal(1.4f);
            okami.enabled = true; okami.Agent.isStopped = false;
            hp = session.Hp;
            okami.DebugStrike();
            var warning = (Transform)typeof(HumanAIController).GetField("warning", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(okami);
            yield return null;
            Vector3 aside = okami.AttackPoint + Vector3.Cross(Vector3.up, okami.StrikeForward) * 1.0f;
            Check(okami.State == HumanAIController.HumanState.Attack && warning.GetComponent<RectTelegraph>() && warning.gameObject.activeSelf && warning.localScale.x > warning.localScale.z * 2
                && okami.IsUnderSlipper(aside) && !chef.IsUnderSlipper(chef.transform.position + Vector3.right * 9), "お盆: 横長の橙の四角で予告（幅 " + warning.localScale.x.ToString("F1") + " × 奥行 " + warning.localScale.z.ToString("F1") + "、横1mも届く）");
            if (playerRun) yield return Shot(space + new Vector3(3.0f, 2.6f, -2.6f), space + new Vector3(-0.4f, 0.2f, 0.8f), "stage3-tray-720p.png");
            yield return WaitFor(() => okami.SlipperLanded, 3);
            yield return null;
            Check(session.Hp == hp - 1, "お盆の下にいるとダメージ");
            FreezeChef();
            yield return WaitForReal(1.4f);

            // ---- The landlady's alcohol spray ----
            PlaceHuman(okami, space + new Vector3(0, 0, 3.4f), 180);
            Place(space);
            helper.Spawn(space + new Vector3(0.4f, 0, 0.8f), 0); helper.SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            hp = session.Hp;
            spray.DebugAim(space);
            yield return null;
            Check(spray.Aiming && spray.InFan(motor.FootPoint) && commander.Threatened(helper.Position, out escape) && !spray.InFan(escape), "アルコールスプレー: 床に橙の扇で予告・仲間は扇の外へ逃げる");
            commander.enabled = false;   // allies hold still (the commander ticks them)
            yield return WaitFor(() => spray.Spraying, 2);
            yield return WaitForReal(0.3f);
            if (playerRun) yield return Shot(space + new Vector3(3.2f, 2.4f, -2.4f), space + new Vector3(-0.3f, 0.6f, 1.6f), "stage3-spray-720p.png");
            yield return null;
            Check(session.Slowed && motor.SpeedMultiplier <= PhaseTwoSession.SlowFactor + 0.01f && session.Hp == hp, "スプレーを浴びると動きが半分に（ダメージはない）");
            Check(Time.time < helper.SlowUntil, "浴びた仲間も遅くなる");
            commander.enabled = true;
            yield return WaitFor(() => !session.Slowed, AlcoholSprayAttack.SlowSeconds + 3);
            yield return null;
            Check(!session.Slowed && motor.SpeedMultiplier > 0.9f, AlcoholSprayAttack.SlowSeconds.ToString("F0") + "秒で元に戻る");
            yield return WaitFor(() => spray.Current == AlcoholSprayAttack.Phase.Idle, 3);
            PlaceHuman(okami, OkamiHome, 160);

            // ---- The part-timer's deck brush ----
            PlaceHuman(baito, space + new Vector3(0, 0, 1.6f), 180);
            Place(space);
            yield return WaitForReal(1.4f);
            hp = session.Hp;
            helper.Spawn(space + new Vector3(0.4f, 0, 0.2f), 0); helper.SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            Vector3 before = motor.FootPoint, helperBefore = helper.Position;
            brush.DebugAim(space);
            yield return null;
            bool helperThreat = commander.Threatened(helper.Position, out escape);
            Check(brush.WindingUp && brush.InBox(motor.FootPoint) && helperThreat && !brush.InBox(escape), "デッキブラシ: 橙の四角で予告・仲間は四角の外へ逃げる（予告 " + brush.WindingUp + "・主人公 " + brush.InBox(motor.FootPoint) + " " + motor.FootPoint.ToString("F1") + "・仲間 " + helperThreat + " " + helper.Position.ToString("F1") + "→" + escape.ToString("F1") + "）");
            commander.enabled = false;   // allies hold still (the commander ticks them)
            if (playerRun) { yield return WaitForReal(0.4f); yield return Shot(space + new Vector3(3.0f, 2.6f, -2.0f), space + new Vector3(-0.2f, 0.2f, 0.4f), "stage3-brush-720p.png"); }
            yield return WaitFor(() => brush.Current == DeckBrushAttack.Phase.Recover, 3);
            for (int i = 0; i < 3; i++) yield return null;
            float pushed = Vector3.Dot(motor.FootPoint - before, brush.Direction);
            Check(brush.PlayerHits == 1 && session.Hp == hp - 1 && pushed > 1.0f && motor.ExternalVelocity == Vector3.zero, "四角の中にいると掃き飛ばされてダメージ（" + pushed.ToString("F1") + "m）");
            Check(brush.AllyHits == 1 && (helper.Stunned || !helper.Alive) && Vector3.Dot(helper.Position - helperBefore, brush.Direction) > 0.5f, "仲間も掃き飛ばされてひっくり返る");
            commander.enabled = true;
            yield return WaitFor(() => brush.Current == DeckBrushAttack.Phase.Idle, 2);
            helper.gameObject.SetActive(false);

            // ---- Mopping and the wet floor ----
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);   // nothing to chase
            PlaceHuman(baito, BaitoHome, 200);
            baito.enabled = true; baito.Agent.isStopped = false;
            mop.Begin();
            yield return WaitFor(() => wet.Count >= 3, 10);
            Check(mop.Mopping && wet.Count >= 3, "バイトはモップがけで歩いた跡を濡らす（濡れた床 " + wet.Count + "か所）");
            FreezeChef();
            Vector3 floorSpot = new Vector3(6.4f, 0, -1.0f);
            yield return Slide(floorSpot);
            float dry = lastSlide;
            wet.Drop(floorSpot + new Vector3(0, 0, 0.0f)); wet.Drop(floorSpot + new Vector3(0, 0, -1.4f)); wet.Drop(floorSpot + new Vector3(0, 0, -2.6f));
            yield return Slide(floorSpot);
            Check(lastSlide > dry + 0.4f && wet.Slides > 0, "濡れた床では止まれず滑る（手を離してから " + lastSlide.ToString("F2") + "m、乾いた床 " + dry.ToString("F2") + "m）");
            if (playerRun) yield return Shot(floorSpot + new Vector3(2.6f, 2.0f, 1.8f), floorSpot + new Vector3(0, 0, -1.4f), "stage3-wetfloor-720p.png");
            Place(new Vector3(3.0f, 0, -4.5f));
            yield return null;
            Check(motor.Traction == 1, "乾いた床に出れば元どおり");
        }

        // Walks forward for half a second on the spot, lets go and measures how far the roach goes on by itself.
        private float lastSlide;
        private IEnumerator Slide(Vector3 at)
        {
            PlaceFacing(at + new Vector3(0, 0.02f, 1.2f), 180);
            yield return null;
            float end = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < end) { Walk(); yield return null; }
            Vector3 released = motor.FootPoint;
            end = Time.realtimeSinceStartup + 1.6f;
            while (Time.realtimeSinceStartup < end) { motor.MovementEnabled = true; motor.SimulateMove(Vector2.zero, false, Time.deltaTime); yield return null; }
            lastSlide = Flat(motor.FootPoint - released).magnitude;
        }
        private static readonly Vector3 OkamiHome = new Vector3(19.0f, 0, -1.6f), BaitoHome = new Vector3(2.5f, 0, 12.4f);
        private void Place(Vector3 position) { position.y = 0.02f; PlaceExact(position); }

        // Step 3: the day and its customers. The clock is driven by hand (Enter) and customers are seated by hand (Admit).
        private IEnumerator BusinessChecks()
        {
            var business = FindFirstObjectByType<DinerBusiness>();
            var guests = FindFirstObjectByType<DinerCustomers>();
            var reaction = colony.Reaction;
            foreach (var h in new[] { chef, okami, baito }) h.ResetHuman();   // out of whatever the staff checks left them in
            FreezeChef();
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);
            var link = new System.Func<string, FoodItem[]>(n => (FoodItem[])typeof(DinerBusiness).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(business));
            var prepFood = link("prepFood"); var dinnerFood = link("dinnerFood");
            business.enabled = true;

            business.Enter(DinerBusiness.Phase.Prep);
            Check(business.Current == DinerBusiness.Phase.Prep && prepFood.All(f => f.State == FoodItem.FoodState.Available) && dinnerFood.All(f => f.State == FoodItem.FoodState.Hidden) && !business.Dark,
                "仕込み: 作業台に餃子が並ぶ（焼売のせいろはまだ出ていない）・明かりはついている");

            business.Enter(DinerBusiness.Phase.Lunch);
            guests.CloseDoors();   // nobody at random: the test seats its own customer
            Check(Mathf.Approximately(chef.BusyRangeScale, DinerBusiness.BusyRange) && Mathf.Approximately(okami.BusyRangeScale, DinerBusiness.BusyRange), "ランチ: 店員は忙しくて見える距離が " + DinerBusiness.BusyRange + " 倍");
            int seatIndex = 0;
            var seat = guests.Seats[seatIndex];
            var guest = guests.Admit(0, seatIndex);
            Check(guest && guest.Current == DinerCustomer.Phase.Enter && guests.Inside == 1, "客（" + guest.Kind + "）が入口から入って席へ向かう");
            Time.timeScale = 3;
            yield return WaitFor(() => guest.Current == DinerCustomer.Phase.Seated, 20);
            Time.timeScale = 1;
            var body = guest.GetComponentInChildren<BlenderHumanAnimator>();
            Check(guest.Current == DinerCustomer.Phase.Seated && body.Pose == BlenderHumanAnimator.LifePose.Eat && guests.Tables[seat.table].plate.activeSelf,
                "客は席に座って食べる（料理の皿が出る）");
            if (playerRun) { yield return WaitForReal(0.6f); yield return Shot(new Vector3(18.6f, 3.4f, -5.6f), new Vector3(15.0f, 1.6f, -3.2f), "stage3-customer-eat-720p.png"); }

            // A roach on the table in front of the customer: a scream, fear, a sighting, staff on their way, the customer runs.
            float fear = reaction.FearGained; int seen = session.Discoveries;
            PlaceExact(seat.hands + Vector3.up * 0.06f);
            yield return WaitFor(() => guests.Screams >= 1, 3);
            Check(guests.Screams == 1 && guest.Current == DinerCustomer.Phase.Flee && reaction.FearGained >= fear + DinerCustomer.FearPerScream - 0.01f && session.Discoveries == seen + 1,
                "客がゴキブリを見て悲鳴（恐怖 +" + (reaction.FearGained - fear).ToString("F0") + "・発見 +1）、走って逃げる");
            Check(okami.State == HumanAIController.HumanState.Search || chef.State == HumanAIController.HumanState.Search || baito.State == HumanAIController.HumanState.Search, "悲鳴を聞いた店員が見に来る");
            if (playerRun) yield return Shot(new Vector3(18.6f, 3.4f, -5.6f), new Vector3(15.0f, 1.6f, -3.2f), "stage3-customer-scream-720p.png");
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);
            Time.timeScale = 3;
            yield return WaitFor(() => !guest.Present, 20);
            Time.timeScale = 1;
            var leftovers = guests.Tables[seat.table].leftovers;
            Check(!guest.Present && leftovers.State == FoodItem.FoodState.Available && !guests.Tables[seat.table].plate.activeSelf, "逃げた客は店を出て、テーブルに食べ残しが残る");
            Time.timeScale = 6;
            yield return WaitFor(() => leftovers.State == FoodItem.FoodState.Hidden, 12);
            Time.timeScale = 1;
            Check(leftovers.State == FoodItem.FoodState.Hidden, "食べ残しは " + DinerCustomers.LeftoverSeconds.ToString("F0") + "秒で片付けられる");

            business.Enter(DinerBusiness.Phase.Break);
            var mop = baito.GetComponent<MopDuty>();
            int traps = reaction.PlacedTraps.Count(t => t.gameObject.activeSelf) + reaction.Baits.Count(x => x.IsArmed);
            Check(business.TrapsSet >= 1 && traps >= 1 && Mathf.Approximately(chef.BusyRangeScale, 1) && guests.Inside == 0, "中休み: 罠を補充（" + business.TrapsSet + "個）・店員は普段どおり見える");
            Check(mop.Mopping, "中休みにバイトがモップがけを始める");

            business.Enter(DinerBusiness.Phase.Dinner);
            guests.CloseDoors();
            Check(dinnerFood.All(f => f.State == FoodItem.FoodState.Available), "ディナー: 2Fの座敷に焼売のせいろが出る");
            int upstairsSeat = -1;
            for (int i = 0; i < guests.Seats.Count; i++) if (guests.Seats[i].upstairs) { upstairsSeat = i; break; }
            var climber = guests.Admit(2, upstairsSeat);
            Time.timeScale = 3;
            yield return WaitFor(() => climber.Current == DinerCustomer.Phase.Seated, 45);
            Time.timeScale = 1;
            Check(climber.Current == DinerCustomer.Phase.Seated && floors.FloorOf(climber.transform.position) == 2 && climber.GetComponentInChildren<BlenderHumanAnimator>().Pose == BlenderHumanAnimator.LifePose.Floor,
                "ディナーの客（" + climber.Kind + "）は階段で2Fの座敷へ上がって座る（" + climber.transform.position.ToString("F1") + "）");
            if (playerRun) { yield return WaitForReal(1.2f); yield return Shot(new Vector3(45.5f, 3.0f, -5.4f), new Vector3(50.0f, 0.8f, -1.0f), "stage3-banquet-dinner-720p.png"); }

            business.Enter(DinerBusiness.Phase.Closed);
            Check(business.Dark && chef.GetComponentInChildren<Light>(true).enabled && guests.Inside == 0 && dinnerFood.All(f => f.State == FoodItem.FoodState.Hidden)
                && Mathf.Approximately(chef.BusyAngleScale, DinerBusiness.TorchAngle), "閉店: 明かりが消え、店主は懐中電灯（見える角度 " + DinerBusiness.TorchAngle + " 倍）・客は帰り、焼売は片付く");
            Time.timeScale = 3;
            yield return WaitFor(() => !okami.gameObject.activeSelf && !baito.gameObject.activeSelf, 30);
            Time.timeScale = 1;
            Check(!okami.gameObject.activeSelf && !baito.gameObject.activeSelf && business.WentHome(0) && business.WentHome(1), "閉店後、おかみさんとバイトは裏口から帰る");
            if (playerRun) yield return Shot(new Vector3(6.6f, 3.2f, -5.0f), new Vector3(-3.0f, 1.4f, 3.0f), "stage3-closed-720p.png");

            business.Enter(DinerBusiness.Phase.Prep);
            Check(okami.gameObject.activeSelf && baito.gameObject.activeSelf && !okami.Suspended && !business.Dark && !chef.GetComponentInChildren<Light>(true).enabled && business.Days == 1,
                "翌朝の仕込み: 2人が戻り、明かりがつく（" + business.Days + "日目の終わり）");
            FreezeChef();
        }

        private static readonly Vector3 StageThreeStart = new Vector3(3.6f, 0, 2.4f), ChefStartStop = new Vector3(3.5f, 0, 2.5f);
        private void FreezeChef() { foreach (var h in new[] { chef, okami, baito }) { if (!h) continue; h.enabled = false; if (h.Agent.isOnNavMesh) h.Agent.isStopped = true; } }
        private void PlaceHuman(HumanAIController h, Vector3 at, float yaw) { h.Agent.Warp(at); h.transform.rotation = Quaternion.Euler(0, yaw, 0); }
        private IEnumerator Shot(Vector3 eye, Vector3 target, string name, string clean = null)
        {
            cameraRig.enabled = false;
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            yield return Capture(name);
            if (clean != null) yield return CleanShot(clean);
            cameraRig.enabled = true;
        }
        // Forward as the player would walk (the session re-derives MovementEnabled from the cursor lock every frame).
        private void Walk()
        {
            motor.MovementEnabled = true;
            motor.SimulateMove(new Vector2(0, 1), false, Time.deltaTime);
        }
        private void PlaceFacing(Vector3 position, float yaw)
        {
            PlaceExact(position);
            motor.transform.rotation = Quaternion.Euler(0, yaw, 0);
            cameraRig.ResetView();
        }
        private void PlaceExact(Vector3 position)
        {
            motor.ResetSurface();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            motor.transform.SetPositionAndRotation(position, motor.transform.rotation);
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSafePosition();
        }
        private IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }
        private IEnumerator WaitForReal(float seconds) { yield return new WaitForSecondsRealtime(seconds); }
        // The same frame without any IMGUI (reference backgrounds for UI mock-ups). Only with -goki-clean-shots:
        // switching the HUDs off for a frame shifts the run's timing, so the normal run leaves it out.
        private IEnumerator CleanShot(string name)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-goki-clean-shots") < 0) yield break;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var hidden = new List<MonoBehaviour>();
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (b.enabled && b != this && !(b is FloorLinks) && b.GetType().GetMethod("OnGUI", flags) != null) { b.enabled = false; hidden.Add(b); }
            yield return Capture(name);
            foreach (var b in hidden) if (b) b.enabled = true;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name));
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException(description + " [focused=" + Application.isFocused + " cursor=" + Cursor.lockState + " fps=" + (1 / Mathf.Max(1e-4f, Time.unscaledDeltaTime)).ToString("F0") + "]"); checks.Add("PASS: " + description); Debug.Log("[STAGE3 PLAY] PASS: " + description); }
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
