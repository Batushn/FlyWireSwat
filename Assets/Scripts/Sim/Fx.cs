using System.Collections;
using FlyWireSwat.Fly;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Code-built particle effects: no asset dependencies, URP particle materials.</summary>
    public static class Fx
    {
        static Material _particleMat, _splatMat;

        public static Material ParticleMaterial
        {
            get
            {
                if (_particleMat == null)
                {
                    _particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                    _particleMat.SetColor("_BaseColor", Color.white);
                    _particleMat.SetFloat("_Surface", 1); _particleMat.SetFloat("_Blend", 0);
                    _particleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _particleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _particleMat.SetInt("_ZWrite", 0); _particleMat.renderQueue = 3000;
                    _particleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _particleMat.mainTexture = SoftCircle();
                }
                return _particleMat;
            }
        }

        static Texture2D _soft;
        public static Texture2D SoftCircle()
        {
            if (_soft != null) return _soft;
            int n = 64; _soft = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d));
            }
            _soft.SetPixels(px); _soft.Apply();
            return _soft;
        }

        static ParticleSystem NewSystem(string name, Vector3 pos, Color color, float size, float life, float speed, bool gravity, int max = 256)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.startColor = color; main.startSize = size; main.startLifetime = life; main.startSpeed = speed;
            main.gravityModifier = gravity ? 1f : 0f; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false; main.loop = false;
            var em = ps.emission; em.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial; r.renderMode = ParticleSystemRenderMode.Billboard;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            return ps;
        }

        static void Burst(ParticleSystem ps, int count, float radius, float lifeExtra = 1.5f)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            ps.Emit(count);
            Object.Destroy(ps.gameObject, ps.main.startLifetime.constant + lifeExtra);
        }

        /// <summary>Dust puff where a weapon hits the table.</summary>
        public static void Dust(Vector3 p, Vector3 normal, float scale = 1f)
        {
            var ps = NewSystem("FxDust", p + normal * 0.005f, new Color(0.9f, 0.85f, 0.75f, 0.5f), 0.025f * scale, 0.8f, 0.35f * scale, false);
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.01f * scale;
            ps.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));
            ps.Emit(Mathf.RoundToInt(18 * scale));
            Object.Destroy(ps.gameObject, 2f);
        }

        /// <summary>Fly guts.</summary>
        public static void Splat(Vector3 p, Vector3 normal)
        {
            var ps = NewSystem("FxSplat", p + normal * 0.004f, new Color(0.55f, 0.08f, 0.05f, 0.95f), 0.006f, 0.6f, 0.5f, true);
            Burst(ps, 30, 0.004f);
            var decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = "SplatDecal";
            Object.Destroy(decal.GetComponent<Collider>());
            decal.transform.position = p + normal * 0.0015f;
            decal.transform.rotation = Quaternion.LookRotation(-normal, Vector3.forward) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            decal.transform.localScale = Vector3.one * 0.045f;
            var m = new Material(ParticleMaterial) { mainTexture = SplatTexture() };
            m.SetColor("_BaseColor", new Color(0.45f, 0.05f, 0.03f, 0.9f));
            decal.GetComponent<Renderer>().sharedMaterial = m;
            Object.Destroy(decal, 30f);
        }

        static Texture2D _splatTex;
        static Texture2D SplatTexture()
        {
            if (_splatTex != null) return _splatTex;
            int n = 96; _splatTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            var rng = new System.Random(3);
            var blobs = new System.Collections.Generic.List<(float x, float y, float r)> { (0.5f, 0.5f, 0.22f) };
            for (int i = 0; i < 9; i++) { float a = (float)rng.NextDouble() * 6.28f, d = 0.15f + (float)rng.NextDouble() * 0.28f; blobs.Add((0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.03f + (float)rng.NextDouble() * 0.08f)); }
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n, a = 0f;
                foreach (var b in blobs) { float d = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y)) / b.r; a = Mathf.Max(a, 1f - d * d); }
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.4f));
            }
            _splatTex.SetPixels(px); _splatTex.Apply();
            return _splatTex;
        }

        public static void Sparks(Vector3 p, Color c, int count = 40)
        {
            var ps = NewSystem("FxSparks", p, c, 0.008f, 0.35f, 1.6f, true);
            var trails = ps.trails; trails.enabled = true; trails.lifetime = 0.15f; trails.widthOverTrail = 0.4f;
            ps.GetComponent<ParticleSystemRenderer>().trailMaterial = ParticleMaterial;
            Burst(ps, count, 0.01f);
            Flash(p, c, 6f, 0.12f);
        }

        public static void Flash(Vector3 p, Color c, float intensity, float seconds)
        {
            var l = new GameObject("FxFlash").AddComponent<Light>();
            l.transform.position = p; l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = 2.5f;
            l.gameObject.AddComponent<FadeAndDie>().Init(seconds);
        }

        public static void MuzzleFlash(Vector3 p, Vector3 dir)
        {
            var ps = NewSystem("FxMuzzle", p, new Color(1f, 0.8f, 0.4f, 0.9f), 0.05f, 0.08f, 2f, false);
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.01f;
            ps.transform.rotation = Quaternion.LookRotation(dir);
            ps.Emit(10);
            var smoke = NewSystem("FxMuzzleSmoke", p, new Color(0.7f, 0.7f, 0.7f, 0.35f), 0.06f, 1.2f, 0.6f, false);
            var sh2 = smoke.shape; sh2.enabled = true; sh2.shapeType = ParticleSystemShapeType.Cone; sh2.angle = 15f; sh2.radius = 0.01f;
            smoke.transform.rotation = Quaternion.LookRotation(dir);
            var sol = smoke.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3f));
            smoke.Emit(8);
            Object.Destroy(ps.gameObject, 1f); Object.Destroy(smoke.gameObject, 3f);
            Flash(p, new Color(1f, 0.8f, 0.5f), 25f, 0.06f);
        }

        public static void Explosion(Vector3 p, float radius)
        {
            var fire = NewSystem("FxFire", p, new Color(1f, 0.45f, 0.1f, 0.9f), radius * 0.35f, 0.9f, radius * 2.2f, false, 512);
            var sol = fire.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.2f));
            Burst(fire, 160, radius * 0.1f);
            var smoke = NewSystem("FxSmoke", p, new Color(0.25f, 0.22f, 0.2f, 0.7f), radius * 0.5f, 3.5f, radius * 0.9f, false, 512);
            var sol2 = smoke.sizeOverLifetime; sol2.enabled = true; sol2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3f));
            var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.6f);
            Burst(smoke, 120, radius * 0.2f, 2f);
            var debris = NewSystem("FxDebris", p, new Color(0.35f, 0.3f, 0.25f, 1f), 0.03f, 2.5f, radius * 3f, true);
            Burst(debris, 80, radius * 0.1f);
            Flash(p, new Color(1f, 0.6f, 0.3f), 120f, 0.5f);
        }

        /// <summary>Continuous emitter (smoke from coffee grounds, spray cloud). Caller destroys it.</summary>
        public static ParticleSystem Smoke(Transform parent, Vector3 localPos, Color c, float size, float life, float rate, float rise)
        {
            var ps = NewSystem("FxSmokeLoop", parent.TransformPoint(localPos), c, size, life, 0.02f, false, 200);
            ps.transform.SetParent(parent, true);
            var main = ps.main; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = true; em.rateOverTime = rate;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.01f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(rise);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.08f; noise.frequency = 1.5f;
            ps.Play();
            return ps;
        }

        public static ParticleSystem SprayCloud(Vector3 origin, Vector3 dir, float seconds)
        {
            var ps = NewSystem("FxSpray", origin, new Color(0.75f, 1f, 0.75f, 0.35f), 0.05f, 2.2f, 1.6f, false, 600);
            var main = ps.main; main.loop = true;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 220f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 22f; sh.radius = 0.01f;
            ps.transform.rotation = Quaternion.LookRotation(dir);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 4f));
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.dampen = 0.6f; lim.limit = 0.3f;
            ps.Play();
            Object.Destroy(ps.gameObject, seconds + 3f);
            ps.gameObject.AddComponent<StopAfter>().seconds = seconds;
            return ps;
        }

        /// <summary>Unlit, alpha-blended, double-sided material for scent clouds and range domes (independent of scene lighting).</summary>
        public static Material UnlitTransparent(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0); m.SetFloat("_Cull", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        /// <summary>Faint dome showing a repellent's / attractant's range. Parent it to the item; caller destroys it.</summary>
        public static GameObject ZoneDome(Transform parent, float radius, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "ZoneDome";
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = Vector3.zero;
            g.transform.localScale = new Vector3(radius * 2f, radius * 1.2f, radius * 2f);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = UnlitTransparent(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            g.AddComponent<DomePulse>();
            return g;
        }

        public static GameObject GlowSphere(Vector3 p, float radius, Color c, float seconds)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.position = p; g.transform.localScale = Vector3.one * radius * 2f;
            g.GetComponent<Renderer>().sharedMaterial = UnlitTransparent(c);
            Object.Destroy(g, seconds);
            return g;
        }
    }

    public class DomePulse : MonoBehaviour
    {
        Material _m; Color _c;
        void Start() { _m = GetComponent<Renderer>().material; _c = _m.color; }
        void Update() { if (_m == null) return; var c = _c; c.a = _c.a * (0.75f + 0.25f * Mathf.Sin(Time.time * 1.5f)); _m.SetColor("_BaseColor", c); _m.color = c; }
    }

    public class FadeAndDie : MonoBehaviour
    {
        float _t, _dur; Light _l; float _i0;
        public void Init(float seconds) { _dur = seconds; _l = GetComponent<Light>(); _i0 = _l != null ? _l.intensity : 0f; }
        void Update() { _t += Time.deltaTime; if (_l != null) _l.intensity = _i0 * Mathf.Clamp01(1f - _t / _dur); if (_t > _dur) Destroy(gameObject); }
    }

    public class StopAfter : MonoBehaviour
    {
        public float seconds; float _t;
        void Update() { _t += Time.deltaTime; if (_t > seconds) { var ps = GetComponent<ParticleSystem>(); if (ps != null) { var em = ps.emission; em.enabled = false; } enabled = false; } }
    }
}
