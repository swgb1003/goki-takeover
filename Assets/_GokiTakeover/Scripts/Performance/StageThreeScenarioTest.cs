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
        private HumanAIController chef;
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
            Check(session.HumanCount == 1 && chef.Label == "店主" && chef.Slaps && ReferenceEquals(chef.Floors, floors) && !chef.Agent.autoTraverseOffMeshLink, "人間は店主（お玉）・階段は自分で上り下りする");
            Check(floors.FloorOf(session.Player.position) == 1 && session.InSafeNest, "主人公は1Fの巣（洗い場のシンク下）から");

            FreezeChef();
            session.AddMaxHp(20);
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
                yield return Shot(new Vector3(1.6f, 0.9f, 1.4f), new Vector3(0.4f, 0.1f, 3.9f), "stage3-food-720p.png");
            }

            // ---- The diner food ----
            var foods = commander.Foods;
            Check(foods.Count == 29 && foods.All(f => FoodValue.IsDiner(f.GetComponent<FoodValue>().FoodKind)), "食料は町中華の10種（" + foods.Count + "か所）");
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
            if (playerRun) { cameraRig.ResetView(); yield return Capture("stage3-2f-arrive-720p.png"); }
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
            yield return WaitFor(() => Flat(ally.Position - new Vector3(50.0f, 0, 0.4f)).magnitude < 1.5f, 25);
            Check(floors.FloorOf(ally.Position) == 2 && Flat(ally.Position - new Vector3(50.0f, 0, 0.4f)).magnitude < 1.5f, "仲間は巣から2Fの宴会座敷まで歩いて行く（" + ally.Position.ToString("F1") + "）");
            ally.SetOrder(AllyRoach.Order.Move, new Vector3(2.0f, 0, 2.5f));
            yield return WaitFor(() => Flat(ally.Position - new Vector3(2.0f, 0, 2.5f)).magnitude < 1.5f, 25);
            Check(floors.FloorOf(ally.Position) == 1 && Flat(ally.Position - new Vector3(2.0f, 0, 2.5f)).magnitude < 1.5f, "2Fから1Fの厨房へも戻れる（" + ally.Position.ToString("F1") + "）");
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

            // ---- Final defence and the clear ----
            colony.DebugGrant(0);
            for (int i = 0; i < progress.ZoneCount; i++) progress.DebugCapture(i);
            yield return WaitForReal(0.4f);
            progress.DebugAddFear(1000);
            PlaceExact(new Vector3(-2.0f, 0.02f, -0.5f));
            colony.Restore(colony.Food, stage.foodGoal, colony.NestLevel, colony.UpgradeLevels, colony.Allies, colony.UpgradesBought, colony.Born);
            chef.enabled = true; chef.Agent.isStopped = false;
            yield return WaitFor(() => progress.Current == StageProgress.Phase.FinalDefence, 3);
            Check(progress.Current == StageProgress.Phase.FinalDefence, "3ゲージ満タンで最終防衛");
            progress.DebugFinishDefence();
            yield return WaitFor(() => progress.Current == StageProgress.Phase.Cleared, 25);
            Check(progress.Current == StageProgress.Phase.Cleared && session.State == PhaseTwoSession.RunState.Cleared, "最終防衛を耐えると店主は裏口から出ていき、ステージクリア");
            var result = SaveData.Load();
            Check(result.Record("stage03") != null, "クリアの記録（ステージ3）");
            if (playerRun) yield return Capture("stage3-clear-720p.png");
            progress.RestartStage();
            yield return null; yield return null;
            Check(floors.FloorOf(session.Player.position) == 1 && session.InSafeNest && !floors.Travelling && floors.FloorOf(chef.transform.position) == 1, "リプレイで主人公は1Fの巣・店主は厨房に戻る");
        }

        private static readonly Vector3 StageThreeStart = new Vector3(3.6f, 0, 2.4f), ChefStartStop = new Vector3(3.5f, 0, 2.5f);
        private void FreezeChef() { chef.enabled = false; if (chef.Agent.isOnNavMesh) chef.Agent.isStopped = true; }
        private void PlaceHuman(HumanAIController h, Vector3 at, float yaw) { h.Agent.Warp(at); h.transform.rotation = Quaternion.Euler(0, yaw, 0); }
        private IEnumerator Shot(Vector3 eye, Vector3 target, string name)
        {
            cameraRig.enabled = false;
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            yield return Capture(name);
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
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name));
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException(description); checks.Add("PASS: " + description); Debug.Log("[STAGE3 PLAY] PASS: " + description); }
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
