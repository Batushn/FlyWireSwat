using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlyWireSwat.Fps
{
    /// <summary>Minimal first-person controller (Input System): WASD, mouse look, shift to run.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsPlayer : MonoBehaviour
    {
        public Transform head;
        public float walkSpeed = 1.6f, runSpeed = 3.2f, mouseSensitivity = 0.08f, eyeHeight = 1.62f;
        public bool inputEnabled = true;
        CharacterController _cc;
        float _pitch, _yaw, _vy;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.height = 1.75f; _cc.radius = 0.25f; _cc.center = new Vector3(0f, 0.875f, 0f);
            _yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            if (!inputEnabled) return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null) return;
            Vector2 look = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            _yaw += look.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += Vector3.forward;
            if (kb.sKey.isPressed) move += Vector3.back;
            if (kb.aKey.isPressed) move += Vector3.left;
            if (kb.dKey.isPressed) move += Vector3.right;
            float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 world = transform.TransformDirection(move.normalized) * speed;
            _vy = _cc.isGrounded ? -0.5f : _vy - 9.81f * Time.deltaTime;
            world.y = _vy;
            _cc.Move(world * Time.deltaTime);
#endif
        }
    }
}
