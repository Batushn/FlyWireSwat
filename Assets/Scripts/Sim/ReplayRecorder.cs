using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace FlyWireSwat.Sim
{
    /// <summary>
    /// Records every weapon's slow-motion replay as a PNG sequence and calls ffmpeg to make MP4s
    /// (one per weapon + one combined reel). Works in the Editor and in a player build.
    /// </summary>
    public class ReplayRecorder : MonoBehaviour
    {
        public SimulationDirector director;
        public int fps = 60;
        public float secondsPerWeapon = 12f;
        public string ffmpegPath = "ffmpeg";
        public bool quitWhenDone;
        Texture2D _readback;
        public bool IsRecording { get; private set; }
        public string Status { get; private set; } = "";

        public static string VideoDirectory
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Videos"));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public void StartRecording() { if (!IsRecording) StartCoroutine(RecordAll()); }

        IEnumerator RecordAll()
        {
            IsRecording = true;
            var made = new List<string>();
            var rt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            string outDir = Path.Combine(VideoDirectory, Screen.height > Screen.width ? "portrait" : "landscape");
            Directory.CreateDirectory(outDir);
            string framesRoot = Path.Combine(outDir, "frames");
            var items = director.AllItems;
            for (int wi = 0; wi < items.Count; wi++)
            {
                var w = items[wi];
                string safe = Sanitize(w.id);
                string frameDir = Path.Combine(framesRoot, $"{wi:00}_{safe}");
                string existing = Path.Combine(outDir, $"{wi:00}_{safe}.mp4");
                if (File.Exists(existing)) { made.Add(existing); continue; }   // resume: keep clips already rendered
                if (Directory.Exists(frameDir)) Directory.Delete(frameDir, true);
                Directory.CreateDirectory(frameDir);

                director.PlayShowcase(wi, true);
                director.Showcase.Playing = false;
                float replayStep = 1000f / fps * director.replayTimeScale;   // ms of sim time per video frame
                int frames = Mathf.CeilToInt(secondsPerWeapon * fps);
                int width = rt.width, height = rt.height;
                for (int f = 0; f < frames; f++)
                {
                    Status = string.Format(L10n.T("rec.status"), w.displayName, wi + 1, items.Count, f + 1, frames);
                    director.Showcase.Step(replayStep);
                    director.SyncBrainToReplay();
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
                    // readback + JPEG (q92) on the main thread: ~15 ms per 1080p frame, no threading surprises
                    if (_readback == null || _readback.width != width || _readback.height != height) _readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    _readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(frameDir, f.ToString("D5") + ".jpg"), _readback.EncodeToJPG(92));
                    if (w.IsKiller && director.Showcase.Finished && f > frames / 2) break;
                    if (!w.IsKiller && director.Showcase.RepellentDecided && f > frames * 0.55f) break;
                }
                string mp4 = Path.Combine(outDir, $"{wi:00}_{safe}.mp4");
                Status = $"ffmpeg: {safe}";
                yield return RunFfmpeg($"-y -framerate {fps} -i \"{Path.Combine(frameDir, "%05d.jpg")}\" -vf \"format=yuv420p\" -c:v libx264 -crf 18 -preset medium \"{mp4}\"");
                if (File.Exists(mp4)) { made.Add(mp4); Directory.Delete(frameDir, true); }
            }
            if (made.Count > 0)
            {
                string list = Path.Combine(outDir, "concat.txt");
                File.WriteAllLines(list, made.ConvertAll(p => $"file '{p.Replace("'", "'\\''")}'"));
                yield return RunFfmpeg($"-y -f concat -safe 0 -i \"{list}\" -c copy \"{Path.Combine(outDir, "all_weapons.mp4")}\"");
            }
            director.Showcase.Playing = true;
            Object.Destroy(rt);
            Status = string.Format(L10n.T("rec.done"), outDir);
            Debug.Log("[FlyWireSwat] " + Status);
            IsRecording = false;
            if (quitWhenDone) Application.Quit();
        }

        IEnumerator RunFfmpeg(string args)
        {
            Process p = null;
            try
            {
                p = Process.Start(new ProcessStartInfo(ffmpegPath, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
            }
            catch (System.Exception e) { Debug.LogError("[FlyWireSwat] ffmpeg başlatılamadı: " + e.Message); yield break; }
            while (!p.HasExited) yield return null;
            if (p.ExitCode != 0) Debug.LogError("[FlyWireSwat] ffmpeg hata: " + p.StandardError.ReadToEnd());
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_').Replace('(', '_').Replace(')', '_').Replace('/', '_');
        }
    }
}
