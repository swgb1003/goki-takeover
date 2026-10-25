using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Tactics view. Lives on the same Camera as ThirdPersonRoachCamera; SwarmCommander enables exactly
    /// one of the two, so there is never a second camera or a second render of the scene.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed class SwarmViewCamera : MonoBehaviour
    {
        [SerializeField] private Vector2 boundsMin = new Vector2(-6.4f, -5.4f), boundsMax = new Vector2(6.4f, 5.4f);
        [SerializeField, Range(35f, 85f)] private float pitch = 58f;
        [SerializeField, Range(6f, 20f)] private float distance = 13f;
        [SerializeField, Min(1f)] private float panSpeed = 6f;
        private Vector3 focus, focusGoal;
        private float zoom;
        private Camera view;
        public bool InputEnabled { get; set; }
        public Camera Camera => view ? view : view = GetComponent<Camera>();
        public Vector3 Focus => focus;

        public void Configure(Vector2 min, Vector2 max, float viewDistance, float speed)
        { boundsMin = min; boundsMax = max; distance = viewDistance; panSpeed = speed; }

        public void Begin(Vector3 point)
        {
            focus = focusGoal = Clamp(point);
            zoom = distance;
            Apply();
        }

        public void FocusOn(Vector3 point) { focusGoal = Clamp(point); }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (InputEnabled && Application.isFocused)
            {
                Vector2 move = GokiInput.Move;
                float speed = panSpeed * (GokiInput.Run ? 2.2f : 1f) * (zoom / 13f);
                focusGoal = Clamp(focusGoal + new Vector3(move.x, 0, move.y) * speed * dt);
                zoom = Mathf.Clamp(zoom - Input.mouseScrollDelta.y * 1.2f, 7f, 18f);
            }
            focus = Vector3.Lerp(focus, focusGoal, 1 - Mathf.Exp(-10f * dt));
            Apply();
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(pitch, 0, 0);
            transform.SetPositionAndRotation(focus + rotation * Vector3.back * zoom, rotation);
        }

        private Vector3 Clamp(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, boundsMin.x, boundsMax.x), 0, Mathf.Clamp(p.z, boundsMin.y, boundsMax.y));
    }
}
