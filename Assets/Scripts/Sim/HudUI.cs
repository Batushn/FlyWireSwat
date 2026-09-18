using System.Collections.Generic;
using FlyWireSwat.Connectome;
using FlyWireSwat.Fly;
using FlyWireSwat.Weapons;
using Unity.Mathematics;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>IMGUI overlay: menu, benchmark progress, replay telemetry, spike strips, leaderboards, brain picture-in-picture.</summary>
    public class HudUI : MonoBehaviour
    {
        public SimulationDirector director;

        /// <summary>Player builds have no emoji glyphs in the IMGUI font; keep them only in the editor.</summary>
        public static string Emo(string emoji)
        {
#if UNITY_EDITOR
            return emoji;
#else
            return "";
#endif
        }

        Texture2D _raster;
        int _rasterBins;
        float _rasterBinMs = 1f;
        static readonly byte[] RasterKinds = { NeuronKind.LC4, NeuronKind.LPLC2, NeuronKind.LPLC1, NeuronKind.LC6, NeuronKind.LC16, NeuronKind.Other, NeuronKind.DNp02, NeuronKind.GF };

        public void OnShowcaseLoaded()
        {
            var d = director;
            var spikes = d.ShowcaseSpikes ?? new List<int2>();
            float durMs = math.max(1f, d.ShowcaseOutput.steps * d.lif.dtMs);
            _rasterBinMs = math.max(0.5f, durMs / 600f);
            _rasterBins = Mathf.CeilToInt(durMs / _rasterBinMs) + 1;
            int rows = RasterKinds.Length;
            if (_raster == null || _raster.width != _rasterBins) _raster = new Texture2D(_rasterBins, rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var counts = new int[rows, _rasterBins];
            var maxPerRow = new int[rows];
            var net = d.Net;
            foreach (var sp in spikes)
            {
                int row = RowOf(net.Kind[sp.y]);
                int bin = Mathf.Clamp(Mathf.FloorToInt(sp.x * d.lif.dtMs / _rasterBinMs), 0, _rasterBins - 1);
                counts[row, bin]++;
            }
            for (int r = 0; r < rows; r++) for (int b = 0; b < _rasterBins; b++) maxPerRow[r] = Mathf.Max(maxPerRow[r], counts[r, b]);
            var px = new Color32[_rasterBins * rows];
            for (int r = 0; r < rows; r++)
            {
                var c = BrainView.ColorOf(RasterKinds[r]);
                for (int b = 0; b < _rasterBins; b++)
                {
                    float v = maxPerRow[r] > 0 ? Mathf.Pow((float)counts[r, b] / maxPerRow[r], 0.6f) : 0f;
                    px[r * _rasterBins + b] = Color.Lerp(new Color(0.07f, 0.07f, 0.1f), c, v);
                }
            }
            _raster.SetPixels32(px); _raster.Apply();
        }

        static int RowOf(byte k)
        {
            for (int i = 0; i < RasterKinds.Length; i++) if (RasterKinds[i] == k) return i;
            if (k >= NeuronKind.DNp02) return 6;
            if (NeuronKind.IsVisualProjection(k)) return 4;
            return 5;
        }

        void OnGUI()
        {
            UiTheme.Ensure();
            var d = director;
            if (d == null) return;
            float W = Screen.width, H = Screen.height, s = UiTheme.S;
            bool portrait = UiTheme.Portrait;

            switch (d.CurrentPhase)
            {
                case SimulationDirector.Phase.Fps: return;
                case SimulationDirector.Phase.Menu: DrawMenu(W, H, s); return;
                case SimulationDirector.Phase.Loading:
                case SimulationDirector.Phase.Benchmark: DrawProgress(W, H, s); return;
            }

            var w = d.CurrentItem;
            var res = d.ShowcaseResult;
            float t = d.Showcase.ReplayMs;
            float m = 10f * s;

            // ---------- weapon card (top-left)
            float cardW = portrait ? W - 2 * m : 430f * s;
            var card = new Rect(m, m, cardW, (w.IsKiller ? 215f : 150f) * s);
            UiTheme.Panel_(card);
            var title = UiTheme.Sized(UiTheme.Title, s); var body = UiTheme.Sized(UiTheme.Body, s); var mono = UiTheme.Sized(UiTheme.Mono, s); var small = UiTheme.Sized(UiTheme.Small, s);
            GUI.Label(new Rect(card.x + 14 * s, card.y + 8 * s, card.width - 28 * s, 34 * s), $"{Emo(w.emoji + "  ")}{w.displayName}   <size={Mathf.RoundToInt(14 * s)}><color=#aaa>[{d.ShowcaseIndex + 1}/{d.AllItems.Count}]</color></size>", title);
            GUI.Label(new Rect(card.x + 14 * s, card.y + 44 * s, card.width - 28 * s, 40 * s), w.description, body);

            if (w.IsKiller)
            {
                string mode = res.decision.mode switch
                {
                    EscapeMode.ShortGiantFiber => UiTheme.Color_(L10n.T("mode.short"), UiTheme.Bad),
                    EscapeMode.LongCoordinated => UiTheme.Color_(L10n.T("mode.long"), UiTheme.Accent),
                    _ => UiTheme.Color_(L10n.T("mode.none"), UiTheme.Muted),
                };
                var st = res.stimulus;
                float lv = st.strikeSpeed > 0 ? st.objectRadius / st.strikeSpeed * 1000f : 0f;
                string gf = res.decision.gfSpikeMs >= 0f ? string.Format(L10n.T("hud.gf.at"), res.decision.gfSpikeMs, res.impactMs - res.decision.gfSpikeMs) : L10n.T("hud.gf.none");
                string tk = res.decision.takeoffMs >= 0f ? $"{res.decision.takeoffMs:0.0} ms" : "-";
                string outcome = res.killed ? UiTheme.Color_("<b>" + L10n.T("res.dead") + "</b>", UiTheme.Bad) : (res.inLethalZone ? UiTheme.Color_(L10n.T("res.hit"), UiTheme.Accent) : UiTheme.Color_("<b>" + L10n.T("res.escaped") + "</b>", UiTheme.Good));
                if (w.stimulusKind == StimulusKind.Passive) outcome = UiTheme.Color_(L10n.T("res.waiting"), UiTheme.Muted);
                GUI.Label(new Rect(card.x + 14 * s, card.y + 86 * s, card.width - 28 * s, 130 * s),
                    string.Format(L10n.T("hud.time"), t, d.replayTimeScale) + "\n" +
                    string.Format(L10n.T("hud.approach"), st.azimuthDeg, lv, res.impactMs) + "\n" +
                    string.Format(L10n.T("hud.gf"), gf) + "\n" +
                    string.Format(L10n.T("hud.takeoff"), tk, mode) + "\n" +
                    string.Format(L10n.T("hud.dist"), res.distanceToImpact * 100f, w.lethalRadius * 100f) + "\n" +
                    string.Format(L10n.T("hud.result"), outcome, res.killProbability), mono);
            }
            else
            {
                var rs = d.RepellentBoard.Find(x => x.item == w);
                string verdict = rs != null ? VerdictText(rs.verdict) : "";
                GUI.Label(new Rect(card.x + 14 * s, card.y + 86 * s, card.width - 28 * s, 64 * s),
                    (rs != null ? $"{L10n.T("rb.h.prot")}: <b>{rs.protection:P0}</b>   {L10n.T("rb.h.kills")}: {rs.killsPerHour:0.0}   {L10n.T("rb.h.verdict")}: {verdict}\n" : "") + $"<size={Mathf.RoundToInt(12 * s)}><color=#bbb>{w.evidence}</color></size>", body);
            }

            // ---------- centre flashes
            var big = UiTheme.Sized(UiTheme.Big, s);
            if (w.IsKiller && res.decision.gfSpikeMs >= 0f && t >= res.decision.gfSpikeMs && t < res.decision.gfSpikeMs + 60f)
            {
                GUI.color = new Color(1f, 0.3f, 0.2f, 1f - (t - res.decision.gfSpikeMs) / 60f);
                GUI.Label(new Rect(0, H * (portrait ? 0.3f : 0.14f), W, 60 * s), Emo("⚡ ") + L10n.T("gf.flash") + Emo(" ⚡"), big);
                GUI.color = Color.white;
            }
            if (w.IsKiller && t >= res.impactMs && t < res.impactMs + 400f && w.stimulusKind != StimulusKind.Passive)
            {
                GUI.color = res.killed ? UiTheme.Bad : UiTheme.Good;
                GUI.Label(new Rect(0, H * (portrait ? 0.36f : 0.22f), W, 60 * s), res.killed ? L10n.T("splat") : L10n.T("miss"), big);
                GUI.color = Color.white;
            }
            if (!w.IsKiller && d.RepellentVerdictFlash > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(d.RepellentVerdictFlash));
                GUI.Label(new Rect(0, H * (portrait ? 0.34f : 0.2f), W, 60 * s), d.RepellentVerdictText, big);
                GUI.color = Color.white;
            }

            // ---------- recording badge
            if (d.Recorder != null && d.Recorder.IsRecording)
            {
                var r = new Rect(W * 0.5f - 260 * s, m, 520 * s, 30 * s);
                UiTheme.Panel_(r, 0.85f);
                GUI.Label(new Rect(r.x + 10 * s, r.y + 4 * s, r.width, 24 * s), UiTheme.Color_("● ", UiTheme.Bad) + string.Format(L10n.T("rec"), d.Recorder.Status), small);
            }

            // ---------- tables (right column in landscape, below the card in portrait)
            float tableW = portrait ? W - 2 * m : Mathf.Min(680f * s, W * 0.42f);
            float tableX = portrait ? m : W - tableW - m;
            float tableY = portrait ? card.yMax + m : m;
            if (d.ShowLeaderboard)
            {
                float y = DrawLeaderboard(new Rect(tableX, tableY, tableW, 0), s, w, portrait);
                if (!portrait || H > 1500) DrawRepellentBoard(new Rect(tableX, y + m, tableW, 0), s, w);
            }

            // ---------- bottom: spike strips + brain PiP
            float rh = 15f * s;
            float stripsH = rh * RasterKinds.Length + 40 * s;
            float pipW = portrait ? W - 2 * m : Mathf.Min(520f * s, W * 0.32f), pipH = pipW * 0.6f;
            if (d.ShowBrain && d.BrainTexture != null)
            {
                float px = portrait ? m : W - pipW - m;
                float py = H - pipH - m - (portrait ? stripsH + m : 0f);
                var pr = new Rect(px - 6 * s, py - 24 * s, pipW + 12 * s, pipH + 30 * s);
                UiTheme.Panel_(pr, 0.8f);
                GUI.Label(new Rect(px, py - 22 * s, pipW, 20 * s), L10n.T("brain.pip"), small);
                GUI.DrawTexture(new Rect(px, py, pipW, pipH), d.BrainTexture, ScaleMode.StretchToFill);
            }
            if (d.ShowBrain && _raster != null && w.IsKiller && w.stimulusKind != StimulusKind.Passive)
            {
                float labelW = 120f * s, stripW = portrait ? W - 2 * m - labelW - 16 * s : Mathf.Min(W * 0.48f, W - pipW - labelW - 4 * m);
                float y0 = H - m - rh * RasterKinds.Length;
                UiTheme.Panel_(new Rect(m, y0 - 26 * s, labelW + stripW + 16 * s, rh * RasterKinds.Length + 34 * s));
                GUI.Label(new Rect(m + 8 * s, y0 - 24 * s, 600 * s, 20 * s), L10n.T("raster"), small);
                for (int r = 0; r < RasterKinds.Length; r++)
                    GUI.Label(new Rect(m + 8 * s, y0 + (RasterKinds.Length - 1 - r) * rh - 2, labelW, rh + 4), BrainView.KindLabel(RasterKinds[r]), small);
                var stripRect = new Rect(m + 8 * s + labelW, y0, stripW, rh * RasterKinds.Length);
                GUI.DrawTexture(stripRect, _raster, ScaleMode.StretchToFill);
                float simMs = t - res.simStartMs;
                float frac = Mathf.Clamp01(simMs / (_rasterBins * _rasterBinMs));
                GUI.color = Color.white; GUI.DrawTexture(new Rect(stripRect.x + stripW * frac, y0, 2, stripRect.height), Texture2D.whiteTexture);
                if (res.decision.gfSpikeMs >= 0f)
                {
                    float gfFrac = Mathf.Clamp01((res.decision.gfSpikeMs - res.simStartMs) / (_rasterBins * _rasterBinMs));
                    GUI.color = UiTheme.Bad; GUI.DrawTexture(new Rect(stripRect.x + stripW * gfFrac, y0 - 6, 2, stripRect.height + 6), Texture2D.whiteTexture);
                }
                float impFrac = Mathf.Clamp01((res.impactMs - res.simStartMs) / (_rasterBins * _rasterBinMs));
                GUI.color = UiTheme.Accent; GUI.DrawTexture(new Rect(stripRect.x + stripW * impFrac, y0 - 6, 2, stripRect.height + 6), Texture2D.whiteTexture);
                GUI.color = Color.white;
                // help line above the strips (landscape only; portrait has no room)
                if (!portrait)
                {
                    UiTheme.Panel_(new Rect(m, y0 - 26 * s - 32 * s, labelW + stripW + 16 * s, 28 * s), 0.6f);
                    GUI.Label(new Rect(m + 8 * s, y0 - 26 * s - 30 * s, labelW + stripW, 24 * s), L10n.T("help.showcase"), small);
                }
            }
        }

        string VerdictText(Verdict v) => v switch
        {
            Verdict.Myth => UiTheme.Color_(L10n.T("verdict.myth"), UiTheme.Bad),
            Verdict.Weak => UiTheme.Color_(L10n.T("verdict.weak"), UiTheme.Accent),
            Verdict.Works => UiTheme.Color_(L10n.T("verdict.works"), UiTheme.Good),
            _ => UiTheme.Color_(L10n.T("verdict.kills"), UiTheme.Good),
        };

        float DrawLeaderboard(Rect area, float s, WeaponDefinition current, bool portrait)
        {
            var d = director;
            if (d.Leaderboard.Count == 0) return area.y;
            float rowH = 24f * s, headH = 62f * s;
            float h = headH + rowH * (d.Leaderboard.Count + 1) + 12 * s;
            var r = new Rect(area.x, area.y, area.width, h);
            UiTheme.Panel_(r);
            var h2 = UiTheme.Sized(UiTheme.H2, s); var small = UiTheme.Sized(UiTheme.Small, s);
            GUI.Label(new Rect(r.x + 14 * s, r.y + 8 * s, r.width, 26 * s), Emo("🏆 ") + L10n.T("lb.title"), h2);
            GUI.Label(new Rect(r.x + 14 * s, r.y + 34 * s, r.width, 20 * s), string.Format(L10n.T("lb.sub"), d.trialsPerWeapon), small);
            // columns: rank, name, score(bar), kill%, first%, time, cost, damage, gf
            float x0 = r.x + 10 * s;
            float[] cw = { 26, 150, 110, 52, 52, 60, 60, 66, 60 };
            float total = 0; foreach (var c in cw) total += c;
            float k = (r.width - 20 * s) / (total * s);
            for (int i = 0; i < cw.Length; i++) cw[i] *= s * k;
            string[] heads = { "#", L10n.T("lb.h.weapon"), L10n.T("lb.h.score"), L10n.T("lb.h.kill"), L10n.T("lb.h.first"), L10n.T("lb.h.time"), L10n.T("lb.h.cost"), L10n.T("lb.h.dmg"), L10n.T("lb.h.gf") };
            float y = r.y + headH;
            var cell = UiTheme.Sized(UiTheme.Cell, s); var cellB = UiTheme.Sized(UiTheme.CellBold, s); var cellR = UiTheme.Sized(UiTheme.CellRight, s);
            var headStyle = new GUIStyle(cell) { normal = { textColor = UiTheme.Muted } }; var headR = new GUIStyle(cellR) { normal = { textColor = UiTheme.Muted } };
            float x = x0;
            for (int i = 0; i < heads.Length; i++) { GUI.Label(new Rect(x, y, cw[i] - 4, rowH), heads[i], i >= 3 ? headR : headStyle); x += cw[i]; }
            GUI.color = UiTheme.PanelLine; GUI.DrawTexture(new Rect(r.x + 10 * s, y + rowH - 1, r.width - 20 * s, 1), Texture2D.whiteTexture); GUI.color = Color.white;
            y += rowH;
            int rank = 1;
            float maxScore = Mathf.Max(1e-3f, d.Leaderboard[0].score);
            foreach (var st in d.Leaderboard)
            {
                if (st.weapon == null) continue;
                bool cur = st.weapon == current;
                if (rank % 2 == 0 || cur) { GUI.color = cur ? new Color(1f, 0.8f, 0.3f, 0.14f) : new Color(1f, 1f, 1f, 0.035f); GUI.DrawTexture(new Rect(r.x + 6 * s, y, r.width - 12 * s, rowH), Texture2D.whiteTexture); GUI.color = Color.white; }
                x = x0;
                var nameStyle = cur ? new GUIStyle(cellB) { normal = { textColor = UiTheme.Accent } } : cellB;
                string medal = rank == 1 ? UiTheme.Color_("1", UiTheme.Accent) : rank.ToString();
                GUI.Label(new Rect(x, y, cw[0], rowH), medal, cell); x += cw[0];
                GUI.Label(new Rect(x, y, cw[1] - 4, rowH), Emo(st.weapon.emoji + " ") + st.weapon.displayName, nameStyle); x += cw[1];
                UiTheme.Bar(new Rect(x, y + rowH * 0.32f, cw[2] - 46 * s, rowH * 0.36f), st.score / maxScore, cur ? UiTheme.Accent : new Color(0.4f, 0.7f, 1f), new Color(1f, 1f, 1f, 0.08f));
                GUI.Label(new Rect(x, y, cw[2] - 4, rowH), $"{st.score:0.0}", cellR); x += cw[2];
                GUI.Label(new Rect(x, y, cw[3] - 4, rowH), UiTheme.Color_($"{st.killRate * 100f:0}%", Color.Lerp(UiTheme.Bad, UiTheme.Good, st.killRate)), cellR); x += cw[3];
                GUI.Label(new Rect(x, y, cw[4] - 4, rowH), $"{st.firstAttemptKillRate * 100f:0}%", cellR); x += cw[4];
                string ttk = float.IsNaN(st.meanTimeToKillSeconds) ? "-" : st.meanTimeToKillSeconds >= 100 ? $"{st.meanTimeToKillSeconds:0}s" : $"{st.meanTimeToKillSeconds:0.0}s";
                GUI.Label(new Rect(x, y, cw[5] - 4, rowH), ttk, cellR); x += cw[5];
                GUI.Label(new Rect(x, y, cw[6] - 4, rowH), st.meanCost >= 100 ? $"{st.meanCost:0}" : $"{st.meanCost:0.00}", cellR); x += cw[6];
                GUI.Label(new Rect(x, y, cw[7] - 4, rowH), st.meanCollateral >= 1000 ? $"{st.meanCollateral / 1000f:0}k" : $"{st.meanCollateral:0}", cellR); x += cw[7];
                GUI.Label(new Rect(x, y, cw[8] - 4, rowH), $"{st.escapeShortRate * 100f:0}%", cellR);
                y += rowH; rank++;
            }
            return r.yMax;
        }

        float DrawRepellentBoard(Rect area, float s, WeaponDefinition current)
        {
            var d = director;
            if (d.RepellentBoard.Count == 0) return area.y;
            float rowH = 24f * s, headH = 62f * s;
            float h = headH + rowH * (d.RepellentBoard.Count + 1) + 12 * s;
            var r = new Rect(area.x, area.y, area.width, h);
            UiTheme.Panel_(r);
            var h2 = UiTheme.Sized(UiTheme.H2, s); var small = UiTheme.Sized(UiTheme.Small, s);
            GUI.Label(new Rect(r.x + 14 * s, r.y + 8 * s, r.width, 26 * s), Emo("🧿 ") + L10n.T("rb.title"), h2);
            GUI.Label(new Rect(r.x + 14 * s, r.y + 34 * s, r.width, 20 * s), string.Format(L10n.T("rb.sub"), d.RepellentBoard[0].landings, d.repellentHours), small);
            float x0 = r.x + 10 * s;
            float[] cw = { 26, 190, 130, 70, 70, 130 };
            float total = 0; foreach (var c in cw) total += c;
            float k = (r.width - 20 * s) / (total * s);
            for (int i = 0; i < cw.Length; i++) cw[i] *= s * k;
            string[] heads = { "#", L10n.T("rb.h.item"), L10n.T("rb.h.prot"), L10n.T("rb.h.kills"), L10n.T("rb.h.cost"), L10n.T("rb.h.verdict") };
            float y = r.y + headH;
            var cell = UiTheme.Sized(UiTheme.Cell, s); var cellB = UiTheme.Sized(UiTheme.CellBold, s); var cellR = UiTheme.Sized(UiTheme.CellRight, s);
            var headStyle = new GUIStyle(cell) { normal = { textColor = UiTheme.Muted } }; var headR = new GUIStyle(cellR) { normal = { textColor = UiTheme.Muted } };
            float x = x0;
            for (int i = 0; i < heads.Length; i++) { GUI.Label(new Rect(x, y, cw[i] - 4, rowH), heads[i], i == 3 || i == 4 ? headR : headStyle); x += cw[i]; }
            GUI.color = UiTheme.PanelLine; GUI.DrawTexture(new Rect(r.x + 10 * s, y + rowH - 1, r.width - 20 * s, 1), Texture2D.whiteTexture); GUI.color = Color.white;
            y += rowH;
            int rank = 1;
            foreach (var st in d.RepellentBoard)
            {
                if (st.item == null) continue;
                bool cur = st.item == current;
                if (rank % 2 == 0 || cur) { GUI.color = cur ? new Color(1f, 0.8f, 0.3f, 0.14f) : new Color(1f, 1f, 1f, 0.035f); GUI.DrawTexture(new Rect(r.x + 6 * s, y, r.width - 12 * s, rowH), Texture2D.whiteTexture); GUI.color = Color.white; }
                x = x0;
                GUI.Label(new Rect(x, y, cw[0], rowH), rank.ToString(), cell); x += cw[0];
                GUI.Label(new Rect(x, y, cw[1] - 4, rowH), Emo(st.item.emoji + " ") + st.item.displayName, cur ? new GUIStyle(cellB) { normal = { textColor = UiTheme.Accent } } : cellB); x += cw[1];
                if (st.protection >= 0f)
                {
                    UiTheme.Bar(new Rect(x, y + rowH * 0.32f, cw[2] - 50 * s, rowH * 0.36f), st.protection, UiTheme.Good, new Color(1f, 1f, 1f, 0.08f));
                    GUI.Label(new Rect(x, y, cw[2] - 4, rowH), $"{st.protection * 100f:0}%", cellR);
                }
                else GUI.Label(new Rect(x, y, cw[2] - 4, rowH), UiTheme.Color_(string.Format(L10n.T("rb.lures"), -st.protection * 100f), new Color(0.8f, 0.6f, 1f)), cellR);
                x += cw[2];
                GUI.Label(new Rect(x, y, cw[3] - 4, rowH), st.killsPerHour > 0 ? $"{st.killsPerHour:0.0}" : "-", cellR); x += cw[3];
                GUI.Label(new Rect(x, y, cw[4] - 4, rowH), $"{st.costFirstHour:0.00}", cellR); x += cw[4];
                GUI.Label(new Rect(x, y, cw[5] - 4, rowH), VerdictText(st.verdict), cellB);
                y += rowH; rank++;
            }
            return r.yMax;
        }

        void DrawProgress(float W, float H, float s)
        {
            var d = director;
            var r = new Rect(W * 0.15f, H * 0.42f, W * 0.7f, 120f * s);
            UiTheme.Panel_(r);
            GUI.Label(new Rect(r.x + 16 * s, r.y + 10 * s, r.width - 32 * s, 30 * s), L10n.T("bench.title"), UiTheme.Sized(UiTheme.H2, s));
            GUI.Label(new Rect(r.x + 16 * s, r.y + 44 * s, r.width - 32 * s, 30 * s), d.StatusLine, UiTheme.Sized(UiTheme.Body, s));
            UiTheme.Bar(new Rect(r.x + 16 * s, r.y + 86 * s, r.width - 32 * s, 14 * s), d.BenchmarkProgress, UiTheme.Good, new Color(1f, 1f, 1f, 0.1f));
        }

        void DrawMenu(float W, float H, float s)
        {
            float bw = Mathf.Min(520f * s, W - 40f), bh = 52f * s, x = W * 0.5f - bw * 0.5f, y = H * 0.38f;
            UiTheme.Panel_(new Rect(x - 30 * s, y - 150 * s, bw + 60 * s, 5 * bh + 200 * s));
            GUI.Label(new Rect(x, y - 140 * s, bw, 44 * s), Emo("🪰 ") + L10n.T("title"), UiTheme.Sized(UiTheme.Title, 1.4f * s));
            GUI.Label(new Rect(x, y - 92 * s, bw, 70 * s), L10n.T("tagline"), UiTheme.Sized(UiTheme.Body, s));
            var bs = UiTheme.Sized(UiTheme.Button, s);
            if (GUI.Button(new Rect(x, y, bw, bh), L10n.T("menu.showcase"), bs)) director.Begin(SimulationDirector.StartMode.Showcase);
            if (GUI.Button(new Rect(x, y + bh + 10 * s, bw, bh), L10n.T("menu.fps"), bs)) director.Begin(SimulationDirector.StartMode.Fps);
            if (GUI.Button(new Rect(x, y + 2 * (bh + 10 * s), bw, bh), L10n.T("menu.record"), bs)) director.Begin(SimulationDirector.StartMode.RecordVideos);
            if (GUI.Button(new Rect(x, y + 3 * (bh + 10 * s), bw, bh), L10n.T("menu.lang"), bs)) L10n.Toggle();
            GUI.Label(new Rect(x, y + 4 * (bh + 10 * s) + 4 * s, bw, 40 * s), director.StatusLine, UiTheme.Sized(UiTheme.Small, s));
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) director.Begin(SimulationDirector.StartMode.Showcase);
                if (kb.digit2Key.wasPressedThisFrame) director.Begin(SimulationDirector.StartMode.Fps);
                if (kb.digit3Key.wasPressedThisFrame) director.Begin(SimulationDirector.StartMode.RecordVideos);
                if (kb.tKey.wasPressedThisFrame) L10n.Toggle();
            }
#endif
        }
    }
}
