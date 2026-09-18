using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Marks where a fly may land (the table top). Bounds are in world space, y = surface height.</summary>
    public class LandingSurface : MonoBehaviour
    {
        public Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(1.6f, 0.01f, 1f));
        public void Init(Bounds b) { worldBounds = b; }

        public Vector3 RandomPoint(System.Random rng, float margin = 0.15f)
        {
            var b = worldBounds;
            float x = Mathf.Lerp(b.min.x + margin, b.max.x - margin, (float)rng.NextDouble());
            float z = Mathf.Lerp(b.min.z + margin, b.max.z - margin, (float)rng.NextDouble());
            return new Vector3(x, b.center.y, z);
        }
    }
}
