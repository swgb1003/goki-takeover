using UnityEngine;

namespace GokiTakeover
{
    [CreateAssetMenu(menuName = "GOKI TAKEOVER/Cockroach Appearance")]
    public sealed class CockroachAppearance : ScriptableObject
    {
        [SerializeField, Range(0.25f, 0.65f)] private float modelSize = 0.45f;
        [SerializeField] private Color bodyColor = new Color(0.25f, 0.10f, 0.035f);
        [SerializeField] private Color shellColor = new Color(0.49f, 0.22f, 0.065f);
        [SerializeField] private Color limbColor = new Color(0.31f, 0.13f, 0.04f);
        [SerializeField] private Color eyeColor = new Color(0.025f, 0.018f, 0.012f);
        public float ModelSize => modelSize;
        public Color BodyColor => bodyColor;
        public Color ShellColor => shellColor;
        public Color LimbColor => limbColor;
        public Color EyeColor => eyeColor;
    }
}
