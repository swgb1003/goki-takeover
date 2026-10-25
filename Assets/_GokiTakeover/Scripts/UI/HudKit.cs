using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// The Phase 2-5 IMGUI look as shared helpers for new screens (title, stage select): dark panel with a
    /// lime or orange edge, lime primary buttons, dark secondary buttons, thin bars. Colours match the HUDs.
    /// </summary>
    public static class HudKit
    {
        public static readonly Color Lime = new Color(0.79f, 0.87f, 0.37f);
        public static readonly Color Orange = new Color(1, 0.55f, 0.21f);
        public static readonly Color Cream = new Color(0.94f, 0.92f, 0.83f);
        public static readonly Color Muted = new Color(0.62f, 0.62f, 0.57f);
        public static readonly Color PanelColor = new Color(0.025f, 0.035f, 0.035f, 0.9f);
        public static readonly Color Row = new Color(0.06f, 0.07f, 0.065f, 0.95f);
        public static readonly Color Off = new Color(0.25f, 0.27f, 0.25f);

        public static void Fill(Rect r, Color color) { GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        public static void Panel(Rect r, Color edge) { Fill(r, PanelColor); Fill(new Rect(r.x, r.y, 3, r.height), edge); }
        public static void Bar(Rect r, float amount, Color color) { Fill(r, new Color(0.2f, 0.22f, 0.2f)); r.width *= Mathf.Clamp01(amount); Fill(r, color); }
        public static void Outline(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
        public static void Ink(GUIStyle style, Color color, System.Action draw)
        { Color before = style.normal.textColor; style.normal.textColor = color; draw(); style.normal.textColor = before; }
        public static void Shadowed(Rect r, string label, GUIStyle style, Color color, float offset = 2)
        {
            GUI.color = new Color(0, 0, 0, 0.85f); GUI.Label(new Rect(r.x + offset, r.y + offset, r.width, r.height), label, style);
            GUI.color = color; GUI.Label(r, label, style); GUI.color = Color.white;
        }

        /// <summary>Primary: lime plate. States: default, hover (lighter), pressed (darker), focused (cream outline), disabled (grey).</summary>
        public static bool Primary(Rect r, string label, GUIStyle centred, bool enabled, bool focused = false)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            bool pressed = hover && Input.GetMouseButton(0);
            Fill(r, !enabled ? new Color(0.1f, 0.09f, 0.07f, 0.92f) : pressed ? new Color(0.55f, 0.62f, 0.25f) : hover || focused ? new Color(0.86f, 0.93f, 0.45f) : Lime);
            if (focused && enabled) Outline(r, Cream, 2);
            Ink(centred, enabled ? new Color(0.06f, 0.07f, 0.04f) : Muted, () => GUI.Label(r, label, centred));
            return Clicked(r, enabled);
        }
        /// <summary>Secondary: dark plate with a lime outline on hover or keyboard focus.</summary>
        public static bool Secondary(Rect r, string label, GUIStyle centred, bool enabled = true, bool focused = false)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            bool pressed = hover && Input.GetMouseButton(0);
            Fill(r, pressed ? new Color(0.04f, 0.05f, 0.04f, 0.96f) : hover || focused ? new Color(0.12f, 0.14f, 0.10f, 0.96f) : new Color(0.07f, 0.08f, 0.07f, 0.94f));
            Outline(r, !enabled ? new Color(0.2f, 0.2f, 0.19f) : hover || focused ? Lime : new Color(0.35f, 0.37f, 0.32f), hover || focused ? 2 : 1);
            Ink(centred, enabled ? Cream : Muted, () => GUI.Label(r, label, centred));
            return Clicked(r, enabled);
        }
        public static bool Tab(Rect r, string label, GUIStyle centred, bool on)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, on ? Lime : hover ? new Color(0.14f, 0.16f, 0.12f) : new Color(0.08f, 0.09f, 0.08f));
            Ink(centred, on ? new Color(0.06f, 0.07f, 0.04f) : Cream, () => GUI.Label(r, label, centred));
            return Clicked(r, true);
        }
        // An invisible button over the drawn widget; a click also plays the menu sound (Phase 6, where present).
        private static bool Clicked(Rect r, bool enabled)
        {
            bool hit = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (hit) GameAudio.Ui();
            return hit;
        }
    }
}
