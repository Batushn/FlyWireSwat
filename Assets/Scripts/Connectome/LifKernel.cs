using FlyWireSwat.Fly;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlyWireSwat.Connectome
{
    public interface ISpikeSink
    {
        void Add(int step, int neuron);
    }

    public struct NullSink : ISpikeSink
    {
        public void Add(int step, int neuron) { }
    }

    public struct ListSink : ISpikeSink
    {
        public NativeList<int2> list;
        public void Add(int step, int neuron) => list.Add(new int2(step, neuron));
    }

    /// <summary>Per-trial outputs written by the kernel.</summary>
    public struct TrialOutput
    {
        public float simStartMs;
        public float simEndMs;
        public int steps;
        public int totalSpikes;
        public int vpnSpikes;
    }

    /// <summary>External drive (mV) per visual projection neuron class for one instant.</summary>
    public struct Drives
    {
        public float lc4, lplc2, lplc1, lc6, lc16, lc22, lc15;
    }

    [BurstCompile]
    public static class LifKernel
    {
        /// <summary>Class-level drives from angular size (deg) and expansion velocity (deg/s). Ache et al. 2019 tuning.</summary>
        public static Drives ComputeDrives(in SensoryTuning tune, float angle, float angVel, float vis)
        {
            float gate = angVel / (angVel + tune.expansionHalfDegPerSec);
            float sizeTerm = angle / (angle + tune.lplc2HalfDeg) * gate;
            float velTerm = angVel / (angVel + tune.lc4HalfDegPerSec);
            float loomMixed = 0.5f * (sizeTerm + velTerm);
            float smallObj = angle < 15f ? math.saturate(angle / 6f) * (1f - angle / 15f) : 0f;
            return new Drives
            {
                lc4 = tune.lc4GainMv * velTerm * vis,
                lplc2 = tune.lplc2GainMv * sizeTerm * vis,
                lplc1 = tune.lplc1GainMv * loomMixed * vis,
                lc6 = tune.lc6GainMv * loomMixed * vis,
                lc16 = tune.lc16GainMv * loomMixed * vis,
                lc22 = tune.lc22GainMv * smallObj * vis,
                lc15 = tune.lc15GainMv * smallObj * vis,
            };
        }

        /// <summary>Advances every neuron by one time step. Shared by the batch trials and the live FPS brain.</summary>
        public static void StepNeurons<TSink>(
            in LifParams p, in Drives d, float2 eye, ref Unity.Mathematics.Random rng,
            NativeArray<int> outStart, NativeArray<int> outPost, NativeArray<float> outWeight,
            NativeArray<byte> kind, NativeArray<sbyte> side, NativeArray<float> hetero,
            NativeSlice<float> v, NativeSlice<float> isyn, NativeSlice<float> refrac,
            NativeArray<int> dnIndices, NativeSlice<float> dnFirstSpike, NativeSlice<int> dnSpikeCount,
            float tMs, int step, ref TrialOutput output, ref TSink sink)
            where TSink : struct, ISpikeSink
        {
            int n = v.Length;
            float synDecay = math.exp(-p.dtMs / p.tauSynMs);
            float dtOverTau = p.dtMs / p.tauMembraneMs;
            float noiseScale = p.noiseMv * math.sqrt(p.dtMs) * 3.4641f; // uniform(-.5,.5) has std 1/sqrt(12)

            for (int i = 0; i < n; i++)
            {
                float iext = 0f;
                byte k = kind[i];
                if (k != NeuronKind.Other && k <= NeuronKind.LC15)
                {
                    float w = side[i] < 0 ? eye.x : side[i] > 0 ? eye.y : 0.5f * (eye.x + eye.y);
                    float dr;
                    switch (k)
                    {
                        case NeuronKind.LC4: dr = d.lc4; break;
                        case NeuronKind.LPLC2: dr = d.lplc2; break;
                        case NeuronKind.LPLC1: dr = d.lplc1; break;
                        case NeuronKind.LC6: dr = d.lc6; break;
                        case NeuronKind.LC16: dr = d.lc16; break;
                        case NeuronKind.LC22: dr = d.lc22; break;
                        default: dr = d.lc15; break;
                    }
                    iext = dr * w * hetero[i];
                }

                float cur = isyn[i];
                isyn[i] = cur * synDecay;

                if (refrac[i] > 0f)
                {
                    refrac[i] -= p.dtMs;
                    v[i] = p.vReset;
                    continue;
                }

                float vi = v[i];
                float noise = (rng.NextFloat() - 0.5f) * noiseScale;
                vi += (-(vi - p.vRest) + cur + iext) * dtOverTau + noise;

                if (vi >= p.vThreshold)
                {
                    v[i] = p.vReset;
                    refrac[i] = p.refractoryMs;
                    output.totalSpikes++;
                    if (k >= NeuronKind.LC4 && k <= NeuronKind.LC15) output.vpnSpikes++;
                    sink.Add(step, i);
                    int e0 = outStart[i], e1 = outStart[i + 1];
                    for (int e = e0; e < e1; e++)
                        isyn[outPost[e]] += outWeight[e];
                    if (k >= NeuronKind.GF)
                    {
                        for (int q = 0; q < dnIndices.Length; q++)
                        {
                            if (dnIndices[q] != i) continue;
                            if (dnFirstSpike[q] < 0f) dnFirstSpike[q] = tMs;
                            dnSpikeCount[q]++;
                            break;
                        }
                    }
                }
                else v[i] = vi;
            }
        }

        /// <summary>
        /// Runs one looming trial through the network. State arrays are slices of length N.
        /// dnFirstSpike / dnSpikeCount are slices of length dnIndices.Length.
        /// </summary>
        public static void RunTrial<TSink>(
            in LifParams p, in SensoryTuning tune, in StimulusSpec stim,
            NativeArray<int> outStart, NativeArray<int> outPost, NativeArray<float> outWeight,
            NativeArray<byte> kind, NativeArray<sbyte> side, NativeArray<float> hetero,
            NativeSlice<float> v, NativeSlice<float> isyn, NativeSlice<float> refrac,
            NativeArray<int> dnIndices, NativeSlice<float> dnFirstSpike, NativeSlice<int> dnSpikeCount,
            ref TrialOutput output, ref TSink sink)
            where TSink : struct, ISpikeSink
        {
            int n = v.Length;
            for (int i = 0; i < n; i++) { v[i] = p.vRest; isyn[i] = 0f; refrac[i] = 0f; }
            for (int k = 0; k < dnIndices.Length; k++) { dnFirstSpike[k] = -1f; dnSpikeCount[k] = 0; }

            float startMs = LoomGeometry.FirstVisibleTimeMs(stim, tune.minAngleDeg);
            float endMs = stim.ImpactTimeMs + 10f;   // a few ms past impact so late GF spikes are counted
            int steps = (int)math.ceil((endMs - startMs) / p.dtMs);
            output.simStartMs = startMs;
            output.simEndMs = endMs;
            output.steps = steps;
            output.totalSpikes = 0;
            output.vpnSpikes = 0;

            var rng = new Unity.Mathematics.Random(stim.seed == 0 ? 1u : stim.seed);
            float2 eye = LoomGeometry.EyeWeights(stim.azimuthDeg, tune.rearVisibility);

            for (int step = 0; step < steps; step++)
            {
                float tMs = startMs + step * p.dtMs;
                float angle = LoomGeometry.AngleDeg(stim, tMs);
                float angVel = LoomGeometry.AngularVelocityDegPerSec(stim, tMs);
                var d = ComputeDrives(tune, angle, angVel, stim.visibility);
                StepNeurons(p, d, eye, ref rng, outStart, outPost, outWeight, kind, side, hetero,
                    v, isyn, refrac, dnIndices, dnFirstSpike, dnSpikeCount, tMs, step, ref output, ref sink);
            }
        }
    }

    /// <summary>
    /// Real-time brain for the FPS mode: keeps its membrane state between frames and advances
    /// by the elapsed wall-clock time each frame with whatever the fly currently sees.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct LiveBrainJob : IJob
    {
        public LifParams Params;
        public SensoryTuning Tuning;
        public float AngleDeg, AngVelDegPerSec, Visibility;
        public float2 Eye;
        public int Steps;
        public float StartMs;
        public uint Seed;

        [ReadOnly] public NativeArray<int> OutStart;
        [ReadOnly] public NativeArray<int> OutPost;
        [ReadOnly] public NativeArray<float> OutWeight;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<sbyte> Side;
        [ReadOnly] public NativeArray<float> Hetero;
        [ReadOnly] public NativeArray<int> DnIndices;

        public NativeArray<float> V;
        public NativeArray<float> Isyn;
        public NativeArray<float> Refrac;
        public NativeArray<float> DnFirstSpike;   // reset each frame: first spike this frame (ms), -1 none
        public NativeArray<int> DnSpikeCount;     // spikes this frame
        public NativeArray<TrialOutput> Output;
        public NativeList<int2> Spikes;           // (step, neuron) this frame

        public void Execute()
        {
            for (int k = 0; k < DnIndices.Length; k++) { DnFirstSpike[k] = -1f; DnSpikeCount[k] = 0; }
            Spikes.Clear();
            var sink = new ListSink { list = Spikes };
            var output = Output[0];
            var rng = new Unity.Mathematics.Random(Seed == 0 ? 1u : Seed);
            var d = LifKernel.ComputeDrives(Tuning, AngleDeg, AngVelDegPerSec, Visibility);
            for (int step = 0; step < Steps; step++)
            {
                float tMs = StartMs + step * Params.dtMs;
                LifKernel.StepNeurons(Params, d, Eye, ref rng, OutStart, OutPost, OutWeight, Kind, Side, Hetero,
                    V.Slice(), Isyn.Slice(), Refrac.Slice(), DnIndices, DnFirstSpike.Slice(), DnSpikeCount.Slice(), tMs, step, ref output, ref sink);
            }
            output.steps += Steps;
            Output[0] = output;
        }
    }

    /// <summary>Simulates many independent trials in parallel (one trial per index).</summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct TrialBatchJob : IJobParallelFor
    {
        public LifParams Params;
        public SensoryTuning Tuning;
        [ReadOnly] public NativeArray<StimulusSpec> Stimuli;

        [ReadOnly] public NativeArray<int> OutStart;
        [ReadOnly] public NativeArray<int> OutPost;
        [ReadOnly] public NativeArray<float> OutWeight;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<sbyte> Side;
        [ReadOnly] public NativeArray<float> Hetero;
        [ReadOnly] public NativeArray<int> DnIndices;

        public int NeuronCount;

        [NativeDisableParallelForRestriction] public NativeArray<float> V;
        [NativeDisableParallelForRestriction] public NativeArray<float> Isyn;
        [NativeDisableParallelForRestriction] public NativeArray<float> Refrac;
        [NativeDisableParallelForRestriction] public NativeArray<float> DnFirstSpike;
        [NativeDisableParallelForRestriction] public NativeArray<int> DnSpikeCount;
        [NativeDisableParallelForRestriction] public NativeArray<TrialOutput> Outputs;

        public void Execute(int t)
        {
            int n = NeuronCount;
            int dn = DnIndices.Length;
            var sink = new NullSink();
            var output = Outputs[t];
            var stim = Stimuli[t];
            LifKernel.RunTrial(Params, Tuning, stim,
                OutStart, OutPost, OutWeight, Kind, Side, Hetero,
                V.Slice(t * n, n), Isyn.Slice(t * n, n), Refrac.Slice(t * n, n),
                DnIndices, DnFirstSpike.Slice(t * dn, dn), DnSpikeCount.Slice(t * dn, dn),
                ref output, ref sink);
            Outputs[t] = output;
        }
    }

    /// <summary>Simulates a single trial and records every spike (for the visual replay).</summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct RecordedTrialJob : IJob
    {
        public LifParams Params;
        public SensoryTuning Tuning;
        public StimulusSpec Stimulus;

        [ReadOnly] public NativeArray<int> OutStart;
        [ReadOnly] public NativeArray<int> OutPost;
        [ReadOnly] public NativeArray<float> OutWeight;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<sbyte> Side;
        [ReadOnly] public NativeArray<float> Hetero;
        [ReadOnly] public NativeArray<int> DnIndices;

        public NativeArray<float> V;
        public NativeArray<float> Isyn;
        public NativeArray<float> Refrac;
        public NativeArray<float> DnFirstSpike;
        public NativeArray<int> DnSpikeCount;
        public NativeArray<TrialOutput> Output;
        public NativeList<int2> Spikes;

        public void Execute()
        {
            var sink = new ListSink { list = Spikes };
            var output = Output[0];
            LifKernel.RunTrial(Params, Tuning, Stimulus,
                OutStart, OutPost, OutWeight, Kind, Side, Hetero,
                V.Slice(), Isyn.Slice(), Refrac.Slice(),
                DnIndices, DnFirstSpike.Slice(), DnSpikeCount.Slice(),
                ref output, ref sink);
            Output[0] = output;
        }
    }
}
