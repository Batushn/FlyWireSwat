using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlyWireSwat.Connectome;
using FlyWireSwat.Fly;
using FlyWireSwat.Fps;
using FlyWireSwat.Weapons;
using Newtonsoft.Json;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlyWireSwat.Sim
{
    /// <summary>
    /// Orchestrates everything: loads the FlyWire circuit, runs (or loads) the Monte-Carlo benchmark,
    /// replays attempts in slow motion with the live spiking network, records videos, and hosts the FPS mode.
    /// </summary>
    public class SimulationDirector : MonoBehaviour
    {
        public enum Phase { Menu, Loading, Benchmark, Showcase, Fps }
        public enum StartMode { Menu, Showcase, Fps, RecordVideos }

        [Header("Start")]
        public StartMode startMode = StartMode.Menu;
        [Tooltip("Reuse Results/leaderboard_latest.json instead of re-running the benchmark")] public bool useCachedResults = true;

        [Header("Benchmark")]
        public int trialsPerWeapon = 150;
        public uint seed = 2024;
        public float timeBudgetSeconds = 1800f;
        [Tooltip("Worker threads for the benchmark (0 = half of the cores). Keeps the CPU cool.")] public int benchmarkWorkerThreads = 0;
        public List<WeaponDefinition> weapons = new List<WeaponDefinition>();
        public List<WeaponDefinition> repellents = new List<WeaponDefinition>();
        [Tooltip("Simulated hours per repellent item")] public int repellentHours = 24;

        [Header("Brain model")]
        public LifParams lif = LifParams.Default;
        public SensoryTuning tuning = SensoryTuning.Default;

        [Header("Replay / video")]
        [Range(0.005f, 1f)] public float replayTimeScale = 0.04f;
        public int targetFrameRate = 60;
        public float videoSecondsPerWeapon = 12f;
        public bool quitAfterRecording = false;

        public Phase CurrentPhase { get; private set; } = Phase.Menu;
        public float BenchmarkProgress { get; private set; }
        public string StatusLine { get; private set; } = "";
        public List<WeaponStats> Leaderboard { get; private set; } = new List<WeaponStats>();
        public List<RepellentStats> RepellentBoard { get; private set; } = new List<RepellentStats>();
        public List<WeaponDefinition> AllItems { get; private set; } = new List<WeaponDefinition>();
        public int ShowcaseIndex => ShowcaseWeaponIndex;
        public WeaponDefinition CurrentItem => AllItems[ShowcaseWeaponIndex];
        public float RepellentVerdictFlash { get; private set; }
        public string RepellentVerdictText { get; private set; } = "";
        public NetworkSpec Net { get; private set; }
        public TrialEngine Engine { get; private set; }
        public ShowcaseView Showcase { get; private set; }
        public BrainView Brain { get; private set; }
        public HudUI Hud { get; private set; }
        public ReplayRecorder Recorder { get; private set; }
        public FpsMode Fps { get; private set; }
        public CinematicCamera Cinematic { get; private set; }
        public RenderTexture BrainTexture { get; private set; }
        public const int BrainLayer = 30;
        public int ShowcaseWeaponIndex { get; private set; }
        public AttemptResult ShowcaseResult { get; private set; }
        public TrialOutput ShowcaseOutput { get; private set; }
        public List<int2> ShowcaseSpikes { get; private set; }
        public string ExportedPath { get; private set; }
        public bool ShowLeaderboard = true;
        public bool ShowBrain = true;
        public bool Loaded { get; private set; }
        uint _showcaseSeed = 77;
        bool _repelFlashShown;
        bool _visualsBuilt;
        StartMode _pendingMode;

        void Start()
        {
            Application.targetFrameRate = targetFrameRate;
            Application.runInBackground = true;   // recordings must not stall when the window loses focus
            if (Screen.height > Screen.width) ShowLeaderboard = false;
            QualitySettings.vSyncCount = 1;
            if (weapons == null || weapons.Count == 0) weapons = WeaponLibrary.CreateDefaults();
            if (repellents == null || repellents.Count == 0) repellents = WeaponLibrary.CreateRepellents();
            AllItems = new List<WeaponDefinition>(weapons); AllItems.AddRange(repellents);
            if (GameObject.Find("Kitchen") == null) EnvironmentBuilder.Build();
            Hud = gameObject.GetComponent<HudUI>() ?? gameObject.AddComponent<HudUI>();
            Hud.director = this;
            var cam = Camera.main;
            if (cam != null)
            {
                Cinematic = cam.GetComponent<CinematicCamera>() ?? cam.gameObject.AddComponent<CinematicCamera>();
                Cinematic.active = true;
                cam.nearClipPlane = 0.01f;
            }
            var args = System.Environment.GetCommandLineArgs();
            foreach (var a in args)
            {
                if (a == "-record") { startMode = StartMode.RecordVideos; quitAfterRecording = true; }
                else if (a == "-fps") startMode = StartMode.Fps;
                else if (a == "-showcase") startMode = StartMode.Showcase;
                else if (a == "-portrait") { Screen.SetResolution(1080, 1920, false); ShowLeaderboard = false; }
                else if (a == "-landscape") Screen.SetResolution(1920, 1080, false);
                else if (a == "-tr") L10n.SetLang("tr");
                else if (a == "-en") L10n.SetLang("en");
                else if (a.StartsWith("-videoseconds=") && float.TryParse(a.Substring(14), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vs)) videoSecondsPerWeapon = vs;
            }
            if (startMode != StartMode.Menu) Begin(startMode);
        }

        /// <summary>Called from the menu (or startMode) to start a mode.</summary>
        public void Begin(StartMode mode)
        {
            _pendingMode = mode;
            StopAllCoroutines();
            StartCoroutine(Run(mode));
        }

        IEnumerator LoadNetwork()
        {
            if (Loaded) yield break;
            CurrentPhase = Phase.Loading;
            StatusLine = L10n.T("loading");
            yield return null;
            Net = NetworkSpec.LoadDefault(lif.mvPerSynapse, tuning.dnInputGain);
            Engine = new TrialEngine(Net, lif, tuning) { TimeBudgetSeconds = timeBudgetSeconds };
            var sum = Net.Source.summary;
            StatusLine = string.Format(L10n.T("loaded"), sum.neuronCount, sum.edgeCount, sum.totalSynapses);
            Debug.Log("[FlyWireSwat] " + StatusLine);
            Loaded = true;
        }

        void BuildVisuals()
        {
            if (_visualsBuilt) return;
            var showGo = new GameObject("Showcase");
            Showcase = showGo.AddComponent<ShowcaseView>();
            Showcase.Build();
            Showcase.TimeScale = replayTimeScale;
            if (Cinematic != null) Cinematic.focus = Showcase.flyRoot;

            // brain view lives far away on its own layer, rendered by a dedicated camera into a texture (HUD picture-in-picture)
            var brainGo = new GameObject("BrainView");
            brainGo.transform.position = new Vector3(0f, -60f, 0f);
            Brain = brainGo.AddComponent<BrainView>();
            Brain.Build(Net, lif.dtMs);
            foreach (var t in brainGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = BrainLayer;
            BrainTexture = new RenderTexture(1024, 614, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var bc = new GameObject("BrainCamera").AddComponent<Camera>();
            bc.transform.SetParent(brainGo.transform, false);
            bc.transform.localPosition = new Vector3(0f, 0f, -1.2f);
            bc.orthographic = true; bc.orthographicSize = Brain.height * 0.68f;
            bc.cullingMask = 1 << BrainLayer; bc.clearFlags = CameraClearFlags.SolidColor; bc.backgroundColor = new Color(0.03f, 0.03f, 0.06f, 1f);
            bc.targetTexture = BrainTexture; bc.nearClipPlane = 0.1f; bc.farClipPlane = 5f;
            bc.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var main = Camera.main; if (main != null) main.cullingMask &= ~(1 << BrainLayer);

            Recorder = gameObject.GetComponent<ReplayRecorder>() ?? gameObject.AddComponent<ReplayRecorder>();
            Recorder.director = this;
            Recorder.fps = targetFrameRate;
            Recorder.secondsPerWeapon = videoSecondsPerWeapon;
            _visualsBuilt = true;
        }

        IEnumerator Run(StartMode mode)
        {
            yield return LoadNetwork();
            if (mode == StartMode.Fps) { EnterFps(); yield break; }

            BuildVisuals();
            SetShowcaseVisible(true);

            if (Leaderboard.Count == 0 && !(useCachedResults && TryLoadCache()))
                yield return RunBenchmark();
            if (RepellentBoard.Count == 0) RunRepellentTest();

            CurrentPhase = Phase.Showcase;
            ShowcaseWeaponIndex = 0;
            PlayShowcase(ShowcaseWeaponIndex, newRandom: true);
            if (mode == StartMode.RecordVideos) { Recorder.quitWhenDone = quitAfterRecording; Recorder.StartRecording(); }
        }

        IEnumerator RunBenchmark()
        {
            CurrentPhase = Phase.Benchmark;
            int oldWorkers = JobsUtility.JobWorkerCount;
            int workers = benchmarkWorkerThreads > 0 ? benchmarkWorkerThreads : Mathf.Max(1, SystemInfo.processorCount / 2);
            JobsUtility.JobWorkerCount = Mathf.Min(workers, JobsUtility.JobWorkerMaximumCount);
            var stats = new List<WeaponStats>();
            var t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                StatusLine = string.Format(L10n.T("bench.running"), w.displayName, i + 1, weapons.Count, trialsPerWeapon, workers);
                BenchmarkProgress = (float)i / weapons.Count;
                var recs = new List<TrialRecord>();
                yield return Engine.RunWeaponAsync(w, trialsPerWeapon, seed + (uint)i * 7919u, recs, p => BenchmarkProgress = (i + p) / weapons.Count);
                stats.Add(WeaponStats.From(w, recs));
            }
            JobsUtility.JobWorkerCount = oldWorkers;
            BenchmarkProgress = 1f;
            Leaderboard = stats.OrderByDescending(s => s.score).ToList();
            var sum = Net.Source.summary;
            ExportedPath = ResultsExporter.Export(Leaderboard, trialsPerWeapon, $"{sum.source}: {sum.neuronCount} neurons / {sum.edgeCount} edges");
            StatusLine = string.Format(L10n.T("bench.done"), Time.realtimeSinceStartup - t0, ExportedPath);
            Debug.Log("[FlyWireSwat] " + StatusLine);
            foreach (var s in Leaderboard)
                Debug.Log($"[FlyWireSwat] {s.weaponName}: score={s.score:0.00} kill={s.killRate:P0} ttk={s.meanTimeToKillSeconds:0.0}s cost=${s.meanCost:0.00} collateral=${s.meanCollateral:0}");
        }

        void RunRepellentTest()
        {
            StatusLine = string.Format(L10n.T("bench.repel"), repellents.Count);
            RepellentBoard = RepellentEngine.RunAll(repellents, repellentHours, seed + 99u);
            ResultsExporter.ExportRepellents(RepellentBoard);
            foreach (var r in RepellentBoard) Debug.Log($"[FlyWireSwat] repellent {r.itemName}: protection={r.protection:P0} kills/h={r.killsPerHour:0.0} verdict={r.verdict}");
        }

        bool TryLoadCache()
        {
            try
            {
                string path = Path.Combine(ResultsExporter.ResultsDirectory, "leaderboard_latest.json");
                if (!File.Exists(path)) return false;
                var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
                var list = JsonConvert.DeserializeObject<List<WeaponStats>>(payload["leaderboard"].ToString());
                foreach (var s in list) s.weapon = weapons.FirstOrDefault(w => w.nameEn == s.weaponName || w.id == s.weaponName);
                list = list.Where(s => s.weapon != null).ToList();
                if (list.Count != weapons.Count) return false;
                Leaderboard = list.OrderByDescending(s => s.score).ToList();
                ExportedPath = path;
                StatusLine = L10n.T("bench.cached");
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("[FlyWireSwat] cache load failed: " + e.Message); return false; }
        }

        void SetShowcaseVisible(bool on)
        {
            if (Showcase != null) Showcase.gameObject.SetActive(on);
            if (Brain != null) Brain.gameObject.SetActive(on && ShowBrain);
        }

        public void PlayShowcase(int weaponIndex, bool newRandom)
        {
            int n = AllItems.Count;
            ShowcaseWeaponIndex = ((weaponIndex % n) + n) % n;
            var w = AllItems[ShowcaseWeaponIndex];
            if (newRandom) _showcaseSeed = math.hash(new uint2(_showcaseSeed, 0x9E3779B9u));
            var rng = new Unity.Mathematics.Random(_showcaseSeed);
            float az = rng.NextFloat(0f, 360f), el = rng.NextFloat(20f, 70f);
            bool portrait = Screen.height > Screen.width;
            if (Cinematic != null)
            {
                Cinematic.Snap(rng.NextFloat(0f, 360f));
                bool wide = w.IsKiller && w.lethalRadius > 0.5f;
                Cinematic.distance = (wide ? 1.1f : 0.42f) * (portrait ? 1.25f : 1f);
                Cinematic.height = (wide ? 0.5f : 0.2f) * (portrait ? 1.1f : 1f);
                Cinematic.focusOffset = new Vector3(0f, portrait ? 0.0f : 0.03f, 0f);
                Cinematic.fov = portrait ? 50f : 42f;
            }
            RepellentVerdictFlash = 0f;
            if (!w.IsKiller)
            {
                var rs = RepellentBoard.Find(x => x.item == w);
                float protection = rs != null ? rs.protection : w.repelStrength;
                float killChance = w.killsOnContact ? Mathf.Clamp01(w.attractStrength * 1.6f) : 0f;
                ShowcaseResult = new AttemptResult { simStartMs = 0f, impactMs = 0f };
                ShowcaseSpikes = new List<int2>(); ShowcaseOutput = default;
                Showcase.LoadRepellent(w, protection, killChance);
                Brain.LoadSpikes(ShowcaseSpikes);
                Hud.OnShowcaseLoaded();
                _repelFlashShown = false;
                return;
            }

            if (w.stimulusKind == StimulusKind.Passive)
            {
                var stim = w.BuildStimulus(az, el, 1);
                var res = new AttemptResult { stimulus = stim, impactMs = 0f, simStartMs = 0f, killed = false };
                res.decision = new EscapeModel.Decision { takeoffMs = -1f, gfSpikeMs = -1f, longDnSpikeMs = -1f };
                ShowcaseResult = res; ShowcaseSpikes = new List<int2>(); ShowcaseOutput = default;
                Showcase.Load(w, res);
                Brain.LoadSpikes(ShowcaseSpikes);
                Hud.OnShowcaseLoaded();
                return;
            }

            var st = w.BuildStimulus(az, el, rng.NextUInt(1, uint.MaxValue));
            TrialEngine.ApplyAttention(ref st, tuning, ref rng);
            ShowcaseResult = Engine.SimulateRecorded(w, st, rng.NextUInt(1, uint.MaxValue), out var spikes, out var output);
            ShowcaseSpikes = spikes; ShowcaseOutput = output;
            Showcase.Load(w, ShowcaseResult);
            Brain.LoadSpikes(spikes);
            Hud.OnShowcaseLoaded();
        }

        public void SyncBrainToReplay()
        {
            if (Brain == null || ShowcaseSpikes == null) return;
            float simMs = Showcase.ReplayMs - ShowcaseResult.simStartMs;
            Brain.SetSimTime(math.max(0f, simMs));
        }

        public void EnterFps()
        {
            var surface = FindAnyObjectByType<LandingSurface>();
            if (surface == null)
            {
                var go = new GameObject("LandingSurface");
                surface = go.AddComponent<LandingSurface>();
                surface.Init(new Bounds(Vector3.zero, new Vector3(1.6f, 0.01f, 1f)));
            }
            SetShowcaseVisible(false);
            CurrentPhase = Phase.Fps;
            Fps = gameObject.AddComponent<FpsMode>();
            Fps.Enter(this, Camera.main, surface, AllItems);
        }

        public void ExitFps()
        {
            if (Fps != null) Fps.Exit();
            Fps = null;
            CurrentPhase = Phase.Menu;
        }

        void Update()
        {
            if (CurrentPhase == Phase.Showcase)
            {
                var cur = CurrentItem;
                if (!cur.IsKiller && Showcase.RepellentDecided && !_repelFlashShown)
                {
                    _repelFlashShown = true; RepellentVerdictFlash = 2.5f;
                    RepellentVerdictText = Showcase.RepellentKilled ? UiTheme.Color_(L10n.T("verdict.kills"), UiTheme.Good)
                        : Showcase.RepellentVeered ? UiTheme.Color_(L10n.T("verdict.works"), UiTheme.Good)
                        : UiTheme.Color_(L10n.T("verdict.myth") + " - " + L10n.Pick("the fly landed anyway", "sinek yine de kondu"), UiTheme.Bad);
                }
                if (RepellentVerdictFlash > 0f) RepellentVerdictFlash -= Time.unscaledDeltaTime * 0.6f;
                SyncBrainToReplay();
                if (Brain != null) Brain.gameObject.SetActive(ShowBrain);
                Showcase.TimeScale = replayTimeScale;
                if (!(Recorder != null && Recorder.IsRecording)) HandleShowcaseInput();
            }
        }

        void HandleShowcaseInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) PlayShowcase(ShowcaseWeaponIndex + 1, true);
            if (kb.leftArrowKey.wasPressedThisFrame) PlayShowcase(ShowcaseWeaponIndex - 1, true);
            if (kb.rKey.wasPressedThisFrame) PlayShowcase(ShowcaseWeaponIndex, true);
            if (kb.enterKey.wasPressedThisFrame) Showcase.Restart();
            if (kb.pKey.wasPressedThisFrame) Showcase.Playing = !Showcase.Playing;
            if (kb.lKey.wasPressedThisFrame) ShowLeaderboard = !ShowLeaderboard;
            if (kb.bKey.wasPressedThisFrame) ShowBrain = !ShowBrain;
            if (kb.vKey.wasPressedThisFrame) Recorder.StartRecording();
            if (kb.fKey.wasPressedThisFrame) EnterFps();
            if (kb.tKey.wasPressedThisFrame) L10n.Toggle();
            if (kb.escapeKey.wasPressedThisFrame) { CurrentPhase = Phase.Menu; SetShowcaseVisible(false); }
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) replayTimeScale = Mathf.Min(1f, replayTimeScale * 1.6f);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) replayTimeScale = Mathf.Max(0.005f, replayTimeScale / 1.6f);
            for (int i = 0; i < 10; i++)
            {
                var key = kb[(Key)((int)Key.Digit1 + i)];
                if (key.wasPressedThisFrame && i < AllItems.Count) PlayShowcase(i, true);
            }
            if (kb.f5Key.wasPressedThisFrame) { Leaderboard.Clear(); useCachedResults = false; Begin(StartMode.Showcase); }
#endif
        }

        void OnDestroy()
        {
            Engine?.Dispose();
            Net?.Dispose();
        }
    }

    /// <summary>Fallback environment from primitives when the kitchen has not been built.</summary>
    public static class EnvironmentBuilder
    {
        public static void Build()
        {
            if (GameObject.Find("Environment") != null) return;
            var root = new GameObject("Environment").transform;
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "TableTop"; table.transform.SetParent(root, false);
            table.transform.localPosition = new Vector3(0f, -0.02f, 0f); table.transform.localScale = new Vector3(1.8f, 0.04f, 1.2f);
            table.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(new Color(0.78f, 0.62f, 0.42f), 0.35f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor"; floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0f, -0.78f, 0f); floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(new Color(0.55f, 0.55f, 0.6f), 0.1f);
            table.AddComponent<LandingSurface>().Init(new Bounds(Vector3.zero, new Vector3(1.6f, 0.01f, 1f)));
        }
    }
}
