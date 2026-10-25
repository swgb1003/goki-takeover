using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Turntable for the encyclopedia: renders one food model into a texture that the IMGUI page draws. It sits
    /// far below the kitchen with its own camera and two small lights, created when the encyclopedia first
    /// shows a model; the camera runs only while a page shows it.
    /// Drag turns the model (and tilts the view), the wheel zooms, and when left alone it turns slowly.
    /// An entry not met yet is shown as a dark silhouette.
    /// </summary>
    public sealed class CodexViewer : MonoBehaviour
    {
        [SerializeField] private Mesh[] models;   // by FoodValue.Kind
        [SerializeField] private Material surface;
        [SerializeField] private Mesh plinth;
        [SerializeField] private Material plinthMaterial;
        private static readonly Vector3 StagePosition = new Vector3(0, -150, 0);
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Camera viewCamera;
        private RenderTexture target;
        private Transform turntable, model, stand;
        private MeshFilter filter;
        private MeshRenderer view;
        private MaterialPropertyBlock block;
        private int shown = -1;
        private bool shownKnown;
        private float yaw = 200, pitch = 24, zoom = 1, idleAfter, lastShown = -1, radius = 0.4f, halfHeight = 0.2f;

        public int Count => models == null ? 0 : models.Length;
        public bool Rendering => viewCamera && viewCamera.enabled;
        public float Yaw => yaw;

        public void Configure(Mesh[] foods, Material material, Mesh disc, Material discMaterial)
        { models = foods; surface = material; plinth = disc; plinthMaterial = discMaterial; }

        // Built on first use: no extra camera exists until the encyclopedia is opened.
        private void Build()
        {
            var root = new GameObject("CodexTurntable").transform;
            root.SetParent(transform, false);
            root.position = StagePosition;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root, false);
            model = new GameObject("Model").transform;
            model.SetParent(turntable, false);
            filter = model.gameObject.AddComponent<MeshFilter>();
            view = model.gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = surface;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            block = new MaterialPropertyBlock();
            if (plinth && plinthMaterial)
            {
                stand = new GameObject("Plinth").transform;
                stand.SetParent(root, false);
                stand.gameObject.AddComponent<MeshFilter>().sharedMesh = plinth;
                var r = stand.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = plinthMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Light(root, "Key", new Vector3(1.3f, 1.5f, -1.4f), new Color(1f, 0.86f, 0.68f), 2.2f);
            Light(root, "Rim", new Vector3(-1.5f, 0.9f, 1.5f), new Color(0.62f, 0.78f, 1f), 1.5f);
            var cameraObject = new GameObject("CodexCamera");
            cameraObject.transform.SetParent(root, false);
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.035f, 0.045f, 0.04f, 1);
            viewCamera.fieldOfView = 30;
            viewCamera.nearClipPlane = 0.05f;
            viewCamera.farClipPlane = 12;
            viewCamera.allowHDR = false;
            viewCamera.depth = -10;
            viewCamera.enabled = false;
        }

        private static void Light(Transform parent, string name, Vector3 position, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.range = 5;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>Called every frame the page draws it; returns the texture to draw.</summary>
        public Texture Show(int index, bool known)
        {
            if (models == null || index < 0 || index >= models.Length || !models[index]) return null;
            if (!viewCamera) Build();
            if (!target)
            {
                target = new RenderTexture(1024, 432, 24) { name = "CodexView", antiAliasing = 4 };
                viewCamera.targetTexture = target;
            }
            if (index != shown || known != shownKnown)
            {
                var mesh = models[index];
                radius = Mathf.Max(0.12f, mesh.bounds.extents.magnitude);
                halfHeight = mesh.bounds.extents.y;
                if (index != shown) { yaw = Tall ? 165 : 200; zoom = 1; idleAfter = 0; pitch = Tall ? 8 : 24; }   // people face the camera, food is seen from above
                shown = index; shownKnown = known;
                filter.sharedMesh = mesh;
                model.localPosition = -mesh.bounds.center;
                if (stand)
                {
                    stand.localPosition = new Vector3(0, -mesh.bounds.extents.y - 0.006f, 0);
                    stand.localScale = new Vector3(radius * 2.7f, 1, radius * 2.7f);
                }
                block.Clear();
                if (!known) block.SetColor(ColorId, new Color(0.02f, 0.02f, 0.02f, 1));
                view.SetPropertyBlock(block);
            }
            lastShown = Time.unscaledTime;
            viewCamera.enabled = true;
            return target;
        }

        private bool Tall => halfHeight > radius * 0.8f;

        public void Drag(Vector2 delta)
        {
            yaw -= delta.x * 0.45f;
            pitch = Mathf.Clamp(pitch + delta.y * 0.3f, 4, 70);
            idleAfter = Time.unscaledTime + 2.5f;
        }
        public void Zoom(float steps) { zoom = Mathf.Clamp(zoom + steps * 0.06f, 0.6f, 1.5f); }

        private void LateUpdate()
        {
            if (!viewCamera || !viewCamera.enabled) return;
            // Off as soon as no page asked for it (the HUD and the title draw it every frame they show it).
            if (Time.unscaledTime - lastShown > 0.2f) { viewCamera.enabled = false; return; }
            if (Time.unscaledTime > idleAfter) yaw += 32 * Time.unscaledDeltaTime;
            turntable.localRotation = Quaternion.Euler(0, yaw, 0);
            float tan = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Food fits by its size; a person by its height (the page crops the picture a little top and bottom).
            float distance = (Tall ? halfHeight * 1.2f / tan : radius * 0.95f / tan) * zoom;
            Quaternion orbit = Quaternion.Euler(pitch, 0, 0);
            viewCamera.transform.localPosition = orbit * new Vector3(0, 0, -distance);
            viewCamera.transform.localRotation = orbit;
        }

        private void OnDestroy()
        {
            if (viewCamera) viewCamera.targetTexture = null;
            if (target) { target.Release(); Destroy(target); }
        }
    }
}
