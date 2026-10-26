using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover.Editor
{
    /// <summary>Stage 3 「町中華」 checks of the generated scene (part of GenerateAndVerify).</summary>
    public static partial class PhaseFiveVerification
    {
        private static void VerifyStageThree(StageDefinition stage)
        {
            EditorSceneManager.OpenScene(StageThreeGenerator.ScenePath);
            var progress = Object.FindFirstObjectByType<StageProgress>();
            Check(Path.GetFileNameWithoutExtension(StageThreeGenerator.ScenePath) == stage.sceneName && new SerializedObject(progress).FindProperty("stage").objectReferenceValue == stage,
                "ステージ3のシーン名が定義と一致・進行がステージ3の定義を参照");
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && Object.FindObjectsByType<RoachMotor>(FindObjectsSortMode.None).Length == 1
                && Object.FindObjectsByType<AllyRoach>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 9, "ステージ3: カメラ1台・主人公1匹・仲間プール9匹");
            Check(!GameObject.Find("KitchenCabinet") && !GameObject.Find("OpenRefrigerator") && !GameObject.Find("WalnutDiningTable") && !GameObject.Find("GOKI_CinematicKitchen/Pendant"),
                "ステージ1の台所の家具・照明は残っていない");
            string[] rooms = { "Diner_Kitchen", "Diner_Hall", "Diner_DishRoom", "Diner_Pantry", "Diner_Banquet2F", "Diner_Staff2F" };
            Check(rooms.All(r => GameObject.Find("Room/" + r)) && GameObject.Find("Room/Diner_Kitchen/ChineseRange") && GameObject.Find("Room/Diner_Kitchen/WorkTable") && GameObject.Find("Room/Diner_Hall/Counter")
                && GameObject.Find("Room/Diner_Hall/Stairs1F") && GameObject.Find("Room/Diner_DishRoom/SinkCabinet") && GameObject.Find("Room/Diner_Pantry/WalkInFridge")
                && GameObject.Find("Room/Diner_Banquet2F/StairWell2F") && GameObject.Find("Room/Diner_Banquet2F/LowTable") && GameObject.Find("Room/Diner_Staff2F/Lockers"),
                "2階建ての町中華: 1F 厨房・客席（階段）・洗い場（巣）・食品庫、2F 宴会座敷（階段の降り口）・休憩室");
            var flicker = Coplanar(rooms.Select(r => "Room/" + r).ToArray());
            Check(flicker.Length == 0, "店の家具に同じ面で重なってちらつく板がない" + (flicker.Length > 0 ? ": " + string.Join(" / ", flicker.Take(20)) : ""));

            // ---- The owner-cook ----
            var session = Object.FindFirstObjectByType<PhaseTwoSession>();
            var humans = Object.FindObjectsByType<HumanAIController>(FindObjectsSortMode.None);
            var chef = session.Human;
            var mesh = chef.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh).FirstOrDefault(m => m && m.name.EndsWith("_Detail0"));
            Check(humans.Length == 3 && session.Family.Count == 2 && chef.Label == "店主" && chef.Slaps && chef.WeaponName == "お玉" && mesh && mesh.name == "Chef_Detail0" && mesh.bindposes.Length == 19 && mesh.vertexCount > 3000,
                "店主（新しいBlenderモデル・住人と同じ19本の骨・お玉で叩く）");
            var okami = session.Family[0]; var baito = session.Family[1];
            System.Func<HumanAIController, Mesh> lod0 = h => h.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh).FirstOrDefault(m => m && m.name.EndsWith("_Detail0"));
            Check(okami.Label == "おかみさん" && lod0(okami).name == "Okami_Detail0" && baito.Label == "バイト" && lod0(baito).name == "Baito_Detail0"
                && lod0(okami).bindposes.Length == 19 && lod0(baito).vertexCount > 3000, "おかみさんとバイト（新しいBlenderモデル・同じ19本の骨）");
            Check(okami.Slaps && okami.WeaponName == "お盆" && okami.StrikeBox && okami.StrikeHalfAcross > okami.StrikeHalfAlong * 2 && !baito.Slaps,
                "おかみさんはお盆で横長の四角を叩く・バイトは叩かない");
            Check(chef.GetComponent<HotWaterAttack>() && okami.GetComponent<AlcoholSprayAttack>() && baito.GetComponent<DeckBrushAttack>() && baito.GetComponent<MopDuty>() && Object.FindFirstObjectByType<WetFloor>()
                && Object.FindFirstObjectByType<WetFloor>().Capacity == StageThreeGenerator.WetPatches, "熱湯（店主）・アルコールスプレー（おかみさん）・デッキブラシとモップがけ（バイト）・濡れた床 " + StageThreeGenerator.WetPatches + "か所分");
            var warnings = humans.Select(h => new SerializedObject(h).FindProperty("warning").objectReferenceValue as Transform).ToArray();
            Check(warnings.Distinct().Count() == 3 && warnings[0] && ((Transform)new SerializedObject(okami).FindProperty("warning").objectReferenceValue).GetComponent<RectTelegraph>(),
                "叩く予告は1人に1つ（おかみさんは四角）");
            Check(humans.All(h => new SerializedObject(h.GetComponentInChildren<BlenderHumanAnimator>()).FindProperty("nozzleLocal").vector3Value.magnitude > 0.3f), "片手鍋・スプレー・デッキブラシの先は左手の先（狙える）");
            var rects = Object.FindObjectsByType<RectTelegraph>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(rects.Length == 3 && rects.All(r => !r.gameObject.activeSelf && r.GetComponentsInChildren<Collider>(true).Length == 0), "四角の予告3つ（お盆・熱湯・デッキブラシ、待機・当たり判定なし）");
            Check(!chef.GetComponentInChildren<ExterminatorLook>(true) && new SerializedObject(chef.GetComponentInChildren<BlenderHumanAnimator>()).FindProperty("nozzleLocal").vector3Value.magnitude > 0.5f,
                "店主はステージ1の業者の姿に変わらない・左手の片手鍋の先（狙える）");

            // ---- Floors ----
            var floors = Object.FindFirstObjectByType<FloorLinks>();
            Check(floors && floors.Links.Count == 2 && floors.Links[0].humans && !floors.Links[1].humans && floors.FloorOf(new Vector3(50, 0, 0)) == 2 && floors.FloorOf(new Vector3(20, 0, 0)) == 1,
                "階のつなぎ: 階段（人間も使う）とダクト（ゴキブリだけ）、x " + floors.SecondFloorX + " から先が2F");
            foreach (var link in floors.Links)
                Check(!link.a.trigger.Contains(link.a.arrive + Vector3.up * 0.2f) && !link.b.trigger.Contains(link.b.arrive + Vector3.up * 0.2f)
                    && floors.FloorOf(link.a.ground) == 1 && floors.FloorOf(link.b.ground) == 2, link.name + ": 着いた場所は入口の外（行き来を繰り返さない）・1Fと2Fをつなぐ");
            var commander = Object.FindFirstObjectByType<SwarmCommander>();
            var grid = commander.Grid;
            Check(new SerializedObject(commander).FindProperty("grid").objectReferenceValue == AssetDatabase.LoadAssetAtPath<AllyNavGrid>(StageThreeGenerator.GridPath)
                && grid.Width == StageThreeGenerator.GridWidth && grid.LinkCount == 2, "店全体の床グリッド（" + grid.Width + "×" + grid.Depth + "・つなぎ2本）");
            Check(!grid.Walkable(new Vector3(33, 0, 0)) && !grid.Walkable(new Vector3(40, 0, 10)) && !grid.Walkable(new Vector3(60, 0, StageThreeGenerator.StairZ)),
                "1Fと2Fの間・2Fの階段の降り口（穴）は床がない");
            var path = new List<Vector3>();
            foreach (var link in floors.Links)
                Check(grid.Walkable(link.a.ground) && grid.Walkable(link.b.ground) && grid.Walkable(link.a.arrive) && grid.Walkable(link.b.arrive), link.name + "の両端は仲間が歩ける床");
            Check(grid.FindPath(commander.NestGround, new Vector3(50, 0, 0.4f), path) && path.Any(p => p.x > floors.SecondFloorX) && path.Any(p => p.x < 30),
                "巣（1F 洗い場）から2Fの宴会座敷まで仲間の経路（つなぎを通る）");
            foreach (var zone in stage.zones)
                Check(grid.Walkable(zone.centre) && grid.FindPath(commander.NestGround, zone.centre, path), "巣から「" + zone.name + "」へ仲間の経路あり");
            Check(stage.zones.Count(z => floors.FloorOf(z.centre) == 2) == 2, "縄張りのうち2区域は2F");
            var rings = GameObject.Find("Phase5_Stage/Territory").GetComponentsInChildren<Transform>(true).Where(t => t.name == "ZoneRing").ToArray();
            Check(rings.Length == 10 && rings.All(r => r.position.y >= 0.04f), "区域の輪は畳・床の上（y ≥ 0.04）");

            // ---- Food ----
            var foods = commander.Foods;
            var kinds = foods.Select(f => f.GetComponent<FoodValue>().FoodKind).ToArray();
            Check(foods.Count == 29 && kinds.All(FoodValue.IsDiner) && kinds.Distinct().Count() == 10, "食料は町中華の10種だけ（" + foods.Count + "か所）");
            Check(foods.All(f => f.transform.Find("FoodModel") is Transform m && m.GetComponent<MeshFilter>().sharedMesh.name == FoodModelIntegration.DinerNames[(int)f.GetComponent<FoodValue>().FoodKind - FoodValue.FirstDinerKind]),
                "食料はそれぞれの種類のBlenderモデル");
            Check(FoodModelIntegration.DinerNames.All(n => AssetDatabase.LoadAssetAtPath<Mesh>(BlenderModelIntegration.Root + "/Meshes/" + n + ".asset") is Mesh m && m.vertexCount > 300 && m.colors.Length == m.vertexCount),
                "町中華の食料10種のメッシュ（頂点カラー付き）");
            Check(FoodValue.AmountOf(FoodValue.Kind.Tenkasu) == 1 && FoodValue.AmountOf(FoodValue.Kind.Gyoza) == 2 && FoodValue.AmountOf(FoodValue.Kind.DinerLeftovers) == 3
                && FoodValue.AmountOf(FoodValue.Kind.Chashu) == 4 && FoodValue.AmountOf(FoodValue.Kind.Shumai) == 5 && FoodValue.AmountOf(FoodValue.Kind.Breadcrumb) == 1 && FoodValue.AmountOf(FoodValue.Kind.Leftovers) == 3,
                "食料の値（天かす1・餃子2・食べ残し3・チャーシュー4・焼売のせいろ5、ステージ1・2の5種は従来どおり）");
            var floating = foods.Where(f => !Physics.Raycast(f.transform.position + Vector3.up * 0.05f, Vector3.down, out var hit, 0.35f, ~(1 << 2), QueryTriggerInteraction.Ignore)).Select(f => f.name).ToArray();
            Check(floating.Length == 0, "どの食料も床か家具の上に載っている" + (floating.Length > 0 ? ": " + string.Join(", ", floating) : ""));
            Check(foods.Count(f => floors.FloorOf(f.transform.position) == 2) == 9 && commander.ScoutPoints.All(p => grid.Walkable(p)) && commander.ScoutPoints.Count(p => floors.FloorOf(p) == 2) == 4,
                "2Fに食料9つ・調査地点4つ（調査地点はすべて床）");
            var low = foods.Where(f => f.transform.position.y < 0.5f).ToArray();
            Check(low.All(f => grid.Walkable(f.transform.position) || grid.NearestWalkable(f.transform.position, 1.0f, out _)), "床の食料はすべて仲間が取りに行ける");

            // ---- The nest ----
            Check(session.HasNestArea && session.NestArea.Contains(session.Nest.position + Vector3.up * 0.1f) && session.NestArea.Contains(session.Player.position + Vector3.up * 0.1f)
                && floors.FloorOf(session.Nest.position) == 1 && session.Nest.position.z > 14 && GameObject.Find("Room/Diner_DishRoom/SinkCabinet/GreaseTrap"),
                "巣は洗い場のシンク下（グリストラップのそば）、主人公もそこから開始");
            var smoking = new SerializedObject(progress);
            Check(smoking.FindProperty("nestSmoke").objectReferenceValue is GameObject smoke && smoke.transform.IsChildOf(GameObject.Find("Room/Diner_DishRoom/SinkCabinet").transform)
                && smoking.FindProperty("nestFogger").objectReferenceValue && grid.Walkable(StageThreeGenerator.NestRefuge), "最終防衛の巣燻し（シンク下の煙・避難先は作業台の下）");

            // ---- People: the NavMesh, the stairs, the patrol, the back door ----
            var navigation = (NavMeshData)new SerializedObject(Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            Check(navigation == AssetDatabase.LoadAssetAtPath<NavMeshData>(StageThreeGenerator.NavPath), "店全体の人間用NavMesh");
            var instance = NavMesh.AddNavMeshData(navigation);
            var links = new List<NavMeshLinkInstance>();
            try
            {
                foreach (var l in floors.Links.Where(l => l.humans))
                    links.Add(NavMesh.AddLink(new NavMeshLinkData { startPosition = l.a.ground, endPosition = l.b.ground, width = 0.9f, bidirectional = true, costModifier = 1 }));
                var walk = new NavMeshPath();
                bool Reach(Vector3 from, Vector3 to) => NavMesh.SamplePosition(to, out var hit, 1.2f, NavMesh.AllAreas) && NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, walk) && walk.status == NavMeshPathStatus.PathComplete;
                var stairs = floors.Links[0];
                Check(NavMesh.SamplePosition(stairs.a.ground, out var s0, 0.3f, NavMesh.AllAreas) && NavMesh.SamplePosition(stairs.b.ground, out var s1, 0.3f, NavMesh.AllAreas), "階段の上り口・降り口は人間の通り道");
                Check(!NavMesh.SamplePosition(new Vector3(21.5f, 3.0f, StageThreeGenerator.StairZ), out _, 0.6f, NavMesh.AllAreas), "階段のスロープは人間の通り道に入らない（人間はつなぎで上り下り）");
                Check(Reach(chef.transform.position, stage.exitPoint), "店主は裏口まで歩ける（撤退）");
                Check(Reach(chef.transform.position, new Vector3(50, 0, 0.4f)) && Reach(new Vector3(50, 0, 0.4f), chef.transform.position), "店主は階段で2Fへ行き来できる");
                var route = (Transform[])typeof(HumanAIController).GetField("patrolPoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(chef);
                Check(route.Length >= 12 && route.All(t => Reach(chef.transform.position, t.position)) && route.Count(t => floors.FloorOf(t.position) == 2) >= 3 && route.Any(t => t.position.z > 8)
                    && route.Any(t => t.position.x > 12 && t.position.x < 30), "店主の巡回 " + route.Length + "か所（厨房・客席・洗い場・食品庫・2F、すべて歩ける）");
                foreach (var member in session.Family)
                {
                    var stops = (Transform[])typeof(HumanAIController).GetField("patrolPoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(member);
                    Check(stops.Length >= 8 && stops.All(t => Reach(member.transform.position, t.position)) && stops.Any(t => floors.FloorOf(t.position) == 2) && stops.Intersect(route).Count() == 0
                        && Reach(member.transform.position, stage.exitPoint), member.Label + "の巡回 " + stops.Length + "か所（2Fも・すべて歩ける・自分の巡回地点）、裏口へ避難できる");
                }
            }
            finally { foreach (var l in links) NavMesh.RemoveLink(l); instance.Remove(); }
        }
    }
}
