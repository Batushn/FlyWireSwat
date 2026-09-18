using FlyWireSwat.Fly;
using FlyWireSwat.Weapons;
using Unity.Mathematics;

namespace FlyWireSwat.Sim
{
    /// <summary>Outcome of one swing / shot / puff against the fly.</summary>
    public struct AttemptResult
    {
        public StimulusSpec stimulus;
        public EscapeModel.Decision decision;
        public float impactMs;
        public float2 aimOffset;         // where the weapon actually lands relative to the fly's spot (m)
        public float3 flyAtImpact;       // fly displacement at impact (m)
        public float distanceToImpact;   // m
        public bool inLethalZone;
        public bool airborne;
        public bool killed;
        public float killProbability;
        public float simStartMs;
    }

    public static class AttemptResolver
    {
        static float Gaussian(ref Unity.Mathematics.Random rng)
        {
            float u1 = math.max(1e-7f, rng.NextFloat());
            float u2 = rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
        }

        public static AttemptResult Resolve(WeaponDefinition w, in StimulusSpec stim, float simStartMs,
            float[] gfFirst, float[] longFirst, int[] longCounts, ref Unity.Mathematics.Random rng)
        {
            var r = new AttemptResult { stimulus = stim, simStartMs = simStartMs };
            r.impactMs = stim.ImpactTimeMs;
            r.decision = EscapeModel.Decide(gfFirst, longFirst, longCounts, stim.azimuthDeg, ref rng);

            r.aimOffset = new float2(Gaussian(ref rng), Gaussian(ref rng)) * w.aimSigma;
            // the cloud keeps growing for a while after engulfing the spot, so evaluate a bit later
            float evalMs = stim.kind == StimulusKind.ExpandingCloud ? r.impactMs + 400f : r.impactMs;
            r.flyAtImpact = EscapeModel.Displacement(r.decision, evalMs);
            r.airborne = r.decision.takeoffMs >= 0f && r.decision.takeoffMs < evalMs;

            float3 impactPoint = new float3(r.aimOffset.x, 0f, r.aimOffset.y);
            r.distanceToImpact = math.distance(r.flyAtImpact, impactPoint);
            r.inLethalZone = r.distanceToImpact <= w.lethalRadius + 0.003f; // + fly body radius

            r.killProbability = r.inLethalZone ? w.killProbabilityInside * (r.airborne ? w.airborneKillFactor : 1f) : 0f;
            r.killed = rng.NextFloat() < r.killProbability;
            return r;
        }
    }
}
