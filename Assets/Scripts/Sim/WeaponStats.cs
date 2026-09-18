using System;
using System.Collections.Generic;
using System.Linq;
using FlyWireSwat.Fly;
using FlyWireSwat.Weapons;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    [Serializable]
    public class WeaponStats
    {
        public string weaponName;
        public int trials;
        public float killRate;               // 0..1
        public float meanTimeToKillSeconds;  // over kills only
        public float medianTimeToKillSeconds;
        public float meanTimeAllSeconds;     // failures count as the full budget
        public float meanAttempts;
        public float meanCost;
        public float meanCollateral;
        public float firstAttemptKillRate;
        public float escapeShortRate;        // GF-mediated escape on first attempt
        public float escapeLongRate;
        public float noEscapeRate;
        public float meanGfLatencyMs;        // impact - GF spike (only when GF fired)
        public float takeoffBeforeImpactRate;
        public float score;                  // the "optimisation" score, higher is better

        [NonSerialized] public WeaponDefinition weapon;
        [NonSerialized] public List<TrialRecord> records;

        /// <summary>
        /// Score = kill-rate^2 x 100 / ((1 + T/60s)^0.7 x (1 + cost)^0.5 x (1 + collateral/50)^0.5)
        /// Rewards certain, quick, cheap, clean kills. All four terms matter; none dominates.
        /// </summary>
        public static float ComputeScore(float killRate, float meanTimeAll, float cost, float collateral)
        {
            float t = Mathf.Pow(1f + meanTimeAll / 60f, 0.7f);
            float c = Mathf.Sqrt(1f + Mathf.Max(0f, cost));
            float d = Mathf.Sqrt(1f + Mathf.Max(0f, collateral) / 50f);
            return killRate * killRate * 100f / (t * c * d);
        }

        public static WeaponStats From(WeaponDefinition w, List<TrialRecord> recs)
        {
            var s = new WeaponStats { weaponName = w.nameEn, weapon = w, records = recs, trials = recs.Count };
            if (recs.Count == 0) return s;
            var kills = recs.Where(r => r.killed).ToList();
            s.killRate = (float)kills.Count / recs.Count;
            s.meanTimeToKillSeconds = kills.Count > 0 ? kills.Average(r => r.timeSeconds) : float.NaN;
            if (kills.Count > 0)
            {
                var sorted = kills.Select(r => r.timeSeconds).OrderBy(x => x).ToList();
                s.medianTimeToKillSeconds = sorted[sorted.Count / 2];
            }
            else s.medianTimeToKillSeconds = float.NaN;
            s.meanTimeAllSeconds = recs.Average(r => r.timeSeconds);
            s.meanAttempts = (float)recs.Average(r => r.attempts);
            s.meanCost = recs.Average(r => r.cost);
            s.meanCollateral = recs.Average(r => r.collateral);
            s.firstAttemptKillRate = (float)recs.Count(r => r.killed && r.attempts == 1) / recs.Count;
            s.escapeShortRate = (float)recs.Count(r => r.firstEscape == EscapeMode.ShortGiantFiber) / recs.Count;
            s.escapeLongRate = (float)recs.Count(r => r.firstEscape == EscapeMode.LongCoordinated) / recs.Count;
            s.noEscapeRate = 1f - s.escapeShortRate - s.escapeLongRate;
            var gf = recs.Where(r => r.firstGfLatencyMs >= 0f).ToList();
            s.meanGfLatencyMs = gf.Count > 0 ? gf.Average(r => r.firstGfLatencyMs) : float.NaN;
            s.takeoffBeforeImpactRate = (float)recs.Count(r => r.firstTakeoffBeforeImpact) / recs.Count;
            s.score = ComputeScore(s.killRate, s.meanTimeAllSeconds, s.meanCost, s.meanCollateral);
            return s;
        }
    }
}
