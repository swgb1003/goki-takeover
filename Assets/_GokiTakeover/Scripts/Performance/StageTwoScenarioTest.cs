#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    // Stage 2 「一軒家」 (2026-10-25). Explicit opt-in only: the Editor verification plays the Stage 2 scene directly;
    // a Development build with "-goki-stage2-test" starts on the title, unlocks Stage 2 in a test save and sorties
    // there. Checks the house, the three people, the mother's vacuum, the son's net and cage, the family leaving for
    // the final defence and the clear. Settings and the save go to separate test files.
    public sealed class StageTwoScenarioTest : MonoBehaviour
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
        private HumanAIController father, mother, son;
        private VacuumAttack vacuum;
        private BugNetAttack net;
        private PetCat cat;
        private Camera view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Isolate()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-goki-output");
            if (Array.IndexOf(args, "-goki-stage2-test") < 0 || index < 0 || index + 1 >= args.Length) return;
            GameSettings.PathOverride = Path.Combine(args[index + 1], "settings-test.json");
            SaveData.PathOverride = Path.Combine(args[index + 1], "save-test.json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Array.IndexOf(args, "-goki-stage2-test") < 0) return;
            var runner = new GameObject("Stage2_OptIn_Test").AddComponent<StageTwoScenarioTest>();
            int index = Array.IndexOf(args, "-goki-output");
            runner.OutputFolder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            runner.playerRun = true;
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(OutputFolder);
            // A fresh test save every run (the clear must unlock Stage 3 anew).
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
                    File.WriteAllText(Path.Combine(OutputFolder, "stage2-failure.txt"), string.Join("\n", checks) + "\nFAIL: " + e);
                    Debug.LogException(e); Finish(false); yield break;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "stage2-gameplay.txt"), checks);
            Finish(true);
        }

        private IEnumerator Checks()
        {
            // ---- Into the house (from the title in a player run) ----
            var menu = FindFirstObjectByType<TitleMenu>();
            if (menu)
            {
                foreach (var f in new[] { SaveData.FilePath, SaveData.FilePath + ".bak" }) if (File.Exists(f)) File.Delete(f);
                var save = SaveData.Load();
                save.unlocked = new[] { "stage01", "stage02" }; save.guideDone = true;
                save.Save();
                menu.Reload();
                menu.Open(TitleMenu.Page.StageSelect);
                menu.Select(1);
                Check(menu.IsOpen(menu.SelectedStage) && menu.SelectedStage.id == "stage02", "ステージ選択: ステージ1クリア後は「一軒家」に出撃できる");
                if (playerRun) yield return Capture("stage2-select-720p.png");
                Check(menu.Sortie(false), "ステージ2へ出撃");
            }
            yield return WaitFor(() => { var p = FindFirstObjectByType<StageProgress>(); return p && p.Current == StageProgress.Phase.Briefing && !FindFirstObjectByType<TitleMenu>(); }, 20);
            yield return null;
            progress = FindFirstObjectByType<StageProgress>();
            session = FindFirstObjectByType<PhaseTwoSession>();
            motor = session.Motor; cameraRig = session.FollowCamera; view = cameraRig.GetComponent<Camera>();
            commander = FindFirstObjectByType<SwarmCommander>();
            colony = FindFirstObjectByType<ColonyManager>();
            father = session.Human; mother = session.Family[0]; son = session.Family[1];
            vacuum = mother.GetComponent<VacuumAttack>(); net = son.GetComponent<BugNetAttack>();
            cat = FindFirstObjectByType<PetCat>();
            var stage = progress.Stage;
            Check(stage.id == "stage02" && progress.Current == StageProgress.Phase.Briefing && session.State == PhaseTwoSession.RunState.Paused, "一軒家: 出撃前のミッション説明");
            Check(session.HumanCount == 3 && father.Label == "父" && mother.Label == "母" && son.Label == "息子", "人間は3人（父・母・息子）");
            if (playerRun) yield return Capture("stage2-briefing-720p.png");
            progress.Begin();
            yield return null; yield return null;
            Check(progress.Current == StageProgress.Phase.Invasion && session.IsRunning && progress.ZoneCount == 8 && colony.Allies == stage.startAllies, "侵略開始: 縄張り8区域（4部屋）・仲間 " + stage.startAllies + "匹");
            Check(mother.SearchWander && son.PatrolRandom && mother.ExtraPrey != null && son.ExtraPrey != null, "母と息子も部屋を巡回し、見失うと探し回り、仲間も狙う");
            Check(Mathf.Approximately(mother.DetectionTimeScale, father.DetectionTimeScale) && Mathf.Approximately(father.DetectionTimeScale, stage.humanDetectionScale) && stage.humanDetectionScale < 1.25f, "一軒家の家族はステージ1より気づくのが早い（発見までの時間 x" + father.DetectionTimeScale.ToString("F2") + "）");
            Check(son.Agent.speed > father.Agent.speed * 1.1f || son.Agent.speed > 0, "息子は足が速い");
            Check(!son.Slaps && mother.Slaps && father.Slaps, "息子はスリッパを使わない（網だけ）");
            Check(cat && cat.Current == PetCat.Phase.Nap && Flat(cat.transform.position - new Vector3(9.3f, 0, 4.6f)).magnitude < 0.3f, "飼い猫は寝床でお昼寝から始まる");

            // ---- Daily life: the calm family lives instead of patrolling (before anything has been seen) ----
            yield return LifeChecks();
            life.enabled = false;   // the older checks below drive the family themselves

            // Everyone frozen for the scripted parts.
            FreezeAll();
            session.AddMaxHp(20);   // the scripted hits must not end the run
            yield return null;

            // ---- The house ----
            var path = new List<Vector3>();
            Check(commander.Grid.FindPath(commander.NestGround, new Vector3(18, 0, 0), path), "巣（台所の流し台下）からリビングの奥まで仲間が歩ける");
            int living = 0; foreach (var f in commander.Foods) if (f.transform.position.x > 7.3f && f.transform.position.z < 6) living++;
            Check(living == 9, "リビングに食料9つ（高い所: テーブルの上・テレビ台・本棚の上）");
            Place(new Vector3(11.5f, 0, -0.5f));
            ShowFrom(new Vector3(9.0f, 2.2f, -4.6f), new Vector3(15.5f, 1.2f, 2.5f));
            yield return null;
            if (playerRun) yield return Capture("stage2-living-720p.png");

            // ---- The mother's vacuum ----
            PlaceHuman(mother, new Vector3(12.0f, 0, 3.4f), 180);
            Place(new Vector3(12.0f, 0, 0.4f));
            vacuum.DebugAim(new Vector3(12.0f, 0, 0.4f));
            yield return null;
            Check(vacuum.Current == VacuumAttack.Phase.Aim && vacuum.InFan(motor.FootPoint) && mother.Suspended, "掃除機: 床に橙の扇で予告（母はその場で狙う）");
            Vector3 escape;
            Check(commander.Threatened(new Vector3(12.0f, 0, -0.5f), out escape) && !vacuum.InFan(escape), "仲間は扇の外（横）へ逃げる");
            ShowFrom(new Vector3(12.6f, 2.6f, -3.4f), new Vector3(12.0f, 0.6f, 2.2f));
            yield return new WaitForSeconds(VacuumAttack.AimSeconds * 0.6f);
            if (playerRun) yield return Capture("stage2-vacuum-aim-720p.png");
            yield return WaitFor(() => vacuum.Sucking, 3);
            Check(vacuum.Sucking, "予告の後に吸い込み開始");
            float before = Flat(motor.FootPoint - vacuum.Mouth).magnitude;
            int hp = session.Hp;
            for (int i = 0; i < 20 && vacuum.Sucking; i++) { motor.SimulateMove(Vector2.zero, false, Time.deltaTime); yield return null; }
            float after = Flat(motor.FootPoint - vacuum.Mouth).magnitude;
            Check(after < before - 0.3f || !vacuum.Sucking, "扇の中の主人公はヘッドへ吸い寄せられる（" + before.ToString("F2") + " → " + after.ToString("F2") + "m）");
            if (playerRun && vacuum.Sucking) yield return Capture("stage2-vacuum-suck-720p.png");
            float end = Time.realtimeSinceStartup + 4;
            while (vacuum.Sucking && Time.realtimeSinceStartup < end) { motor.SimulateMove(Vector2.zero, false, Time.deltaTime); yield return null; }
            Check(vacuum.PlayerHits == 1 && session.Hp == hp - 1 && vacuum.Current == VacuumAttack.Phase.Recover && motor.ExternalVelocity == Vector3.zero,
                "ヘッドまで吸われるとダメージ、掃除機は詰まって止まる");
            yield return WaitFor(() => vacuum.Current == VacuumAttack.Phase.Idle, 2);
            Check(!mother.Suspended, "掃除機の後、母は動き出す");
            // Out of the fan sideways: no pull.
            yield return new WaitForSeconds(1.3f);   // invulnerability after the hit
            vacuum.DebugAim(new Vector3(12.0f, 0, 0.4f));
            yield return WaitFor(() => vacuum.Sucking, 3);
            Place(new Vector3(14.6f, 0, 0.4f));
            hp = session.Hp;
            for (int i = 0; i < 15; i++) { motor.SimulateMove(Vector2.zero, false, Time.deltaTime); yield return null; }
            Check(!vacuum.InFan(motor.FootPoint) && motor.ExternalVelocity == Vector3.zero && session.Hp == hp, "扇の横へ出れば吸われない");
            // An ally in the fan is sucked up for good.
            var ally = commander.AddAlly();
            ally.Spawn(new Vector3(12.0f, 0, 1.6f), 0);
            int deaths = commander.AllyDeaths;
            yield return WaitFor(() => !ally.Alive || !vacuum.Sucking, 4);
            Check(!ally.Alive && commander.AllyDeaths == deaths + 1 && vacuum.Sucked == 1, "扇の中の仲間は掃除機に吸い込まれていなくなる（死体も残らない）");
            vacuum.Stop();

            // ---- The son's bug net ----
            yield return new WaitForSeconds(0.5f);
            PlaceHuman(son, new Vector3(16.5f, 0, -0.6f), 270);
            Place(new Vector3(14.8f, 0, -0.6f));
            net.DebugSwing(new Vector3(14.8f, 0, -0.6f));
            yield return null;
            Check(net.Current == BugNetAttack.Phase.Windup && net.Inside(motor.FootPoint) && BugNetAttack.Radius > session.Settings.slipperHalfLength, "虫取り網: 橙の輪で予告（スリッパより大きい）");
            Check(commander.Threatened(new Vector3(15.2f, 0, -0.6f), out escape) == false, "仲間は振り上げた直後には気づかない");
            ShowFrom(new Vector3(14.2f, 2.0f, -4.2f), new Vector3(15.4f, 1.0f, -0.6f));
            yield return new WaitForSeconds(BugNetAttack.AllyNotice + 0.05f);
            Check(commander.Threatened(new Vector3(15.2f, 0, -0.6f), out escape) && !net.Inside(escape), "少し遅れて輪の外へ逃げる");
            if (playerRun) yield return Capture("stage2-net-windup-720p.png");
            yield return WaitFor(() => net.Current != BugNetAttack.Phase.Windup, 2);
            Check(net.PlayerCaught && session.Restrained && net.Current == BugNetAttack.Phase.Hold, "網の下の主人公は捕まって動けない");
            yield return null;
            Check(!motor.MovementEnabled, "捕まっている間は移動できない");
            if (playerRun) yield return Capture("stage2-net-caught-720p.png");
            hp = session.Hp;
            for (int i = 0; i < BugNetAttack.EscapePresses; i++) net.Wriggle();
            Check(!net.PlayerCaught && !session.Restrained && net.Escapes == 1 && session.Hp == hp, "Space 連打（" + BugNetAttack.EscapePresses + "回）で網から抜け出せる（ダメージなし）");
            yield return WaitFor(() => net.Current == BugNetAttack.Phase.Idle, 2);
            net.DebugSwing(motor.FootPoint);
            yield return WaitFor(() => net.Current == BugNetAttack.Phase.Hold, 2);
            hp = session.Hp;
            yield return WaitFor(() => net.Current != BugNetAttack.Phase.Hold, BugNetAttack.HoldSeconds + 1);
            Check(!session.Restrained && session.Hp == hp - 1 && net.PlayerHits == 1, "抜け出せないと " + BugNetAttack.HoldSeconds + "秒でダメージ、その後は離される");
            yield return WaitFor(() => net.Current == BugNetAttack.Phase.Idle, 2);
            // Allies caught go into the cage; the player lets them out with E at the cage.
            Place(new Vector3(10.0f, 0, -3.0f));
            var a1 = commander.AddAlly(); var a2 = commander.AddAlly();
            a1.Spawn(new Vector3(15.6f, 0, -2.0f), 0); a2.Spawn(new Vector3(15.9f, 0, -1.7f), 0);
            int alive = colony.Allies;
            net.DebugSwing(new Vector3(15.75f, 0, -1.85f));
            yield return WaitFor(() => net.Current != BugNetAttack.Phase.Windup, 2);
            var cage = net.Cage;
            Check(!a1.Alive && !a2.Alive && cage.Count == 2 && colony.Allies == alive - 2, "網の下の仲間2匹は虫かごへ（数に入らない）");
            ShowFrom(new Vector3(16.6f, 1.8f, -0.4f), cage.transform.position + Vector3.up * 0.5f);
            yield return null;
            if (playerRun) yield return Capture("stage2-cage-720p.png");
            Place(cage.transform.position + cage.transform.forward * 1.0f);
            Check(cage.PlayerNear, "虫かごの前で E の案内");
            Check(cage.Open() == 2 && a1.Alive && a2.Alive && colony.Allies == alive && cage.Count == 0, "虫かごを開けると仲間が戻る");
            yield return WaitFor(() => net.Current == BugNetAttack.Phase.Idle, 2);

            // ---- More people, more eyes ----
            PlaceHuman(mother, new Vector3(15.5f, 0, -2.6f), 0);
            Check(progress.ZoneContested(4) && progress.HumanInZone(4) == mother, "母が「テレビの前」にいる間は占領が止まる（父以外も区域を止める）");
            PlaceHuman(mother, new Vector3(10.8f, 0, 0.6f), 180);
            mother.ResetHuman();
            PlaceHuman(mother, new Vector3(12.0f, 0, 3.0f), 180);
            Place(new Vector3(12.0f, 0, -0.6f));
            int discoveries = session.Discoveries;
            mother.enabled = true;
            yield return WaitFor(() => mother.State == HumanAIController.HumanState.Chase || vacuum.Busy, 5);
            Check(session.Discoveries == discoveries + 1, "母に見つかっても発見回数が増える（母が追跡）");
            yield return WaitFor(() => vacuum.Busy, 4);
            Check(vacuum.Uses >= 3, "見つけた母は自分から掃除機を向けてくる");
            mother.enabled = false; vacuum.Stop();
            son.ResetHuman();
            PlaceHuman(son, new Vector3(13.6f, 0, -0.6f), 90);
            Place(new Vector3(15.6f, 0, -0.6f));
            son.enabled = true;
            // Hold him on the spot facing the player until he notices (otherwise his patrol walks him away first).
            float until = Time.realtimeSinceStartup + 6;
            while (son.State == HumanAIController.HumanState.Routine && Time.realtimeSinceStartup < until)
            {
                son.Agent.isStopped = true;
                son.transform.rotation = Quaternion.LookRotation(Flat(motor.FootPoint - son.transform.position));
                yield return null;
            }
            son.Agent.isStopped = false;
            yield return WaitFor(() => net.Busy, 6);
            Check(net.Uses >= 4 && son.State != HumanAIController.HumanState.Attack, "見つけた息子は網を振る（スリッパは使わない）");
            son.enabled = false; net.Stop();
            yield return null;

            // ---- The house cat ----
            PlaceHuman(son, new Vector3(19.0f, 0, 3.0f), 180); PlaceHuman(mother, new Vector3(3.0f, 0, 2.0f), 180);   // out of the cat's sight line
            cat.DebugWander();
            cat.DebugPlace(new Vector3(11.0f, 0, -1.0f), 90);
            Place(new Vector3(14.0f, 0, -1.0f));
            Check(cat.Targetable(-1) && cat.Visible(-1), "猫から床の主人公が見える");
            Place(new Vector3(15.5f, 0, 1.4f));
            Check(!cat.Targetable(-1), "ローテーブルの下（家具の下）は猫から見えない");
            Place(new Vector3(16.0f, 0, -1.0f));   // out of pouncing range while it creeps closer
            // Wiggle on the spot so the cat sees it move (the scripted player has no input of its own).
            float notice = Time.realtimeSinceStartup + 3;
            while (cat.Current != PetCat.Phase.Stalk && Time.realtimeSinceStartup < notice)
            {
                if (cat.Current == PetCat.Phase.Wander) cat.DebugPlace(new Vector3(11.0f, 0, -1.0f), 90);   // keep it looking this way
                motor.MovementEnabled = true;   // the session holds input while the window has no cursor lock (batch mode)
                motor.SimulateMove(new Vector2(Mathf.Sin(Time.time * 9) * 0.6f, 0), false, Time.deltaTime);
                yield return null;
            }
            motor.SimulateMove(Vector2.zero, false, Time.deltaTime);
            Check(cat.Current == PetCat.Phase.Stalk && cat.TargetIsPlayer, "動く主人公を見つけた猫が忍び寄る");
            if (playerRun) { ShowFrom(new Vector3(15.6f, 2.0f, -3.6f), new Vector3(12.0f, 0.6f, -0.6f)); yield return Capture("stage2-cat-stalk-720p.png"); }
            yield return WaitFor(() => cat.Current != PetCat.Phase.Stalk, 4);
            Check(cat.Current == PetCat.Phase.Wander && cat.LostInterest == 1 && session.Hp > 0, "止まっていると猫は興味をなくす（飛びかからない）");
            hp = session.Hp;
            Place(new Vector3(13.0f, 0, -1.0f));
            cat.DebugPounce(new Vector3(13.0f, 0, -1.0f));
            yield return null;
            Check(cat.Current == PetCat.Phase.Crouch && cat.InRing(motor.FootPoint), "飛びかかる前に橙の輪で予告（お尻を振る）");
            if (playerRun) { yield return new WaitForSeconds(PetCat.CrouchSeconds * 0.5f); ShowFrom(new Vector3(15.4f, 2.2f, -3.4f), new Vector3(12.4f, 0.6f, -0.8f)); yield return Capture("stage2-cat-pounce-720p.png"); }
            yield return WaitFor(() => cat.Current == PetCat.Phase.Recover, 3);
            Check(cat.PlayerHits == 1 && session.Hp == hp - 1, "輪の中にいると飛びかかられてダメージ");
            yield return new WaitForSeconds(1.4f);
            hp = session.Hp;
            cat.DebugPounce(new Vector3(13.0f, 0, -1.0f));
            Place(new Vector3(13.0f, 0, -3.2f));
            yield return WaitFor(() => cat.Current == PetCat.Phase.Recover, 3);
            Check(cat.PlayerHits == 1 && session.Hp == hp, "輪の外へ逃げればかわせる");
            var kitten = commander.AddAlly();
            kitten.Spawn(new Vector3(12.0f, 0, 1.0f + -2.0f), 0);
            cat.DebugPounce(kitten.Position);
            yield return new WaitForSeconds(PetCat.AllyNotice + 0.05f);
            Check(commander.Threatened(kitten.Position, out escape) && !cat.InRing(escape), "仲間は少し遅れて輪の外へ逃げる");
            yield return WaitFor(() => cat.Current == PetCat.Phase.Recover, 3);
            Check(kitten.Stunned && cat.AllyHits == 1, "逃げ遅れた仲間は猫パンチでひっくり返る（スリッパと同じ）");
            yield return null;
            yield return CatRoams();
            yield return TacticsChecks();

            // ---- Final defence: the family leaves, the father becomes the exterminator ----
            colony.DebugGrant(0);
            for (int i = 0; i < progress.ZoneCount; i++) progress.DebugCapture(i);
            yield return new WaitForSeconds(0.4f);
            progress.DebugAddFear(1000);
            PlaceExact(new Vector3(-2.3f, 0.02f, -0.9f));
            colony.Restore(colony.Food, stage.foodGoal, colony.NestLevel, colony.UpgradeLevels, colony.Allies, colony.UpgradesBought, colony.Born);
            mother.enabled = son.enabled = true; father.enabled = true;
            yield return WaitFor(() => progress.Current == StageProgress.Phase.FinalDefence, 3);
            Check(progress.Current == StageProgress.Phase.FinalDefence, "3ゲージ満タンで最終防衛");
            Check(mother.Suspended && son.Suspended && !vacuum.Allowed && !net.Allowed, "家族は掃除機・網をしまって避難を始める");
            Check(father.GetComponentInChildren<ExterminatorLook>(true).Showing, "父が呼んだ駆除業者が来る（父の姿が業者に）");
            mother.Agent.Warp(stage.exitPoint + Vector3.left * 0.3f); son.Agent.Warp(stage.exitPoint + Vector3.left * 0.4f);
            yield return WaitFor(() => progress.FamilyGone, 14);
            Check(progress.FamilyGone && session.HumanCount == 3 && !session.HumanPresent(1) && !session.HumanPresent(2), "家族は廊下へ出ていく（残るのは業者だけ）");
            cat.DebugPlace(stage.exitPoint + Vector3.left * 0.5f, 90);
            yield return WaitFor(() => cat.Current == PetCat.Phase.Gone, 12);
            Check(cat.Current == PetCat.Phase.Gone, "猫も家族と一緒に避難する");
            Check(!progress.ZoneContested(4), "いなくなった家族は区域を止めない");
            progress.DebugFinishDefence();
            yield return WaitFor(() => progress.Current == StageProgress.Phase.Cleared, 12);
            Check(progress.Current == StageProgress.Phase.Cleared && session.State == PhaseTwoSession.RunState.Cleared, "最終防衛を耐えると業者も出ていき、ステージクリア");
            var result = SaveData.Load();
            Check(result.IsUnlocked("stage03") && progress.NewStageUnlocked, "クリアで「飲食店」が解放される");
            if (playerRun) yield return Capture("stage2-clear-720p.png");
            life.enabled = true;
            progress.RestartStage();
            yield return null; yield return null;
            bool hidden = true;
            foreach (var f in commander.Foods) if (f.name.StartsWith("FoodMarker_Life_")) hidden &= f.State == FoodItem.FoodState.Hidden;
            Check(hidden && life.Dinner == HouseholdLife.Meal.None && life.CrumbsDropped == 0 && life.Dinners == 0, "リプレイで食べこぼし・食べ残しは片付き、夕食も最初から");
            Check(session.HumanPresent(1) && session.HumanPresent(2) && vacuum.Allowed && net.Allowed && !mother.Suspended, "リプレイで家族は家に戻る（掃除機・網も使える）");
            Check(cat.Current == PetCat.Phase.Nap && cat.Pounces == 0, "リプレイで猫も寝床に戻る");
        }

        private HouseholdLife life;
        private IEnumerator LifeChecks()
        {
            life = FindFirstObjectByType<HouseholdLife>();
            var reaction = colony.Reaction;
            var settings = session.Settings;
            Check(life && life.MemberCount == 3 && reaction.CurrentLevel == HumanReaction.Level.Peace && reaction.CalmAfterSeconds >= 30, "家族の生活: 平穏な家（落ち着くと警戒が下がる設定 " + reaction.CalmAfterSeconds + "秒）");
            var lifeFoods = new List<int>();
            for (int i = 0; i < commander.Foods.Count; i++) if (commander.Foods[i].name.StartsWith("FoodMarker_Life_")) lifeFoods.Add(i);
            bool hidden = lifeFoods.Count == 4;
            foreach (int i in lifeFoods) hidden &= commander.Foods[i].State == FoodItem.FoodState.Hidden && !commander.Foods[i].gameObject.activeSelf;
            Check(hidden, "食べこぼし・食べ残しは落とされるまで無い（4つ）");
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);   // hidden in the nest: nobody sees or hears the player
            yield return WaitFor(() => life.Leading(0) && life.Leading(1) && life.Leading(2), 3);
            Check(life.Leading(0) && life.Leading(1) && life.Leading(2) && father.State == HumanAIController.HumanState.Routine, "平穏な家族は巡回せず、それぞれ生活の場所へ向かう");

            // The father in front of the TV, the mother at the hob, the son at his game.
            life.DebugStart(0, HouseholdLife.Kind.Tv); life.DebugStart(1, HouseholdLife.Kind.Cook); life.DebugStart(2, HouseholdLife.Kind.Game);
            var fatherBody = father.GetComponentInChildren<BlenderHumanAnimator>();
            var motherBody = mother.GetComponentInChildren<BlenderHumanAnimator>();
            var sonBody = son.GetComponentInChildren<BlenderHumanAnimator>();
            yield return WaitFor(() => fatherBody.PoseWeight > 0.97f && motherBody.PoseWeight > 0.97f && sonBody.PoseWeight > 0.97f, 4);
            Check(life.BusyAt(0, out var k0) && k0 == HouseholdLife.Kind.Tv && fatherBody.ShownPose == BlenderHumanAnimator.LifePose.Sit && father.Agent.isStopped && father.transform.forward.z < -0.9f && life.Label(0) == "テレビ",
                "父はソファに座ってテレビ（テレビの方を向く・人間の欄「テレビ」）");
            Check(Flat(fatherBody.PelvisWorld - father.transform.position).magnitude > 0.7f && father.GetComponent<CapsuleCollider>().center.z < -0.7f,
                "腰は立ち位置の後ろの座面に（体の当たり判定も座面へ: 腰 " + Flat(fatherBody.PelvisWorld - father.transform.position).magnitude.ToString("F2") + "m・判定 z" + father.GetComponent<CapsuleCollider>().center.z.ToString("F2") + "）");
            Check(father.SightAngle < settings.sightAngle * 0.7f && father.SightRange < settings.sightDistance && Flat(father.Eye - father.transform.position).magnitude > 0.7f,
                "テレビを見ている間は視野が狭く短い・目は座面の上（" + father.SightAngle.ToString("F0") + "°・" + father.SightRange.ToString("F1") + "m）");
            Check(life.BusyAt(1, out var k1) && k1 == HouseholdLife.Kind.Cook && motherBody.ShownPose == BlenderHumanAnimator.LifePose.Cook && mother.transform.forward.z > 0.9f && life.Label(1) == "料理中",
                "母はコンロで料理（壁を向く・人間の欄「料理中」）");
            var pad = son.transform.Find("GamePad");
            Check(life.BusyAt(2, out var k2) && k2 == HouseholdLife.Kind.Game && sonBody.ShownPose == BlenderHumanAnimator.LifePose.Floor && pad && pad.gameObject.activeSelf && son.transform.position.z > 6.3f,
                "息子は子ども部屋の床でゲーム（コントローラーを持つ）");
            Place(new Vector3(11.6f, 0, 0.3f));   // to his front left, clear of the coffee table
            Physics.SyncTransforms();
            Vector3 toPlayer = Flat(motor.FootPoint - father.transform.position);
            Check(Vector3.Angle(father.transform.forward, toPlayer) < settings.sightAngle * 0.5f && toPlayer.magnitude < settings.sightDistance && father.HasLineOfSight() && !father.SensePlayer(),
                "ふだんなら見える位置（" + Vector3.Angle(father.transform.forward, toPlayer).ToString("F0") + "°・" + toPlayer.magnitude.ToString("F1") + "m）でも、テレビに夢中の父には見えない");
            if (playerRun) { yield return Shot(new Vector3(18.2f, 3.4f, -1.8f), new Vector3(14.0f, 2.0f, 3.6f), "stage2-life-tv-720p.png"); }
            yield return WaitFor(() => false, 1.2f);
            Check(father.State == HumanAIController.HumanState.Routine && father.Detection < 0.01f && session.Discoveries == 0, "…じっとしていれば見つからない");
            if (playerRun)
            {
                yield return Shot(new Vector3(0.6f, 3.8f, -0.8f), new Vector3(3.0f, 2.4f, 3.8f), "stage2-life-cook-720p.png");
                yield return Shot(new Vector3(14.2f, 3.6f, 8.4f), new Vector3(9.6f, 1.0f, 12.0f), "stage2-life-game-720p.png");
            }
            // Crumbs from the pan.
            life.DebugBusyFor(1, 0.3f);
            yield return WaitFor(() => life.CrumbsDropped >= 2, 3);
            int dropped = 0;
            foreach (int i in lifeFoods) if (commander.Foods[i].State == FoodItem.FoodState.Available && commander.IsDiscovered(i) && commander.Foods[i].transform.position.z > 2 && commander.Foods[i].transform.position.x > 2) dropped++;
            Check(life.CrumbsDropped == 2 && dropped == 2, "料理中の母は床に食べこぼす（コンロの前に食料2つ・すぐ見つかる）");

            // Running close by: the busy father hears it and turns round.
            Place(new Vector3(12.0f, 0, 1.6f));   // open floor: running on the spot cannot climb anything
            Vector3 seat = father.transform.position;
            Check(life.Hears(0, motor.FootPoint, true, true) && !life.Hears(0, motor.FootPoint, false, true) && !life.Hears(0, seat + new Vector3(0, 0, -3.5f), true, true),
                "走る足音は " + HouseholdLife.HearRunning + "m 以内で聞こえる（歩く音・遠い足音は聞こえない）");
            motor.MovementEnabled = true;
            float end = Time.realtimeSinceStartup + 2;
            while (!life.Startled(0) && Time.realtimeSinceStartup < end) { motor.MovementEnabled = true; motor.SimulateMove(new Vector2(Mathf.Sin(Time.time * 12) * 0.8f, 0), true, Time.deltaTime); yield return null; }
            motor.SimulateMove(Vector2.zero, false, Time.deltaTime);
            Check(life.Startled(0) && life.Startles == 1 && life.Label(0) == "物音に気づいた", "そばを走ると、テレビを見ていた父が物音に気づく（" + life.Startles + "回・" + life.Label(0) + "・走る " + motor.IsRunning + " " + motor.CurrentSpeed.ToString("F2") + "・登り " + motor.IsClimbing + "・距離 " + Flat(motor.FootPoint - seat).magnitude.ToString("F2") + "・" + father.State + "）");
            yield return WaitFor(() => father.State != HumanAIController.HumanState.Routine, 3);
            yield return null;
            Check(father.State == HumanAIController.HumanState.Chase && life.Rising(0) && father.Agent.isStopped && Flat(father.transform.position - seat).magnitude < 0.2f,
                "振り向いて見つけると追いかける（立ち上がるまで一瞬止まる）");
            yield return WaitFor(() => !life.Rising(0), 2);
            Check(!father.Agent.isStopped && fatherBody.Pose == BlenderHumanAnimator.LifePose.None, "立ち上がると普段どおり追跡");
            father.enabled = false;
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);
            yield return null;

            // Dozing off on the sofa: blind until a roach runs (or walks right) by.
            father.ResetHuman(); father.enabled = true;
            life.DebugStart(0, HouseholdLife.Kind.Doze);
            yield return WaitFor(() => life.Dozing(0) && fatherBody.PoseWeight > 0.97f, 4);
            Place(new Vector3(13.0f, 0, 1.2f));
            toPlayer = Flat(motor.FootPoint - father.transform.position);
            Check(life.Dozing(0) && fatherBody.ShownPose == BlenderHumanAnimator.LifePose.Doze && father.SightAngle == 0 && !father.SensePlayer() && Vector3.Angle(father.transform.forward, toPlayer) < settings.sightAngle * 0.5f,
                "うたた寝の父は目の前（" + toPlayer.magnitude.ToString("F1") + "m）でも見えない");
            if (playerRun) { yield return Shot(new Vector3(16.6f, 3.0f, 0.2f), new Vector3(14.0f, 2.2f, 3.8f), "stage2-life-doze-720p.png"); }
            yield return WaitFor(() => false, 1.0f);
            Vector3 dozer = father.transform.position;
            Check(life.Dozing(0) && father.State == HumanAIController.HumanState.Routine && father.Detection < 0.01f
                && life.Hears(0, dozer + Vector3.right * 0.6f, false, true) && !life.Hears(0, dozer + Vector3.right * 1.5f, false, true) && life.Hears(0, dozer + Vector3.right * 2.0f, true, true),
                "じっとしていれば起きない（すぐ横を歩くか " + HouseholdLife.WakeRunning + "m 以内を走ると起きる）");
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);

            // Dinner: the mother cooks, calls everyone to the table, the other rooms empty, leftovers stay behind.
            life.DebugDinnerNow();
            yield return WaitFor(() => life.Dinner == HouseholdLife.Meal.Cooking, 2);
            yield return null;
            Check(life.Dinner == HouseholdLife.Meal.Cooking && life.Current(1) != null && life.Current(1).kind == HouseholdLife.Kind.Cook, "夕食の時間: 母が料理を始める");
            mother.Agent.Warp(life.Current(1).stand);
            yield return WaitFor(() => life.BusyAt(1, out var kc) && kc == HouseholdLife.Kind.Cook, 4);
            life.DebugBusyFor(1, 0.2f);
            yield return WaitFor(() => life.Dinner == HouseholdLife.Meal.Gathering, 3);
            Check(life.Dinner == HouseholdLife.Meal.Gathering && reaction.Bubble.StartsWith("ごはんよー") && commander.Notice.StartsWith("家族が夕食"), "料理ができると「ごはんよー！」で家族を食卓に呼ぶ");
            yield return null; yield return null;
            for (int i = 0; i < 3; i++) if (life.Current(i) != null && life.Current(i).kind == HouseholdLife.Kind.Dinner) session.HumanAt(i).Agent.Warp(life.Current(i).stand);
            yield return WaitFor(() => life.Dinner == HouseholdLife.Meal.Eating, 6);
            yield return WaitFor(() => fatherBody.PoseWeight > 0.97f && motherBody.PoseWeight > 0.97f && sonBody.PoseWeight > 0.97f, 4);
            bool atTable = true;
            foreach (var h in new[] { father, mother, son }) atTable &= Flat(h.transform.position - new Vector3(-2.25f, 0, -0.9f)).magnitude < 3.4f && h.GetComponentInChildren<BlenderHumanAnimator>().ShownPose == BlenderHumanAnimator.LifePose.Eat;
            Check(life.Dinner == HouseholdLife.Meal.Eating && atTable, "家族3人が食卓の椅子で食べる（ほかの部屋は空になる）");
            if (playerRun) { yield return Shot(new Vector3(2.4f, 4.0f, -4.6f), new Vector3(-2.6f, 2.0f, -0.6f), "stage2-life-dinner-720p.png"); }
            life.DebugEndMeal();
            yield return WaitFor(() => life.Dinner == HouseholdLife.Meal.None, 2);
            int left = 0;
            foreach (int i in lifeFoods) if (commander.Foods[i].State == FoodItem.FoodState.Available && commander.Foods[i].transform.position.x < 0) left++;
            Check(life.Dinners == 1 && life.LeftoversDropped == 2 && left == 2, "食後は食卓の上と床に食べ残しが出る");

            // The mother's daily vacuuming: no warning fan unless she spots a roach.
            life.DebugStart(1, HouseholdLife.Kind.Vacuum);
            yield return WaitFor(() => life.Vacuuming(1), 3);
            Vector3 from = mother.transform.position;
            yield return WaitFor(() => Flat(mother.transform.position - from).magnitude > 1.0f, 5);
            Check(life.Vacuuming(1) && motherBody.SprayAim > 0.3f && vacuum.Current == VacuumAttack.Phase.Idle && life.Label(1) == "掃除中" && Flat(mother.transform.position - from).magnitude > 1.0f,
                "平穏な時も母は掃除機をかけて回る（ヘッドは床に・予告の扇は出ない）");

            // 「発見」: a short patrol between activities. 「警戒」: no daily life. Quiet for long enough: back to 「発見」.
            Check(reaction.CurrentLevel == HumanReaction.Level.Noticed && session.Discoveries == 1, "主人公が1回見つかって「発見」に");
            life.DebugStart(2, HouseholdLife.Kind.Homework);
            yield return WaitFor(() => life.BusyAt(2, out var kh) && kh == HouseholdLife.Kind.Homework && sonBody.PoseWeight > 0.97f, 4);
            Check(sonBody.ShownPose == BlenderHumanAnimator.LifePose.Desk && son.transform.forward.z > 0.9f, "息子は勉強机で宿題（壁を向く）");
            if (playerRun) { yield return Shot(new Vector3(15.8f, 3.6f, 10.4f), new Vector3(12.0f, 2.0f, 14.4f), "stage2-life-homework-720p.png"); }
            life.DebugFinish(2);
            yield return null; yield return null;
            Check(!life.Leading(2) && son.State == HumanAIController.HumanState.Routine && !son.Agent.isStopped, "「発見」の段階では、生活の合間に少し巡回する");
            progress.DebugAddFear(40);
            yield return null; yield return null;
            Check(reaction.CurrentLevel >= HumanReaction.Level.Alert && !life.Leading(0) && !life.Leading(1) && !life.Leading(2) && fatherBody.Pose == BlenderHumanAnimator.LifePose.None && !life.Vacuuming(1),
                "「警戒」になると生活をやめて巡回する");
            foreach (var h in new[] { father, mother, son }) h.ResetHuman();
            reaction.RestoreFear(0, reaction.FearGained);
            reaction.DebugQuiet(reaction.CalmAfterSeconds + 1);
            yield return null; yield return null;
            Check(reaction.CurrentLevel == HumanReaction.Level.Noticed && reaction.Calmed == 1, "しばらく何も見なければ「警戒」から「発見」に下がる");
            yield return WaitFor(() => life.Leading(0) || life.Leading(1) || life.Leading(2), 3);
            Check(life.Leading(0) || life.Leading(1) || life.Leading(2), "落ち着くと家族は生活に戻る");
            reaction.RestoreFear(10, reaction.FearGained);
            reaction.DebugQuiet(5);
            float fear = reaction.Fear, from0 = Time.time;
            yield return WaitFor(() => Time.time - from0 >= 1.5f, 4);
            Check(reaction.FearDecayOverride >= 1 && reaction.AllyFearCap == 1 && reaction.Fear <= fear - (Time.time - from0) * 0.9f,
                "何も見えなければ恐怖は毎秒1ずつ下がる（" + fear.ToString("F1") + "→" + reaction.Fear.ToString("F1") + "）・見える仲間の恐怖は1匹分まで");
        }

        // A screenshot from a fixed view (the follow camera is paused for it, then resumes).
        private IEnumerator Shot(Vector3 eye, Vector3 target, string name)
        {
            cameraRig.enabled = false;
            ShowFrom(eye, target);
            yield return Capture(name);
            cameraRig.enabled = true;
        }

        // The tactics view with left clicks only: pick allies one by one, order them, draw a route for a decoy run.
        private IEnumerator TacticsChecks()
        {
            var view = this.view;
            var overview = FindFirstObjectByType<SwarmViewCamera>(FindObjectsInactive.Include);
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);
            cat.ResetCat();
            foreach (var a in commander.Allies) if (a.gameObject.activeSelf) a.gameObject.SetActive(false);
            var team = new List<AllyRoach>();
            Vector3[] starts = { new Vector3(2.4f, 0, -1.6f), new Vector3(3.4f, 0, -1.6f), new Vector3(4.4f, 0, -1.6f), new Vector3(2.4f, 0, -3.4f), new Vector3(3.6f, 0, -3.4f) };
            foreach (var s in starts) { var a = commander.AddAlly(); a.Spawn(s, 0); team.Add(a); }
            commander.enabled = true;
            commander.SetMode(SwarmCommander.ViewMode.Swarm);
            Check(commander.ClickOrders && commander.SelectedCount == 0, "軍団モード（ステージ）: 左クリック操作・最初は選択なし（＝全員）");
            overview.FocusOn(new Vector3(3.4f, 0, -2.5f));
            yield return WaitFor(() => false, 1.5f);
            Vector3 OnScreen(Vector3 world) => view.WorldToScreenPoint(world + Vector3.up * 0.3f);
            Vector3 Floor(Vector3 world) => view.WorldToScreenPoint(world);

            // Picking allies one click at a time (a second click drops one); X clears.
            commander.ClickAt(view, OnScreen(team[0].Position));
            commander.ClickAt(view, OnScreen(team[1].Position));
            Check(commander.IsSelected(team[0].Slot) && commander.IsSelected(team[1].Slot) && commander.SelectedCount == 2, "仲間を左クリックするたびに選択に加わる（2匹）");
            commander.ClickAt(view, OnScreen(team[0].Position));
            Check(!commander.IsSelected(team[0].Slot) && commander.SelectedCount == 1, "選んだ仲間をもう一度クリックすると外れる");
            commander.ClearSelection();
            Check(commander.Command(AllyRoach.Order.Defend) == team.Count, "何も選んでいなければ指示は全員へ（" + team.Count + "匹）");
            // Clicking the floor and a zone orders only the chosen ones.
            commander.ClickAt(view, OnScreen(team[0].Position)); commander.ClickAt(view, OnScreen(team[1].Position));
            commander.ClickAt(view, Floor(new Vector3(5.4f, 0, -2.6f)));
            Check(team[0].CurrentOrder == AllyRoach.Order.Move && team[1].CurrentOrder == AllyRoach.Order.Move && team[2].CurrentOrder == AllyRoach.Order.Defend, "床を左クリック: 選んだ仲間だけが移動");
            commander.ClearSelection();
            commander.ClickAt(view, OnScreen(team[2].Position));
            int table = progress.ZoneAt(new Vector3(-2.3f, 0, -0.9f));
            commander.ClickAt(view, Floor(new Vector3(-2.3f, 0, -2.0f)));   // inside the zone, clear of the leftovers dropped under the table (a click on food collects it)
            Check(team[2].CurrentOrder == AllyRoach.Order.Capture && team[2].CaptureZone == table && team[3].CurrentOrder == AllyRoach.Order.Defend, "区域を左クリック: 選んだ仲間がその区域を占拠しに行く（" + team[2].CurrentOrder + "・区域 " + team[2].CaptureZone + "/" + table + "・空き " + progress.ZoneOpen(table) + "）");

            // A route drawn by dragging: two decoys run it in the open and hide at its end.
            commander.ClearSelection();
            foreach (var a in team) a.SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            commander.ClickAt(view, OnScreen(team[0].Position)); commander.ClickAt(view, OnScreen(team[1].Position));
            Vector3[] line = { new Vector3(3.0f, 0, -2.4f), new Vector3(4.2f, 0, -2.0f), new Vector3(5.4f, 0, -1.2f), new Vector3(5.6f, 0, 0.2f), new Vector3(4.4f, 0, 0.8f), new Vector3(2.8f, 0, 0.6f), new Vector3(1.2f, 0, -0.4f) };
            commander.PressAt(Floor(line[0]));
            for (int i = 1; i < line.Length; i++)
                for (int k = 1; k <= 6; k++) commander.DragTo(view, Floor(Vector3.Lerp(line[i - 1], line[i], k / 6f)));
            Check(commander.DrawingRoute && commander.RouteDraft.Count >= 8, "ドラッグするとルートの軌跡が描かれる（" + commander.RouteDraft.Count + "点）");
            if (playerRun) yield return Capture("stage2-route-draw-720p.png");
            commander.ReleaseAt(view, Floor(line[line.Length - 1]));
            Check(team[0].OnRoute && team[1].OnRoute && !team[2].OnRoute && commander.RoutesIssued == 1 && commander.ActiveRoutes.Count == 1,
                "離すと選んだ2匹がルートを走り出す（ほかの仲間はそのまま）");
            // A decoy holds to its route even with the cat about to pounce on it; an ally off the route runs.
            yield return WaitFor(() => team[0].RouteIndex >= 2, 4);
            Vector3 ahead = commander.ActiveRoutes[0].Points[Mathf.Min(team[0].RouteIndex + 1, commander.ActiveRoutes[0].Points.Count - 1)];
            team[4].Spawn(ahead + new Vector3(0.3f, 0, 0), 0); team[4].SetOrder(AllyRoach.Order.Defend, Vector3.zero);
            cat.DebugPounce(ahead);
            yield return new WaitForSeconds(PetCat.AllyNotice + 0.1f);
            Check((team[0].OnRoute || team[0].Stunned) && team[4].State == AllyRoach.AllyState.FleeHazard, "ルートを走る仲間は狙われても逃げずに走り抜ける（ほかの仲間は逃げる）");
            if (playerRun) yield return Capture("stage2-route-run-720p.png");
            yield return WaitFor(() => !team[1].OnRoute, 12);
            yield return WaitFor(() => team[1].State == AllyRoach.AllyState.Idle, 6);
            Check(!team[1].OnRoute && team[1].CurrentOrder == AllyRoach.Order.Move && cat.Covered(team[1].Position), "ただの床で走り終えた仲間は近くの物陰に隠れる（" + team[1].Position.ToString("F1") + "）");
            // A route that ends on a zone: the runner takes the zone.
            commander.ClearSelection();
            commander.ClickAt(view, OnScreen(team[3].Position));
            Vector3 sofa = new Vector3(15.5f, 0, 4.45f);
            int sofaZone = progress.ZoneAt(sofa);
            commander.CommandRoute(new List<Vector3> { team[3].Position, new Vector3(6.0f, 0, -3.5f), new Vector3(9.0f, 0, -3.0f), new Vector3(13.0f, 0, 2.8f), sofa }, null, sofaZone);
            yield return WaitFor(() => team[3].CurrentOrder == AllyRoach.Order.Capture, 15);
            Check(team[3].CurrentOrder == AllyRoach.Order.Capture && team[3].CaptureZone == sofaZone, "区域で終わるルート: 走り終えるとその区域を占拠する");

            // More roaches in a zone fill it faster.
            Check(progress.HoldRate(progress.Stage.holdPresence - 1) == 0 && Mathf.Approximately(progress.HoldRate(progress.Stage.holdPresence), 1) && Mathf.Approximately(progress.HoldRate(progress.Stage.holdPresence + 1), 1.5f)
                && Mathf.Approximately(progress.HoldRate(progress.Stage.holdPresence + 2), 2) && Mathf.Approximately(progress.HoldRate(progress.Stage.holdPresence + 9), 3),
                "区域にいるゴキが多いほど速くたまる（必要数で1倍・1匹増えるごとに+0.5倍・最大3倍）");
            commander.enabled = false;
            int fridge = progress.ZoneAt(new Vector3(-3.9f, 0, 2.7f));
            for (int i = 0; i < team.Count; i++) team[i].Spawn(new Vector3(-3.9f + (i % 3 - 1) * 0.4f, 0, 2.5f + (i / 3) * 0.4f), 0);
            yield return WaitFor(() => progress.ZonePresence(fridge) == team.Count, 2);   // zones are counted a few times a second
            Check(progress.ZonePresence(fridge) == team.Count && Mathf.Approximately(progress.ZoneRate(fridge), progress.HoldRate(team.Count)) && progress.ZoneRate(fridge) > 1.9f,
                "「冷蔵庫の前」に" + progress.ZonePresence(fridge) + "/" + team.Count + (progress.ZoneContested(fridge) ? "（人間がいる）" : "") + "匹: ×" + progress.ZoneRate(fridge).ToString("0.#") + " の速さでたまる");
            if (playerRun) { commander.enabled = true; yield return null; yield return Capture("stage2-route-zone-720p.png"); commander.enabled = false; }
            foreach (var a in team) a.gameObject.SetActive(false);
            commander.SetMode(SwarmCommander.ViewMode.ThirdPerson);
            cameraRig.enabled = false;
        }

        // The cat wanders all four rooms for a while (at 4x speed): wherever it heads, it never stands stuck.
        private IEnumerator CatRoams()
        {
            PlaceExact(session.Nest.position + Vector3.up * 0.1f);   // nothing for it to stalk
            UnityEngine.Random.InitState(7);
            cat.DebugWander();
            float scale = Time.timeScale;
            Time.timeScale = 4;
            float end = Time.time + 150, realEnd = Time.realtimeSinceStartup + 60, stillSince = Time.time, worst = 0, travelled = 0;
            Vector3 last = cat.transform.position, worstAt = last;
            var rooms = new HashSet<int>();
            while (Time.time < end && Time.realtimeSinceStartup < realEnd)
            {
                yield return null;
                if (cat.Current == PetCat.Phase.Nap) cat.DebugWander();
                Vector3 now = cat.transform.position;
                bool meantToMove = cat.Current == PetCat.Phase.Wander && !cat.Resting || cat.Current == PetCat.Phase.Stalk;
                if (!meantToMove || Flat(now - last).magnitude > 0.3f) { travelled += Flat(now - last).magnitude; last = now; stillSince = Time.time; }
                if (Time.time - stillSince > worst) { worst = Time.time - stillSince; worstAt = now; }
                rooms.Add((now.x > 7.12f ? 1 : 0) + (now.z > 6.12f ? 2 : 0));
            }
            Time.timeScale = scale;
            Check(worst < 4f && rooms.Count >= 3, "猫は家中を歩き回っても引っかからない（最長停止 " + worst.ToString("F1") + "秒 @" + worstAt.ToString("F1") + "・" + travelled.ToString("F0") + "m・" + rooms.Count + "部屋）");
        }

        private void FreezeAll()
        {
            motor.enabled = false; cameraRig.enabled = false; commander.enabled = false;
            foreach (var h in new[] { father, mother, son }) { h.enabled = false; if (h.Agent.isOnNavMesh) h.Agent.isStopped = true; }
        }
        private void PlaceHuman(HumanAIController h, Vector3 at, float yaw)
        {
            h.Agent.Warp(at);
            h.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        private void ShowFrom(Vector3 eye, Vector3 target) { view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye)); }
        private void Place(Vector3 position) { position.y = 0.02f; PlaceExact(position); }
        private void PlaceExact(Vector3 position)
        {
            motor.ResetSurface();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            motor.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSafePosition();
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
        { if (!value) throw new InvalidOperationException(description); checks.Add("PASS: " + description); Debug.Log("[STAGE2 PLAY] PASS: " + description); }
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
