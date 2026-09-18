using Unity.Mathematics;

namespace FlyWireSwat.Fly
{
    public enum StimulusKind : byte
    {
        Approach = 0,        // solid object moving toward the fly (hand, swatter, bullet, rocket...)
        ExpandingCloud = 1,  // aerosol cloud growing in place (insect spray)
        Passive = 2,         // nothing approaches (sticky trap) - no network simulation
    }

    /// <summary>
    /// Blittable description of one looming event, as seen from the fly.
    /// Two-phase approach: a slow "wind-up" (raising the arm) followed by the strike.
    /// Distances in metres, speeds in m/s, times in ms.
    /// </summary>
    public struct StimulusSpec
    {
        public StimulusKind kind;
        public float objectRadius;      // half-size of the approaching object (m)
        public float startDistance;     // where the wind-up begins (m)
        public float windupSpeed;       // m/s during wind-up (0 = no wind-up)
        public float strikeDistance;    // distance at which the strike phase begins (m)
        public float strikeSpeed;       // m/s during strike
        public float cloudGrowth;       // m/s radius growth (ExpandingCloud)
        public float azimuthDeg;        // 0 = straight ahead of the fly, 180 = from behind
        public float elevationDeg;      // 0 = horizontal, 90 = straight down
        public float visibility;        // 0..1 multiplier on all visual drive
        public uint seed;

        public float WindupDurationMs => windupSpeed > 0f ? (startDistance - strikeDistance) / windupSpeed * 1000f : 0f;
        public float StrikeDurationMs => strikeSpeed > 0f ? strikeDistance / strikeSpeed * 1000f : 0f;
        /// <summary>Time at which the object reaches the fly (Approach) or the cloud engulfs it (Cloud).</summary>
        public float ImpactTimeMs
        {
            get
            {
                if (kind == StimulusKind.ExpandingCloud)
                    return cloudGrowth > 0f ? math.max(0f, (startDistance - objectRadius) / cloudGrowth) * 1000f : 0f;
                return WindupDurationMs + StrikeDurationMs;
            }
        }
    }

    public static class LoomGeometry
    {
        /// <summary>Distance from the fly to the nearest face of the object at time t.</summary>
        public static float Distance(in StimulusSpec s, float tMs)
        {
            if (s.kind == StimulusKind.ExpandingCloud)
            {
                float r = s.objectRadius + s.cloudGrowth * tMs * 0.001f;
                return math.max(0f, s.startDistance - r);
            }
            float wind = s.WindupDurationMs;
            if (tMs < wind)
                return s.startDistance - s.windupSpeed * tMs * 0.001f;
            return math.max(0f, s.strikeDistance - s.strikeSpeed * (tMs - wind) * 0.001f);
        }

        public static float ObjectRadius(in StimulusSpec s, float tMs)
        {
            if (s.kind == StimulusKind.ExpandingCloud) return s.objectRadius + s.cloudGrowth * tMs * 0.001f;
            return s.objectRadius;
        }

        /// <summary>Full angular size (degrees) subtended by the object.</summary>
        public static float AngleDeg(in StimulusSpec s, float tMs)
        {
            float d = Distance(s, tMs);
            float r = ObjectRadius(s, tMs);
            if (d <= 1e-5f) return 180f;
            return math.degrees(2f * math.atan(r / d));
        }

        /// <summary>Angular expansion velocity (deg/s), finite difference over 1 ms.</summary>
        public static float AngularVelocityDegPerSec(in StimulusSpec s, float tMs)
        {
            float a0 = AngleDeg(s, math.max(0f, tMs - 0.5f));
            float a1 = AngleDeg(s, tMs + 0.5f);
            return math.max(0f, (a1 - a0) * 1000f);
        }

        /// <summary>First time (ms) at which the stimulus exceeds the minimum visible angle.</summary>
        public static float FirstVisibleTimeMs(in StimulusSpec s, float minAngleDeg)
        {
            float end = s.ImpactTimeMs;
            if (AngleDeg(s, 0f) >= minAngleDeg) return 0f;
            float lo = 0f, hi = end;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (AngleDeg(s, mid) >= minAngleDeg) hi = mid; else lo = mid;
            }
            return hi;
        }

        /// <summary>Left/right eye weighting for a threat at the given azimuth (0 = front).</summary>
        public static float2 EyeWeights(float azimuthDeg, float rearVisibility)
        {
            float az = math.radians(azimuthDeg);
            float lateral = math.sin(az);            // +1 = right side
            float frontness = math.cos(az);          // +1 = front, -1 = behind
            float rear = math.lerp(1f, rearVisibility, math.saturate(-frontness));
            float wr = math.saturate(0.6f + 0.4f * lateral) * rear;
            float wl = math.saturate(0.6f - 0.4f * lateral) * rear;
            return new float2(wl, wr);
        }
    }
}
