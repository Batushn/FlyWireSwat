using System;
using System.Collections.Generic;
using FlyWireSwat.Connectome;
using FlyWireSwat.Fly;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlyWireSwat.Fps
{
    /// <summary>
    /// The connectome running in real time. Feed it what the fly sees every frame; it tells you
    /// when the Giant Fiber or the long-mode descending neurons fire.
    /// </summary>
    public sealed class LiveBrain : IDisposable
    {
        readonly NetworkSpec _net;
        readonly LifParams _lif;
        readonly SensoryTuning _tune;
        readonly NativeArray<int> _dn;
        readonly int _gfCount;
        NativeArray<float> _v, _isyn, _refrac, _first;
        NativeArray<int> _count;
        NativeArray<TrialOutput> _out;
        NativeList<int2> _spikes;
        float _clockMs;
        uint _seed = 1234;
        float _carryMs;

        public readonly List<int> SpikedNeurons = new List<int>();
        public bool GfFiredThisFrame { get; private set; }
        public int LongDnSpikesThisFrame { get; private set; }
        public float ClockMs => _clockMs;
        public float LastAngleDeg { get; private set; }
        public float LastAngVel { get; private set; }

        public LiveBrain(NetworkSpec net, LifParams lif, SensoryTuning tune)
        {
            _net = net; _lif = lif; _tune = tune;
            var dn = new List<int>(net.GfIndices); dn.AddRange(net.LongModeDnIndices);
            _dn = new NativeArray<int>(dn.ToArray(), Allocator.Persistent);
            _gfCount = net.GfIndices.Count;
            int n = net.NeuronCount;
            _v = new NativeArray<float>(n, Allocator.Persistent);
            _isyn = new NativeArray<float>(n, Allocator.Persistent);
            _refrac = new NativeArray<float>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) _v[i] = lif.vRest;
            _first = new NativeArray<float>(dn.Count, Allocator.Persistent);
            _count = new NativeArray<int>(dn.Count, Allocator.Persistent);
            _out = new NativeArray<TrialOutput>(1, Allocator.Persistent);
            _spikes = new NativeList<int2>(8192, Allocator.Persistent);
        }

        /// <summary>Advance by dtSeconds of wall-clock time with the given percept.</summary>
        public void Tick(float dtSeconds, float angleDeg, float angVelDegPerSec, float azimuthDeg, float visibility)
        {
            float ms = dtSeconds * 1000f + _carryMs;
            int steps = (int)(ms / _lif.dtMs);
            _carryMs = ms - steps * _lif.dtMs;
            steps = math.min(steps, 1000);   // never simulate more than 100 ms per frame
            LastAngleDeg = angleDeg; LastAngVel = angVelDegPerSec;
            GfFiredThisFrame = false; LongDnSpikesThisFrame = 0; SpikedNeurons.Clear();
            if (steps <= 0) return;
            _seed = math.hash(new uint2(_seed, 0x9E3779B9u));
            var job = new LiveBrainJob
            {
                Params = _lif, Tuning = _tune,
                AngleDeg = angleDeg, AngVelDegPerSec = angVelDegPerSec, Visibility = visibility,
                Eye = LoomGeometry.EyeWeights(azimuthDeg, _tune.rearVisibility),
                Steps = steps, StartMs = _clockMs, Seed = _seed,
                OutStart = _net.OutStart, OutPost = _net.OutPost, OutWeight = _net.OutWeight,
                Kind = _net.Kind, Side = _net.Side, Hetero = _net.Hetero, DnIndices = _dn,
                V = _v, Isyn = _isyn, Refrac = _refrac, DnFirstSpike = _first, DnSpikeCount = _count, Output = _out, Spikes = _spikes,
            };
            job.Schedule().Complete();
            _clockMs += steps * _lif.dtMs;
            for (int k = 0; k < _gfCount; k++) if (_count[k] > 0) GfFiredThisFrame = true;
            for (int k = _gfCount; k < _count.Length; k++) LongDnSpikesThisFrame += _count[k];
            for (int i = 0; i < _spikes.Length; i++) SpikedNeurons.Add(_spikes[i].y);
        }

        public void Reset()
        {
            for (int i = 0; i < _v.Length; i++) { _v[i] = _lif.vRest; _isyn[i] = 0f; _refrac[i] = 0f; }
        }

        public void Dispose()
        {
            _dn.Dispose(); _v.Dispose(); _isyn.Dispose(); _refrac.Dispose(); _first.Dispose(); _count.Dispose(); _out.Dispose(); _spikes.Dispose();
        }
    }
}
