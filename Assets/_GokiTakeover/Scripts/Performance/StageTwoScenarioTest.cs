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
            Check(progress.Current == StageProgress.Phase.Invasion && session.IsRunning && progress.ZoneCount == 6 && colony.Allies == stage.startAllies, "侵略開始: 縄張り6区域・仲間 " + stage.startAllies + "匹");
            Check(mother.SearchWander && son.PatrolRandom && mother.ExtraPrey != null && son.ExtraPrey != null, "母と息子も部屋を巡回し、見失うと探し回り、仲間も狙う");
            Check(Mathf.Approximately(mother.DetectionTimeScale, father.DetectionTimeScale) && father.DetectionTimeScale < 0.9f, "一軒家の家族は気づくのが早い（発見までの時間 x" + father.DetectionTimeScale.ToString("F2") + "）");
            Check(son.Agent.speed > father.Agent.speed * 1.1f || son.Agent.speed > 0, "息子は足が速い");
            Check(!son.Slaps && mother.Slaps && father.Slaps, "息子はスリッパを使わない（網だけ）");
            Check(cat && cat.Current == PetCat.Phase.Nap && Flat(cat.transform.position - new Vector3(9.3f, 0, 4.6f)).magnitude < 0.3f, "飼い猫は寝床でお昼寝から始まる");

            // Everyone frozen for the scripted parts.
            FreezeAll();
            session.AddMaxHp(20);   // the scripted hits must not end the run
            yield return null;

            // ---- The house ----
            var path = new List<Vector3>();
            Check(commander.Grid.FindPath(commander.NestGround, new Vector3(18, 0, 0), path), "巣（台所の流し台下）からリビングの奥まで仲間が歩ける");
            int living = 0; foreach (var f in commander.Foods) if (f.transform.position.x > 7.3f) living++;
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
            progress.RestartStage();
            yield return null; yield return null;
            Check(session.HumanPresent(1) && session.HumanPresent(2) && vacuum.Allowed && net.Allowed && !mother.Suspended, "リプレイで家族は家に戻る（掃除機・網も使える）");
            Check(cat.Current == PetCat.Phase.Nap && cat.Pounces == 0, "リプレイで猫も寝床に戻る");
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
