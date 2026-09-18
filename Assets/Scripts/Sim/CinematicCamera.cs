using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Slow orbit + gentle handheld sway around the fly for the showcase and the videos.</summary>
    public class CinematicCamera : MonoBehaviour
    {
        public Transform focus;
        public Vector3 focusOffset = new Vector3(0f, 0.03f, 0f);
        public float distance = 0.42f;
        public float height = 0.2f;
        public float orbitSpeedDeg = 6f;
        public float startAngleDeg = 210f;
        public float swayAmplitude = 0.004f;
        public float fov = 42f;
        public bool active = true;
        float _angle;

        public void Snap(float angle) { _angle = angle; Apply(0f); }

        void OnEnable() { _angle = startAngleDeg; }

        void LateUpdate()
        {
            if (!active) return;
            _angle += orbitSpeedDeg * Time.unscaledDeltaTime;
            Apply(Time.unscaledTime);
        }

        void Apply(float t)
        {
            Vector3 f = (focus != null ? focus.position : Vector3.zero) + focusOffset;
            float a = _angle * Mathf.Deg2Rad;
            Vector3 pos = f + new Vector3(Mathf.Sin(a) * distance, height, Mathf.Cos(a) * distance);
            pos += new Vector3(Mathf.PerlinNoise(t * 0.4f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 0.35f) - 0.5f, 0f) * swayAmplitude * 2f;
            transform.position = pos;
            transform.LookAt(f);
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = fov;
        }
    }
}
