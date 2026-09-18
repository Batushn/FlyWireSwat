using System.Collections.Generic;
using FlyWireSwat.Connectome;
using FlyWireSwat.Sim;
using FlyWireSwat.Weapons;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlyWireSwat.Fps
{
    /// <summary>Sets up and tears down the playable first-person mode and draws its HUD.</summary>
    public class FpsMode : MonoBehaviour
    {
        public SimulationDirector director;
        public FpsPlayer player;
        public FpsWeaponController weapon;
        public FlyAgent fly;
        public LiveBrain brain;
        public BrainView wallBrain;
        public float startTime;
        Camera _cam;
        Transform _camOldParent;
        Vector3 _camOldPos; Quaternion _camOldRot;
        GUIStyle _big, _mid, _small;
        int _fliesSpawned;

        public void Enter(SimulationDirector d, Camera cam, LandingSurface surface, List<WeaponDefinition> items)
        {
            director = d; _cam = cam;
            _camOldParent = cam.transform.parent; _camOldPos = cam.transform.position; _camOldRot = cam.transform.rotation;
            var cine = cam.GetComponent<CinematicCamera>(); if (cine != null) cine.active = false;

            // player standing at the short side of the table looking at the fly
            var pgo = new GameObject("Player");
            pgo.transform.position = new Vector3(0f, surface.worldBounds.center.y - 0.877f, -1.9f);
            pgo.transform.rotation = Quaternion.identity;
            player = pgo.AddComponent<FpsPlayer>();
            var head = new GameObject("Head").transform;
            head.SetParent(pgo.transform, false); head.localPosition = new Vector3(0f, player.eyeHeight, 0f);
            player.head = head;
            cam.transform.SetParent(head, false);
            cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.02f;

            brain = new LiveBrain(d.Net, d.lif, d.tuning);
            SpawnFly(surface);

            weapon = pgo.AddComponent<FpsWeaponController>();
            weapon.Init(cam, fly, items);

            var wall = new GameObject("WallBrain");
            wall.transform.position = new Vector3(-2.95f, 0.75f, 0.4f);
            wall.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            wall.transform.localScale = Vector3.one * 1.6f;
            wallBrain = wall.AddComponent<BrainView>();
            wallBrain.Build(d.Net, d.lif.dtMs);

            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            startTime = Time.time;
        }

        void SpawnFly(LandingSurface surface)
        {
            if (fly != null) Destroy(fly.gameObject, 4f);
            var fgo = new GameObject("LiveFly");
            fgo.transform.position = _fliesSpawned == 0 ? surface.worldBounds.center : surface.RandomPoint(new System.Random());
            fgo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            fly = fgo.AddComponent<FlyAgent>();
            fly.Init(brain, surface);
            brain.Reset();
            if (weapon != null) weapon.fly = fly;
            _fliesSpawned++;
        }

        void Update()
        {
            if (fly != null && !fly.Alive && fly.state == FlyAgent.State.Dead)
            {
                if (_respawn < 0f) _respawn = 2.5f;
                _respawn -= Time.deltaTime;
                if (_respawn <= 0f) { SpawnFly(fly.surface); _respawn = -1f; }
            }
            if (wallBrain != null && brain != null) wallBrain.FlashLive(brain.SpikedNeurons, Time.deltaTime * 1000f);
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) director.ExitFps();
            if (kb != null && kb.tabKey.wasPressedThisFrame) { Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = !Cursor.visible; }
#endif
        }
        float _respawn = -1f;

        public void Exit()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _cam.transform.SetParent(_camOldParent, true);
            _cam.transform.position = _camOldPos; _cam.transform.rotation = _camOldRot;
            var cine = _cam.GetComponent<CinematicCamera>(); if (cine != null) cine.active = true;
            weapon?.CleanUp();
            if (fly != null) Destroy(fly.gameObject);
            if (player != null) Destroy(player.gameObject);
            if (wallBrain != null) Destroy(wallBrain.gameObject);
            brain?.Dispose(); brain = null;
            Destroy(this);
        }

        void OnDestroy() { brain?.Dispose(); }

        void OnGUI()
        {
            UiTheme.Ensure();
            float W = Screen.width, H = Screen.height, s = UiTheme.S;
            var big = UiTheme.Sized(UiTheme.Title, s); var mid = UiTheme.Sized(UiTheme.Body, s); var small = UiTheme.Sized(UiTheme.Small, s);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(W / 2 - 8, H / 2 - 1, 16, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(W / 2 - 1, H / 2 - 8, 2, 16), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var w = weapon != null ? weapon.Current : null;
            float elapsed = Time.time - startTime;
            var card = new Rect(10 * s, 10 * s, 440 * s, 150 * s);
            UiTheme.Panel_(card);
            GUI.Label(new Rect(card.x + 12 * s, card.y + 6 * s, card.width, 34 * s), w != null ? $"{HudUI.Emo(w.emoji + " ")}{w.displayName}" : "", big);
            GUI.Label(new Rect(card.x + 12 * s, card.y + 44 * s, card.width - 24 * s, 100 * s),
                string.Format(L10n.T("fps.kills"), weapon?.kills, weapon?.attempts, elapsed) + "\n" +
                string.Format(L10n.T("fps.cost"), weapon?.cost, weapon?.collateral) + "\n" +
                string.Format(L10n.T("fps.fly"), FlyState()) + "\n" +
                string.Format(L10n.T("fps.angle"), brain?.LastAngleDeg, brain?.LastAngVel), mid);
            // item belt
            if (weapon != null)
            {
                float bx = 10 * s, by = card.yMax + 8 * s;
                for (int i = 0; i < weapon.items.Count; i++)
                {
                    var it = weapon.items[i];
                    bool cur = i == weapon.index;
                    var r = new Rect(bx, by + i * 20 * s, 300 * s, 20 * s);
                    if (cur) { GUI.color = new Color(1f, 0.8f, 0.3f, 0.25f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
                    GUI.Label(r, $"{(i < 9 ? (i + 1).ToString() : i == 9 ? "0" : " ")}  {HudUI.Emo(it.emoji + " ")}{it.displayName}" + (it.IsKiller ? "" : UiTheme.Color_("  " + L10n.Pick("(place)", "(yerleştir)"), UiTheme.Muted)), cur ? new GUIStyle(small) { normal = { textColor = UiTheme.Accent } } : small);
                }
            }
            if (fly != null && Time.time - fly.lastGfTime < 0.6f)
            {
                GUI.color = new Color(1f, 0.3f, 0.2f, 1f - (Time.time - fly.lastGfTime) / 0.6f);
                GUI.Label(new Rect(0, H * 0.2f, W, 50 * s), HudUI.Emo("⚡ ") + L10n.T("gf.flash") + HudUI.Emo(" ⚡"), UiTheme.Sized(UiTheme.Big, s));
                GUI.color = Color.white;
            }
            if (weapon != null && Time.time - weapon.lastMessageTime < 3f)
                GUI.Label(new Rect(0, H * 0.62f, W, 44 * s), weapon.lastMessage, new GUIStyle(UiTheme.Sized(UiTheme.Title, s)) { alignment = TextAnchor.MiddleCenter });
            UiTheme.Panel_(new Rect(0, H - 30 * s, W, 30 * s), 0.6f);
            GUI.Label(new Rect(10 * s, H - 27 * s, W - 20 * s, 24 * s), L10n.T("fps.help"), small);
        }

        string FlyState()
        {
            if (fly == null) return "";
            return fly.state switch
            {
                FlyAgent.State.Resting => UiTheme.Color_(L10n.T("fly.resting"), UiTheme.Good) + $" ({fly.threatName})",
                FlyAgent.State.Escaping => UiTheme.Color_(L10n.T("fly.escaping"), UiTheme.Bad) + $" ({fly.lastEscape})",
                FlyAgent.State.Flying => UiTheme.Color_(L10n.T("fly.flying"), UiTheme.Accent),
                FlyAgent.State.Landing => UiTheme.Color_(L10n.T("fly.landing"), UiTheme.Accent),
                FlyAgent.State.Poisoned => UiTheme.Color_(L10n.T("fly.poisoned"), new Color(0.8f, 0.5f, 1f)),
                FlyAgent.State.Stuck => UiTheme.Color_(L10n.T("fly.stuck"), UiTheme.Accent),
                FlyAgent.State.Dead => UiTheme.Color_(L10n.T("fly.dead"), UiTheme.Bad),
                _ => "",
            };
        }
    }
}
