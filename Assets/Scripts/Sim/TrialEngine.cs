using System;
using System.Collections;
using System.Collections.Generic;
using FlyWireSwat.Connectome;
using FlyWireSwat.Fly;
using FlyWireSwat.Weapons;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlyWireSwat.Sim
{
    /// <summary>One full engagement: repeated attempts until the fly dies or we give up.</summary>
    public class TrialRecord
    {
        public int index;
        public bool killed;
        public float timeSeconds;        // wall-clock time until death (or until giving up)
        public int attempts;
        public float cost;
        public float collateral;
        public EscapeMode firstEscape;   // what the fly did on the very first attempt
        public float firstGfLatencyMs;   // impact - GF spike on the first attempt (-1 if no GF spike)
        public bool firstTakeoffBeforeImpact;
        public List<AttemptResult> attemptsLog = new List<AttemptResult>();
    }

    public class TrialEngine : IDisposable
    {
        public readonly NetworkSpec Net;
        public LifParams Lif;
        public SensoryTuning Tuning;
        public float TimeBudgetSeconds = 1800f;    // 30 minutes of trying, then we give up
        public float MeanRelandSeconds = 20f;      // how long a scared fly stays in the air
        public bool KeepAttemptLogs = false;

        NativeArray<int> _dnIndices;
        readonly int[] _gfSlots;
        readonly int[] _longSlots;

        public TrialEngine(NetworkSpec net, LifParams lif, SensoryTuning tuning)
        {
            Net = net; Lif = lif; Tuning = tuning;
            var dn = new List<int>();
            dn.AddRange(net.GfIndices);
            dn.AddRange(net.LongModeDnIndices);
            _dnIndices = new NativeArray<int>(dn.ToArray(), Allocator.Persistent);
            _gfSlots = new int[net.GfIndices.Count];
            for (int i = 0; i < _gfSlots.Length; i++) _gfSlots[i] = i;
            _longSlots = new int[net.LongModeDnIndices.Count];
            for (int i = 0; i < _longSlots.Length; i++) _longSlots[i] = net.GfIndices.Count + i;
        }

        public NativeArray<int> DnIndices => _dnIndices;

        /// <summary>Buffers of an in-flight batch simulation.</summary>
        public sealed class BatchHandle
        {
            public JobHandle Handle;
            public int Count, N, Dn;
            public NativeArray<StimulusSpec> Stim;
            public NativeArray<float> V, Isyn, Refrac, First;
            public NativeArray<int> Count_;
            public NativeArray<TrialOutput> Outputs;
            public bool IsCompleted => Handle.IsCompleted;
        }

        /// <summary>Schedules a batch of stimuli through the network (parallel Burst job).</summary>
        public BatchHandle BeginBatch(StimulusSpec[] stimuli)
        {
            int t = stimuli.Length, n = Net.NeuronCount, dn = _dnIndices.Length;
            var h = new BatchHandle
            {
                Count = t, N = n, Dn = dn,
                Stim = new NativeArray<StimulusSpec>(stimuli, Allocator.Persistent),
                V = new NativeArray<float>(t * n, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                Isyn = new NativeArray<float>(t * n, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                Refrac = new NativeArray<float>(t * n, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                First = new NativeArray<float>(t * dn, Allocator.Persistent),
                Count_ = new NativeArray<int>(t * dn, Allocator.Persistent),
                Outputs = new NativeArray<TrialOutput>(t, Allocator.Persistent),
            };
            var job = new TrialBatchJob
            {
                Params = Lif, Tuning = Tuning, Stimuli = h.Stim,
                OutStart = Net.OutStart, OutPost = Net.OutPost, OutWeight = Net.OutWeight,
                Kind = Net.Kind, Side = Net.Side, Hetero = Net.Hetero, DnIndices = _dnIndices,
                NeuronCount = n, V = h.V, Isyn = h.Isyn, Refrac = h.Refrac,
                DnFirstSpike = h.First, DnSpikeCount = h.Count_, Outputs = h.Outputs,
            };
            h.Handle = job.Schedule(t, 1);
            JobHandle.ScheduleBatchedJobs();
            return h;
        }

        /// <summary>Completes a batch and copies the descending-neuron results to managed arrays.</summary>
        public void EndBatch(BatchHandle h, out float[][] gfFirst, out float[][] longFirst, out int[][] longCounts, out float[] simStart)
        {
            h.Handle.Complete();
            int t = h.Count, dn = h.Dn;
            try
            {
                gfFirst = new float[t][]; longFirst = new float[t][]; longCounts = new int[t][]; simStart = new float[t];
                for (int i = 0; i < t; i++)
                {
                    gfFirst[i] = new float[_gfSlots.Length];
                    for (int k = 0; k < _gfSlots.Length; k++) gfFirst[i][k] = h.First[i * dn + _gfSlots[k]];
                    longFirst[i] = new float[_longSlots.Length];
                    longCounts[i] = new int[_longSlots.Length];
                    for (int k = 0; k < _longSlots.Length; k++)
                    {
                        longFirst[i][k] = h.First[i * dn + _longSlots[k]];
                        longCounts[i][k] = h.Count_[i * dn + _longSlots[k]];
                    }
                    simStart[i] = h.Outputs[i].simStartMs;
                }
            }
            finally
            {
                h.Stim.Dispose(); h.V.Dispose(); h.Isyn.Dispose(); h.Refrac.Dispose(); h.First.Dispose(); h.Count_.Dispose(); h.Outputs.Dispose();
            }
        }

        public void SimulateBatch(StimulusSpec[] stimuli, out float[][] gfFirst, out float[][] longFirst, out int[][] longCounts, out float[] simStart)
            => EndBatch(BeginBatch(stimuli), out gfFirst, out longFirst, out longCounts, out simStart);

        /// <summary>A feeding or grooming fly only half-attends to the world.</summary>
        public static void ApplyAttention(ref StimulusSpec stim, in SensoryTuning tuning, ref Unity.Mathematics.Random rng)
        {
            if (rng.NextFloat() < tuning.distractedProbability) stim.visibility *= tuning.distractedVisibility;
        }

        /// <summary>Simulates one stimulus and returns every spike (step, neuron) for replay.</summary>
        public AttemptResult SimulateRecorded(WeaponDefinition w, StimulusSpec stim, uint rngSeed, out List<int2> spikes, out TrialOutput output)
        {
            int n = Net.NeuronCount, dn = _dnIndices.Length;
            var v = new NativeArray<float>(n, Allocator.TempJob);
            var isyn = new NativeArray<float>(n, Allocator.TempJob);
            var refrac = new NativeArray<float>(n, Allocator.TempJob);
            var first = new NativeArray<float>(dn, Allocator.TempJob);
            var count = new NativeArray<int>(dn, Allocator.TempJob);
            var outArr = new NativeArray<TrialOutput>(1, Allocator.TempJob);
            var list = new NativeList<int2>(65536, Allocator.TempJob);
            try
            {
                var job = new RecordedTrialJob
                {
                    Params = Lif, Tuning = Tuning, Stimulus = stim,
                    OutStart = Net.OutStart, OutPost = Net.OutPost, OutWeight = Net.OutWeight,
                    Kind = Net.Kind, Side = Net.Side, Hetero = Net.Hetero, DnIndices = _dnIndices,
                    V = v, Isyn = isyn, Refrac = refrac, DnFirstSpike = first, DnSpikeCount = count, Output = outArr, Spikes = list,
                };
                job.Schedule().Complete();
                output = outArr[0];
                spikes = new List<int2>(list.Length);
                for (int i = 0; i < list.Length; i++) spikes.Add(list[i]);

                var gf = new float[_gfSlots.Length];
                for (int k = 0; k < gf.Length; k++) gf[k] = first[_gfSlots[k]];
                var lf = new float[_longSlots.Length];
                var lc = new int[_longSlots.Length];
                for (int k = 0; k < lf.Length; k++) { lf[k] = first[_longSlots[k]]; lc[k] = count[_longSlots[k]]; }
                var rng = new Unity.Mathematics.Random(rngSeed == 0 ? 7u : rngSeed);
                return AttemptResolver.Resolve(w, stim, output.simStartMs, gf, lf, lc, ref rng);
            }
            finally
            {
                v.Dispose(); isyn.Dispose(); refrac.Dispose(); first.Dispose(); count.Dispose(); outArr.Dispose(); list.Dispose();
            }
        }

        static float Exponential(ref Unity.Mathematics.Random rng, float mean) => -mean * math.log(math.max(1e-6f, rng.NextFloat()));

        /// <summary>Runs <paramref name="trialCount"/> full engagements with a weapon (blocking).</summary>
        public List<TrialRecord> RunWeapon(WeaponDefinition w, int trialCount, uint seed)
        {
            var records = new List<TrialRecord>(trialCount);
            var it = RunWeaponAsync(w, trialCount, seed, records, null);
            while (it.MoveNext()) { }
            return records;
        }

        /// <summary>Coroutine version: yields while Burst jobs run so the editor stays responsive.</summary>
        public IEnumerator RunWeaponAsync(WeaponDefinition w, int trialCount, uint seed, List<TrialRecord> records, Action<float> progress)
        {
            records.Clear();
            var rngs = new Unity.Mathematics.Random[trialCount];
            for (int i = 0; i < trialCount; i++)
            {
                records.Add(new TrialRecord { index = i, timeSeconds = w.setupSeconds, cost = 0f, collateral = 0f, firstEscape = EscapeMode.None, firstGfLatencyMs = -1f });
                rngs[i] = new Unity.Mathematics.Random(math.hash(new uint2(seed, (uint)i + 1)));
            }

            if (w.stimulusKind == StimulusKind.Passive)
            {
                for (int i = 0; i < trialCount; i++)
                {
                    var rec = records[i];
                    rec.attempts = 1; rec.cost = w.costPerAttempt; rec.collateral = w.collateralPerAttempt;
                    float wait = Exponential(ref rngs[i], w.passiveMeanWaitSeconds);
                    bool caught = rngs[i].NextFloat() < w.killProbabilityInside;
                    float t = w.setupSeconds + wait;
                    if (caught && t <= TimeBudgetSeconds) { rec.killed = true; rec.timeSeconds = t; }
                    else { rec.killed = false; rec.timeSeconds = TimeBudgetSeconds; }
                }
                progress?.Invoke(1f);
                yield break;
            }

            var pending = new List<int>(trialCount);
            for (int i = 0; i < trialCount; i++) pending.Add(i);

            for (int attempt = 1; attempt <= w.maxAttempts && pending.Count > 0; attempt++)
            {
                var stimuli = new StimulusSpec[pending.Count];
                for (int p = 0; p < pending.Count; p++)
                {
                    var rng = rngs[pending[p]];
                    float az = rng.NextFloat(0f, 360f);
                    float el = rng.NextFloat(20f, 70f);
                    stimuli[p] = w.BuildStimulus(az, el, rng.NextUInt(1, uint.MaxValue));
                    ApplyAttention(ref stimuli[p], Tuning, ref rng);
                    rngs[pending[p]] = rng;
                }

                var handle = BeginBatch(stimuli);
                while (!handle.IsCompleted) yield return null;
                EndBatch(handle, out var gfFirst, out var longFirst, out var longCounts, out var simStart);

                var still = new List<int>();
                for (int p = 0; p < pending.Count; p++)
                {
                    int i = pending[p];
                    var rec = records[i];
                    var rng = rngs[i];
                    var res = AttemptResolver.Resolve(w, stimuli[p], simStart[p], gfFirst[p], longFirst[p], longCounts[p], ref rng);
                    rec.attempts = attempt;
                    rec.cost += w.costPerAttempt;
                    rec.collateral += w.collateralPerAttempt;
                    rec.timeSeconds += res.impactMs * 0.001f;
                    if (KeepAttemptLogs) rec.attemptsLog.Add(res);
                    if (attempt == 1)
                    {
                        rec.firstEscape = res.decision.mode;
                        rec.firstGfLatencyMs = res.decision.gfSpikeMs >= 0f ? res.impactMs - res.decision.gfSpikeMs : -1f;
                        rec.firstTakeoffBeforeImpact = res.decision.takeoffMs >= 0f && res.decision.takeoffMs < res.impactMs;
                    }

                    if (res.killed)
                    {
                        rec.killed = true;
                        rec.timeSeconds += w.killDelaySeconds;
                        rngs[i] = rng;
                        continue;
                    }

                    // miss: wait for the fly to come back, then retry
                    bool flew = res.airborne || (w.scaresFlyOnMiss && rng.NextFloat() < 0.8f);
                    rec.timeSeconds += w.retrySeconds + (flew ? Exponential(ref rng, MeanRelandSeconds) : 0f);
                    rngs[i] = rng;
                    if (rec.timeSeconds < TimeBudgetSeconds) still.Add(i);
                    else rec.timeSeconds = TimeBudgetSeconds;
                }
                pending = still;
                progress?.Invoke(1f - (float)pending.Count / trialCount);
            }
            foreach (var i in pending) records[i].timeSeconds = math.min(records[i].timeSeconds, TimeBudgetSeconds);
            progress?.Invoke(1f);
        }

        public void Dispose()
        {
            if (_dnIndices.IsCreated) _dnIndices.Dispose();
        }
    }
}
