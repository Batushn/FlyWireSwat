using System.Collections.Generic;
using FlyWireSwat.Connectome;
using Unity.Mathematics;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>
    /// Live view of the 1.5k-neuron escape circuit: one particle per neuron laid out by class,
    /// lines for the synapses onto the Giant Fiber, neurons flash when they spike.
    /// </summary>
    public class BrainView : MonoBehaviour
    {
        public float width = 0.9f, height = 0.55f;
        public float glowDecayMs = 25f;

        NetworkSpec _net;
        ParticleSystem _ps;
        ParticleSystem.Particle[] _particles;
        Vector3[] _positions;
        Color[] _baseColors;
        float[] _glow;
        List<int2> _spikes;
        int _cursor;
        float _lastSimMs;
        float _dtMs;
        Transform _gfMarkerL, _gfMarkerR;

        static readonly Dictionary<byte, Color> KindColor = new Dictionary<byte, Color>
        {
            { NeuronKind.LC4, new Color(0.2f, 0.8f, 1f) },
            { NeuronKind.LPLC2, new Color(0.4f, 1f, 0.5f) },
            { NeuronKind.LPLC1, new Color(0.7f, 0.9f, 0.3f) },
            { NeuronKind.LC6, new Color(0.9f, 0.6f, 1f) },
            { NeuronKind.LC16, new Color(1f, 0.5f, 0.7f) },
            { NeuronKind.LC22, new Color(1f, 0.75f, 0.4f) },
            { NeuronKind.LC15, new Color(1f, 0.95f, 0.5f) },
            { NeuronKind.GF, new Color(1f, 0.15f, 0.1f) },
            { NeuronKind.DNp02, new Color(1f, 0.45f, 0.1f) },
            { NeuronKind.DNp04, new Color(1f, 0.45f, 0.1f) },
            { NeuronKind.DNp06, new Color(1f, 0.45f, 0.1f) },
            { NeuronKind.DNp11, new Color(1f, 0.45f, 0.1f) },
            { NeuronKind.Other, new Color(0.45f, 0.45f, 0.55f) },
        };

        public static string KindLabel(byte k)
        {
            switch (k)
            {
                case NeuronKind.LC4: return L10n.Pick("LC4 (velocity)", "LC4 (hız)");
                case NeuronKind.LPLC2: return L10n.Pick("LPLC2 (size)", "LPLC2 (boyut)");
                case NeuronKind.LPLC1: return "LPLC1";
                case NeuronKind.LC6: return "LC6";
                case NeuronKind.LC16: return "LC16";
                case NeuronKind.LC22: return "LC22";
                case NeuronKind.LC15: return "LC15";
                case NeuronKind.GF: return "GF (DNp01)";
                default: return k >= NeuronKind.GF ? "DNp02/04/06/11" : L10n.Pick("interneurons", "ara nöronlar");
            }
        }

        public static Color ColorOf(byte k) => KindColor.TryGetValue(k, out var c) ? c : KindColor[NeuronKind.Other];

        public void Build(NetworkSpec net, float dtMs)
        {
            _net = net; _dtMs = dtMs;
            int n = net.NeuronCount;
            _positions = new Vector3[n];
            _baseColors = new Color[n];
            _glow = new float[n];

            // Layout: 7 VPN columns (left half = left eye, right half = right eye), DN row at the bottom centre,
            // "other" interneurons scattered in a band between them.
            byte[] vpnOrder = { NeuronKind.LC4, NeuronKind.LPLC2, NeuronKind.LPLC1, NeuronKind.LC6, NeuronKind.LC16, NeuronKind.LC22, NeuronKind.LC15 };
            var counters = new Dictionary<(byte, int), int>();
            var rng = new Unity.Mathematics.Random(99);
            for (int i = 0; i < n; i++)
            {
                byte k = net.Kind[i];
                int side = net.Side[i];
                _baseColors[i] = ColorOf(k);
                if (NeuronKind.IsVisualProjection(k))
                {
                    int col = System.Array.IndexOf(vpnOrder, k);
                    var key = (k, side);
                    counters.TryGetValue(key, out int c); counters[key] = c + 1;
                    int total = 0;
                    foreach (var idx in net.ByKind[k]) if (net.Side[idx] == side) total++;
                    float half = side < 0 ? -1f : 1f;
                    float x = half * (0.04f + (col + 0.5f) / 7f * 0.46f) * width;
                    float colW = width / 7f * 0.42f;
                    int rows = Mathf.CeilToInt(total / 3f);
                    float y = height * 0.5f - (c / 3) * (height * 0.55f / Mathf.Max(1, rows));
                    x += ((c % 3) - 1) * colW * 0.35f;
                    _positions[i] = new Vector3(x, y, 0f);
                }
                else if (NeuronKind.IsDescending(k))
                {
                    float x = (k == NeuronKind.GF ? 0f : (k - NeuronKind.GF) * 0.06f * width) * (side < 0 ? -1f : 1f) + (side < 0 ? -0.08f : 0.08f) * width;
                    if (k == NeuronKind.GF) x = side < 0 ? -0.06f * width : 0.06f * width;
                    _positions[i] = new Vector3(x, -height * 0.5f, 0f);
                }
                else
                {
                    float x = rng.NextFloat(-0.45f, 0.45f) * width;
                    float y = rng.NextFloat(-0.25f, -0.08f) * height;
                    _positions[i] = new Vector3(x, y, 0f);
                }
            }

            _ps = gameObject.AddComponent<ParticleSystem>();
            var main = _ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = n; main.startLifetime = 1e9f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSize = 0.012f;
            var em = _ps.emission; em.enabled = false;
            var sh = _ps.shape; sh.enabled = false;
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetColor("_BaseColor", Color.white);
            r.sharedMaterial = mat;
            _particles = new ParticleSystem.Particle[n];
            for (int i = 0; i < n; i++)
            {
                _particles[i].position = _positions[i];
                _particles[i].startColor = _baseColors[i] * 0.55f;
                _particles[i].startSize = NeuronKind.IsDescending(net.Kind[i]) ? 0.035f : 0.012f;
                _particles[i].remainingLifetime = 1e9f;
                _particles[i].startLifetime = 1e9f;
            }
            _ps.SetParticles(_particles, n);

            BuildGfLines();
            BuildFrame();
        }

        void BuildGfLines()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var idx = new List<int>();
            var gfSet = new HashSet<int>(_net.GfIndices);
            gfSet.UnionWith(_net.LongModeDnIndices);
            int n = _net.NeuronCount;
            for (int i = 0; i < n; i++)
            {
                for (int e = _net.OutStart[i]; e < _net.OutStart[i + 1]; e++)
                {
                    int post = _net.OutPost[e];
                    if (!gfSet.Contains(post)) continue;
                    float w = Mathf.Abs(_net.OutWeight[e]);
                    if (w < 1.2f) continue;  // only the strongest synapses (>= ~5 synapses)
                    var c = _baseColors[i]; c.a = Mathf.Clamp01(0.05f + w / 40f);
                    if (_net.OutWeight[e] < 0) c = new Color(0.9f, 0.2f, 0.9f, c.a);
                    idx.Add(verts.Count); verts.Add(_positions[i]); cols.Add(c);
                    idx.Add(verts.Count); verts.Add(_positions[post]); cols.Add(c);
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetIndices(idx, MeshTopology.Lines, 0);
            var go = new GameObject("GF_Synapses");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mr.sharedMaterial = mat;
        }

        void BuildFrame()
        {
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Backdrop";
            Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            bg.transform.localScale = new Vector3(width * 1.12f, height * 1.25f, 1f);
            bg.GetComponent<Renderer>().sharedMaterial = Fly.FlyVisual.MakeUnlit(new Color(0.03f, 0.03f, 0.06f));
        }

        public void LoadSpikes(List<int2> spikes)
        {
            _spikes = spikes; _cursor = 0; _lastSimMs = -1f;
            for (int i = 0; i < _glow.Length; i++) _glow[i] = 0f;
        }

        /// <summary>Advance the view to simulation time (ms since sim start).</summary>
        public void SetSimTime(float simMs)
        {
            if (_spikes == null || _particles == null) return;
            if (simMs < _lastSimMs) { _cursor = 0; for (int i = 0; i < _glow.Length; i++) _glow[i] = 0f; _lastSimMs = -1f; }
            float dt = _lastSimMs < 0 ? 0f : simMs - _lastSimMs;
            float decay = Mathf.Exp(-dt / glowDecayMs);
            for (int i = 0; i < _glow.Length; i++) _glow[i] *= decay;
            int step = Mathf.FloorToInt(simMs / _dtMs);
            while (_cursor < _spikes.Count && _spikes[_cursor].x <= step)
            {
                _glow[_spikes[_cursor].y] = 1f;
                _cursor++;
            }
            _lastSimMs = simMs;
            for (int i = 0; i < _particles.Length; i++)
            {
                float g = _glow[i];
                Color c = Color.Lerp(_baseColors[i] * 0.45f, Color.white, g);
                if (g > 0.01f) c = Color.Lerp(_baseColors[i] * 0.45f, Color.Lerp(_baseColors[i], Color.white, 0.6f), g);
                _particles[i].startColor = c;
                _particles[i].startSize = (NeuronKind.IsDescending(_net.Kind[i]) ? 0.035f : 0.012f) * (1f + 1.2f * g);
            }
            _ps.SetParticles(_particles, _particles.Length);
        }

        /// <summary>Live mode: flash the given neurons and decay the rest by dtMs.</summary>
        public void FlashLive(List<int> spiked, float dtMs)
        {
            float decay = Mathf.Exp(-dtMs / glowDecayMs);
            for (int i = 0; i < _glow.Length; i++) _glow[i] *= decay;
            foreach (var i in spiked) _glow[i] = 1f;
            for (int i = 0; i < _particles.Length; i++)
            {
                float g = _glow[i];
                _particles[i].startColor = g > 0.01f ? Color.Lerp(_baseColors[i] * 0.45f, Color.Lerp(_baseColors[i], Color.white, 0.6f), g) : _baseColors[i] * 0.45f;
                _particles[i].startSize = (NeuronKind.IsDescending(_net.Kind[i]) ? 0.035f : 0.012f) * (1f + 1.2f * g);
            }
            _ps.SetParticles(_particles, _particles.Length);
        }

        public Vector3 WorldPositionOf(int neuron) => transform.TransformPoint(_positions[neuron]);
    }
}
