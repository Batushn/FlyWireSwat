using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlyWireSwat.Connectome
{
    /// <summary>
    /// Read-only, Burst-friendly representation of the connectome subgraph:
    /// CSR adjacency (outgoing edges per neuron) with signed synaptic weights.
    /// </summary>
    public sealed class NetworkSpec : IDisposable
    {
        public int NeuronCount;
        public int EdgeCount;
        public CircuitData Source;

        public NativeArray<int> OutStart;      // N+1
        public NativeArray<int> OutPost;       // E
        public NativeArray<float> OutWeight;   // E  (mV jump, signed)
        public NativeArray<byte> Kind;         // N  NeuronKind
        public NativeArray<sbyte> Side;        // N  -1 left, +1 right, 0 center/unknown
        public NativeArray<float> Hetero;      // N  per-neuron input gain heterogeneity (receptive-field proxy)

        public readonly List<int> GfIndices = new List<int>();
        public readonly List<int> LongModeDnIndices = new List<int>();   // DNp02/04/06/11
        public readonly Dictionary<byte, List<int>> ByKind = new Dictionary<byte, List<int>>();

        public static NetworkSpec Build(CircuitData data, float mvPerSynapse, float dnInputGain = 1f, uint seed = 12345)
        {
            var spec = new NetworkSpec { Source = data };
            int n = data.neurons.Count;
            spec.NeuronCount = n;

            var outEdges = new List<(int post, float w)>[n];
            for (int i = 0; i < n; i++) outEdges[i] = new List<(int, float)>();
            foreach (var e in data.edges)
            {
                float w = e.sign * e.syn * mvPerSynapse;
                if (NeuronKind.IsDescending(NeuronKind.FromType(data.neurons[e.post].type))) w *= dnInputGain;
                if (math.abs(w) < 1e-6f) continue;
                outEdges[e.pre].Add((e.post, w));
            }

            int edgeCount = 0;
            foreach (var l in outEdges) edgeCount += l.Count;
            spec.EdgeCount = edgeCount;

            spec.OutStart = new NativeArray<int>(n + 1, Allocator.Persistent);
            spec.OutPost = new NativeArray<int>(math.max(1, edgeCount), Allocator.Persistent);
            spec.OutWeight = new NativeArray<float>(math.max(1, edgeCount), Allocator.Persistent);
            spec.Kind = new NativeArray<byte>(n, Allocator.Persistent);
            spec.Side = new NativeArray<sbyte>(n, Allocator.Persistent);
            spec.Hetero = new NativeArray<float>(n, Allocator.Persistent);

            var rng = new Unity.Mathematics.Random(seed);
            int cursor = 0;
            for (int i = 0; i < n; i++)
            {
                spec.OutStart[i] = cursor;
                foreach (var (post, w) in outEdges[i])
                {
                    spec.OutPost[cursor] = post;
                    spec.OutWeight[cursor] = w;
                    cursor++;
                }
                var info = data.neurons[i];
                byte kind = NeuronKind.FromType(info.type);
                spec.Kind[i] = kind;
                spec.Side[i] = (sbyte)(info.side == "left" ? -1 : info.side == "right" ? 1 : 0);
                // Each visual projection neuron covers a different patch of the visual field;
                // we approximate that with a fixed per-neuron gain drawn once (log-normal-ish).
                spec.Hetero[i] = math.clamp(rng.NextFloat(0.55f, 1.45f), 0.3f, 2f);

                if (!spec.ByKind.TryGetValue(kind, out var list)) spec.ByKind[kind] = list = new List<int>();
                list.Add(i);
                if (kind == NeuronKind.GF) spec.GfIndices.Add(i);
                else if (NeuronKind.IsDescending(kind)) spec.LongModeDnIndices.Add(i);
            }
            spec.OutStart[n] = cursor;
            return spec;
        }

        public static NetworkSpec LoadDefault(float mvPerSynapse, float dnInputGain = 1f)
        {
            var ta = Resources.Load<TextAsset>("Connectome/escape_circuit");
            if (ta == null) throw new InvalidOperationException("Resources/Connectome/escape_circuit.json missing. Run Tools/extract_escape_circuit.py");
            var data = CircuitData.FromJson(ta.text);
            return Build(data, mvPerSynapse, dnInputGain);
        }

        public void Dispose()
        {
            if (OutStart.IsCreated) OutStart.Dispose();
            if (OutPost.IsCreated) OutPost.Dispose();
            if (OutWeight.IsCreated) OutWeight.Dispose();
            if (Kind.IsCreated) Kind.Dispose();
            if (Side.IsCreated) Side.Dispose();
            if (Hetero.IsCreated) Hetero.Dispose();
        }
    }
}
