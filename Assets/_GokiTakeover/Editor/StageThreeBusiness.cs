using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Stage 3 step 3 (2026-10-26): the diner's day and its customers. DinerBusiness runs prepping, lunch, the break,
    /// dinner and closing; DinerCustomers seats the customers (three new Blender models, two of each, copies of the
    /// cook's body with the AI held still) at the tables, the counter and, at dinner, the banquet room's cushions.
    /// Each table has hidden leftovers a customer may leave and a plate shown while someone eats there.
    /// </summary>
    public static partial class StageThreeGenerator
    {
        public static readonly Vector3 Entrance = new Vector3(20.5f, 0, -5.2f);
        public static readonly string[] CustomerActors = { "Salaryman", "Student", "Grandma", "Salaryman", "Student", "Grandma" };
        private static readonly Dictionary<string, (string label, float speed)> CustomerKinds = new Dictionary<string, (string, float)>
        { { "Salaryman", ("サラリーマン", 1.0f) }, { "Student", ("学生", 1.15f) }, { "Grandma", ("おばあさん", 0.75f) } };
        // Tables: the four in the hall, the counter, two low tables upstairs. The top centre (leftovers, the plate).
        public static readonly Vector3[] TableTops =
        {
            new Vector3(15.5f, 2.62f, -3.8f), new Vector3(21.5f, 2.62f, -3.8f), new Vector3(15.5f, 2.62f, 0.6f), new Vector3(21.5f, 2.62f, 0.6f),
            new Vector3(10.2f, 2.68f, 2.3f), new Vector3(49.0f, 1.11f, -2.0f), new Vector3(54.5f, 1.11f, -2.0f)
        };
        // Business food: gyoza laid out while prepping (the work table), leftovers per table (hidden until left there).
        public static readonly Vector3[] PrepGyoza = { new Vector3(-1.6f, 2.77f, -1.0f), new Vector3(-2.5f, 2.77f, 0.0f) };
        public static readonly Vector3 DinnerShumai = new Vector3(48.4f, 1.18f, -2.0f);
        private static readonly Vector3[] TrapSpots =
        {
            new Vector3(5.0f, 0, -4.0f), new Vector3(13.6f, 0, -1.8f), new Vector3(-1.5f, 0, 9.0f), new Vector3(18.6f, 0, -1.6f), new Vector3(10.5f, 0, 8.0f),
            new Vector3(51.5f, 0, -4.2f), new Vector3(2.0f, 0, 2.6f), new Vector3(25.0f, 0, 0.5f), new Vector3(48.5f, 0, 12.5f)
        };
        // Rounds for each part of the day.
        private static readonly Vector3[] KitchenRound = { new Vector3(3.5f, 0, 2.5f), new Vector3(5.6f, 0, -2.0f), new Vector3(-4.5f, 0, 2.4f), new Vector3(-4.3f, 0, -2.6f), new Vector3(0.5f, 0, -3.8f), new Vector3(6.5f, 0, 2.0f) };
        private static readonly Vector3[] DishRound = { new Vector3(-2.0f, 0, 11.5f), new Vector3(2.5f, 0, 12.6f), new Vector3(-4.5f, 0, 9.0f) };
        private static readonly Vector3[] PantryRound = { new Vector3(9.5f, 0, 10.5f), new Vector3(12.0f, 0, 8.6f) };
        private static readonly Vector3[] HallRound = { new Vector3(13.5f, 0, -1.5f), new Vector3(19.0f, 0, -1.6f), new Vector3(24.5f, 0, 1.8f), new Vector3(14.6f, 0, 3.0f), new Vector3(12.6f, 0, -4.6f), new Vector3(24.0f, 0, -3.4f) };
        private static readonly Vector3[] BanquetRound = { new Vector3(50.0f, 0, 0.4f), new Vector3(56.0f, 0, -3.5f), new Vector3(47.0f, 0, -4.0f) };
        private static readonly Vector3[] StaffRoomRound = { new Vector3(50.0f, 0, 11.5f), new Vector3(53.0f, 0, 13.0f), new Vector3(47.5f, 0, 13.4f) };

        /// <summary>The extra food piles the day needs (made with the rest in Generate): kind, place, name.</summary>
        private static IEnumerable<(FoodValue.Kind kind, Vector3 at, string name)> BusinessFood()
        {
            for (int i = 0; i < PrepGyoza.Length; i++) yield return (FoodValue.Kind.Gyoza, PrepGyoza[i], "FoodMarker_Prep_" + i);
            for (int t = 0; t < TableTops.Length; t++) yield return (FoodValue.Kind.DinerLeftovers, TableTops[t] + new Vector3(0.5f, 0.07f, 0.1f), "FoodMarker_Leftover_" + t);
        }

        private static void BuildBusiness(Transform stageRoot, PhaseTwoSession session, SwarmCommander commander, ColonyManager colony, StageProgress progress, HumanAIController chef, Transform room)
        {
            var floors = stageRoot.GetComponent<FloorLinks>();
            var foods = commander.Foods;
            FoodItem Named(string n) => foods.First(f => f.name == n);

            // ---- Customers: copies of the cook's body wearing the customer models, the AI held still ----
            var people = new DinerCustomer[CustomerActors.Length];
            for (int i = 0; i < people.Length; i++)
            {
                string actor = CustomerActors[i];
                var body = CloneStaff(chef, "Customer_" + actor + "_" + i, Entrance + new Vector3(i * 0.6f - 1.5f, 0, 0.6f), 0, actor);
                foreach (var a in body.GetComponents<HouseholdAttack>()) UnityEngine.Object.DestroyImmediate(a);
                var kind = CustomerKinds[actor];
                body.ConfigureMember(kind.label, false, 3.4f, kind.speed);
                body.ConfigureWeapon("", 0, 0, false);
                EditorUtility.SetDirty(body);
                foreach (var t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;   // never cover nor a wall for sight lines
                people[i] = body.gameObject.AddComponent<DinerCustomer>();
                people[i].Configure(kind.label, kind.speed);
            }

            // ---- Seats and tables ----
            var seats = new List<DinerCustomers.Seat>();
            for (int t = 0; t < 4; t++)
            {
                Vector3 c = TableTops[t]; c.y = 0;
                foreach (var (dx, dz) in new[] { (-0.75f, -1.45f), (0.75f, 1.45f) })
                {
                    float side = Mathf.Sign(dx), yaw = dz < 0 ? 0 : 180;
                    seats.Add(new DinerCustomers.Seat
                    {
                        stand = c + new Vector3(side * 1.9f, 0, dz), yaw = yaw, seat = c + new Vector3(dx, 1.62f, dz),
                        hands = c + new Vector3(dx, 2.66f, dz * 0.4f), look = c + new Vector3(-dx, 3.2f, -dz), table = t
                    });
                }
            }
            foreach (float z in new[] { -0.4f, 1.35f, 3.1f })   // counter stools, facing the hatch
                seats.Add(new DinerCustomers.Seat
                {
                    stand = new Vector3(12.95f, 0, z + 0.55f), yaw = 270, seat = new Vector3(11.9f, 2.02f, z), hands = new Vector3(10.6f, 2.72f, z), look = new Vector3(8.5f, 3.6f, z), table = 4
                });
            for (int t = 5; t < 7; t++)   // banquet cushions (dinner only), sitting on the floor
            {
                Vector3 c = TableTops[t]; c.y = 0;
                foreach (var (dx, dz) in new[] { (-0.6f, -1.65f), (0.6f, 1.65f) })
                    seats.Add(new DinerCustomers.Seat
                    {
                        stand = c + new Vector3(dx, 0, dz + Mathf.Sign(dz) * 0.15f), yaw = dz < 0 ? 0 : 180, seat = c + new Vector3(dx, 0.2f, dz),
                        hands = c + new Vector3(dx, 1.15f, dz * 0.45f), look = c + new Vector3(-dx, 1.6f, -dz), pose = BlenderHumanAnimator.LifePose.Floor, table = t, upstairs = true
                    });
            }
            // Stand points snapped onto the NavMesh (beside the chair, where a person fits).
            WithNav(room, () =>
            {
                foreach (var s in seats)
                {
                    if (!NavMesh.SamplePosition(s.stand, out var hit, 0.9f, NavMesh.AllAreas)) throw new InvalidOperationException("客の席の横に立てません: " + s.stand);
                    s.stand = hit.position;
                }
            });
            var plateMaterial = P.Material("Stage03_Plate", new Color(0.93f, 0.92f, 0.88f), 0.6f);
            var mealMaterial = P.Material("Stage03_Meal", new Color(0.74f, 0.45f, 0.18f), 0.4f);
            var tables = new DinerCustomers.Table[TableTops.Length];
            for (int t = 0; t < tables.Length; t++)
            {
                var plate = GeneratedAssets.Empty("Plate_" + t, stageRoot, TableTops[t] + new Vector3(-0.3f, 0.02f, 0));
                P.Sphere("Dish", plate, Vector3.zero, new Vector3(0.75f, 0.05f, 0.75f), plateMaterial);
                P.Sphere("Food", plate, new Vector3(0, 0.07f, 0), new Vector3(0.45f, 0.14f, 0.45f), mealMaterial);
                P.Rod("Chopsticks", plate, new Vector3(0.35f, 0.04f, -0.2f), new Vector3(0.35f, 0.04f, 0.3f), 0.03f, darkWood);
                foreach (var tr in plate.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
                plate.gameObject.SetActive(false);
                tables[t] = new DinerCustomers.Table { leftovers = Named("FoodMarker_Leftover_" + t), plate = plate.gameObject };
            }
            var customers = stageRoot.gameObject.AddComponent<DinerCustomers>();
            customers.Configure(session, commander, colony, floors, people, seats.ToArray(), tables, Entrance);

            // ---- The day ----
            Transform[] Round(string name, params Vector3[][] parts) => MakeStops(chef.transform.parent, name, parts.SelectMany(p => p).ToArray());
            var chefRounds = new DinerBusiness.Rounds
            {
                prep = Round("Round_Chef_Prep_", KitchenRound), lunch = Round("Round_Chef_Lunch_", KitchenRound),
                rest = Round("Round_Chef_Rest_", KitchenRound, DishRound, PantryRound), dinner = Round("Round_Chef_Dinner_", KitchenRound),
                closed = Round("Round_Chef_Closed_", KitchenRound, HallRound, DishRound, PantryRound, BanquetRound, StaffRoomRound)
            };
            var okamiRounds = new DinerBusiness.Rounds
            {
                prep = Round("Round_Okami_Prep_", HallRound), lunch = Round("Round_Okami_Lunch_", HallRound),
                rest = Round("Round_Okami_Rest_", HallRound, BanquetRound, new[] { KitchenRound[5] }), dinner = Round("Round_Okami_Dinner_", HallRound, BanquetRound),
                closed = Round("Round_Okami_Closed_", HallRound)
            };
            var baitoRounds = new DinerBusiness.Rounds
            {
                prep = Round("Round_Baito_Prep_", KitchenRound, DishRound), lunch = Round("Round_Baito_Lunch_", DishRound, HallRound.Take(3).ToArray()),
                rest = Round("Round_Baito_Rest_", DishRound, PantryRound, StaffRoomRound), dinner = Round("Round_Baito_Dinner_", DishRound, HallRound.Take(3).ToArray(), BanquetRound),
                closed = Round("Round_Baito_Closed_", DishRound)
            };
            var lights = room.GetComponentsInChildren<Light>(true).Where(l => l.name != "NestGlow").ToList();
            var key = GameObject.Find("WindowKey_OneShadowMap");
            if (key) lights.Add(key.GetComponent<Light>());
            var torch = GeneratedAssets.Empty("Torch", chef.transform, new Vector3(0.3f, 2.9f, 0.4f)).gameObject.AddComponent<Light>();
            torch.transform.localRotation = Quaternion.Euler(28, 0, 0);
            torch.type = LightType.Spot; torch.spotAngle = 55; torch.range = 10; torch.intensity = 3.2f; torch.color = new Color(1f, 0.95f, 0.82f);
            torch.shadows = LightShadows.None; torch.renderMode = LightRenderMode.ForcePixel; torch.enabled = false;
            var prep = PrepGyoza.Select((p, i) => Named("FoodMarker_Prep_" + i)).ToArray();
            var dinner = foods.Where(f => (f.transform.position - DinnerShumai).sqrMagnitude < 0.01f).ToArray();
            if (dinner.Length != 1) throw new InvalidOperationException("ディナーの焼売のせいろが見つかりません");
            var business = stageRoot.gameObject.AddComponent<DinerBusiness>();
            var baito = session.Family[1];
            business.Configure(session, progress, commander, colony, customers, baito.GetComponent<MopDuty>(), chefRounds, okamiRounds, baitoRounds, prep, dinner,
                lights.ToArray(), torch, TrapSpots, ExitPoint);
        }

        /// <summary>Patrol stops snapped onto the NavMesh, named for a part of the day.</summary>
        private static Transform[] MakeStops(Transform parent, string prefix, Vector3[] points)
        {
            var navigation = (NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            var instance = NavMesh.AddNavMeshData(navigation);
            try
            {
                var stops = new Transform[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    if (!NavMesh.SamplePosition(points[i], out var hit, 1.2f, NavMesh.AllAreas)) throw new InvalidOperationException("巡回地点が人間の通り道にありません: " + points[i]);
                    stops[i] = GeneratedAssets.Empty(prefix + i, parent, Vector3.zero);
                    stops[i].position = hit.position;
                }
                return stops;
            }
            finally { instance.Remove(); }
        }
        private static void WithNav(Transform room, Action work)
        {
            var navigation = (NavMeshData)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<KitchenNavigation>()).FindProperty("data").objectReferenceValue;
            var instance = NavMesh.AddNavMeshData(navigation);
            try { work(); }
            finally { instance.Remove(); }
        }
    }
}
