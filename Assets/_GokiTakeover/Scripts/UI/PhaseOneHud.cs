using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Small provisional IMGUI HUD, no uGUI/TMP package dependency. Cached 4 Hz readout.</summary>
    public sealed class PhaseOneHud : MonoBehaviour
    {
        [SerializeField] private RoachMotor motor;
        [SerializeField, Min(1)] private int hitPoints = 3;
        [SerializeField] private Font japaneseFont;
        private GUIStyle text, title, small;
        private Font ownedFont;
        private float elapsed;
        private int frames;
        private string fpsText = "FPS 計測中";
        private string speedText = "待機";
        private string hpText;
        private string actionText = "行動：待機";
        public bool ShowCursorPrompt { get; set; } = true;
        private readonly Color lime = new Color(0.82f, 0.94f, 0.12f);
        private readonly Color orange = new Color(1f, 0.62f, 0.14f);
        public void Configure(RoachMotor player) { motor = player; }

        private void Awake()
        {
            hpText = "HP   " + hitPoints + " / " + hitPoints;
            if (!japaneseFont)
            {
                ownedFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP" }, 20);
                japaneseFont = ownedFont;
            }
            if (!japaneseFont) Debug.LogWarning("[GOKI] 日本語OSフォントが見つかりません。HUDにFontを指定してください。", this);
            if (!motor) Debug.LogWarning("[GOKI] HUDの移動参照が未設定です。HP・FPSのみ表示します。", this);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 0.25f) return;
            fpsText = "FPS  " + (frames / elapsed).ToString("F0") + "   /   " + (elapsed * 1000 / frames).ToString("F1") + " ms";
            speedText = motor ? (motor.CurrentSpeed < 0.05f ? "待機" : motor.IsRunning ? "ダッシュ" : "歩行") : "参照なし";
            actionText = "行動：" + speedText;
            frames = 0;
            elapsed = 0;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { font = japaneseFont, fontSize = 19, fontStyle = FontStyle.Bold };
                text.normal.textColor = new Color(1f, 0.96f, 0.83f);
                title = new GUIStyle(text) { fontSize = 24 };
                title.normal.textColor = lime;
                small = new GUIStyle(text) { fontSize = 16, fontStyle = FontStyle.Normal };
            }
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * 0.5f,
                (Screen.height - 720 * scale) * 0.5f, 0), Quaternion.identity, Vector3.one * scale);
            Panel(new Rect(24, 24, 440, 100), lime);
            GUI.Label(new Rect(42, 35, 410, 34), "キッチンへの潜入", title);
            GUI.Label(new Rect(42, 77, 410, 30), "試験目標：歩く・走る・家具の下を探索", small);
            Panel(new Rect(1000, 24, 256, 72), orange);
            GUI.Label(new Rect(1016, 33, 230, 28), fpsText, small);
            GUI.Label(new Rect(1016, 62, 230, 25), "試作 01  /  三人称", small);
            Panel(new Rect(24, 594, 244, 70), orange);
            GUI.Label(new Rect(40, 603, 220, 28), "GOKI TAKEOVER", text);
            GUI.Label(new Rect(40, 634, 220, 25), actionText, small);
            Panel(new Rect(946, 594, 310, 70), lime);
            GUI.Label(new Rect(964, 604, 260, 28), hpText, text);
            GUI.color = lime;
            GUI.DrawTexture(new Rect(964, 642, 268, 6), Texture2D.whiteTexture);
            GUI.color = oldColor;
            Panel(new Rect(24, 678, 1232, 32), orange);
            GUI.Label(new Rect(40, 679, 1200, 28), "WASD：移動     Shift：ダッシュ     マウス：視点     Esc：操作解除     左クリック：操作再開", small);
            if (ShowCursorPrompt && Cursor.lockState != CursorLockMode.Locked)
            {
                Panel(new Rect(410, 310, 460, 94), lime);
                GUI.Label(new Rect(434, 327, 420, 30), "画面をクリックして探索を開始", text);
                GUI.Label(new Rect(434, 362, 420, 26), "Escでマウスを解放・移動を停止できます", small);
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        private void Panel(Rect rect, Color accent)
        {
            GUI.color = new Color(0.065f, 0.043f, 0.025f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void OnDestroy() { if (ownedFont) Destroy(ownedFont); }
    }
}
