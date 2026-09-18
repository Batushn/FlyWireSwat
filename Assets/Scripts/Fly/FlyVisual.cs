using System.Collections.Generic;
using UnityEngine;

namespace FlyWireSwat.Fly
{
    /// <summary>
    /// Procedural house fly with realistic proportions: large red compound eyes, humped thorax,
    /// striped tapering abdomen, veined translucent wings, halteres, six two-segment legs.
    /// Real Musca domestica is ~7 mm; we scale it up so a camera can see it.
    /// </summary>
    public class FlyVisual : MonoBehaviour
    {
        public float bodyLength = 0.007f;
        public float visualScale = 6f;
        public bool wingsFlapping;
        public float flapHz = 200f;

        Transform _leftWing, _rightWing, _body;
        readonly List<Transform> _legs = new List<Transform>();
        bool _dead;

        public static Material MakeMat(Color c, float smooth = 0.4f, float metallic = 0f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material MakeUnlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c); m.color = c;
            return m;
        }

        public static Material MakeTransparentLit(Color c, float smooth)
        {
            var m = MakeMat(c, smooth, 0f);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.SetFloat("_Cull", 0); // double sided
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return g;
        }

        /// <summary>Wing: an elliptical fan mesh with a slightly darker leading edge, so it reads as a real wing.</summary>
        static Mesh WingMesh(float length, float width, int segments = 18)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var cols = new List<Color> { new Color(0.9f, 0.95f, 1f, 0.55f) };
            var uvs = new List<Vector2> { new Vector2(0f, 0.5f) };
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)i / segments);
                // asymmetric ellipse: rounded tip, narrower at the root
                float x = Mathf.Cos(a) * length;
                float y = Mathf.Sin(a) * width * (0.55f + 0.45f * Mathf.Cos(a));
                verts.Add(new Vector3(x, 0f, y));
                cols.Add(new Color(0.85f, 0.9f, 1f, 0.35f + 0.25f * (1f - Mathf.Abs(Mathf.Sin(a)))));
                uvs.Add(new Vector2(x / length, 0.5f + y / width * 0.5f));
            }
            var tris = new List<int>();
            for (int i = 1; i <= segments; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            var m = new Mesh();
            m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public void Build()
        {
            float s = bodyLength * visualScale;   // total body length on screen (~4 cm)
            var chitin = MakeMat(new Color(0.16f, 0.13f, 0.11f), 0.55f, 0.25f);
            var thoraxMat = MakeMat(new Color(0.24f, 0.2f, 0.16f), 0.45f, 0.1f);
            var stripeMat = MakeMat(new Color(0.09f, 0.08f, 0.07f), 0.5f, 0.2f);
            var eyeMat = MakeMat(new Color(0.75f, 0.12f, 0.06f), 0.9f, 0.1f);
            var wingMat = MakeTransparentLit(new Color(0.85f, 0.9f, 1f, 0.45f), 0.95f);
            var veinMat = MakeMat(new Color(0.25f, 0.22f, 0.18f), 0.3f);
            var legMat = MakeMat(new Color(0.12f, 0.1f, 0.08f), 0.35f);

            var root = new GameObject("FlyBody").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, s * 0.2f, 0f);
            _body = root;

            // thorax (humped), abdomen (tapering, striped), head
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, s * 0.04f, s * 0.12f), new Vector3(s * 0.34f, s * 0.32f, s * 0.36f), thoraxMat, "Thorax");
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, s * 0.14f, s * 0.13f), new Vector3(s * 0.22f, s * 0.1f, s * 0.28f), stripeMat, "ThoraxStripes");
            float[] segZ = { -0.1f, -0.24f, -0.36f, -0.45f };
            float[] segR = { 0.34f, 0.31f, 0.25f, 0.16f };
            for (int i = 0; i < segZ.Length; i++)
            {
                Prim(PrimitiveType.Sphere, root, new Vector3(0f, -s * 0.01f * i, s * segZ[i]), new Vector3(s * segR[i], s * segR[i] * 0.82f, s * 0.2f), i % 2 == 0 ? chitin : stripeMat, "Abdomen" + i);
            }
            var head = Prim(PrimitiveType.Sphere, root, new Vector3(0f, s * 0.03f, s * 0.36f), new Vector3(s * 0.26f, s * 0.24f, s * 0.2f), chitin, "Head");
            Prim(PrimitiveType.Sphere, root, new Vector3(-s * 0.09f, s * 0.05f, s * 0.38f), new Vector3(s * 0.15f, s * 0.17f, s * 0.15f), eyeMat, "EyeL");
            Prim(PrimitiveType.Sphere, root, new Vector3(s * 0.09f, s * 0.05f, s * 0.38f), new Vector3(s * 0.15f, s * 0.17f, s * 0.15f), eyeMat, "EyeR");
            Prim(PrimitiveType.Capsule, root, new Vector3(0f, -s * 0.06f, s * 0.42f), new Vector3(s * 0.05f, s * 0.05f, s * 0.05f), chitin, "Proboscis");
            // antennae
            Prim(PrimitiveType.Capsule, root, new Vector3(-s * 0.03f, s * 0.01f, s * 0.47f), new Vector3(s * 0.015f, s * 0.03f, s * 0.015f), legMat, "AntL", new Vector3(70f, 0f, 20f));
            Prim(PrimitiveType.Capsule, root, new Vector3(s * 0.03f, s * 0.01f, s * 0.47f), new Vector3(s * 0.015f, s * 0.03f, s * 0.015f), legMat, "AntR", new Vector3(70f, 0f, -20f));
            // halteres
            Prim(PrimitiveType.Sphere, root, new Vector3(-s * 0.16f, s * 0.05f, -s * 0.02f), Vector3.one * s * 0.035f, new Material(chitin) { color = new Color(0.8f, 0.7f, 0.5f) }, "HaltereL");
            Prim(PrimitiveType.Sphere, root, new Vector3(s * 0.16f, s * 0.05f, -s * 0.02f), Vector3.one * s * 0.035f, new Material(chitin) { color = new Color(0.8f, 0.7f, 0.5f) }, "HaltereR");

            // wings
            var wingMesh = WingMesh(s * 0.62f, s * 0.22f);
            _leftWing = MakeWing(root, wingMesh, wingMat, veinMat, new Vector3(-s * 0.08f, s * 0.17f, s * 0.06f), -1f, s);
            _rightWing = MakeWing(root, wingMesh, wingMat, veinMat, new Vector3(s * 0.08f, s * 0.17f, s * 0.06f), 1f, s);

            // legs: femur + tibia, angled outward and down, tarsi touching the ground
            float[] legZ = { 0.2f, 0.06f, -0.1f };
            float[] legSpread = { 40f, 5f, -35f };
            for (int i = 0; i < 3; i++)
            {
                foreach (int sign in new[] { -1, 1 })
                {
                    var hip = new GameObject("Leg" + i + (sign < 0 ? "L" : "R")).transform;
                    hip.SetParent(root, false);
                    hip.localPosition = new Vector3(sign * s * 0.13f, -s * 0.06f, s * legZ[i]);
                    hip.localRotation = Quaternion.Euler(0f, sign * legSpread[i], sign * 55f);
                    Prim(PrimitiveType.Capsule, hip, new Vector3(sign * s * 0.11f, 0f, 0f), new Vector3(s * 0.022f, s * 0.12f, s * 0.022f), legMat, "Femur", new Vector3(0f, 0f, 90f));
                    var knee = new GameObject("Knee").transform;
                    knee.SetParent(hip, false);
                    knee.localPosition = new Vector3(sign * s * 0.22f, 0f, 0f);
                    knee.localRotation = Quaternion.Euler(0f, 0f, -sign * 115f);
                    Prim(PrimitiveType.Capsule, knee, new Vector3(sign * s * 0.12f, 0f, 0f), new Vector3(s * 0.016f, s * 0.13f, s * 0.016f), legMat, "Tibia", new Vector3(0f, 0f, 90f));
                    Prim(PrimitiveType.Capsule, knee, new Vector3(sign * s * 0.26f, 0f, 0f), new Vector3(s * 0.012f, s * 0.04f, s * 0.012f), legMat, "Tarsus", new Vector3(0f, 0f, 60f * sign));
                    _legs.Add(hip);
                }
            }
        }

        static Transform MakeWing(Transform root, Mesh mesh, Material wingMat, Material veinMat, Vector3 pos, float side, float s)
        {
            var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = pos;
            var w = new GameObject("WingMesh");
            w.transform.SetParent(pivot, false);
            w.transform.localScale = new Vector3(side, 1f, 1f);
            w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            w.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = w.AddComponent<MeshRenderer>();
            mr.sharedMaterial = wingMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // three veins along the wing
            for (int v = 0; v < 3; v++)
            {
                float ang = -18f + v * 18f;
                var vein = Prim(PrimitiveType.Cylinder, pivot, Quaternion.Euler(0f, side * (90f + ang) - (side < 0 ? 180f : 0f), 0f) * new Vector3(0f, 0f, s * 0.28f), new Vector3(s * 0.006f, s * 0.29f, s * 0.006f), veinMat, "Vein" + v);
                vein.transform.localRotation = Quaternion.Euler(90f, side * (90f + ang) - (side < 0 ? 180f : 0f), 0f);
                vein.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return pivot;
        }

        public void SetDead(bool splat)
        {
            _dead = true;
            wingsFlapping = false;
            if (splat && _body != null) { _body.localScale = new Vector3(1.5f, 0.2f, 1.5f); _body.localPosition = new Vector3(0f, 0.004f, 0f); }
            else if (_body != null) _body.localRotation = Quaternion.Euler(0f, 0f, 180f); // on its back
        }

        public void ResetVisual()
        {
            _dead = false;
            wingsFlapping = false;
            if (_body != null) { _body.localScale = Vector3.one; _body.localRotation = Quaternion.identity; _body.localPosition = new Vector3(0f, bodyLength * visualScale * 0.2f, 0f); }
            if (_leftWing != null) _leftWing.localRotation = Quaternion.Euler(0f, 12f, 4f);
            if (_rightWing != null) _rightWing.localRotation = Quaternion.Euler(0f, -12f, -4f);
        }

        void Update()
        {
            if (_dead || _leftWing == null) return;
            if (wingsFlapping)
            {
                // ~200 Hz is way above the frame rate; show a motion-blur-like wide flutter
                float a = Mathf.Sin(Time.unscaledTime * flapHz * 0.37f) * 45f;
                _leftWing.localRotation = Quaternion.Euler(0f, 55f, a);
                _rightWing.localRotation = Quaternion.Euler(0f, -55f, -a);
                for (int i = 0; i < _legs.Count; i++) _legs[i].localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? -1 : 1) * 20f, (i % 2 == 0 ? -1 : 1) * 25f);
            }
        }
    }
}
