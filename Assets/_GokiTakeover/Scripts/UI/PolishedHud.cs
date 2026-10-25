using UnityEngine;

namespace GokiTakeover
{
    public sealed class PolishedHud : MonoBehaviour
    {
        [SerializeField] private RoachMotor motor;
        private Font font;
        private Texture2D vignette;
        private GUIStyle title, body, caption, number;
        private float elapsed;
        private int frames;
        private string performance = "計測中";
        private string action = "待機";
        public bool ShowCursorPrompt { get; set; } = true;
        private static readonly Color Ivory = new Color(0.94f, 0.92f, 0.83f);
        private static readonly Color Accent = new Color(0.79f, 0.87f, 0.37f);
        public void Configure(RoachMotor player) { motor = player; }
        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, 24);
            if (!font) Debug.LogWarning("[GOKI] 日本語OSフォントを取得できませんでした。", this);
            vignette = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            vignette.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x - 31.5f) / 42, (y - 31.5f) / 42).magnitude;
                pixels[y * 64 + x] = new Color(0.012f, 0.017f, 0.022f, Mathf.SmoothStep(0, 0.30f, Mathf.Clamp01(radius - 0.35f)));
            }
            vignette.SetPixels(pixels);
            vignette.Apply(false, true);
        }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 0.4f) return;
            performance = (frames / elapsed).ToString("F0") + " FPS";
            action = !motor || motor.CurrentSpeed < 0.05f ? "待機" : motor.IsRunning ? "ダッシュ" : "探索中";
            elapsed = 0;
            frames = 0;
        }
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (title == null)
            {
                body = Style(17, Ivory);
                caption = Style(13, new Color(0.70f, 0.73f, 0.70f));
                title = Style(25, Ivory, FontStyle.Bold);
                number = Style(22, Accent);
            }
            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * 0.5f, (Screen.height - 720 * scale) * 0.5f), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(32, 32, 3, 84), Accent);
            Fill(new Rect(35, 32, 350, 84), new Color(0.035f, 0.045f, 0.045f, 0.78f));
            GUI.Label(new Rect(52, 40, 315, 22), "01  /  夜のキッチン", caption);
            GUI.Label(new Rect(50, 61, 320, 34), "小さな侵入者", title);
            GUI.Label(new Rect(52, 94, 320, 23), "家具の下を抜け、キッチンを探索しよう", caption);
            GUI.Label(new Rect(1160, 34, 100, 25), performance, caption);
            Fill(new Rect(34, 600, 228, 82), new Color(0.035f, 0.045f, 0.045f, 0.8f));
            GUI.Label(new Rect(50, 610, 150, 25), "生命力", caption);
            GUI.Label(new Rect(194, 607, 65, 32), "3 / 3", number);
            for (int i = 0; i < 3; i++) Fill(new Rect(50 + i * 66, 647, 59, 5), Accent);
            GUI.Label(new Rect(50, 658, 180, 22), action, caption);
            Fill(new Rect(348, 648, 584, 38), new Color(0.035f, 0.045f, 0.045f, 0.76f));
            GUI.Label(new Rect(366, 656, 568, 28), "WASD  移動     Shift  ダッシュ     マウス  視点     Esc  操作解除", caption);
            GUI.Label(new Rect(1060, 654, 200, 28), "GOKI TAKEOVER", body);
            if (ShowCursorPrompt && Cursor.lockState != CursorLockMode.Locked)
            {
                Fill(new Rect(420, 304, 440, 100), new Color(0.03f, 0.04f, 0.04f, 0.95f));
                GUI.Label(new Rect(454, 323, 380, 34), "クリックして探索を始める", title);
                GUI.Label(new Rect(455, 365, 360, 28), "Escでカーソルを解放できます", body);
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }
        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = weight };
            style.normal.textColor = color;
            return style;
        }
        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void OnDestroy()
        {
            if (font) Destroy(font);
            if (vignette) Destroy(vignette);
        }
    }
}
