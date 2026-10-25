using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Phase 1 uses only the built-in Input Manager. No axes asset or Input System package required.</summary>
    public static class GokiInput
    {
        public static Vector2 Move => new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        public static bool Run => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool Escape => Input.GetKeyDown(KeyCode.Escape);
        public static bool Click => Input.GetMouseButtonDown(0);
    }
}
