using Unity.Mathematics;

namespace FlyWireSwat.Fly
{
    public enum EscapeMode : byte { None = 0, ShortGiantFiber = 1, LongCoordinated = 2 }

    /// <summary>
    /// Turns descending-neuron spikes into a take-off decision and kinematics.
    /// Latencies from von Reyn et al. 2014 (Nat. Neurosci.) and Card & Dickinson 2008:
    ///   GF spike -> TTM/DLM -> legs extend: ~7 ms ("short mode", uncoordinated, random-ish heading)
    ///   non-GF descending activity -> wing raise -> jump: ~60-130 ms ("long mode", directed away)
    /// </summary>
    public static class EscapeModel
    {
        public const float ShortModeLatencyMs = 7f;
        public const float LongModeLatencyMs = 90f;
        public const float ShortModeSpeed = 0.6f;    // m/s at leg extension
        public const float LongModeSpeed = 0.45f;
        public const float FlightSpeed = 1.0f;       // cruise speed once wings work
        public const float FlightAccel = 8f;         // m/s^2

        public struct Decision
        {
            public EscapeMode mode;
            public float gfSpikeMs;        // -1 if none
            public float longDnSpikeMs;    // -1 if none
            public float takeoffMs;        // -1 if no takeoff
            public float3 direction;       // unit vector of the jump
            public float initialSpeed;
        }

        /// <param name="gfFirst">first spike time of each GF neuron (-1 none)</param>
        /// <param name="longFirst">first spike time of each long-mode DN (-1 none)</param>
        /// <param name="longCounts">spike counts of long-mode DNs</param>
        /// <param name="threatAzimuthDeg">direction of the threat, 0 = in front of the fly</param>
        public static Decision Decide(float[] gfFirst, float[] longFirst, int[] longCounts, float threatAzimuthDeg, ref Unity.Mathematics.Random rng)
        {
            var d = new Decision { mode = EscapeMode.None, gfSpikeMs = -1f, longDnSpikeMs = -1f, takeoffMs = -1f, direction = math.up(), initialSpeed = 0f };
            foreach (var t in gfFirst) if (t >= 0f && (d.gfSpikeMs < 0f || t < d.gfSpikeMs)) d.gfSpikeMs = t;

            // Long mode needs a bit of accumulated descending drive: at least 3 spikes across the DN pool.
            int total = 0;
            for (int i = 0; i < longCounts.Length; i++) total += longCounts[i];
            if (total >= 3)
                foreach (var t in longFirst) if (t >= 0f && (d.longDnSpikeMs < 0f || t < d.longDnSpikeMs)) d.longDnSpikeMs = t;

            float shortTakeoff = d.gfSpikeMs >= 0f ? d.gfSpikeMs + ShortModeLatencyMs + rng.NextFloat(-1f, 2f) : float.MaxValue;
            float longTakeoff = d.longDnSpikeMs >= 0f ? d.longDnSpikeMs + LongModeLatencyMs + rng.NextFloat(-25f, 35f) : float.MaxValue;

            float threatRad = math.radians(threatAzimuthDeg);
            float3 away = new float3(-math.sin(threatRad), 0f, -math.cos(threatRad)); // fly faces +Z

            if (shortTakeoff < longTakeoff && shortTakeoff < float.MaxValue)
            {
                d.mode = EscapeMode.ShortGiantFiber;
                d.takeoffMs = shortTakeoff;
                // short mode: legs fire before the fly has oriented; heading only loosely away
                float jitter = rng.NextFloat(-110f, 110f);
                float yaw = math.atan2(away.x, away.z) + math.radians(jitter);
                float pitch = math.radians(rng.NextFloat(35f, 75f));
                d.direction = new float3(math.sin(yaw) * math.cos(pitch), math.sin(pitch), math.cos(yaw) * math.cos(pitch));
                d.initialSpeed = ShortModeSpeed * rng.NextFloat(0.8f, 1.2f);
            }
            else if (longTakeoff < float.MaxValue)
            {
                d.mode = EscapeMode.LongCoordinated;
                d.takeoffMs = longTakeoff;
                float jitter = rng.NextFloat(-30f, 30f);
                float yaw = math.atan2(away.x, away.z) + math.radians(jitter);
                float pitch = math.radians(rng.NextFloat(25f, 50f));
                d.direction = new float3(math.sin(yaw) * math.cos(pitch), math.sin(pitch), math.cos(yaw) * math.cos(pitch));
                d.initialSpeed = LongModeSpeed * rng.NextFloat(0.85f, 1.15f);
            }
            return d;
        }

        /// <summary>Fly position (m, relative to its resting spot) at time t after take-off.</summary>
        public static float3 Displacement(in Decision d, float tMs)
        {
            if (d.takeoffMs < 0f || tMs <= d.takeoffMs) return float3.zero;
            float t = (tMs - d.takeoffMs) * 0.001f;
            // speed ramps from initial jump speed to flight speed
            float tRamp = math.max(0f, (FlightSpeed - d.initialSpeed) / FlightAccel);
            float dist;
            if (t < tRamp) dist = d.initialSpeed * t + 0.5f * FlightAccel * t * t;
            else dist = d.initialSpeed * tRamp + 0.5f * FlightAccel * tRamp * tRamp + FlightSpeed * (t - tRamp);
            // gravity sag on the short mode (it is basically a ballistic hop for the first ~50 ms)
            float3 p = d.direction * dist;
            if (d.mode == EscapeMode.ShortGiantFiber) p.y -= 0.5f * 9.81f * t * t * math.saturate(1f - t / 0.08f);
            return p;
        }
    }
}
