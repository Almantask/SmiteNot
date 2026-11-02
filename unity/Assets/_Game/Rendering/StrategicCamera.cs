using UnityEngine;
using UnityEngine.InputSystem;

namespace GodGame.Rendering
{
    /// <summary>
    /// The strategic-zoom camera (M2 debug view): orbits a point on the ground from 15 m up to 1.5 km away, tilting
    /// from a low three-quarter view close in to nearly straight down at strategic zoom. WASD or arrows pan (faster
    /// when zoomed out), the wheel zooms toward the cursor's distance, Q/E or a middle-button drag turns. Drives the
    /// transform of the Cinemachine camera it sits on (no follow target).
    /// </summary>
    public sealed class StrategicCamera : MonoBehaviour
    {
        public const float MinDistance = 15f;
        public const float MaxDistance = 1500f;

        [SerializeField]
        private Vector3 _focus = new Vector3(600, 0, 400);

        [SerializeField]
        private float _distance = 900f;

        [SerializeField]
        private float _yaw;

        [SerializeField]
        private Vector2 _bounds = new Vector2(1200, 800);

        public float Distance => _distance;

        /// <summary>Looks at the middle of a map of this size from far enough to see it all.</summary>
        public void Frame(float widthMetres, float depthMetres)
        {
            _bounds = new Vector2(widthMetres, depthMetres);
            _focus = new Vector3(widthMetres / 2, 0, depthMetres / 2);
            _distance = Mathf.Clamp(
                Mathf.Max(widthMetres, depthMetres) * 0.9f,
                MinDistance,
                MaxDistance
            );
            _yaw = 0;
            Apply();
        }

        /// <summary>Pitch for a distance: 35° close in, 80° at strategic zoom.</summary>
        public static float PitchFor(float distance)
        {
            float t = Mathf.InverseLerp(
                Mathf.Log(MinDistance),
                Mathf.Log(MaxDistance),
                Mathf.Log(distance)
            );
            return Mathf.Lerp(35f, 80f, t);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            if (keyboard != null)
            {
                Vector2 move = Vector2.zero;
                move.y += keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0;
                move.y -= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0;
                move.x += keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0;
                move.x -= keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0;
                float speed = _distance * 0.8f * (keyboard.leftShiftKey.isPressed ? 3f : 1f);
                var forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
                _focus += (forward * move.y + right * move.x) * speed * dt;
                _yaw +=
                    ((keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0))
                    * 90f
                    * dt;
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _distance *= Mathf.Pow(0.85f, Mathf.Sign(scroll));
                }
                if (mouse.middleButton.isPressed)
                {
                    _yaw += mouse.delta.ReadValue().x * 0.25f;
                }
            }
            _distance = Mathf.Clamp(_distance, MinDistance, MaxDistance);
            _focus.x = Mathf.Clamp(_focus.x, 0, _bounds.x);
            _focus.z = Mathf.Clamp(_focus.z, 0, _bounds.y);
            Apply();
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(PitchFor(_distance), _yaw, 0);
            transform.SetPositionAndRotation(
                _focus - rotation * Vector3.forward * _distance,
                rotation
            );
        }
    }
}
