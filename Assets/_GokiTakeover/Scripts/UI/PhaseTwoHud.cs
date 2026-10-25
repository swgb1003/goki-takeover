using UnityEngine;

namespace GokiTakeover
{
    public sealed class PhaseTwoHud : MonoBehaviour
    {
        [SerializeField] private PhaseTwoSession session;
        private Font font;
        private GUIStyle title, text, small, button;
        private float elapsed;
        private int frames;
        private string fps = "計測中";
        private static readonly Color Lime = new Color(0.79f, 0.87f, 0.37f);
        private static readonly Color Orange = new Color(1, 0.55f, 0.21f);
        public void Configure(PhaseTwoSession game) { session = game; }
        private void Awake() { font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, 24); }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 0.5f) return;
            fps = (frames / elapsed).ToString("F0") + " FPS";
            frames = 0; elapsed = 0;
        }
        private void OnGUI()
        {
            if (!session) return;
            if (title == null)
            {
                title = Style(28); text = Style(18); small = Style(14);
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20 };
            }
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            Panel(new Rect(30, 28, 385, 122));
            GUI.Label(new Rect(48, 38, 350, 25), "01  /  夜のキッチン", small);
            GUI.Label(new Rect(48, 66, 350, 38), "パンくずを巣へ持ち帰ろう", title);
            GUI.Label(new Rect(48, 110, 350, 28), "納品  " + session.Delivered + " / " + session.Settings.deliveryGoal + "    所持  " + (session.Interactor.Carried ? "パンくず 1個" : "なし"), text);
            GUI.Label(new Rect(1170, 30, 95, 25), fps, small);
            Panel(new Rect(30, 574, 300, 109));
            GUI.Label(new Rect(48, 583, 265, 30), "生命力  " + session.Hp + " / " + session.Settings.maximumHp, text);
            GUI.Label(new Rect(48, 618, 270, 30), "人間  " + HumanLabel(session.Human.State), text);
            Bar(new Rect(48, 655, 258, 7), session.Human.Detection, Orange);
            Panel(new Rect(365, 647, 650, 38));
            GUI.Label(new Rect(380, 655, 630, 28), "WASD  移動    Shift  ダッシュ    E  回収・納品    Esc  ポーズ", small);
            if (session.IsRunning)
            {
                string prompt = session.Interactor.Carried ? (session.Interactor.AtNest ? "E  巣に納品する" : "巣へ戻ろう  ◇") :
                    session.Interactor.NearbyFood ? "E  パンくずを回収する" : session.Interactor.AtNest ? "巣の周りは安全です" : "黄緑の目印が巣です";
                Panel(new Rect(430, 570, 420, 48));
                GUI.Label(new Rect(450, 580, 385, 35), prompt, text);
                if (session.IsSticky) Banner("粘着シート！  外に出ると速度が戻ります", 190);
                if (session.Human.State == HumanAIController.HumanState.Attack) Banner("スリッパ注意！  橙の枠からダッシュで逃げろ", 240);
                else if (session.Human.State == HumanAIController.HumanState.Chase) Banner("見つかった！  家具の下に逃げよう", 240);
                DrawMarker(session.Nest.position, "◇ 巣  " + Vector3.Distance(session.Player.position, session.Nest.position).ToString("F0") + "m", Lime);
                if (Cursor.lockState != CursorLockMode.Locked) Banner("クリックして操作を再開", 320);
            }
            else
            {
                Panel(new Rect(350, 226, 580, 265));
                string heading = session.State == PhaseTwoSession.RunState.Paused ? "ポーズ" : session.State == PhaseTwoSession.RunState.Dead ? "つかまってしまった！" : "練習クリア！";
                GUI.Label(new Rect(390, 250, 500, 45), heading, title);
                GUI.Label(new Rect(390, 307, 500, 35), session.State == PhaseTwoSession.RunState.Paused ? "人間も止まっています。Escで再開。" :
                    "納品 " + session.Delivered + "個  /  発見 " + session.Discoveries + "回  /  " + session.RunSeconds.ToString("F0") + "秒", text);
                if (session.State == PhaseTwoSession.RunState.Paused)
                { if (GUI.Button(new Rect(390, 367, 500, 56), "探索を再開  [Esc]", button)) session.Resume(); }
                else if (GUI.Button(new Rect(390, 367, 500, 56), "最初からやり直す  [R]", button)) session.ResetRun();
                GUI.Label(new Rect(390, 447, 500, 26), "GOKI TAKEOVER  /  潜入練習", small);
            }
            GUI.matrix = saved; GUI.color = savedColor;
        }
        private void DrawMarker(Vector3 world, string label, Color color)
        {
            if (!Camera.main) return;
            Vector3 p = Camera.main.WorldToViewportPoint(world + Vector3.up * 0.5f);
            bool behind = p.z < 0;
            float x = behind ? (p.x > 0.5f ? 80 : 1060) : Mathf.Clamp(p.x * 1280, 80, 1060);
            float y = behind ? 390 : Mathf.Clamp((1 - p.y) * 720, 170, 530);
            Panel(new Rect(x - 35, y, 180, 32));
            GUI.color = color; GUI.Label(new Rect(x - 25, y + 3, 165, 28), label, small); GUI.color = Color.white;
        }
        private void Banner(string label, float y)
        { Panel(new Rect(390, y, 510, 40)); GUI.Label(new Rect(407, y + 6, 480, 32), label, text); }
        private static string HumanLabel(HumanAIController.HumanState state)
        { return state == HumanAIController.HumanState.Routine ? "巡回中" : state == HumanAIController.HumanState.Chase ? "追跡中！" : state == HumanAIController.HumanState.Search ? "捜索中" : "攻撃予兆！"; }
        private GUIStyle Style(int size)
        { var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size }; style.normal.textColor = new Color(0.94f, 0.92f, 0.83f); return style; }
        private static void Panel(Rect r) { Fill(r, new Color(0.025f, 0.035f, 0.035f, 0.88f)); Fill(new Rect(r.x, r.y, 3, r.height), Lime); }
        private static void Bar(Rect r, float amount, Color color) { Fill(r, new Color(0.2f, 0.22f, 0.2f)); r.width *= amount; Fill(r, color); }
        private static void Fill(Rect r, Color color) { GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        private void OnDestroy() { if (font) Destroy(font); }
    }
}
