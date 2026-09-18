using System;
using System.Collections.Generic;
using System.Linq;
using FlyWireSwat.Weapons;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    [Serializable]
    public class RepellentStats
    {
        public string itemId;
        public string itemName;
        public int landings;
        public float baselineZoneLandings;   // landings within 50 cm of the spot with nothing there
        public float zoneLandings;           // with the item
        public float protection;             // 1 - zone/baseline
        public float killsPerHour;
        public float costFirstHour;
        public Verdict verdict;
        public float score;
        [NonSerialized] public WeaponDefinition item;
    }

    /// <summary>
    /// Monte-Carlo landing test: a fly makes many landing choices on the table over one simulated hour.
    /// A repellent reduces landings inside its radius; an attractant pulls landings toward it and may kill.
    /// </summary>
    public static class RepellentEngine
    {
        public const float TableW = 2.1f, TableD = 1.25f, ZoneRadius = 0.5f, LandingsPerHour = 40f;

        public static RepellentStats Run(WeaponDefinition item, int hours, uint seed)
        {
            var rng = new System.Random((int)seed);
            int landings = Mathf.RoundToInt(LandingsPerHour * hours);
            float zoneBase = 0f, zoneWith = 0f; int kills = 0;
            for (int i = 0; i < landings; i++)
            {
                // baseline: uniform landing on the table (item sits at the centre)
                float x = ((float)rng.NextDouble() - 0.5f) * TableW, z = ((float)rng.NextDouble() - 0.5f) * TableD;
                float r = Mathf.Sqrt(x * x + z * z);
                if (r < ZoneRadius) zoneBase++;

                // with the item
                float xi = x, zi = z, ri = r;
                if (item.attractStrength > 0f && rng.NextDouble() < item.attractStrength * Mathf.Clamp01(item.attractRadius / 1.0f))
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = (float)rng.NextDouble() * item.attractRadius * 0.5f;
                    xi = Mathf.Cos(a) * d; zi = Mathf.Sin(a) * d; ri = d;
                }
                if (item.repelStrength > 0f && ri < item.repelRadius && rng.NextDouble() < item.repelStrength)
                {
                    // goes somewhere outside the repel radius (or off the table)
                    ri = item.repelRadius + (float)rng.NextDouble() * 0.6f;
                }
                if (item.killsOnContact && ri < item.contactRadius + 0.02f) kills++;
                if (ri < ZoneRadius) zoneWith++;
            }
            var s = new RepellentStats
            {
                itemId = item.id, itemName = item.nameEn, item = item, landings = landings,
                baselineZoneLandings = zoneBase / hours, zoneLandings = zoneWith / hours,
                protection = zoneBase > 0 ? 1f - zoneWith / zoneBase : 0f,
                killsPerHour = (float)kills / hours,
                costFirstHour = item.purchaseCost + item.runningCostPerHour,
                verdict = item.expectedVerdict,
            };
            s.score = (s.protection * 100f + s.killsPerHour * 8f) / Mathf.Sqrt(1f + s.costFirstHour / 5f);
            return s;
        }

        public static List<RepellentStats> RunAll(List<WeaponDefinition> items, int hours, uint seed)
            => items.Select((it, i) => Run(it, hours, seed + (uint)i * 131u)).OrderByDescending(s => s.score).ToList();
    }
}
