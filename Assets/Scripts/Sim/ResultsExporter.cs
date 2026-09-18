using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    public static class ResultsExporter
    {
        public static string ResultsDirectory
        {
            get
            {
                string dir = Path.Combine(Application.dataPath, "..", "Results");
                Directory.CreateDirectory(dir);
                return Path.GetFullPath(dir);
            }
        }

        public static string ExportRepellents(List<RepellentStats> stats)
        {
            var ci = CultureInfo.InvariantCulture;
            string path = Path.Combine(ResultsDirectory, "repellents_latest.csv");
            var sb = new StringBuilder();
            sb.AppendLine("rank,item,score,protection,kills_per_hour,cost_first_hour_usd,verdict,zone_landings_per_hour,baseline_zone_landings_per_hour");
            int rank = 1;
            foreach (var s in stats)
                sb.AppendLine(string.Join(",", rank++, "\"" + s.itemName + "\"", s.score.ToString("0.###", ci), s.protection.ToString("0.###", ci), s.killsPerHour.ToString("0.##", ci), s.costFirstHour.ToString("0.##", ci), s.verdict, s.zoneLandings.ToString("0.#", ci), s.baselineZoneLandings.ToString("0.#", ci)));
            File.WriteAllText(path, sb.ToString());
            File.WriteAllText(Path.Combine(ResultsDirectory, "repellents_latest.json"), JsonConvert.SerializeObject(stats, Formatting.Indented));
            return path;
        }

        public static string Export(List<WeaponStats> stats, int trialsPerWeapon, string circuitSummary)
        {
            var ci = CultureInfo.InvariantCulture;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string csvPath = Path.Combine(ResultsDirectory, $"leaderboard_{stamp}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("rank,weapon,score,kill_rate,first_attempt_kill_rate,mean_ttk_s,median_ttk_s,mean_time_all_s,mean_attempts,mean_cost_usd,mean_collateral_usd,escape_short_gf,escape_long,no_escape,mean_gf_latency_ms,takeoff_before_impact");
            int rank = 1;
            foreach (var s in stats)
            {
                sb.AppendLine(string.Join(",",
                    rank++, Quote(s.weaponName), F(s.score), F(s.killRate), F(s.firstAttemptKillRate), F(s.meanTimeToKillSeconds), F(s.medianTimeToKillSeconds),
                    F(s.meanTimeAllSeconds), F(s.meanAttempts), F(s.meanCost), F(s.meanCollateral), F(s.escapeShortRate), F(s.escapeLongRate), F(s.noEscapeRate),
                    F(s.meanGfLatencyMs), F(s.takeoffBeforeImpactRate)));
            }
            File.WriteAllText(csvPath, sb.ToString());

            string jsonPath = Path.Combine(ResultsDirectory, $"leaderboard_{stamp}.json");
            var payload = new Dictionary<string, object>
            {
                ["generated"] = DateTime.Now.ToString("o"),
                ["trialsPerWeapon"] = trialsPerWeapon,
                ["circuit"] = circuitSummary,
                ["scoreFormula"] = "killRate^2*100 / ((1+T/60)^0.7 * sqrt(1+cost) * sqrt(1+collateral/50))",
                ["leaderboard"] = stats,
            };
            string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            File.WriteAllText(jsonPath, json);
            File.WriteAllText(Path.Combine(ResultsDirectory, "leaderboard_latest.json"), json);
            return csvPath;

            string F(float x) => float.IsNaN(x) ? "" : x.ToString("0.####", ci);
            string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
