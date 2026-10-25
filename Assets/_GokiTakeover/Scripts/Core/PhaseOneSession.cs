using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Scene-local low quality settings, restored when leaving the dedicated test scene.</summary>
    public sealed class PhaseOneSession : MonoBehaviour
    {
        [SerializeField, Min(30)] private int targetFrameRate = 60;
        private int oldFrameRate, oldVsync, oldAA, oldPixelLights;
        private ShadowQuality oldShadows;
        private void OnEnable()
        {
            oldFrameRate = Application.targetFrameRate;
            oldVsync = QualitySettings.vSyncCount;
            oldAA = QualitySettings.antiAliasing;
            oldPixelLights = QualitySettings.pixelLightCount;
            oldShadows = QualitySettings.shadows;
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.shadows = ShadowQuality.Disable;
        }
        private void OnDisable()
        {
            Application.targetFrameRate = oldFrameRate;
            QualitySettings.vSyncCount = oldVsync;
            QualitySettings.antiAliasing = oldAA;
            QualitySettings.pixelLightCount = oldPixelLights;
            QualitySettings.shadows = oldShadows;
        }
    }
}
