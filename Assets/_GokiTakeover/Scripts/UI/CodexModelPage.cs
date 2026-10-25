using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Encyclopedia tabs that show models, shared by the title and the pause menu: a list on the left, the Blender
    /// model on a turntable (drag to turn, wheel to zoom, ←→ also turn) and a comical write-up on the right.
    /// The food tab has the five foods, the human tab the resident and the exterminator. Entries not met yet show
    /// as a silhouette with "？？？". ↑↓ pick an entry.
    /// </summary>
    public static class CodexModelPage
    {
        // Indexes into CodexViewer's models: the five FoodValue.Kinds, then the two people.
        public const int ResidentModel = 5, ExterminatorModel = 6, MotherModel = 7, SonModel = 8;
        public struct Page
        {
            public int model;
            public string id, name, badge, tag, nickname, body, rating;
        }
        public sealed class Book
        {
            public Page[] pages;
            public int selected;
            public string hint, ratingLabel, unknownBody;
        }

        public static readonly Book Foods = new Book
        {
            hint = "巣へ運んだ食料が記録される。\n↑↓ 選ぶ   ←→・ドラッグ 回す", ratingLabel = "ゴキ評価",
            unknownBody = "見つけて巣へ運ぶと、ここに記録される。シルエットから正体を想像しよう。",
            pages = new[]
            {
                Food(FoodValue.Kind.Breadcrumb, null, "トーストのこぼれ話",
                    "人間がトーストをかじるたびに床へ降ってくる、天からの恵み。外はカリッ、中はふわっ、床はザラッ。ゴキブリ界では「主食」と呼ばれる。迷ったら持ち帰れ。迷わなくても持ち帰れ。",
                    "★★★☆☆  「ふつうにうまい。ふつうが一番むずかしい」"),
                Food(FoodValue.Kind.Rice, "food_Rice", "炊飯器からの脱走者",
                    "しゃもじのすきをついて逃げ出した白い一粒。一粒に七人の神様が宿るというが、運ぶのはゴキブリ一匹。乾くと石のように固くなるので、ツヤのあるうちにどうぞ。",
                    "★★☆☆☆  「小さい。でもツヤがいい」"),
                Food(FoodValue.Kind.Sweets, "food_Sweets", "「あと一枚だけ」の残骸",
                    "人間が「あと一枚だけ」と言いながら残した、かじりかけのチョコチップクッキー。歯型は持ち主のもの。甘さで仲間の士気は上がるが、目立つ場所に置かれがち。",
                    "★★★★☆  「罪の味がする」"),
                Food(FoodValue.Kind.Leftovers, "food_Leftovers", "ひと口で飽きられたごちそう",
                    "ひと口かじって放置された骨付きチキン。こんなごちそうを残すとは、人間はやはり理解できない。重くて足は遅くなるが、価値はパンくず3つぶん。骨はちょうどいい持ち手。",
                    "★★★★★  「虫生が変わる」"),
                Food(FoodValue.Kind.Garbage, "food_Garbage", "すべらせ係、引退",
                    "人間を転ばせる伝説の罠……と思いきや、ただの捨て忘れ。内側の白いすじは通好みの熟成の味。人間の通り道の近くに落ちていがち。ちなみにゴキブリは滑らない。",
                    "★★★☆☆  「熟成された甘み」"),
            }
        };

        public static readonly Book Humans = new Book
        {
            hint = "出会った人間が記録される。\n↑↓ 選ぶ   ←→・ドラッグ 回す", ratingLabel = "危険度",
            unknownBody = "まだ会っていない。会わずに済むなら、それが一番。",
            pages = new[]
            {
                new Page { model = ResidentModel, id = "resident", name = "住人", badge = "▲ ★★★", tag = "武器: スリッパ・罠・毒餌・スプレー",
                    nickname = "台所の主（自称）",
                    body = "このワンルームの持ち主。夜中のカップ麺と食べこぼしの量はプロ級で、我々にとっては最大の食料供給源。悲鳴は大きいが、スリッパを握ると急に強気になる。弱点は自分のだらしなさ。",
                    rating = "★★★☆☆  「スリッパの素振りだけは一流」" },
                new Page { model = ExterminatorModel, id = "fogger", name = "駆除業者", badge = "▲ ★★★★★", tag = "武器: 燻煙剤・噴霧器・スリッパ",
                    nickname = "白装束の最終兵器",
                    body = "住人が半泣きで呼んだプロ。燻煙剤を焚き、巣まで燻し、天井に逃げれば背中のタンクから噴霧器で撒いてくる。最新装備に身を包みながら、最後に頼るのはなぜかスリッパ。",
                    rating = "★★★★★  「目が合ったら終わり」" },
                new Page { model = MotherModel, id = "mother", name = "一軒家の母", badge = "▲ ★★★★", tag = "武器: スリッパ・コードレス掃除機",
                    nickname = "吸引力の変わらないただ一人の母",
                    body = "一軒家の家事を一手に引き受ける。床にゴキブリを見つけると掃除機のヘッドを向け、橙の扇の中のものを丸ごと吸い寄せる。真後ろへ逃げるより横へ逃げろ。ヘッドまで吸われると痛い。掃除機が詰まると少し落ち込む。",
                    rating = "★★★★☆  「床に落ちているものは全部ゴミ（我々も含む）」" },
                new Page { model = SonModel, id = "son", name = "一軒家の息子", badge = "▲ ★★★", tag = "武器: 虫取り網・虫かご",
                    nickname = "夏休みの昆虫ハンター",
                    body = "8歳。カブトムシとゴキブリの区別がまだ曖昧。足が速く、近づくと虫取り網を振り下ろしてくる。捕まったら Space 連打で網から抜け出せ。仲間は虫かごに入れられるが、かごの前で E を押せば助け出せる。",
                    rating = "★★★☆☆  「悪気がないのがいちばん怖い」" },
            }
        };

        private static Page Food(FoodValue.Kind kind, string id, string nickname, string body, string rating) => new Page
        {
            model = (int)kind, id = id, name = FoodValue.NameOf(kind), badge = "◆ +" + FoodValue.AmountOf(kind), tag = "◆ 食料 +" + FoodValue.AmountOf(kind),
            nickname = nickname, body = body, rating = rating
        };

        /// <summary>The book for an encyclopedia tab (CodexData.Tabs), or null for a text-only tab.</summary>
        public static Book ForTab(int tab) => tab == 1 ? Humans : tab == 2 ? Foods : null;

        private static GUIStyle wrap;

        public static bool Known(GameSettings options, Page page) => page.id == null || (options != null && options.Knows(page.id));

        /// <summary>Keyboard: ↑↓ choose, ←→ turn. Call from Update while the tab is open.</summary>
        public static void Keys(Book book, CodexViewer viewer)
        {
            if (book == null) return;
            int n = book.pages.Length;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { book.selected = (book.selected + 1) % n; GameAudio.Ui(GameAudio.Sfx.Focus); }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { book.selected = (book.selected + n - 1) % n; GameAudio.Ui(GameAudio.Sfx.Focus); }
            float turn = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0);
            if (viewer && turn != 0) viewer.Drag(new Vector2(turn * 260 * Time.unscaledDeltaTime, 0));
        }

        /// <summary>Draws the tab body inside the encyclopedia panel r (the tabs and the back button stay with the caller).</summary>
        public static void Draw(Book book, Rect r, GameSettings options, CodexViewer viewer, GUIStyle big, GUIStyle text, GUIStyle small)
        {
            if (wrap == null || wrap.font != text.font) wrap = new GUIStyle(text) { wordWrap = true };
            book.selected = Mathf.Clamp(book.selected, 0, book.pages.Length - 1);

            // List: name, badge, nickname; the selected row outlined, unknown rows grey with "？？？".
            for (int i = 0; i < book.pages.Length; i++)
            {
                var page = book.pages[i];
                bool known = Known(options, page), picked = i == book.selected;
                var row = new Rect(r.x + 30, r.y + 84 + i * 70, 300, 62);
                bool hover = row.Contains(Event.current.mousePosition);
                HudKit.Fill(row, picked ? new Color(0.11f, 0.13f, 0.09f, 0.97f) : hover ? new Color(0.09f, 0.10f, 0.08f, 0.96f) : HudKit.Row);
                HudKit.Fill(new Rect(row.x, row.y, 3, row.height), known ? HudKit.Lime : new Color(0.35f, 0.35f, 0.33f));
                if (picked) HudKit.Outline(row, HudKit.Lime, 2);
                HudKit.Ink(big, known ? HudKit.Cream : HudKit.Muted, () => GUI.Label(new Rect(row.x + 16, row.y + 6, 190, 30), known ? page.name : "？？？", big));
                HudKit.Ink(small, known ? HudKit.Lime : HudKit.Muted, () => GUI.Label(new Rect(row.x + 196, row.y + 10, 100, 24), page.badge, small));
                HudKit.Ink(small, known ? new Color(0.8f, 0.79f, 0.72f) : HudKit.Muted, () => GUI.Label(new Rect(row.x + 16, row.y + 36, 280, 22), known ? page.nickname : "まだ出会っていない", small));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && !picked) { book.selected = i; GameAudio.Ui(GameAudio.Sfx.Focus); }
            }
            HudKit.Ink(small, HudKit.Muted, () => GUI.Label(new Rect(r.x + 30, r.y + 440, 300, 44), book.hint, small));

            // Turntable view.
            var current = book.pages[book.selected];
            bool met = Known(options, current);
            var stage = new Rect(r.x + 350, r.y + 84, 640, 248);
            HudKit.Fill(stage, new Color(0.035f, 0.045f, 0.04f, 1));
            // The HUDs pre-draw every page off-screen once (glyph warm-up): that pass must not start the camera.
            bool onScreen = GUI.matrix.MultiplyPoint3x4(stage.position).x > -1000;
            var picture = viewer && onScreen ? viewer.Show(current.model, met) : null;
            if (picture) GUI.DrawTexture(stage, picture, ScaleMode.ScaleAndCrop, false);
            else if (onScreen) HudKit.Ink(small, HudKit.Muted, () => GUI.Label(new Rect(stage.x + 20, stage.y + 20, 400, 24), "（モデルを表示できません）", small));
            HudKit.Outline(stage, new Color(0.25f, 0.27f, 0.22f), 1);
            HudKit.Ink(small, HudKit.Muted, () => GUI.Label(new Rect(stage.x + 14, stage.yMax - 28, 300, 22), "◎ ドラッグで回転・ホイールで拡大", small));
            var e = Event.current;
            if (viewer && stage.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag) { viewer.Drag(e.delta); e.Use(); }
                else if (e.type == EventType.ScrollWheel) { viewer.Zoom(e.delta.y); e.Use(); }
                else if (e.type == EventType.MouseDown) e.Use();
            }

            // Write-up.
            float y = r.y + 342;
            HudKit.Ink(big, met ? HudKit.Cream : HudKit.Muted, () => GUI.Label(new Rect(r.x + 352, y, 150, 30), met ? current.name : "？？？", big));
            HudKit.Ink(small, met ? HudKit.Lime : HudKit.Muted, () => GUI.Label(new Rect(r.x + 470, y + 6, 520, 24), met ? "―  " + current.nickname + "   " + current.tag : "―  まだ出会っていない", small));
            HudKit.Ink(wrap, met ? new Color(0.86f, 0.85f, 0.78f) : HudKit.Muted, () => GUI.Label(new Rect(r.x + 352, y + 34, 630, 88), met ? current.body : book.unknownBody, wrap));
            if (met) HudKit.Ink(small, HudKit.Orange, () => GUI.Label(new Rect(r.x + 352, y + 126, 470, 24), book.ratingLabel + "  " + current.rating, small));
        }
    }
}
