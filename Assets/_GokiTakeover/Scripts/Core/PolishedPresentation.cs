using UnityEngine;

namespace GokiTakeover
{
    /// <summary>One bounded shadow map and modest MSAA, active only in the polished scene.</summary>
    public sealed class PolishedPresentation : MonoBehaviour
    {
        [SerializeField, Range(0, 4)] private int antiAliasing = 2;
        [SerializeField, Range(6, 25)] private float shadowDistance = 16;
        [SerializeField] private bool softShadows = true;
        private int frameRate, vsync, aa, lights, cascades;
        private float previousDistance;
        private ShadowQuality shadows;
        private ShadowResolution resolution;
        private ShadowProjection projection;
        private void OnEnable()
        {
            frameRate = Application.targetFrameRate;
            vsync = QualitySettings.vSyncCount;
            aa = QualitySettings.antiAliasing;
            lights = QualitySettings.pixelLightCount;
            cascades = QualitySettings.shadowCascades;
            shadows = QualitySettings.shadows;
            resolution = QualitySettings.shadowResolution;
            projection = QualitySettings.shadowProjection;
            previousDistance = QualitySettings.shadowDistance;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = antiAliasing == 0 ? 0 : 2;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.shadows = softShadows ? ShadowQuality.All : ShadowQuality.HardOnly;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowDistance = shadowDistance;
        }
        private void OnDisable()
        {
            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = vsync;
            QualitySettings.antiAliasing = aa;
            QualitySettings.pixelLightCount = lights;
            QualitySettings.shadows = shadows;
            QualitySettings.shadowResolution = resolution;
            QualitySettings.shadowProjection = projection;
            QualitySettings.shadowCascades = cascades;
            QualitySettings.shadowDistance = previousDistance;
        }
    }
}
