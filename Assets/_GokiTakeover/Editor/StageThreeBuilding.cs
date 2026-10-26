using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Stage 3 「町中華」: the building. Ground floor (1F): the kitchen, the dining room with the counter and the stairs,
    /// the dish room with the nest under the twin sink (by the grease trap) and the pantry with the walk-in fridge.
    /// First floor (2F), laid out apart at x ≥ F2 on the same plane: the banquet room (tatami, low tables) and the staff
    /// room. Walls and ceilings 5.4 high like the other stages; every solid piece is climbable.
    /// </summary>
    public static partial class StageThreeGenerator
    {
        // ---- Floor plan (world x, z) ----
        public const float F2 = 44f;                                  // the first floor starts here (x)
        public const float SecondFloorX = 36f;                        // anything beyond this is 2F
        public const float WallH = 5.4f, DoorH = 4.4f;
        public static readonly Rect Kitchen = new Rect(-7, -6, 16, 12);          // x -7..9,  z -6..6
        public static readonly Rect Hall = new Rect(9.24f, -6, 18, 12);          // x 9.24..27.24
        public static readonly Rect Wash = new Rect(-7, 6.24f, 12, 10);          // x -7..5,  z 6.24..16.24
        public static readonly Rect Pantry = new Rect(5.24f, 6.24f, 9.76f, 10);  // x 5.24..15
        public static readonly Rect Banquet = new Rect(F2, -6, 18, 12);          // x 44..62
        public static readonly Rect Staff = new Rect(F2, 6.24f, 12, 10);         // x 44..56, z 6.24..16.24
        // Stairs: 1F along the hall's back wall rising toward +x; 2F the well going down in the banquet room.
        public const float StairZ0 = 3.7f, StairZ1 = 5.9f, StairX0 = 17.6f, StepRun = 0.65f, StepRise = 0.42f;
        public const int StepCount = 13;
        public const float WellX0 = 57.6f;
        public static float StairZ => (StairZ0 + StairZ1) / 2;
        // Ducts: a low grated mouth in the kitchen's front wall and in the staff room's back wall.
        public static readonly Vector3 KitchenVent = new Vector3(-3.5f, 0, -6.12f), StaffVent = new Vector3(46.5f, 0, 16.36f);
        // The nest: under the twin sink on the dish room's back wall (by the grease trap).
        public static readonly Vector3 SinkCentre = new Vector3(-2.5f, 0, 15.2f);
        public static readonly Vector3 NestAreaSize = new Vector3(5.7f, 1.2f, 1.85f);
        public static readonly Vector3 NestRefuge = new Vector3(1.5f, 0, 10.0f);   // under the dish room's work table
        public static readonly Vector3 ExitPoint = new Vector3(-6.2f, 0, 13.3f);   // the back door

        private static Material steel, steelDark, tileWhite, kitchenFloor, hallFloor, hallWall, wainscot, redTop, redVinyl, chrome, darkWood,
            lightWood, washFloor, concrete, cardboard, tatami, tatamiEdge, plaster, staffFloor, staffWall, black, cream, glow, contact;
        private static readonly List<Renderer> fronts = new List<Renderer>(), ceilings = new List<Renderer>();

        private static void LoadMaterials()
        {
            var tiles = AssetDatabase.LoadAssetAtPath<Texture2D>(P.Root + "/Textures/StoneTiles.asset");
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(P.Root + "/Textures/WalnutGrain.asset");
            steel = P.Material("Stage03_Stainless", new Color(0.70f, 0.71f, 0.72f), 0.72f, 0.55f);
            steelDark = P.Material("Stage03_SteelDark", new Color(0.30f, 0.31f, 0.32f), 0.5f, 0.5f);
            tileWhite = P.Material("Stage03_WallTile", new Color(0.82f, 0.83f, 0.80f), 0.45f, 0, tiles, new Vector2(8, 3));
            kitchenFloor = P.Material("Stage03_KitchenFloor", new Color(0.48f, 0.52f, 0.50f), 0.35f, 0, tiles, new Vector2(8, 6));
            hallFloor = P.Material("Stage03_HallFloor", new Color(0.62f, 0.36f, 0.26f), 0.3f, 0, tiles, new Vector2(9, 6));
            hallWall = P.Material("Stage03_HallWall", new Color(0.80f, 0.72f, 0.56f), 0.08f);
            wainscot = P.Material("Stage03_Wainscot", new Color(0.40f, 0.24f, 0.14f), 0.25f, 0, wood, new Vector2(4, 1));
            redTop = P.Material("Stage03_RedTop", new Color(0.66f, 0.10f, 0.08f), 0.55f);
            redVinyl = P.Material("Stage03_RedVinyl", new Color(0.58f, 0.08f, 0.07f), 0.45f);
            chrome = P.Material("Stage03_Chrome", new Color(0.80f, 0.80f, 0.82f), 0.85f, 0.8f);
            darkWood = P.Material("Stage03_DarkWood", new Color(0.26f, 0.15f, 0.08f), 0.3f, 0, wood, new Vector2(2, 2));
            lightWood = P.Material("Stage03_LightWood", new Color(0.72f, 0.56f, 0.36f), 0.3f, 0, wood, new Vector2(2, 2));
            washFloor = P.Material("Stage03_WashFloor", new Color(0.40f, 0.43f, 0.44f), 0.5f, 0, tiles, new Vector2(6, 5));
            concrete = P.Material("Stage03_Concrete", new Color(0.50f, 0.49f, 0.46f), 0.15f);
            cardboard = P.Material("Stage03_Cardboard", new Color(0.66f, 0.50f, 0.30f), 0.08f);
            tatami = P.Material("Stage03_Tatami", new Color(0.70f, 0.68f, 0.44f), 0.12f, 0, TatamiTexture(), new Vector2(1, 1));
            tatamiEdge = P.Material("Stage03_TatamiEdge", new Color(0.14f, 0.16f, 0.12f), 0.15f);
            plaster = P.Material("Stage03_Plaster", new Color(0.80f, 0.74f, 0.60f), 0.06f);
            staffFloor = P.Material("Stage03_StaffFloor", new Color(0.46f, 0.40f, 0.33f), 0.25f, 0, wood, new Vector2(5, 4));
            staffWall = P.Material("Stage03_StaffWall", new Color(0.66f, 0.67f, 0.62f), 0.06f);
            black = P.Material("Stage03_Black", new Color(0.04f, 0.04f, 0.04f), 0.3f);
            cream = P.Material("Stage03_Cream", new Color(0.92f, 0.88f, 0.76f), 0.1f);
            glow = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/LampGlow.mat");
            contact = AssetDatabase.LoadAssetAtPath<Material>(P.Root + "/Materials/FurnitureContact.mat");
        }

        /// <summary>Woven rush with the dark cloth border, 1 m tatami pattern (the material tiles it per mat).</summary>
        private static Texture2D TatamiTexture()
        {
            string path = P.Root + "/Textures/Stage03_Tatami.asset";
            GeneratedAssets.RequireOwned(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool created = !texture;
            if (created) texture = new Texture2D(128, 256, TextureFormat.RGBA32, true) { name = "Stage03_Tatami", wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 256; y++)
            for (int x = 0; x < 128; x++)
            {
                float rush = 0.9f + 0.1f * Mathf.Sin(y * 1.6f) + 0.04f * Mathf.PerlinNoise(x * 0.3f, y * 0.05f);
                bool border = x < 9 || x > 118;
                texture.SetPixel(x, y, border ? new Color(0.22f, 0.25f, 0.2f) : new Color(rush, rush, rush * 0.95f));
            }
            texture.Apply(true);
            if (created) { AssetDatabase.CreateAsset(texture, path); GeneratedAssets.Mark(texture); }
            else EditorUtility.SetDirty(texture);
            return texture;
        }

        // ---------------- Shell ----------------
        private struct Gap { public float min, max, y0, y1; public Gap(float a, float b, float lo = 0, float hi = DoorH) { min = a; max = b; y0 = lo; y1 = hi; } }

        /// <summary>A wall from..to along x (or z) at the given line, with openings (doorways, a hatch, a vent).</summary>
        private static List<Renderer> Wall(string name, Transform parent, bool alongX, float at, float from, float to, Material material, params Gap[] gaps)
        {
            var parts = new List<Renderer>();
            void Piece(float a, float b, float y0, float y1)
            {
                if (b - a < 0.01f || y1 - y0 < 0.01f) return;
                float mid = (a + b) / 2, len = b - a;
                var position = alongX ? new Vector3(mid, (y0 + y1) / 2, at) : new Vector3(at, (y0 + y1) / 2, mid);
                var size = alongX ? new Vector3(len, y1 - y0, 0.24f) : new Vector3(0.24f, y1 - y0, len);
                parts.Add(Climb(P.Box(name, parent, position, size, material)).GetComponent<Renderer>());
            }
            float cursor = from;
            foreach (var g in gaps.OrderBy(g => g.min))
            {
                Piece(cursor, g.min, 0, WallH);
                Piece(g.min, g.max, 0, g.y0);
                Piece(g.min, g.max, g.y1, WallH);
                cursor = g.max;
            }
            Piece(cursor, to, 0, WallH);
            return parts;
        }
        private static Renderer Floor(string name, Transform parent, Rect area, Material material)
            => P.Box(name, parent, new Vector3(area.center.x, -0.15f, area.center.y), new Vector3(area.width + 0.5f, 0.3f, area.height + 0.5f), material).GetComponent<Renderer>();
        private static void Ceiling(string name, Transform parent, Rect area, Material material, params Rect[] holes)
        {
            // Split into strips around any hole (the stairwell).
            var pieces = new List<Rect> { new Rect(area.xMin - 0.24f, area.yMin - 0.24f, area.width + 0.48f, area.height + 0.48f) };
            foreach (var h in holes)
            {
                var next = new List<Rect>();
                foreach (var r in pieces)
                {
                    if (!r.Overlaps(h)) { next.Add(r); continue; }
                    if (h.xMin > r.xMin) next.Add(Rect.MinMaxRect(r.xMin, r.yMin, h.xMin, r.yMax));
                    if (h.xMax < r.xMax) next.Add(Rect.MinMaxRect(h.xMax, r.yMin, r.xMax, r.yMax));
                    float x0 = Mathf.Max(r.xMin, h.xMin), x1 = Mathf.Min(r.xMax, h.xMax);
                    if (h.yMin > r.yMin) next.Add(Rect.MinMaxRect(x0, r.yMin, x1, h.yMin));
                    if (h.yMax < r.yMax) next.Add(Rect.MinMaxRect(x0, h.yMax, x1, r.yMax));
                }
                pieces = next;
            }
            foreach (var r in pieces)
            {
                var c = Climb(P.Box(name, parent, new Vector3(r.center.x, 5.5f, r.center.y), new Vector3(r.width, 0.2f, r.height), material));
                c.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                ceilings.Add(c.GetComponent<Renderer>());
            }
        }
        private static Light PointLight(string name, Transform parent, Vector3 at, Color color, float intensity, float range, bool pixel)
        {
            var light = GeneratedAssets.Empty(name, parent, at).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None; light.renderMode = pixel ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
            return light;
        }

        /// <summary>Builds the whole shop under room. Returns the walls the tactics view hides (the front ones).</summary>
        private static void BuildShop(Transform room)
        {
            fronts.Clear(); ceilings.Clear();
            var k = GeneratedAssets.Empty("Diner_Kitchen", room, Vector3.zero);
            var h = GeneratedAssets.Empty("Diner_Hall", room, Vector3.zero);
            var w = GeneratedAssets.Empty("Diner_DishRoom", room, Vector3.zero);
            var p = GeneratedAssets.Empty("Diner_Pantry", room, Vector3.zero);
            var b = GeneratedAssets.Empty("Diner_Banquet2F", room, Vector3.zero);
            var s = GeneratedAssets.Empty("Diner_Staff2F", room, Vector3.zero);

            // ---- 1F shell ----
            Floor("Floor", k, Kitchen, kitchenFloor);
            Floor("Floor", h, Hall, hallFloor);
            Floor("Floor", w, Wash, washFloor);
            Floor("Floor", p, Pantry, concrete);
            // Front (z -6.12): kitchen with the duct mouth, the hall with the entrance (closed glass door, decoration).
            fronts.AddRange(Wall("Wall_Front", k, true, -6.12f, -7.24f, 9.0f, tileWhite, new Gap(KitchenVent.x - VentWidth / 2, KitchenVent.x + VentWidth / 2, 0, VentHeight)));
            fronts.AddRange(Wall("Wall_Front", h, true, -6.12f, 9.0f, 27.48f, hallWall));
            Wall("Wall_Left", k, false, -7.12f, -6.24f, 6.24f, tileWhite);
            Wall("Wall_Left", w, false, -7.12f, 6.0f, 16.48f, tileWhite);
            Wall("Wall_Right", h, false, 27.36f, -6.24f, 6.24f, hallWall);
            // Kitchen | hall: the staff doorway and the serving hatch over the counter.
            Wall("Wall_KitchenHall", k, false, 9.12f, -6.0f, 6.0f, tileWhite, new Gap(-5.0f, -2.0f), new Gap(1.0f, 4.6f, 2.9f, 4.3f));
            // Back of the ground floor (z 6.12): kitchen -> dish room, kitchen -> pantry; behind the hall solid.
            Wall("Wall_Mid", k, true, 6.12f, -7.0f, 9.0f, tileWhite, new Gap(2.3f, 4.8f), new Gap(6.0f, 8.5f));
            Wall("Wall_Mid", h, true, 6.12f, 9.0f, 27.24f, hallWall);
            Wall("Wall_WashPantry", w, false, 5.12f, 6.24f, 16.24f, tileWhite, new Gap(11.0f, 13.5f));
            Wall("Wall_Right", p, false, 15.12f, 6.0f, 16.48f, concrete);
            Wall("Wall_Back", w, true, 16.36f, -7.24f, 5.0f, tileWhite);
            Wall("Wall_Back", p, true, 16.36f, 5.0f, 15.24f, concrete);
            Ceiling("Ceiling", k, Kitchen, P.Material("Stage03_KitchenCeiling", new Color(0.78f, 0.78f, 0.75f), 0.1f));
            Ceiling("Ceiling", h, Hall, hallWall, Rect.MinMaxRect(StairX0 + 4.4f, StairZ0 - 0.1f, 27.24f, 6.0f));
            Ceiling("Ceiling", w, Wash, tileWhite);
            Ceiling("Ceiling", p, Pantry, concrete);
            // Hall: wood wainscot to waist height on the walls facing the room.
            foreach (var (at, from, to, alongX) in new[] { (-5.98f, 9.3f, 27.2f, true), (5.98f, 9.3f, 27.2f, true), (27.22f, -5.9f, 5.9f, false) })
                P.Box("Wainscot", h, alongX ? new Vector3((from + to) / 2, 0.9f, at) : new Vector3(at, 0.9f, (from + to) / 2), alongX ? new Vector3(to - from, 1.8f, 0.04f) : new Vector3(0.04f, 1.8f, to - from), wainscot, false);

            // ---- 2F shell ----
            P.Box("Floor", b, new Vector3((F2 - 0.25f + WellX0) / 2, -0.15f, 0), new Vector3(WellX0 - F2 + 0.25f, 0.3f, 12.5f), staffFloor);   // up to the well's edge
            P.Box("Floor", b, new Vector3((WellX0 + Banquet.xMax) / 2, -0.15f, (Banquet.yMin + StairZ0 - 0.1f) / 2), new Vector3(Banquet.xMax + 0.25f - WellX0, 0.3f, StairZ0 - 0.1f - Banquet.yMin + 0.25f), staffFloor);
            P.Box("Floor", b, new Vector3((WellX0 + Banquet.xMax) / 2, -0.15f, (StairZ1 + 0.1f + 6.25f) / 2), new Vector3(Banquet.xMax + 0.25f - WellX0, 0.3f, 6.25f - StairZ1 - 0.1f), staffFloor);
            Floor("Floor", s, Staff, staffFloor);
            fronts.AddRange(Wall("Wall_Front", b, true, -6.12f, F2 - 0.24f, Banquet.xMax + 0.24f, plaster));
            Wall("Wall_Left", b, false, F2 - 0.12f, -6.24f, 6.24f, plaster);
            Wall("Wall_Right", b, false, Banquet.xMax + 0.12f, -6.24f, 6.24f, plaster);
            Wall("Wall_Mid", b, true, 6.12f, F2 - 0.24f, Banquet.xMax + 0.24f, plaster, new Gap(48.0f, 50.5f));
            Wall("Wall_Left", s, false, F2 - 0.12f, 6.0f, 16.48f, staffWall);
            Wall("Wall_Right", s, false, Staff.xMax + 0.12f, 6.0f, 16.48f, staffWall);
            Wall("Wall_Back", s, true, 16.36f, F2 - 0.24f, Staff.xMax + 0.24f, staffWall, new Gap(StaffVent.x - VentWidth / 2, StaffVent.x + VentWidth / 2, 0, VentHeight));
            Ceiling("Ceiling", b, Banquet, plaster);
            Ceiling("Ceiling", s, Staff, staffWall);

            FurnishKitchen(k);
            FurnishHall(h);
            FurnishDishRoom(w);
            FurnishPantry(p);
            FurnishBanquet(b);
            FurnishStaff(s);
            BuildStairs(h, b);
            BuildDuct(k, KitchenVent, -1);
            BuildDuct(s, StaffVent, 1);
            foreach (var root in new[] { k, h, w, p, b, s })
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        // ---------------- 1F kitchen ----------------
        private static void FurnishKitchen(Transform k)
        {
            // The range along the back wall: on steel legs (roaches hide underneath), burners and a wok, a stock pot.
            var range = GeneratedAssets.Empty("ChineseRange", k, new Vector3(-3.9f, 0, 5.05f));
            Climb(P.Box("Top", range, new Vector3(0, 2.55f, 0), new Vector3(5.8f, 0.2f, 1.9f), steel));
            Climb(P.Box("Body", range, new Vector3(0, 1.85f, 0.05f), new Vector3(5.8f, 1.2f, 1.8f), steelDark));
            Climb(P.Box("Back", range, new Vector3(0, 3.4f, 0.85f), new Vector3(5.8f, 1.5f, 0.12f), steel));
            foreach (float x in new[] { -2.8f, 2.8f })
                foreach (float z in new[] { -0.85f, 0.85f })
                {
                    P.Rod("Leg", range, new Vector3(x, 0, z), new Vector3(x, 1.25f, z), 0.12f, steel);
                    P.Box("LegCollider", range, new Vector3(x, 0.62f, z), new Vector3(0.12f, 1.25f, 0.12f), steel).GetComponent<MeshRenderer>().enabled = false;
                }
            for (int i = 0; i < 3; i++)
            {
                float x = -1.9f + i * 1.9f;
                P.Rod("Burner", range, new Vector3(x, 2.65f, -0.1f), new Vector3(x, 2.72f, -0.1f), 0.9f, black);
            }
            var wok = P.Sphere("Wok", range, new Vector3(-1.9f, 2.86f, -0.1f), new Vector3(1.3f, 0.36f, 1.3f), black);
            P.Rod("WokHandle", range, new Vector3(-1.3f, 2.95f, -0.3f), new Vector3(-0.4f, 3.05f, -0.75f), 0.08f, darkWood);
            Climb(P.Rod("StockPot", range, new Vector3(1.9f, 2.65f, -0.05f), new Vector3(1.9f, 3.85f, -0.05f), 1.2f, steel));
            P.Shadow("RangeAO", k, new Vector3(-3.9f, 0.006f, 5.0f), new Vector2(6.2f, 2.2f), contact);
            // The fryer beside it, a box of steel with two baskets.
            var fryer = GeneratedAssets.Empty("Fryer", k, new Vector3(0.0f, 0, 5.15f));
            Climb(P.Box("Body", fryer, new Vector3(0, 1.3f, 0), new Vector3(1.6f, 2.6f, 1.7f), steel));
            P.Box("Oil", fryer, new Vector3(0, 2.61f, -0.05f), new Vector3(1.3f, 0.02f, 1.3f), P.Material("Stage03_Oil", new Color(0.55f, 0.36f, 0.08f), 0.9f), false);
            foreach (float x in new[] { -0.32f, 0.32f })
                P.Box("Basket", fryer, new Vector3(x, 2.8f, -0.1f), new Vector3(0.5f, 0.35f, 0.9f), steelDark, false);
            // The island work table: steel top on tall legs (cover underneath), a shelf low down.
            var table = GeneratedAssets.Empty("WorkTable", k, new Vector3(-2.0f, 0, -0.5f));
            Climb(P.Box("Top", table, new Vector3(0, 2.62f, 0), new Vector3(6.0f, 0.16f, 2.2f), steel));
            foreach (float x in new[] { -2.85f, 2.85f })
                foreach (float z in new[] { -1.0f, 1.0f })
                {
                    P.Rod("Leg", table, new Vector3(x, 0, z), new Vector3(x, 2.55f, z), 0.1f, steel);
                    P.Box("LegCollider", table, new Vector3(x, 1.27f, z), new Vector3(0.1f, 2.55f, 0.1f), steel).GetComponent<MeshRenderer>().enabled = false;
                }
            P.Box("Cutting", table, new Vector3(-1.2f, 2.73f, 0.2f), new Vector3(1.6f, 0.06f, 1.0f), cream, false);
            P.Shadow("TableAO", k, new Vector3(-2.0f, 0.006f, -0.5f), new Vector2(6.4f, 2.6f), contact);
            // The tall reach-in fridge by the left wall.
            var fridge = GeneratedAssets.Empty("ReachInFridge", k, new Vector3(-6.15f, 0, -3.0f));
            Climb(P.Box("Body", fridge, new Vector3(0, 2.5f, 0), new Vector3(1.6f, 5.0f, 3.0f), steel));
            foreach (float z in new[] { -0.74f, 0.74f })
            {
                P.Box("Door", fridge, new Vector3(0.81f, 2.6f, z), new Vector3(0.04f, 4.4f, 1.42f), chrome, false);
                P.Rod("Handle", fridge, new Vector3(0.88f, 1.9f, z - 0.55f * Mathf.Sign(z)), new Vector3(0.88f, 3.3f, z - 0.55f * Mathf.Sign(z)), 0.06f, steelDark);
            }
            // Seasoning shelf on the front wall (beside the duct), pots and jars on it.
            var shelf = GeneratedAssets.Empty("SpiceShelf", k, new Vector3(2.4f, 0, -5.55f));
            Climb(P.Box("Body", shelf, new Vector3(0, 1.75f, 0), new Vector3(3.8f, 3.5f, 0.85f), steel));
            Color[] jars = { new Color(0.75f, 0.15f, 0.1f), new Color(0.85f, 0.7f, 0.2f), new Color(0.2f, 0.12f, 0.06f), new Color(0.9f, 0.9f, 0.85f) };
            for (int i = 0; i < 8; i++)
                P.Rod("Jar", shelf, new Vector3(-1.5f + i * 0.42f, 3.5f, 0), new Vector3(-1.5f + i * 0.42f, 3.5f + 0.35f + (i % 3) * 0.1f, 0), 0.26f, P.Material("Stage03_Jar" + (i % 4), jars[i % 4], 0.5f));
            // The serving table under the hatch.
            var pass = GeneratedAssets.Empty("PassTable", k, new Vector3(8.2f, 0, 2.8f));
            Climb(P.Box("Top", pass, new Vector3(0, 2.62f, 0), new Vector3(1.4f, 0.16f, 4.4f), steel));
            foreach (float z in new[] { -2.0f, 2.0f })
            {
                P.Rod("Leg", pass, new Vector3(0, 0, z), new Vector3(0, 2.55f, z), 0.1f, steel);
                P.Box("LegCollider", pass, new Vector3(0, 1.27f, z), new Vector3(0.1f, 2.55f, 0.1f), steel).GetComponent<MeshRenderer>().enabled = false;
            }
            P.Sphere("Bowl", pass, new Vector3(0, 2.85f, 1.0f), new Vector3(0.7f, 0.3f, 0.7f), cream);
            // A rice cooker and a bin by the door.
            Climb(P.Rod("RiceCooker", k, new Vector3(5.6f, 0, 5.2f), new Vector3(5.6f, 1.6f, 5.2f), 1.1f, cream));
            Climb(P.Rod("Bin", k, new Vector3(7.9f, 0, -5.2f), new Vector3(7.9f, 1.3f, -5.2f), 1.0f, steelDark));
            // Noren in the staff doorway to the hall (cloth strips, no collision).
            var noren = P.Material("Stage03_Noren", new Color(0.12f, 0.16f, 0.36f), 0.05f);
            for (int i = 0; i < 4; i++) P.Box("Noren", k, new Vector3(9.12f, 3.6f, -4.6f + i * 0.74f), new Vector3(0.03f, 1.5f, 0.66f), noren, false);
            // Lights: a bright work light over the table, a warm one over the range.
            P.Box("FluorescentTube", k, new Vector3(-2.0f, 5.36f, -0.5f), new Vector3(4.0f, 0.1f, 0.3f), glow, false);
            P.Box("FluorescentTube", k, new Vector3(4.0f, 5.36f, 2.0f), new Vector3(3.0f, 0.1f, 0.3f), glow, false);
            PointLight("KitchenLight", k, new Vector3(-1.0f, 4.6f, 0.5f), new Color(1f, 0.93f, 0.82f), 2.6f, 12f, true);
            PointLight("RangeGlow", k, new Vector3(-3.9f, 3.6f, 4.0f), new Color(1f, 0.62f, 0.3f), 1.2f, 5f, false);
        }

        // ---------------- 1F hall ----------------
        private static void FurnishHall(Transform h)
        {
            // The counter along the kitchen wall: red top on a wood body, stools in front.
            var counter = GeneratedAssets.Empty("Counter", h, new Vector3(10.1f, 0, 2.3f));
            Climb(P.Box("Body", counter, new Vector3(0, 1.25f, 0), new Vector3(1.5f, 2.5f, 7.0f), darkWood));
            Climb(P.Box("Top", counter, new Vector3(0.15f, 2.6f, 0), new Vector3(1.9f, 0.16f, 7.1f), redTop));
            for (int i = 0; i < 4; i++)
            {
                var stool = GeneratedAssets.Empty("Stool", h, new Vector3(11.9f, 0, -0.4f + i * 1.75f));
                P.Rod("Post", stool, Vector3.zero, new Vector3(0, 1.75f, 0), 0.14f, chrome);
                P.Rod("Base", stool, Vector3.zero, new Vector3(0, 0.06f, 0), 0.8f, chrome);
                Climb(P.Rod("Seat", stool, new Vector3(0, 1.75f, 0), new Vector3(0, 1.98f, 0), 0.95f, redVinyl));
                P.Box("PostCollider", stool, new Vector3(0, 0.88f, 0), new Vector3(0.14f, 1.75f, 0.14f), chrome).GetComponent<MeshRenderer>().enabled = false;
            }
            // Four tables with chrome legs and red tops; chairs on two sides.
            foreach (var at in new[] { new Vector3(15.5f, 0, -3.8f), new Vector3(21.5f, 0, -3.8f), new Vector3(15.5f, 0, 0.6f), new Vector3(21.5f, 0, 0.6f) })
            {
                var table = GeneratedAssets.Empty("Table", h, at);
                Climb(P.Box("Top", table, new Vector3(0, 2.55f, 0), new Vector3(3.0f, 0.14f, 1.8f), redTop));
                foreach (float x in new[] { -1.3f, 1.3f })
                    foreach (float z in new[] { -0.7f, 0.7f })
                    {
                        P.Rod("Leg", table, new Vector3(x, 0, z), new Vector3(x, 2.48f, z), 0.09f, chrome);
                        P.Box("LegCollider", table, new Vector3(x, 1.24f, z), new Vector3(0.09f, 2.48f, 0.09f), chrome).GetComponent<MeshRenderer>().enabled = false;
                    }
                P.Rod("SoySauce", table, new Vector3(1.0f, 2.62f, 0.4f), new Vector3(1.0f, 2.95f, 0.4f), 0.16f, black);
                P.Rod("ChopstickPot", table, new Vector3(1.2f, 2.62f, -0.2f), new Vector3(1.2f, 3.0f, -0.2f), 0.22f, lightWood);
                foreach (float z in new[] { -1.45f, 1.45f })
                    foreach (float x in new[] { -0.75f, 0.75f })
                    {
                        var chair = GeneratedAssets.Empty("Chair", table, new Vector3(x, 0, z));
                        Climb(P.Box("Seat", chair, new Vector3(0, 1.55f, 0), new Vector3(0.95f, 0.14f, 0.9f), redVinyl));
                        Climb(P.Box("Back", chair, new Vector3(0, 2.35f, Mathf.Sign(z) * 0.42f), new Vector3(0.95f, 1.4f, 0.08f), redVinyl));
                        foreach (float cx in new[] { -0.4f, 0.4f })
                            foreach (float cz in new[] { -0.38f, 0.38f })
                                P.Rod("ChairLeg", chair, new Vector3(cx, 0, cz), new Vector3(cx, 1.5f, cz), 0.06f, chrome);
                    }
                P.Shadow("TableAO", h, at + new Vector3(0, 0.006f, 0), new Vector2(3.4f, 2.2f), contact);
            }
            // The register by the entrance.
            var register = GeneratedAssets.Empty("Register", h, new Vector3(25.6f, 0, -5.2f));
            Climb(P.Box("Body", register, new Vector3(0, 1.3f, 0), new Vector3(3.0f, 2.6f, 1.4f), darkWood));
            Climb(P.Box("Till", register, new Vector3(0.6f, 2.95f, 0), new Vector3(1.0f, 0.7f, 0.8f), cream));
            P.Sphere("LuckyCat", register, new Vector3(-0.8f, 3.0f, 0), new Vector3(0.45f, 0.65f, 0.4f), cream);
            // The entrance: a glass door in the front wall (closed), menu strips (短冊) along the back wall.
            var glass = P.Material("Stage03_DoorGlass", new Color(0.30f, 0.38f, 0.42f), 0.9f, 0, null, null, new Color(0.08f, 0.1f, 0.12f));
            var door = GeneratedAssets.Empty("Entrance", h, new Vector3(20.5f, 0, -5.97f));
            P.Box("Glass", door, new Vector3(0, 2.15f, 0), new Vector3(2.4f, 4.3f, 0.04f), glass, false);
            P.Box("Frame", door, new Vector3(0, 4.35f, 0), new Vector3(2.4f, 0.12f, 0.08f), chrome, false);   // between the side posts
            foreach (float x in new[] { -1.25f, 1.25f }) P.Box("Frame", door, new Vector3(x, 2.2f, 0), new Vector3(0.1f, 4.4f, 0.08f), chrome, false);
            for (int i = 0; i < 11; i++)
            {
                var strip = P.Box("MenuStrip", h, new Vector3(10.4f + i * 0.62f, 4.4f, 5.97f), new Vector3(0.48f, 1.4f, 0.02f), cream, false);
                P.Box("MenuInk", h, new Vector3(10.4f + i * 0.62f, 4.5f, 5.955f), new Vector3(0.12f, 0.9f, 0.01f), P.Material("Stage03_MenuInk", new Color(0.12f, 0.08f, 0.06f), 0.1f), false);
            }
            // Red paper lanterns and warm pendants.
            var lantern = P.Material("Stage03_Lantern", new Color(0.8f, 0.12f, 0.08f), 0.2f, 0, null, null, new Color(0.45f, 0.08f, 0.03f));
            foreach (var at in new[] { new Vector3(18.5f, 4.6f, -1.6f), new Vector3(24.5f, 4.6f, -1.6f) })
            {
                P.Rod("Cord", h, at + Vector3.up * 0.4f, at + Vector3.up * 0.9f, 0.03f, black);
                P.Sphere("Lantern", h, at, new Vector3(0.7f, 0.9f, 0.7f), lantern);
            }
            P.Box("FluorescentTube", h, new Vector3(15.5f, 5.36f, -1.6f), new Vector3(3.0f, 0.1f, 0.3f), glow, false);
            PointLight("HallLight", h, new Vector3(17.5f, 4.5f, -1.0f), new Color(1f, 0.82f, 0.6f), 2.6f, 13f, true);
            PointLight("LanternGlow", h, new Vector3(24.5f, 4.2f, -1.6f), new Color(1f, 0.4f, 0.25f), 1.0f, 5f, false);
            PointLight("CounterGlow", h, new Vector3(11.5f, 4.0f, 2.3f), new Color(1f, 0.85f, 0.6f), 1.2f, 6f, false);
        }

        // ---------------- 1F dish room (the nest) ----------------
        private static void FurnishDishRoom(Transform w)
        {
            BuildSinkNest(w);
            // Dishwasher beside the sink, a dish rack on the left wall, a low work table (the refuge) and the slop bucket.
            Climb(P.Box("Dishwasher", w, new Vector3(1.6f, 1.45f, 15.25f), new Vector3(1.8f, 2.9f, 1.9f), steel));
            P.Box("DishwasherDoor", w, new Vector3(1.6f, 1.6f, 14.28f), new Vector3(1.6f, 1.9f, 0.04f), chrome, false);
            var rack = GeneratedAssets.Empty("DishRack", w, new Vector3(-6.45f, 0, 9.6f));
            Climb(P.Box("Body", rack, new Vector3(0, 2.1f, 0), new Vector3(1.1f, 4.2f, 3.6f), steel));
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 6; i++)
                    P.Rod("Plate", rack, new Vector3(0.45f, 1.0f + row * 1.2f, -1.4f + i * 0.55f), new Vector3(0.45f, 1.0f + row * 1.2f + 0.6f, -1.4f + i * 0.55f), 0.55f, cream).localRotation = Quaternion.Euler(0, 0, 90);
            var table = GeneratedAssets.Empty("PrepTable", w, NestRefuge);
            Climb(P.Box("Top", table, new Vector3(0, 2.55f, 0), new Vector3(3.2f, 0.14f, 1.6f), steel));
            foreach (float x in new[] { -1.45f, 1.45f })
                foreach (float z in new[] { -0.65f, 0.65f })
                {
                    P.Rod("Leg", table, new Vector3(x, 0, z), new Vector3(x, 2.48f, z), 0.09f, steel);
                    P.Box("LegCollider", table, new Vector3(x, 1.24f, z), new Vector3(0.09f, 2.48f, 0.09f), steel).GetComponent<MeshRenderer>().enabled = false;
                }
            Climb(P.Rod("SlopBucket", w, new Vector3(3.9f, 0, 7.4f), new Vector3(3.9f, 1.5f, 7.4f), 1.2f, P.Material("Stage03_Bucket", new Color(0.18f, 0.32f, 0.55f), 0.4f)));
            // The back door (where the staff leave).
            var door = GeneratedAssets.Empty("BackDoor", w, new Vector3(-6.97f, 0, ExitPoint.z));
            P.Box("Panel", door, new Vector3(0, 2.15f, 0), new Vector3(0.05f, 4.3f, 1.9f), P.Material("Stage03_BackDoor", new Color(0.36f, 0.40f, 0.38f), 0.3f, 0.3f), false);
            P.Sphere("Knob", door, new Vector3(0.08f, 2.0f, 0.7f), Vector3.one * 0.14f, chrome);
            // A drain grating in the floor.
            P.Box("Drain", w, new Vector3(-1.0f, 0.004f, 11.5f), new Vector3(0.8f, 0.008f, 3.2f), steelDark, false);
            P.Box("FluorescentTube", w, new Vector3(-1.0f, 5.36f, 11.0f), new Vector3(3.0f, 0.1f, 0.3f), glow, false);
            PointLight("DishLight", w, new Vector3(-1.0f, 4.5f, 11.0f), new Color(0.9f, 0.95f, 1f), 2.2f, 11f, true);
        }

        /// <summary>
        /// The twin sink on the back wall, hollow underneath like the Stage 1 cabinet: steel sides, back and deck, closed
        /// doors but one hanging open. Inside: the grease trap box, egg cases, scraps, a dim warm light. The nest.
        /// </summary>
        private static Transform BuildSinkNest(Transform w)
        {
            var sink = GeneratedAssets.Empty("SinkCabinet", w, SinkCentre);
            const float half = 2.95f;
            Climb(P.Box("Side", sink, new Vector3(-half, 1.325f, 0), new Vector3(0.1f, 2.65f, 2.05f), steel));
            Climb(P.Box("Side", sink, new Vector3(half, 1.325f, 0), new Vector3(0.1f, 2.65f, 2.05f), steel));
            Climb(P.Box("Back", sink, new Vector3(0, 1.32f, 0.975f), new Vector3(5.8f, 2.64f, 0.1f), steel));
            Climb(P.Box("Deck", sink, new Vector3(0, 2.6f, 0), new Vector3(5.8f, 0.1f, 2.05f), steel));
            Climb(P.Box("Worktop", sink, new Vector3(0, 2.72f, 0), new Vector3(6.0f, 0.14f, 2.1f), steel));
            Climb(P.Box("Rail", sink, new Vector3(0, 2.56f, -1.0f), new Vector3(5.8f, 0.16f, 0.1f), steel));
            foreach (float x in new[] { -1.4f, 1.0f }) P.Box("Basin", sink, new Vector3(x, 2.8f, -0.05f), new Vector3(2.1f, 0.03f, 1.5f), steelDark, false);
            foreach (float x in new[] { -1.4f, 1.0f })
            {
                P.Rod("Tap", sink, new Vector3(x, 2.79f, 0.8f), new Vector3(x, 3.6f, 0.8f), 0.1f, chrome);
                P.Rod("Spout", sink, new Vector3(x, 3.6f, 0.8f), new Vector3(x, 3.6f, 0.4f), 0.08f, chrome);
            }
            var inside = P.Material("Phase5_CabinetInside", new Color(0.075f, 0.06f, 0.05f), 0.05f);
            var stain = P.Material("Phase5_CabinetFloor", new Color(0.12f, 0.095f, 0.07f), 0.25f);
            P.Box("InsideBack", sink, new Vector3(0, 1.3f, 0.92f), new Vector3(5.8f, 2.5f, 0.01f), inside, false);
            P.Box("InsideTop", sink, new Vector3(0, 2.545f, -0.04f), new Vector3(5.8f, 0.01f, 1.9f), inside, false);
            P.Box("DampFloor", sink, new Vector3(0, 0.004f, -0.04f), new Vector3(5.8f, 0.006f, 1.9f), stain, false);
            // Doors left to right; the second hangs open on its left hinge.
            for (int i = 0; i < 4; i++)
            {
                float x = -2.2f + i * 1.47f;
                if (i == 1)
                {
                    var hinge = GeneratedAssets.Empty("OpenDoorHinge", sink, new Vector3(x - 0.7f, 0, -1.04f));
                    var leaf = P.Box("Door", hinge, new Vector3(0.7f, 1.42f, 0), new Vector3(1.4f, 2.1f, 0.06f), steel);
                    Climb(leaf);
                    hinge.localRotation = Quaternion.Euler(0, 60, 0);
                    var obstacle = leaf.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box; obstacle.size = Vector3.one; obstacle.carving = true;
                    continue;
                }
                Climb(P.Box("Door", sink, new Vector3(x, 1.42f, -1.0f), new Vector3(1.4f, 2.1f, 0.06f), steel));
                Climb(P.Box("Plinth", sink, new Vector3(x, 0.18f, -0.95f), new Vector3(1.44f, 0.36f, 0.1f), steelDark));
                P.Rod("Handle", sink, new Vector3(x + 0.5f, 1.7f, -1.06f), new Vector3(x + 0.5f, 2.2f, -1.06f), 0.05f, chrome);
            }
            // The grease trap: a steel box with a ribbed lid at the back (set dressing, no collision).
            P.Box("GreaseTrap", sink, new Vector3(1.6f, 0.35f, 0.45f), new Vector3(1.6f, 0.7f, 0.8f), steelDark, false);
            for (int i = 0; i < 5; i++) P.Box("TrapRib", sink, new Vector3(1.0f + i * 0.3f, 0.72f, 0.45f), new Vector3(0.06f, 0.04f, 0.78f), steel, false);
            P.Rod("DrainPipe", sink, new Vector3(-1.4f, 2.55f, 0.2f), new Vector3(-1.4f, 1.2f, 0.2f), 0.12f, steelDark);
            P.Rod("DrainPipe", sink, new Vector3(-1.4f, 1.2f, 0.2f), new Vector3(1.0f, 0.75f, 0.45f), 0.12f, steelDark);
            var ootheca = P.Material("Phase5_EggCase", new Color(0.24f, 0.11f, 0.05f), 0.45f);
            var paper = P.Material("Phase5_PaperScrap", new Color(0.62f, 0.58f, 0.48f), 0.05f);
            var dirt = P.Material("Phase5_Droppings", new Color(0.04f, 0.03f, 0.025f), 0.1f);
            Vector3[] eggs = { new Vector3(-2.5f, 0.05f, 0.7f), new Vector3(-2.3f, 0.05f, 0.75f), new Vector3(-0.3f, 0.05f, 0.78f), new Vector3(2.6f, 0.05f, 0.55f) };
            for (int i = 0; i < eggs.Length; i++) P.Sphere("EggCase", sink, eggs[i], new Vector3(0.17f, 0.085f, 0.095f), ootheca).localRotation = Quaternion.Euler(0, 20 + i * 47, 0);
            for (int i = 0; i < 7; i++)
                P.Box("PaperScrap", sink, new Vector3(-2.3f + i * 0.78f, 0.012f + (i % 2) * 0.006f, 0.25f - (i % 3) * 0.32f), new Vector3(0.42f, 0.008f, 0.3f), paper, false).localRotation = Quaternion.Euler(0, i * 53, (i % 2) * 4);
            for (int i = 0; i < 16; i++)
                P.Sphere("Dropping", sink, new Vector3(-2.6f + ((i * 37) % 52) * 0.1f, 0.015f, -0.75f + ((i * 13) % 15) * 0.1f), Vector3.one * 0.035f, dirt);
            var light = GeneratedAssets.Empty("NestGlow", sink, new Vector3(0.4f, 1.6f, -0.1f)).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 3.4f; light.intensity = 0.55f; light.color = new Color(1f, 0.72f, 0.45f); light.shadows = LightShadows.None;
            return sink;
        }

        // ---------------- 1F pantry ----------------
        private static void FurnishPantry(Transform p)
        {
            // The walk-in fridge (closed for now: its door opens in a later step), steel shelving, sacks and boxes.
            var walkIn = GeneratedAssets.Empty("WalkInFridge", p, new Vector3(12.75f, 0, 13.9f));
            Climb(P.Box("Body", walkIn, new Vector3(0, 2.5f, 0), new Vector3(4.5f, 5.0f, 4.7f), P.Material("Stage03_WalkIn", new Color(0.78f, 0.80f, 0.82f), 0.5f, 0.3f)));
            P.Box("Door", walkIn, new Vector3(-0.4f, 2.2f, -2.37f), new Vector3(1.8f, 4.2f, 0.06f), chrome, false);
            P.Box("Handle", walkIn, new Vector3(0.3f, 2.2f, -2.45f), new Vector3(0.12f, 1.0f, 0.1f), steelDark, false);
            P.Box("Thermo", walkIn, new Vector3(1.4f, 3.6f, -2.38f), new Vector3(0.5f, 0.3f, 0.04f), black, false);
            var shelf = GeneratedAssets.Empty("SteelShelf", p, new Vector3(14.45f, 0, 8.6f));
            for (int level = 0; level < 4; level++) Climb(P.Box("Shelf", shelf, new Vector3(0, 0.4f + level * 1.2f, 0), new Vector3(1.0f, 0.08f, 3.8f), steel));
            foreach (float z in new[] { -1.85f, 1.85f }) Climb(P.Box("Post", shelf, new Vector3(0, 2.2f, z), new Vector3(1.04f, 4.4f, 0.08f), steel));
            for (int level = 0; level < 3; level++)
                for (int i = 0; i < 3; i++)
                    P.Box("Box", shelf, new Vector3(-0.05f, 0.85f + level * 1.2f, -1.2f + i * 1.2f), new Vector3(0.8f, 0.8f, 1.0f), cardboard, false);
            var sack = P.Material("Stage03_RiceSack", new Color(0.86f, 0.82f, 0.70f), 0.08f);
            foreach (var (at, size) in new[] { (new Vector3(6.4f, 0.55f, 15.2f), new Vector3(1.6f, 1.1f, 1.1f)), (new Vector3(6.4f, 1.55f, 15.15f), new Vector3(1.5f, 0.9f, 1.0f)),
                (new Vector3(8.3f, 0.5f, 15.3f), new Vector3(1.6f, 1.0f, 1.0f)), (new Vector3(6.5f, 0.5f, 13.6f), new Vector3(1.5f, 1.0f, 1.2f)) })
                Climb(P.Box("Sack", p, at, size, sack));
            Climb(P.Box("Carton", p, new Vector3(7.2f, 0.6f, 8.0f), new Vector3(1.6f, 1.2f, 1.4f), cardboard));
            Climb(P.Box("Carton", p, new Vector3(7.3f, 1.65f, 8.05f), new Vector3(1.3f, 0.9f, 1.2f), cardboard));
            P.Box("FluorescentTube", p, new Vector3(9.5f, 5.36f, 10.5f), new Vector3(2.4f, 0.1f, 0.3f), glow, false);
            PointLight("PantryLight", p, new Vector3(9.5f, 4.5f, 10.5f), new Color(0.95f, 0.92f, 0.85f), 1.8f, 9f, false);
        }

        // ---------------- 2F banquet room ----------------
        private static void FurnishBanquet(Transform b)
        {
            // Tatami mats over the floor (thin, below the floor markings), a raised alcove (床の間) with a scroll and a vase.
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < 6; z++)
                {
                    float cx = F2 + 1.1f + x * 2.25f, cz = -5.0f + z * 2.0f;
                    if (cx > WellX0 - 1.0f && cz > StairZ0 - 1.2f) continue;   // round the stairwell
                    P.Box("Tatami", b, new Vector3(cx, 0.005f, cz), new Vector3(2.2f, 0.01f, 1.96f), tatami, false);   // below every floor marking
                }
            var alcove = GeneratedAssets.Empty("Tokonoma", b, new Vector3(F2 + 0.6f, 0, 3.2f));
            Climb(P.Box("Dais", alcove, new Vector3(0, 0.15f, 0), new Vector3(1.0f, 0.3f, 4.0f), darkWood));
            P.Box("Scroll", alcove, new Vector3(-0.42f, 3.0f, 0), new Vector3(0.03f, 2.6f, 1.0f), cream, false);
            P.Box("ScrollInk", alcove, new Vector3(-0.40f, 3.1f, 0), new Vector3(0.02f, 1.6f, 0.18f), black, false);
            Climb(P.Rod("Vase", alcove, new Vector3(0, 0.3f, -1.2f), new Vector3(0, 1.3f, -1.2f), 0.5f, P.Material("Stage03_Vase", new Color(0.2f, 0.32f, 0.5f), 0.7f)));
            // Low tables (座卓): roaches fit underneath, the slipper does not. Cushions round them.
            var cushion = P.Material("Stage03_Zabuton", new Color(0.36f, 0.12f, 0.30f), 0.06f);
            foreach (var at in new[] { new Vector3(49.0f, 0, -2.0f), new Vector3(54.5f, 0, -2.0f), new Vector3(49.0f, 0, 2.6f) })
            {
                var table = GeneratedAssets.Empty("LowTable", b, at);
                Climb(P.Box("Top", table, new Vector3(0, 1.05f, 0), new Vector3(4.0f, 0.12f, 2.2f), darkWood));
                foreach (float x in new[] { -1.8f, 1.8f })
                    foreach (float z in new[] { -0.9f, 0.9f })
                        Climb(P.Box("Leg", table, new Vector3(x, 0.5f, z), new Vector3(0.18f, 1.0f, 0.18f), darkWood));
                foreach (float x in new[] { -1.2f, 0, 1.2f })
                    foreach (float z in new[] { -1.65f, 1.65f })
                        P.Box("Zabuton", b, at + new Vector3(x, 0.08f, z), new Vector3(1.0f, 0.14f, 1.0f), cushion, false);
                P.Sphere("Platter", table, new Vector3(-1.0f, 1.16f, 0), new Vector3(1.1f, 0.06f, 1.1f), cream);
                P.Shadow("TableAO", b, at + new Vector3(0, 0.02f, 0), new Vector2(4.4f, 2.6f), contact);
            }
            // Shoji windows on the front wall and a hanging lamp.
            var shoji = P.Material("Stage03_Shoji", new Color(0.94f, 0.92f, 0.84f), 0.05f, 0, null, null, new Color(0.25f, 0.24f, 0.2f));
            for (int i = 0; i < 3; i++)
            {
                float x = F2 + 3.5f + i * 5.0f;
                P.Box("Shoji", b, new Vector3(x, 3.1f, -5.98f), new Vector3(3.2f, 2.2f, 0.02f), shoji, false);
                for (int j = 0; j < 4; j++) P.Box("ShojiBar", b, new Vector3(x - 1.2f + j * 0.8f, 3.1f, -5.96f), new Vector3(0.05f, 2.2f, 0.02f), darkWood, false);
                P.Box("ShojiBar", b, new Vector3(x, 3.1f, -5.96f), new Vector3(3.2f, 0.05f, 0.02f), darkWood, false);
            }
            P.Sphere("Lamp", b, new Vector3(51.5f, 4.9f, 0.3f), new Vector3(1.6f, 0.6f, 1.6f), glow);
            PointLight("BanquetLight", b, new Vector3(51.5f, 4.3f, 0.3f), new Color(1f, 0.84f, 0.62f), 2.4f, 13f, true);
            PointLight("WellFill", b, new Vector3(58.5f, 4.0f, -1.0f), new Color(1f, 0.86f, 0.66f), 1.6f, 9f, true);   // the far end by the stairwell
        }

        // ---------------- 2F staff room ----------------
        private static void FurnishStaff(Transform s)
        {
            var lockers = GeneratedAssets.Empty("Lockers", s, new Vector3(52.6f, 0, 15.6f));
            Climb(P.Box("Body", lockers, new Vector3(0, 2.4f, 0), new Vector3(5.4f, 4.8f, 1.1f), P.Material("Stage03_Locker", new Color(0.46f, 0.52f, 0.56f), 0.4f, 0.3f)));
            for (int i = 0; i < 4; i++)
            {
                P.Box("LockerSeam", lockers, new Vector3(-2.0f + i * 1.35f, 2.4f, -0.56f), new Vector3(0.03f, 4.6f, 0.02f), steelDark, false);
                P.Box("Vent", lockers, new Vector3(-2.0f + i * 1.35f + 0.67f, 4.0f, -0.56f), new Vector3(0.6f, 0.25f, 0.02f), steelDark, false);
            }
            // The staff table with stools, a sofa on legs for naps, a desk with a small TV.
            var table = GeneratedAssets.Empty("StaffTable", s, new Vector3(47.5f, 0, 10.0f));
            Climb(P.Box("Top", table, new Vector3(0, 2.5f, 0), new Vector3(3.0f, 0.12f, 2.0f), lightWood));
            foreach (float x in new[] { -1.3f, 1.3f })
                foreach (float z in new[] { -0.8f, 0.8f })
                {
                    P.Rod("Leg", table, new Vector3(x, 0, z), new Vector3(x, 2.45f, z), 0.1f, steelDark);
                    P.Box("LegCollider", table, new Vector3(x, 1.22f, z), new Vector3(0.1f, 2.45f, 0.1f), steelDark).GetComponent<MeshRenderer>().enabled = false;
                }
            foreach (var at in new[] { new Vector3(46.6f, 0, 8.4f), new Vector3(48.4f, 0, 8.4f), new Vector3(47.5f, 0, 11.7f) })
            {
                Climb(P.Rod("Stool", s, at, at + Vector3.up * 1.6f, 0.8f, redVinyl));
            }
            var sofa = GeneratedAssets.Empty("NapSofa", s, new Vector3(44.9f, 0, 13.0f));
            var fabric = P.Material("Stage03_SofaFabric", new Color(0.28f, 0.22f, 0.16f), 0.06f);
            Climb(P.Box("Seat", sofa, new Vector3(0, 1.45f, 0), new Vector3(1.6f, 0.55f, 4.4f), fabric));
            Climb(P.Box("Back", sofa, new Vector3(-0.65f, 2.4f, 0), new Vector3(0.4f, 1.6f, 4.3f), fabric));
            foreach (float z in new[] { -2.0f, 2.0f })
                foreach (float x in new[] { -0.6f, 0.6f })
                {
                    P.Rod("Leg", sofa, new Vector3(x, 0, z), new Vector3(x, 1.18f, z), 0.14f, darkWood);
                    P.Box("LegCollider", sofa, new Vector3(x, 0.59f, z), new Vector3(0.14f, 1.18f, 0.14f), darkWood).GetComponent<MeshRenderer>().enabled = false;
                }
            var desk = GeneratedAssets.Empty("Desk", s, new Vector3(54.4f, 0, 9.4f));
            Climb(P.Box("Top", desk, new Vector3(0, 2.45f, 0), new Vector3(2.4f, 0.12f, 3.2f), lightWood));
            Climb(P.Box("Drawers", desk, new Vector3(0.5f, 1.2f, -0.9f), new Vector3(1.3f, 2.4f, 1.3f), lightWood));
            P.Box("DeskLegCollider", desk, new Vector3(0.5f, 1.2f, 1.45f), new Vector3(1.3f, 2.4f, 0.1f), lightWood);
            Climb(P.Box("Tv", desk, new Vector3(0.6f, 3.1f, 0.6f), new Vector3(0.3f, 1.1f, 1.6f), black));
            P.Box("Papers", desk, new Vector3(-0.3f, 2.53f, -0.6f), new Vector3(0.9f, 0.04f, 1.2f), cream, false);
            P.Box("FluorescentTube", s, new Vector3(50.0f, 5.36f, 11.2f), new Vector3(3.0f, 0.1f, 0.3f), glow, false);
            PointLight("StaffLight", s, new Vector3(50.0f, 4.5f, 11.2f), new Color(0.95f, 0.95f, 0.9f), 2.0f, 11f, false);
        }

        // ---------------- Stairs and ducts ----------------
        private static void BuildStairs(Transform h, Transform b)
        {
            // 1F: thirteen solid wooden steps up the hall's back wall into the opening in the ceiling.
            var stairs = GeneratedAssets.Empty("Stairs1F", h, Vector3.zero);
            for (int i = 0; i < StepCount; i++)
            {
                float x0 = StairX0 + i * StepRun, top = (i + 1) * StepRise;
                Climb(P.Box("Step", stairs, new Vector3(x0 + StepRun / 2, top / 2, StairZ), new Vector3(StepRun, top, StairZ1 - StairZ0), lightWood));
                P.Box("Nosing", stairs, new Vector3(x0 + 0.02f, top - 0.02f, StairZ), new Vector3(0.1f, 0.05f, StairZ1 - StairZ0 + 0.02f), darkWood, false);   // proud of the riser
            }
            // An invisible ramp along the nosings: roaches walk up and down the flight (the 0.42 risers would make them
            // climb every step). Left out of the human NavMesh (people take the stairs as a link).
            Ramp(stairs, new Vector3(StairX0 - StepRun, 0, StairZ), new Vector3(StairX0 + (StepCount - 1) * StepRun, StepCount * StepRise, StairZ));
            float landingX0 = StairX0 + StepCount * StepRun;
            Climb(P.Box("Landing", stairs, new Vector3((landingX0 + 27.24f) / 2, StepCount * StepRise / 2, StairZ), new Vector3(27.24f - landingX0, StepCount * StepRise, StairZ1 - StairZ0), lightWood));
            for (int i = 0; i <= 6; i++)
            {
                float x = StairX0 + 0.3f + i * 1.4f, y = (x - StairX0) / StepRun * StepRise;
                P.Rod("Baluster", stairs, new Vector3(x, y, StairZ0 + 0.05f), new Vector3(x, y + 1.6f, StairZ0 + 0.05f), 0.07f, darkWood);
            }
            P.Rod("Handrail", stairs, new Vector3(StairX0 + 0.3f, 1.6f + 0.2f, StairZ0 + 0.05f), new Vector3(StairX0 + 8.7f, 1.6f + 8.4f / StepRun * StepRise, StairZ0 + 0.05f), 0.09f, darkWood);
            // The shaft above the ceiling opening: dark walls so nothing outside shows.
            var shaftMat = P.Material("Stage03_Shaft", new Color(0.12f, 0.1f, 0.08f), 0.05f);
            float sx0 = StairX0 + 4.4f;
            // (No two faces in one plane: the roof overhangs the walls, the walls stop under it.)
            P.Box("Shaft", stairs, new Vector3((sx0 + 27.24f) / 2, 7.45f, StairZ0 - 0.2f), new Vector3(27.24f - sx0, 3.9f, 0.2f), shaftMat, false);
            P.Box("Shaft", stairs, new Vector3((sx0 - 0.25f + 27.3f) / 2, 9.4f, StairZ), new Vector3(27.3f - sx0 + 0.25f, 0.2f, StairZ1 - StairZ0 + 0.7f), shaftMat, false);
            P.Box("Shaft", stairs, new Vector3(sx0 - 0.1f, 7.45f, StairZ - 0.02f), new Vector3(0.2f, 3.9f, StairZ1 - StairZ0 + 0.5f), shaftMat, false);

            // 2F: the well going down at the end of the banquet room, behind a rail, steps sinking into the dark.
            var well = GeneratedAssets.Empty("StairWell2F", b, Vector3.zero);
            for (int i = 0; i < 7; i++)
            {
                float x0 = WellX0 + i * StepRun, top = -(i + 1) * StepRise;
                Climb(P.Box("Step", well, new Vector3(x0 + StepRun / 2, (top - 3.6f) / 2, StairZ), new Vector3(StepRun, top + 3.6f, StairZ1 - StairZ0), lightWood));
            }
            Ramp(well, new Vector3(WellX0, 0, StairZ), new Vector3(Banquet.xMax - 0.1f, -(Banquet.xMax - 0.1f - WellX0) * StepRise / StepRun, StairZ));
            Climb(P.Box("WellSide", well, new Vector3((WellX0 + Banquet.xMax) / 2, -1.8f, StairZ0 - 0.15f), new Vector3(Banquet.xMax - WellX0 + 0.1f, 3.6f, 0.1f), shaftMat));
            Climb(P.Box("WellSide", well, new Vector3((WellX0 + Banquet.xMax) / 2, -1.8f, StairZ1 + 0.2f), new Vector3(Banquet.xMax - WellX0 + 0.1f, 3.6f, 0.1f), shaftMat));
            Climb(P.Box("WellEnd", well, new Vector3(Banquet.xMax - 0.05f, -1.85f, StairZ), new Vector3(0.1f, 3.5f, StairZ1 - StairZ0 + 0.3f), shaftMat));
            Climb(P.Box("WellBottom", well, new Vector3((WellX0 + Banquet.xMax) / 2, -3.65f, StairZ), new Vector3(Banquet.xMax - WellX0, 0.1f, StairZ1 - StairZ0 + 0.4f), shaftMat));
            for (int i = 0; i <= 3; i++)
            {
                float x = WellX0 + 0.2f + i * 1.4f;
                P.Rod("Baluster", well, new Vector3(x, 0, StairZ0 - 0.15f), new Vector3(x, 1.7f, StairZ0 - 0.15f), 0.07f, darkWood);
            }
            P.Rod("Handrail", well, new Vector3(WellX0 + 0.2f, 1.7f, StairZ0 - 0.15f), new Vector3(Banquet.xMax - 0.2f, 1.7f, StairZ0 - 0.15f), 0.09f, darkWood);
        }

        public const string RampName = "StairRamp";
        /// <summary>A thin invisible slab whose top runs from bottom to top (along x), as wide as the flight.</summary>
        private static void Ramp(Transform parent, Vector3 bottom, Vector3 top)
        {
            Vector3 run = top - bottom;
            float angle = Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0, 0, angle);
            var ramp = P.Box(RampName, parent, (bottom + top) / 2 - rotation * Vector3.up * 0.05f, new Vector3(run.magnitude, 0.1f, StairZ1 - StairZ0), steel);
            ramp.localRotation = rotation;
            ramp.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>
        /// A grated duct mouth low in a wall, a short closed steel duct behind it (side = which way the duct runs, in z).
        /// Big enough for a roach (body radius about 0.47): VentWidth x VentHeight, DuctDepth deep.
        /// </summary>
        public const float VentWidth = 1.3f, VentHeight = 1.1f, DuctDepth = 1.6f;
        private static void BuildDuct(Transform parent, Vector3 mouth, int side)
        {
            var duct = GeneratedAssets.Empty("Duct", parent, mouth);
            float w = VentWidth + 0.2f, z = side * (0.12f + DuctDepth / 2);
            Climb(P.Box("DuctFloor", duct, new Vector3(0, -0.02f, z), new Vector3(w, 0.04f, DuctDepth), steelDark));
            Climb(P.Box("DuctTop", duct, new Vector3(0, VentHeight + 0.05f, z), new Vector3(w, 0.1f, DuctDepth), steelDark));
            foreach (float x in new[] { -w / 2, w / 2 }) Climb(P.Box("DuctSide", duct, new Vector3(x, VentHeight / 2, z), new Vector3(0.1f, VentHeight + 0.1f, DuctDepth), steelDark));
            Climb(P.Box("DuctEnd", duct, new Vector3(0, VentHeight / 2, side * (0.12f + DuctDepth)), new Vector3(w, VentHeight + 0.1f, 0.1f), steelDark));
            // The grate swung open at the mouth (the roaches pushed it), the inside dark.
            P.Box("Inside", duct, new Vector3(0, VentHeight / 2, side * (0.12f + DuctDepth - 0.06f)), new Vector3(VentWidth - 0.1f, VentHeight - 0.1f, 0.01f), black, false);
            var grate = GeneratedAssets.Empty("Grate", duct, new Vector3(-VentWidth / 2, 0, -side * 0.14f));
            for (int i = 0; i < 7; i++) P.Box("Bar", grate, new Vector3(VentWidth / 2, 0.1f + i * 0.15f, 0), new Vector3(VentWidth, 0.04f, 0.03f), chrome, false);
            grate.localRotation = Quaternion.Euler(0, side * 70, 0);
        }

        private static Transform Climb(Transform part)
        {
            if (!part.GetComponent<Collider>()) part.gameObject.AddComponent<BoxCollider>();
            if (!part.GetComponent<ClimbableSurface>()) part.gameObject.AddComponent<ClimbableSurface>();
            return part;
        }
    }
}
