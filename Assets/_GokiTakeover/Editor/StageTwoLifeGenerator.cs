using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Pose = GokiTakeover.BlenderHumanAnimator.LifePose;
using Kind = GokiTakeover.HouseholdLife.Kind;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Stage 2, second pass (2026-10-25): the house was too small for three people and a cat. Behind the kitchen and the
    /// living room come the parents' bedroom (a double bed on tall legs, a wardrobe, the father's desk with a laptop, the
    /// laundry basket) and the son's room (his bed, a study desk, a TV with a game console, a toy shelf). Doorways join
    /// the kitchen to the bedroom, the living room to the son's room and the two new rooms to each other. Then the
    /// family's daily life: where each activity happens, how they sit or stand there and what they watch (HouseholdLife).
    /// </summary>
    public static partial class StageTwoGenerator
    {
        public const float NorthMinZ = 6.24f, NorthMaxZ = 16.24f;
        public const float BedroomDoorXMin = 4.7f, BedroomDoorXMax = 6.7f, KidDoorXMin = 10.6f, KidDoorXMax = 12.6f, InnerDoorZMin = 8.0f, InnerDoorZMax = 10.0f;
        private static readonly (FoodValue.Kind kind, Vector3 at, bool known)[] NorthFoods =
        {
            (FoodValue.Kind.Sweets, new Vector3(-0.6f, 0.07f, 12.6f), false),     // under the double bed
            (FoodValue.Kind.Sweets, new Vector3(-6.2f, 4.67f, 8.6f), false),      // on top of the wardrobe
            (FoodValue.Kind.Breadcrumb, new Vector3(2.6f, 1.57f, 15.4f), false),  // on the nightstand
            (FoodValue.Kind.Garbage, new Vector3(3.9f, 0.07f, 8.6f), false),      // by the laundry basket
            (FoodValue.Kind.Sweets, new Vector3(12.4f, 0.07f, 15.3f), false),     // under the study desk
            (FoodValue.Kind.Breadcrumb, new Vector3(11.2f, 2.49f, 15.15f), false),// on the study desk
            (FoodValue.Kind.Leftovers, new Vector3(19.3f, 0.07f, 12.4f), false),  // under the son's bed
            (FoodValue.Kind.Sweets, new Vector3(9.6f, 0.07f, 10.4f), false),      // crisps by the game corner
            (FoodValue.Kind.Rice, new Vector3(15.6f, 3.67f, 15.55f), false),      // on top of the toy shelf
        };
        // Hidden until dropped: crumbs in front of the hob, leftovers on the plate and under the table.
        private static readonly (FoodValue.Kind kind, Vector3 at)[] LifeCrumbs = { (FoodValue.Kind.Breadcrumb, new Vector3(2.2f, 0.07f, 2.85f)), (FoodValue.Kind.Rice, new Vector3(4.3f, 0.07f, 2.6f)) };
        private static readonly (FoodValue.Kind kind, Vector3 at)[] LifeLeftovers = { (FoodValue.Kind.Leftovers, new Vector3(-1.3f, 2.88f, -0.7f)), (FoodValue.Kind.Rice, new Vector3(-3.0f, 0.07f, -0.15f)) };
        private static readonly Vector3[] NorthScouts =
        {
            new Vector3(-1.0f, 0, 8.2f), new Vector3(4.0f, 0, 10.5f), new Vector3(-5.0f, 0, 13.0f), new Vector3(-0.8f, 0, 12.6f),
            new Vector3(9.5f, 0, 9.0f), new Vector3(15.0f, 0, 10.8f), new Vector3(12.0f, 0, 14.9f), new Vector3(19.4f, 0, 12.6f)
        };
        private static readonly Vector3[] NorthRespawns =
        {
            new Vector3(-4.6f, 0.07f, 7.4f), new Vector3(3.6f, 0.07f, 13.4f), new Vector3(0.6f, 0.07f, 7.2f),
            new Vector3(9.0f, 0.07f, 7.4f), new Vector3(15.2f, 0.07f, 8.0f), new Vector3(16.8f, 0.07f, 14.0f)
        };
        private static readonly Vector3[] BedroomStops = { new Vector3(3.8f, 0, 11.0f), new Vector3(-1.0f, 0, 8.2f), new Vector3(-4.8f, 0, 13.0f), new Vector3(4.0f, 0, 7.6f) };
        private static readonly Vector3[] KidStops = { new Vector3(9.6f, 0, 8.4f), new Vector3(14.6f, 0, 9.0f), new Vector3(16.6f, 0, 12.4f), new Vector3(13.8f, 0, 12.0f), new Vector3(9.4f, 0, 14.4f) };
        // The mother vacuums the living room, the kitchen and back.
        private static readonly Vector3[] VacuumRoute =
        {
            new Vector3(9.6f, 0, 1.8f), new Vector3(11.8f, 0, -2.6f), new Vector3(14.5f, 0, -1.6f), new Vector3(18.6f, 0, 0.6f), new Vector3(13.0f, 0, -3.9f),
            new Vector3(9.0f, 0, -3.6f), new Vector3(5.4f, 0, -3.4f), new Vector3(3.4f, 0, -0.6f), new Vector3(-0.6f, 0, -3.7f), new Vector3(-5.6f, 0, -3.4f), new Vector3(-5.8f, 0, 0.6f)
        };
        public static readonly Vector3 MotherChair = new Vector3(-2.25f, 0, 1.45f);

        // ---------------- The rooms ----------------
        private static (Transform bedroom, Transform kids, Renderer[] hide, Renderer[] ceiling) BuildNorthRooms(Transform room)
        {
            var bedroom = GeneratedAssets.Empty("Bedroom", room, Vector3.zero);
            var kids = GeneratedAssets.Empty("KidsRoom", room, Vector3.zero);
            var hide = new List<Renderer>();
            var ceilings = new List<Renderer>();
            var bedroomPaint = P.Material("Stage02_BedroomWall", new Color(0.52f, 0.55f, 0.58f), 0.08f);
            var kidPaint = P.Material("Stage02_KidsWall", new Color(0.66f, 0.6f, 0.4f), 0.08f);
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(P.Root + "/Textures/WalnutGrain.asset");
            var bedroomFloor = P.Material("Stage02_BedroomFloor", new Color(0.56f, 0.42f, 0.3f), 0.3f, 0, wood, new Vector2(5, 4));
            var kidFloor = P.Material("Stage02_KidsFloor", new Color(0.8f, 0.66f, 0.46f), 0.34f, 0, wood, new Vector2(5, 4));

            // The kitchen's back wall now opens into the bedroom; its window and skirting go (a room lies behind it).
            var kitchenWall = room.Find("Wall_Back");
            var kitchenPlaster = kitchenWall.GetComponent<MeshRenderer>().sharedMaterial;
            Kill(kitchenWall.gameObject);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && (t.name == "WindowRecess" || t.name == "NightGlass" || t.name == "WindowMullion" || t.name == "WindowCrossbar") && t.position.z > 5.5f && t.position.x < 7) Kill(t.gameObject);
            foreach (Transform t in room.Cast<Transform>().ToArray())
                if (t.name == "Skirting" && t.localPosition.z > 5.5f && Mathf.Abs(t.localPosition.x) < 6.5f) Kill(t.gameObject);
            hide.AddRange(WallWithDoor("Wall_Back", room, -7.24f, 7.0f, BedroomDoorXMin, BedroomDoorXMax, 6.12f, true, kitchenPlaster));
            Skirting(room, -7.0f, 7.0f, BedroomDoorXMin, BedroomDoorXMax, 5.96f, true);
            foreach (Transform t in room.Find("LivingRoom").Cast<Transform>()) if (t.name == "Wall_Back") hide.Add(t.GetComponent<Renderer>());
            // Their room sides wear the new rooms' colours.
            hide.AddRange(WallWithDoor("Facing", bedroom, -7.0f, 7.0f, BedroomDoorXMin, BedroomDoorXMax, 6.245f, true, bedroomPaint, 0.01f, false));
            hide.AddRange(WallWithDoor("Facing", kids, LivingMinX, LivingMaxX, KidDoorXMin, KidDoorXMax, 6.245f, true, kidPaint, 0.01f, false));
            hide.AddRange(DoorTrims(room, BedroomDoorXMin, BedroomDoorXMax, 6.12f, true));
            hide.AddRange(DoorTrims(room, KidDoorXMin, KidDoorXMax, 6.12f, true));

            Shell(bedroom, -7.0f, 7.0f, bedroomFloor, bedroomPaint, ceilings);
            Shell(kids, LivingMinX, LivingMaxX, kidFloor, kidPaint, ceilings);
            Climb(P.Box("Wall_Left", bedroom, new Vector3(-7.12f, 2.7f, (6.0f + NorthMaxZ + 0.24f) / 2), new Vector3(0.24f, 5.4f, NorthMaxZ + 0.24f - 6.0f), bedroomPaint));
            Climb(P.Box("Wall_Right", kids, new Vector3(LivingMaxX + 0.12f, 2.7f, (6.0f + NorthMaxZ + 0.24f) / 2), new Vector3(0.24f, 5.4f, NorthMaxZ + 0.24f - 6.0f), kidPaint));
            // Between the two new rooms: a wall with a doorway near the front.
            WallWithDoor("Wall_Divider", bedroom, 6.0f, NorthMaxZ + 0.24f, InnerDoorZMin, InnerDoorZMax, 7.12f, false, bedroomPaint);
            WallWithDoor("Facing", kids, NorthMinZ, NorthMaxZ, InnerDoorZMin, InnerDoorZMax, 7.245f, false, kidPaint, 0.01f, false);
            DoorTrims(bedroom, InnerDoorZMin, InnerDoorZMax, 7.12f, false);
            Skirting(bedroom, NorthMinZ, NorthMaxZ, InnerDoorZMin, InnerDoorZMax, 6.96f, false);
            Skirting(kids, NorthMinZ, NorthMaxZ, InnerDoorZMin, InnerDoorZMax, 7.28f, false);
            P.Box("Skirting", bedroom, new Vector3(-6.96f, 0.15f, (NorthMinZ + NorthMaxZ) / 2), new Vector3(0.08f, 0.3f, NorthMaxZ - NorthMinZ), walnut, false);
            P.Box("Skirting", kids, new Vector3(LivingMaxX - 0.04f, 0.15f, (NorthMinZ + NorthMaxZ) / 2), new Vector3(0.08f, 0.3f, NorthMaxZ - NorthMinZ), walnut, false);
            Skirting(bedroom, -7.0f, 7.0f, BedroomDoorXMin, BedroomDoorXMax, NorthMinZ + 0.04f, true);
            Skirting(kids, LivingMinX, LivingMaxX, KidDoorXMin, KidDoorXMax, NorthMinZ + 0.04f, true);
            P.Box("Threshold", bedroom, new Vector3((BedroomDoorXMin + BedroomDoorXMax) / 2, 0.012f, 6.12f), new Vector3(BedroomDoorXMax - BedroomDoorXMin, 0.02f, 0.3f), oak, false);
            P.Box("Threshold", kids, new Vector3((KidDoorXMin + KidDoorXMax) / 2, 0.012f, 6.12f), new Vector3(KidDoorXMax - KidDoorXMin, 0.02f, 0.3f), oak, false);
            P.Box("Threshold", bedroom, new Vector3(7.12f, 0.012f, (InnerDoorZMin + InnerDoorZMax) / 2), new Vector3(0.3f, 0.02f, InnerDoorZMax - InnerDoorZMin), oak, false);

            FurnishBedroom(bedroom);
            FurnishKids(kids);
            // A third chair at the kitchen table, for the mother at dinner.
            Chair(room, "DiningChair_Mother", MotherChair, 180);
            return (bedroom, kids, hide.Where(r => r).ToArray(), ceilings.ToArray());
        }

        // Floor (also under the doorways), back wall, ceiling and back skirting of one of the new rooms.
        private static void Shell(Transform parent, float x0, float x1, Material floor, Material paint, List<Renderer> ceilings)
        {
            float cx = (x0 + x1) / 2, w = x1 - x0, cz = (NorthMinZ + NorthMaxZ) / 2;
            P.Box("Floor", parent, new Vector3(cx, -0.15f, (6.0f + NorthMaxZ) / 2), new Vector3(w + 0.48f, 0.3f, NorthMaxZ - 6.0f), floor);   // under the walls and doorways too
            Climb(P.Box("Wall_Back", parent, new Vector3(cx, 2.7f, NorthMaxZ + 0.12f), new Vector3(w + 0.48f, 5.4f, 0.24f), paint));
            var ceiling = Climb(P.Box("Ceiling", parent, new Vector3(cx, 5.5f, cz), new Vector3(w + 0.48f, 0.2f, NorthMaxZ - NorthMinZ + 0.48f), paint));
            ceiling.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ceilings.Add(ceiling.GetComponent<Renderer>());
            P.Box("Skirting", parent, new Vector3(cx, 0.15f, NorthMaxZ - 0.04f), new Vector3(w, 0.3f, 0.08f), walnut, false);
        }

        private static void FurnishBedroom(Transform bedroom)
        {
            var shadow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/FurnitureContact.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/LampGlow.mat");
            var linen = P.Material("Stage02_BedLinen", new Color(0.9f, 0.88f, 0.82f), 0.05f);
            var pillow = P.Material("Stage02_Pillow", new Color(0.95f, 0.93f, 0.88f), 0.05f);
            // Double bed on tall legs: roaches fit underneath (the cat does not).
            Bed(bedroom, "DoubleBed", new Vector3(-1.0f, 0, 12.8f), 5.2f, 6.5f, linen, P.Material("Stage02_Duvet", new Color(0.36f, 0.48f, 0.62f), 0.05f), pillow, 2, shadow);
            var folded = P.Material("Stage02_Laundry", new Color(0.86f, 0.62f, 0.52f), 0.05f);
            P.Box("FoldedShirt", bedroom, new Vector3(0.9f, 1.84f, 11.2f), new Vector3(0.6f, 0.1f, 0.5f), folded, false);
            P.Box("FoldedTowel", bedroom, new Vector3(0.9f, 1.84f, 12.0f), new Vector3(0.6f, 0.14f, 0.5f), linen, false);

            var nightstand = GeneratedAssets.Empty("Nightstand", bedroom, new Vector3(2.6f, 0, 15.55f));
            Climb(P.Box("Body", nightstand, new Vector3(0, 0.75f, 0), new Vector3(1.1f, 1.5f, 1.0f), walnut));
            P.Box("Drawer", nightstand, new Vector3(0, 0.95f, -0.51f), new Vector3(0.95f, 0.45f, 0.03f), oak, false);
            P.Rod("LampStem", nightstand, new Vector3(0.25f, 1.5f, -0.1f), new Vector3(0.25f, 2.2f, -0.1f), 0.1f, metal);
            P.Sphere("LampShade", nightstand, new Vector3(0.25f, 2.35f, -0.1f), new Vector3(0.62f, 0.45f, 0.62f), glow);
            Lamp(nightstand, new Vector3(0.25f, 2.3f, -0.3f), new Color(1f, 0.66f, 0.36f), 1.2f, 4.5f, false);

            var wardrobe = GeneratedAssets.Empty("Wardrobe", bedroom, new Vector3(-6.2f, 0, 8.6f));
            Climb(P.Box("Body", wardrobe, new Vector3(0, 2.3f, 0), new Vector3(1.5f, 4.6f, 3.0f), oak));
            P.Box("DoorLine", wardrobe, new Vector3(0.76f, 2.3f, 0), new Vector3(0.03f, 4.3f, 0.04f), walnut, false);
            for (int s = -1; s <= 1; s += 2) P.Rod("Handle", wardrobe, new Vector3(0.8f, 2.0f, s * 0.2f), new Vector3(0.8f, 2.6f, s * 0.2f), 0.07f, metal);
            P.Shadow("WardrobeAO", bedroom, new Vector3(-6.2f, 0.006f, 8.6f), new Vector2(2.1f, 3.6f), shadow);

            // The father's desk against the wall with the son's room: he sits with his back to the room.
            var desk = GeneratedAssets.Empty("WorkDesk", bedroom, new Vector3(6.25f, 0, 13.5f));
            Climb(P.Box("Top", desk, new Vector3(0, 2.55f, 0), new Vector3(1.4f, 0.15f, 3.0f), walnut));
            foreach (var at in new[] { new Vector3(-0.6f, 0, -1.4f), new Vector3(0.6f, 0, -1.4f), new Vector3(-0.6f, 0, 1.4f), new Vector3(0.6f, 0, 1.4f) })
            {
                P.Rod("Leg", desk, at, at + Vector3.up * 2.48f, 0.12f, charcoal);
                P.Box("LegCollider", desk, at + Vector3.up * 1.24f, new Vector3(0.12f, 2.48f, 0.12f), charcoal).GetComponent<MeshRenderer>().enabled = false;
            }
            var screen = P.Material("Stage02_LaptopScreen", new Color(0.2f, 0.32f, 0.42f), 0.6f, 0, null, null, new Color(0.25f, 0.42f, 0.6f));
            P.Box("LaptopBase", desk, new Vector3(-0.05f, 2.66f, 0), new Vector3(0.7f, 0.05f, 1.0f), charcoal, false);
            P.Box("LaptopLid", desk, new Vector3(0.3f, 2.98f, 0), new Vector3(0.04f, 0.64f, 1.0f), charcoal, false).localRotation = Quaternion.Euler(0, 0, -10);
            P.Box("LaptopScreen", desk, new Vector3(0.27f, 2.98f, 0), new Vector3(0.01f, 0.56f, 0.9f), screen, false).localRotation = Quaternion.Euler(0, 0, -10);
            P.Rod("Mug", desk, new Vector3(-0.2f, 2.62f, 1.0f), new Vector3(-0.2f, 2.95f, 1.0f), 0.28f, ivory);
            OfficeChair(bedroom, "DeskChair", new Vector3(4.8f, 0, 13.5f), 90);

            var basket = GeneratedAssets.Empty("LaundryBasket", bedroom, new Vector3(2.75f, 0, 9.2f));
            Climb(P.Rod("Basket", basket, Vector3.zero, new Vector3(0, 0.8f, 0), 1.1f, P.Material("Stage02_Wicker", new Color(0.66f, 0.5f, 0.3f), 0.1f)));
            P.Sphere("Clothes", basket, new Vector3(0, 0.8f, 0), new Vector3(0.95f, 0.35f, 0.95f), folded);
            P.Box("Rug", bedroom, new Vector3(-1.0f, 0.012f, 8.4f), new Vector3(4.6f, 0.02f, 2.0f), P.Material("Stage02_BedroomRug", new Color(0.72f, 0.66f, 0.58f), 0.04f), false);
            Window(bedroom, 4.4f, 3.0f, 3.9f, 3.0f);
            P.Sphere("CeilingLamp", bedroom, new Vector3(0, 5.33f, 11.2f), new Vector3(1.5f, 0.18f, 1.5f), glow);
            Lamp(bedroom, new Vector3(0, 4.4f, 11.2f), new Color(1f, 0.76f, 0.52f), 2.4f, 10.5f, true);
        }

        private static void FurnishKids(Transform kids)
        {
            var shadow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/FurnitureContact.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/LampGlow.mat");
            var red = P.Material("Stage02_ToyRed", new Color(0.78f, 0.12f, 0.1f), 0.5f);
            var blue = P.Material("Stage02_ToyBlue", new Color(0.12f, 0.32f, 0.72f), 0.5f);
            var yellow = P.Material("Stage02_ToyYellow", new Color(0.92f, 0.72f, 0.1f), 0.5f);
            Bed(kids, "KidsBed", new Vector3(19.65f, 0, 12.95f), 3.0f, 6.0f, P.Material("Stage02_BedLinen", new Color(0.9f, 0.88f, 0.82f), 0.05f),
                P.Material("Stage02_KidsDuvet", new Color(0.82f, 0.36f, 0.18f), 0.05f), P.Material("Stage02_Pillow", new Color(0.95f, 0.93f, 0.88f), 0.05f), 1, shadow);

            // Study desk with a hutch: he sits facing the wall.
            var desk = GeneratedAssets.Empty("StudyDesk", kids, new Vector3(12.0f, 0, 15.5f));
            Climb(P.Box("Top", desk, new Vector3(0, 2.35f, 0), new Vector3(3.2f, 0.14f, 1.4f), oak));
            foreach (var at in new[] { new Vector3(-1.45f, 0, -0.6f), new Vector3(1.45f, 0, -0.6f), new Vector3(-1.45f, 0, 0.6f), new Vector3(1.45f, 0, 0.6f) })
            {
                P.Rod("Leg", desk, at, at + Vector3.up * 2.28f, 0.12f, walnut);
                P.Box("LegCollider", desk, at + Vector3.up * 1.14f, new Vector3(0.12f, 2.28f, 0.12f), walnut).GetComponent<MeshRenderer>().enabled = false;
            }
            Climb(P.Box("Hutch", desk, new Vector3(0, 3.22f, 0.5f), new Vector3(3.2f, 1.6f, 0.36f), oak));
            P.Box("Notebook", desk, new Vector3(0, 2.44f, -0.25f), new Vector3(0.8f, 0.04f, 0.6f), ivory, false).localRotation = Quaternion.Euler(0, 8, 0);
            P.Rod("Pencil", desk, new Vector3(0.5f, 2.45f, -0.3f), new Vector3(0.9f, 2.45f, -0.1f), 0.04f, yellow);
            P.Rod("DeskLampStem", desk, new Vector3(-1.2f, 2.42f, 0.2f), new Vector3(-1.0f, 3.1f, -0.1f), 0.06f, metal);
            P.Sphere("DeskLampShade", desk, new Vector3(-0.9f, 3.1f, -0.2f), new Vector3(0.4f, 0.24f, 0.4f), glow);
            OfficeChair(kids, "StudyChair", new Vector3(12.0f, 0, 13.6f), 0);

            // The game corner: a small TV on a cabinet against the bedroom wall, a cushion on the floor in front.
            var corner = GeneratedAssets.Empty("GameCorner", kids, new Vector3(7.95f, 0, 12.0f));
            Climb(P.Box("Cabinet", corner, new Vector3(0, 0.55f, 0), new Vector3(1.3f, 1.1f, 2.6f), walnut));
            Climb(P.Box("TvBody", corner, new Vector3(0.1f, 1.85f, 0), new Vector3(0.14f, 1.4f, 2.2f), charcoal));
            var screen = P.Material("Stage02_KidsTv", new Color(0.3f, 0.5f, 0.42f), 0.6f, 0, null, null, new Color(0.3f, 0.62f, 0.5f));
            P.Box("TvScreen", corner, new Vector3(0.18f, 1.87f, 0), new Vector3(0.02f, 1.22f, 2.0f), screen, false);
            P.Box("Console", corner, new Vector3(-0.1f, 1.18f, 0.85f), new Vector3(0.6f, 0.14f, 0.45f), ivory, false);
            P.Sphere("FloorCushion", kids, new Vector3(10.6f, 0.16f, 12.0f), new Vector3(1.3f, 0.32f, 1.3f), P.Material("Stage02_FloorCushion", new Color(0.3f, 0.56f, 0.36f), 0.05f));

            var shelf = GeneratedAssets.Empty("ToyShelf", kids, new Vector3(15.6f, 0, 15.65f));
            Climb(P.Box("Body", shelf, new Vector3(0, 1.8f, 0), new Vector3(3.0f, 3.6f, 1.0f), ivory));
            var bins = new[] { red, blue, yellow };
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3; i++) P.Box("Bin", shelf, new Vector3(-0.95f + i * 0.95f, 0.5f + row * 1.15f, -0.52f), new Vector3(0.8f, 0.8f, 0.06f), bins[(i + row) % 3], false);   // proud of the shelf front (not in its plane)
            P.Shadow("ShelfAO", kids, new Vector3(15.6f, 0.006f, 15.5f), new Vector2(3.5f, 1.6f), shadow);
            P.Box("Rug", kids, new Vector3(13.2f, 0.012f, 10.6f), new Vector3(6.0f, 0.02f, 4.2f), P.Material("Stage02_KidsRug", new Color(0.36f, 0.5f, 0.66f), 0.04f), false);
            P.Sphere("Ball", kids, new Vector3(15.0f, 0.4f, 8.2f), Vector3.one * 0.8f, red);
            // An insect poster (he hunts bugs, after all) and a small window over his bed.
            P.Box("Poster", kids, new Vector3(LivingMaxX - 0.02f, 3.7f, 8.6f), new Vector3(0.03f, 1.5f, 2.0f), P.Material("Stage02_Poster", new Color(0.9f, 0.84f, 0.5f), 0.1f), false);
            P.Sphere("PosterBeetle", kids, new Vector3(LivingMaxX - 0.05f, 3.7f, 8.6f), new Vector3(0.03f, 0.55f, 0.8f), charcoal);
            Window(kids, 19.65f, 2.0f, 4.35f, 1.5f);
            P.Sphere("CeilingLamp", kids, new Vector3(14.2f, 5.33f, 11.2f), new Vector3(1.5f, 0.18f, 1.5f), glow);
            Lamp(kids, new Vector3(14.2f, 4.4f, 11.2f), new Color(1f, 0.84f, 0.62f), 2.5f, 10.5f, true);
        }

        // ---------------- Pieces ----------------
        /// <summary>A wall along x (or along z) with a doorway in it: both sides and the lintel. Returns their renderers.</summary>
        private static Renderer[] WallWithDoor(string name, Transform parent, float from, float to, float doorMin, float doorMax, float at, bool alongX, Material material, float thickness = 0.24f, bool solid = true)
        {
            var parts = new List<Renderer>();
            void Piece(float a, float b, float y0, float y1)
            {
                float mid = (a + b) / 2, len = b - a;
                var position = alongX ? new Vector3(mid, (y0 + y1) / 2, at) : new Vector3(at, (y0 + y1) / 2, mid);
                var size = alongX ? new Vector3(len, y1 - y0, thickness) : new Vector3(thickness, y1 - y0, len);
                var box = P.Box(name, parent, position, size, material, solid);
                parts.Add((solid ? Climb(box) : box).GetComponent<Renderer>());
            }
            Piece(from, doorMin, 0, 5.4f);
            Piece(doorMax, to, 0, 5.4f);
            Piece(doorMin, doorMax, DoorHeight, 5.4f);
            return parts.ToArray();
        }
        private static void Skirting(Transform parent, float from, float to, float doorMin, float doorMax, float at, bool alongX)
        {
            foreach (var (a, b) in new[] { (from, doorMin), (doorMax, to) })
            {
                float mid = (a + b) / 2, len = b - a;
                P.Box("Skirting", parent, alongX ? new Vector3(mid, 0.15f, at) : new Vector3(at, 0.15f, mid), alongX ? new Vector3(len, 0.3f, 0.08f) : new Vector3(0.08f, 0.3f, len), walnut, false);
            }
        }
        // Oak trim round a doorway, on both faces of the wall.
        private static Renderer[] DoorTrims(Transform parent, float doorMin, float doorMax, float at, bool alongX)
        {
            var parts = new List<Renderer>();
            foreach (float face in new[] { -0.14f, 0.14f })
            {
                foreach (float edge in new[] { doorMin - 0.05f, doorMax + 0.05f })
                    parts.Add(P.Box("DoorTrim", parent, alongX ? new Vector3(edge, DoorHeight / 2, at + face) : new Vector3(at + face, DoorHeight / 2, edge),
                        alongX ? new Vector3(0.12f, DoorHeight, 0.06f) : new Vector3(0.06f, DoorHeight, 0.12f), oak, false).GetComponent<Renderer>());
                float mid = (doorMin + doorMax) / 2, len = doorMax - doorMin + 0.2f;
                parts.Add(P.Box("DoorTrim", parent, alongX ? new Vector3(mid, DoorHeight + 0.05f, at + face) : new Vector3(at + face, DoorHeight + 0.05f, mid),
                    alongX ? new Vector3(len, 0.12f, 0.06f) : new Vector3(0.06f, 0.12f, len), oak, false).GetComponent<Renderer>());
            }
            return parts.ToArray();
        }
        /// <summary>A bed on tall legs: the frame starts 1.0 above the floor, so a roach fits underneath (the ally grid's probe too).</summary>
        private static void Bed(Transform parent, string name, Vector3 centre, float width, float length, Material linen, Material duvet, Material pillow, int pillows, Material shadow)
        {
            var bed = GeneratedAssets.Empty(name, parent, centre);
            Climb(P.Box("Base", bed, new Vector3(0, 1.15f, 0), new Vector3(width, 0.3f, length), walnut));
            Climb(P.Box("Mattress", bed, new Vector3(0, 1.55f, -0.05f), new Vector3(width - 0.2f, 0.5f, length - 0.3f), linen));
            P.Box("Duvet", bed, new Vector3(0, 1.84f, -length * 0.14f), new Vector3(width - 0.1f, 0.12f, length * 0.62f), duvet, false);
            for (int i = 0; i < pillows; i++)
                P.Box("Pillow", bed, new Vector3(pillows == 1 ? 0 : (i - 0.5f) * width * 0.5f, 1.92f, length / 2 - 0.75f), new Vector3(Mathf.Min(1.6f, width * 0.42f), 0.3f, 0.8f), pillow, false);
            Climb(P.Box("Headboard", bed, new Vector3(0, 1.8f, length / 2 + 0.06f), new Vector3(width + 0.2f, 3.6f, 0.2f), walnut));
            foreach (var c in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
            {
                var at = new Vector3(c.x * (width / 2 - 0.2f), 0, c.z * (length / 2 - 0.2f));
                P.Rod("Leg", bed, at, at + Vector3.up * 1.0f, 0.16f, walnut);
                P.Box("LegCollider", bed, at + Vector3.up * 0.5f, new Vector3(0.16f, 1.0f, 0.16f), walnut).GetComponent<MeshRenderer>().enabled = false;
            }
            P.Shadow(name + "AO", parent, centre + new Vector3(0, 0.006f, 0), new Vector2(width + 0.6f, length + 0.6f), shadow);
        }
        /// <summary>A chair like the kitchen's (oak seat 1.64 high, walnut legs); the sitter faces local +z.</summary>
        private static void Chair(Transform parent, string name, Vector3 position, float yaw)
        {
            var chair = GeneratedAssets.Empty(name, parent, position);
            chair.localRotation = Quaternion.Euler(0, yaw, 0);
            P.Box("Seat", chair, new Vector3(0, 1.55f, 0), new Vector3(1.3f, 0.17f, 1.3f), oak);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    P.Rod("ChairLeg", chair, new Vector3(x * 0.53f, 0, z * 0.53f), new Vector3(x * 0.48f, 1.52f, z * 0.48f), 0.13f, walnut);
                    P.Box("LegCollider", chair, new Vector3(x * 0.5f, 0.75f, z * 0.5f), new Vector3(0.13f, 1.5f, 0.13f), walnut).GetComponent<MeshRenderer>().enabled = false;
                }
            for (int x = -1; x <= 1; x += 2) P.Rod("BackPost", chair, new Vector3(x * 0.53f, 1.5f, -0.53f), new Vector3(x * 0.53f, 2.9f, -0.67f), 0.13f, walnut);
            for (int i = 0; i < 3; i++) P.Box("BackSlat", chair, new Vector3((i - 1) * 0.34f, 2.25f, -0.6f), new Vector3(0.17f, 1.1f, 0.075f), oak, false);
            P.Box("BackRail", chair, new Vector3(0, 2.9f, -0.67f), new Vector3(1.3f, 0.2f, 0.16f), walnut);
            P.Shadow("ChairAO", chair, new Vector3(0, 0.006f, 0), new Vector2(2, 2), AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/FurnitureContact.mat"));
        }
        /// <summary>A desk chair on one pole (seat 1.64 high): only the pole blocks the floor, so roaches pass under the desk.</summary>
        private static void OfficeChair(Transform parent, string name, Vector3 position, float yaw)
        {
            var chair = GeneratedAssets.Empty(name, parent, position);
            chair.localRotation = Quaternion.Euler(0, yaw, 0);
            var fabric = P.Material("Stage02_ChairFabric", new Color(0.2f, 0.26f, 0.32f), 0.08f);
            P.Box("Seat", chair, new Vector3(0, 1.55f, 0), new Vector3(1.2f, 0.17f, 1.2f), fabric);
            P.Box("Back", chair, new Vector3(0, 2.4f, -0.58f), new Vector3(1.1f, 1.2f, 0.14f), fabric, false);
            P.Rod("BackPost", chair, new Vector3(0, 1.5f, -0.5f), new Vector3(0, 2.0f, -0.58f), 0.12f, charcoal);
            P.Rod("Pole", chair, new Vector3(0, 0.15f, 0), new Vector3(0, 1.48f, 0), 0.14f, metal);
            P.Box("PoleCollider", chair, new Vector3(0, 0.75f, 0), new Vector3(0.14f, 1.5f, 0.14f), metal).GetComponent<MeshRenderer>().enabled = false;
            for (int i = 0; i < 5; i++)
            {
                var foot = Quaternion.Euler(0, i * 72, 0) * new Vector3(0, 0.12f, 0.55f);
                P.Rod("BaseArm", chair, new Vector3(0, 0.15f, 0), foot, 0.08f, charcoal);
                P.Sphere("Wheel", chair, foot + Vector3.down * 0.06f, Vector3.one * 0.12f, charcoal);
            }
            P.Shadow("ChairAO", chair, new Vector3(0, 0.006f, 0), new Vector2(1.6f, 1.6f), AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/FurnitureContact.mat"));
        }
        // A night window with curtains (hanging from the rail at 5.1) on a new room's back wall.
        private static void Window(Transform parent, float x, float width, float y, float curtainLength)
        {
            var night = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/NightWindow.mat");
            float z = NorthMaxZ, height = Mathf.Min(1.9f, (5.0f - y) * 2);
            P.Box("WindowRecess", parent, new Vector3(x, y, z - 0.06f), new Vector3(width, height, 0.12f), charcoal, false);
            P.Box("NightGlass", parent, new Vector3(x, y, z - 0.14f), new Vector3(width - 0.2f, height - 0.2f, 0.035f), night, false);
            P.Box("WindowCrossbar", parent, new Vector3(x, y + 0.05f, z - 0.17f), new Vector3(width - 0.1f, 0.07f, 0.06f), ivory, false);
            var curtain = P.Material("Stage02_Curtain", new Color(0.62f, 0.52f, 0.36f), 0.05f);
            for (int s = -1; s <= 1; s += 2) P.Box("Curtain", parent, new Vector3(x + s * (width / 2 + 0.25f), 5.1f - curtainLength / 2, z - 0.2f), new Vector3(0.6f, curtainLength, 0.08f), curtain, false);
            P.Rod("CurtainRail", parent, new Vector3(x - width / 2 - 0.6f, 5.1f, z - 0.2f), new Vector3(x + width / 2 + 0.6f, 5.1f, z - 0.2f), 0.05f, metal);
        }
        // A framed picture where the living room's window was (the son's room is behind that wall now).
        private static void Picture(Transform parent, Vector3 at, float yaw, Color sky, Color hill)
        {
            var picture = GeneratedAssets.Empty("Picture", parent, at);
            picture.localRotation = Quaternion.Euler(0, yaw, 0);
            P.Box("Frame", picture, Vector3.zero, new Vector3(3.6f, 2.3f, 0.06f), walnut, false);
            P.Box("Canvas", picture, new Vector3(0, 0, -0.035f), new Vector3(3.3f, 2.0f, 0.02f), P.Material("Stage02_PictureSky", sky, 0.1f), false);
            P.Box("Hill", picture, new Vector3(-0.3f, -0.55f, -0.05f), new Vector3(2.6f, 0.8f, 0.02f), P.Material("Stage02_PictureHill", hill, 0.1f), false).localRotation = Quaternion.Euler(0, 0, 8);
            P.Sphere("Sun", picture, new Vector3(1.0f, 0.45f, -0.05f), new Vector3(0.45f, 0.45f, 0.02f), P.Material("Stage02_PictureSun", new Color(0.95f, 0.78f, 0.36f), 0.1f));
        }
        private static void Lamp(Transform parent, Vector3 at, Color color, float intensity, float range, bool pixel)
        {
            var light = GeneratedAssets.Empty("RoomLight", parent, at).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
            light.renderMode = pixel ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
        }

        // ---------------- Daily life ----------------
        private static HouseholdLife.Spot Spot(int member, Kind kind, Pose pose, Vector3 stand, float yaw, Vector3 seat, Vector3 hands, Vector3 look, float angle, float range)
            => new HouseholdLife.Spot { member = member, kind = kind, pose = pose, stand = stand, yaw = yaw, seat = seat, hands = hands, look = look, sightAngle = angle, sightRange = range };
        /// <summary>
        /// Where each activity happens. Seats: the hips go on the chair (1.64), the sofa cushion (1.98) or the floor cushion;
        /// the person stands beside it on the NavMesh. Sight while busy: a fraction of the normal cone (110 deg, 7 m).
        /// </summary>
        public static HouseholdLife.Spot[] LifeSpots()
        {
            var chairFather = new Vector3(-2.25f, 1.64f, -3.2f);
            var sofa = new Vector3(14.0f, 1.95f, 4.1f);   // on the cushion (1.98), toward its front edge
            return new[]
            {
                // The father: the paper at the table, TV on the sofa (and nodding off there), the laptop in the bedroom.
                Spot(0, Kind.Paper, Pose.Read, new Vector3(-0.95f, 0, -3.25f), 0, chairFather, Vector3.zero, Vector3.zero, 0.45f, 0.55f),
                Spot(0, Kind.Tv, Pose.Sit, new Vector3(14.0f, 0, 3.0f), 180, sofa, Vector3.zero, new Vector3(15.5f, 2.75f, -5.2f), 0.6f, 0.85f),
                Spot(0, Kind.Doze, Pose.Doze, new Vector3(14.0f, 0, 3.0f), 180, sofa, Vector3.zero, Vector3.zero, 0, 0),
                Spot(0, Kind.Desk, Pose.Desk, new Vector3(4.8f, 0, 12.15f), 90, new Vector3(4.8f, 1.64f, 13.5f), new Vector3(6.05f, 2.72f, 13.5f), new Vector3(6.45f, 3.0f, 13.5f), 0.5f, 0.6f),
                Spot(0, Kind.Dinner, Pose.Eat, new Vector3(-0.95f, 0, -3.25f), 0, chairFather, new Vector3(-2.25f, 2.84f, -2.15f), new Vector3(-2.25f, 2.84f, -1.6f), 0.8f, 0.8f),
                // The mother: the hob, the sink (to the side of the nest's door), the laundry on the bed, the vacuum round the house.
                Spot(1, Kind.Cook, Pose.Cook, new Vector3(3.0f, 0, 3.05f), 0, Vector3.zero, new Vector3(3.0f, 3.05f, 4.3f), new Vector3(3.0f, 2.95f, 4.7f), 0.55f, 0.6f),
                Spot(1, Kind.Wash, Pose.Wash, new Vector3(-0.4f, 0, 3.05f), 0, Vector3.zero, new Vector3(-0.15f, 2.85f, 4.3f), new Vector3(0, 2.85f, 4.7f), 0.55f, 0.6f),
                Spot(1, Kind.Laundry, Pose.Fold, new Vector3(2.35f, 0, 11.6f), 270, Vector3.zero, new Vector3(1.2f, 1.9f, 11.6f), new Vector3(1.0f, 1.8f, 11.6f), 0.6f, 0.65f),
                Spot(1, Kind.Vacuum, Pose.None, VacuumRoute[0], 0, Vector3.zero, Vector3.zero, Vector3.zero, 1, 1),
                Spot(1, Kind.Dinner, Pose.Eat, new Vector3(-0.95f, 0, 1.5f), 180, MotherChair + Vector3.up * 1.64f, new Vector3(-2.25f, 2.84f, 0.3f), new Vector3(-2.25f, 2.84f, -0.2f), 0.8f, 0.8f),
                // The son: games on the floor in his room, homework at his desk, the toys in the living room.
                Spot(2, Kind.Game, Pose.Floor, new Vector3(10.6f, 0, 12.0f), 270, new Vector3(10.6f, 0.33f, 12.0f), Vector3.zero, new Vector3(8.1f, 1.9f, 12.0f), 0.4f, 0.7f),
                Spot(2, Kind.Homework, Pose.Desk, new Vector3(13.35f, 0, 13.6f), 0, new Vector3(12.0f, 1.64f, 13.6f), new Vector3(12.0f, 2.5f, 14.95f), new Vector3(12.0f, 2.42f, 15.1f), 0.5f, 0.6f),
                Spot(2, Kind.Toys, Pose.Floor, new Vector3(17.5f, 0, -1.6f), 160, new Vector3(17.5f, 0.04f, -1.6f), Vector3.zero, new Vector3(17.35f, 0.3f, -2.2f), 0.8f, 0.8f),
                Spot(2, Kind.Dinner, Pose.Eat, new Vector3(-4.85f, 0, -2.15f), 90, new Vector3(-4.85f, 1.64f, -0.9f), new Vector3(-3.95f, 2.84f, -0.9f), new Vector3(-3.4f, 2.84f, -0.9f), 0.8f, 0.8f),
            };
        }

        private static void BuildLife(Transform stageRoot, PhaseTwoSession session, StageProgress progress, ColonyManager colony,
            HumanAIController father, HumanAIController mother, HumanAIController son, FoodItem[] crumbFoods, FoodItem[] leftoverFoods)
        {
            var spots = LifeSpots();
            var route = new Vector3[VacuumRoute.Length];
            WithNavMesh(() =>
            {
                foreach (var s in spots)
                {
                    if (!NavMesh.SamplePosition(s.stand, out var hit, 0.8f, NavMesh.AllAreas) || Vector3.ProjectOnPlane(hit.position - s.stand, Vector3.up).magnitude > 0.45f)
                        throw new InvalidOperationException("生活の場所が人間の通り道にありません: " + s.kind + " " + s.stand);
                    s.stand = hit.position;
                }
                for (int i = 0; i < route.Length; i++)
                {
                    if (!NavMesh.SamplePosition(VacuumRoute[i], out var hit, 1.2f, NavMesh.AllAreas)) throw new InvalidOperationException("掃除の経路が人間の通り道にありません: " + VacuumRoute[i]);
                    route[i] = hit.position;
                }
            });
            spots.First(s => s.kind == Kind.Vacuum).stand = route[0];
            // What they hold: the father's newspaper, the son's game pad (shown only while reading or playing).
            var paperMat = P.Material("Stage02_Newspaper", new Color(0.86f, 0.85f, 0.8f), 0.05f);
            var ink = P.Material("Stage02_NewsInk", new Color(0.22f, 0.22f, 0.24f), 0.05f);
            var paper = P.Box("Newspaper", father.transform, Vector3.zero, new Vector3(1.1f, 0.75f, 0.02f), paperMat, false);
            for (int i = 0; i < 4; i++) P.Box("Print", paper, new Vector3(0, 0.3f - i * 0.2f, -0.6f), new Vector3(0.8f, i == 0 ? 0.12f : 0.05f, 0.5f), ink, false);
            paper.gameObject.SetActive(false);
            var pad = P.Box("GamePad", son.transform, Vector3.zero, new Vector3(0.42f, 0.09f, 0.22f), charcoal, false);
            for (int s = -1; s <= 1; s += 2) P.Sphere("Grip", pad, new Vector3(s * 0.4f, -0.3f, -0.2f), new Vector3(0.35f, 1.2f, 0.9f), charcoal);
            pad.gameObject.SetActive(false);
            var life = stageRoot.gameObject.AddComponent<HouseholdLife>();
            life.Configure(session, progress, colony, spots, route, crumbFoods, leftoverFoods, paper, pad);
            EditorUtility.SetDirty(life);
        }
    }
}
