using System.Collections;
using System.Collections.Generic;
using FlyWireSwat.Fly;
using FlyWireSwat.Sim;
using FlyWireSwat.Weapons;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlyWireSwat.Fps
{
    /// <summary>Holds the current item in front of the camera; attacks the live fly or places repellents.</summary>
    public class FpsWeaponController : MonoBehaviour
    {
        public Camera cam;
        public FlyAgent fly;
        public List<WeaponDefinition> items;     // killers followed by repellents
        public int index;
        public bool inputEnabled = true;
        public int kills, attempts;
        public float cost, collateral;
        public string lastMessage = "";
        public float lastMessageTime;
        public const float MeleeReach = 1.0f;

        Transform _hand;
        GameObject _model;
        readonly Vector3 _restLocal = new Vector3(0.26f, -0.2f, 0.42f);
        bool _busy;
        readonly System.Random _rng = new System.Random();
        readonly List<GameObject> _spawned = new List<GameObject>();
        float _vacuumHold;
        GameObject _placePreview;

        public WeaponDefinition Current => items[index];

        public void Init(Camera c, FlyAgent f, List<WeaponDefinition> all)
        {
            cam = c; fly = f; items = all;
            _hand = new GameObject("Hand").transform;
            _hand.SetParent(cam.transform, false);
            _hand.localPosition = _restLocal;
            Equip(0);
        }

        public void Equip(int i)
        {
            index = ((i % items.Count) + items.Count) % items.Count;
            if (_model != null) Destroy(_model);
            if (_placePreview != null) Destroy(_placePreview);
            _model = FpsViewModels.Build(Current, true);
            _model.transform.SetParent(_hand, false);
            _busy = false;
            Say($"{HudUI.Emo(Current.emoji + " ")}{Current.displayName}");
        }

        public void Say(string s) { lastMessage = s; lastMessageTime = Time.time; }

        void Update()
        {
            if (!inputEnabled) return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null) return;
            for (int i = 0; i < 10; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame && i < items.Count) Equip(i);
            if (kb.qKey.wasPressedThisFrame) Equip(index - 1);
            if (kb.eKey.wasPressedThisFrame) Equip(index + 1);
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.5f) Equip(index + 1); else if (scroll < -0.5f) Equip(index - 1);
                if (mouse.leftButton.wasPressedThisFrame && !_busy) StartCoroutine(Attack());
                if (Current.behaviour == ItemBehaviour.Vacuum) Vacuum(mouse.leftButton.isPressed);
            }
#endif
            // what the fly sees: the item in the hand (or the player's body when the item is far / a placeable)
            if (fly != null && fly.Alive)
            {
                Vector3 tip = _model != null ? _model.transform.position + cam.transform.forward * 0.08f : cam.transform.position;
                float dTip = Vector3.Distance(tip, fly.transform.position);
                float r = Mathf.Max(0.02f, Current.objectRadius);
                if (dTip < 1.6f && Current.IsKiller) fly.SetThreat(tip, r, Current.displayName);
                else fly.SetThreat(cam.transform.position - Vector3.up * 0.5f, 0.25f, L10n.Pick("human", "insan"));
            }
            if (!_busy && _hand != null)
                _hand.localPosition = _restLocal + new Vector3(Mathf.Sin(Time.time * 1.3f) * 0.004f, Mathf.Sin(Time.time * 2.1f) * 0.004f, 0f);

            // placement preview
            if (Current.behaviour == ItemBehaviour.Placeable)
            {
                if (_placePreview == null) { _placePreview = FpsViewModels.Build(Current, false); foreach (var rr in _placePreview.GetComponentsInChildren<Renderer>()) rr.enabled = true; }
                Vector3 p = AimPoint(2.2f, out var n, out bool hit);
                _placePreview.SetActive(hit);
                if (hit) { _placePreview.transform.position = p; _placePreview.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f); }
            }
            else if (_placePreview != null) Destroy(_placePreview);
        }

        Vector3 AimPoint(float reach, out Vector3 normal, out bool hitSomething)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, reach)) { normal = hit.normal; hitSomething = true; return hit.point; }
            normal = Vector3.up; hitSomething = false;
            return ray.origin + ray.direction * reach;
        }

        Vector2 AimError(float sigma)
        {
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            float g1 = (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2 * System.Math.PI * u2));
            float g2 = (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2 * System.Math.PI * u2));
            return new Vector2(g1, g2) * sigma;
        }

        IEnumerator Attack()
        {
            var w = Current;
            _busy = true;
            switch (w.behaviour)
            {
                case ItemBehaviour.Placeable: yield return Place(w); break;
                case ItemBehaviour.Trap: attempts++; cost += w.costPerAttempt; yield return PlaceTrap(w); break;
                case ItemBehaviour.Spray: attempts++; cost += w.costPerAttempt; collateral += w.collateralPerAttempt; yield return SprayAttack(w); break;
                case ItemBehaviour.Rocket: attempts++; cost += w.costPerAttempt; collateral += w.collateralPerAttempt; yield return RocketAttack(w); break;
                case ItemBehaviour.Gun: attempts++; cost += w.costPerAttempt; collateral += w.collateralPerAttempt; yield return GunAttack(w); break;
                case ItemBehaviour.Cat: attempts++; collateral += w.collateralPerAttempt; yield return CatAttack(w); break;
                case ItemBehaviour.Vacuum: yield return new WaitForSeconds(0.2f); break;
                default: attempts++; cost += w.costPerAttempt; collateral += w.collateralPerAttempt; yield return MeleeAttack(w); break;
            }
            _busy = false;
        }

        string EscapeName(EscapeMode m) => m switch { EscapeMode.ShortGiantFiber => L10n.T("mode.short"), EscapeMode.LongCoordinated => L10n.T("mode.long"), _ => L10n.T("mode.none") };

        bool Resolve(Vector3 impact, WeaponDefinition w, string hitMsg, string missMsg)
        {
            bool killed = fly != null && fly.TryKill(impact, w.lethalRadius, w.killProbabilityInside, w.airborneKillFactor);
            if (killed) { kills++; Say(string.Format(hitMsg, kills)); }
            else Say(fly != null && fly.Airborne ? string.Format(L10n.T("msg.escaped"), missMsg, EscapeName(fly.lastEscape)) : missMsg);
            return killed;
        }

        /// <summary>Distance from the fly to the swing path (segment start->end) - lets a wide racket catch a fly in the air.</summary>
        float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        IEnumerator MeleeAttack(WeaponDefinition w)
        {
            Vector3 impact = AimPoint(MeleeReach, out var n, out bool hitSurface);
            Vector2 err = AimError(w.aimSigma);
            if (hitSurface) impact += Vector3.ProjectOnPlane(cam.transform.right, n).normalized * err.x + Vector3.ProjectOnPlane(cam.transform.up, n).normalized * err.y;
            Vector3 start = _hand.position;
            float dist = Vector3.Distance(start, impact);
            float dur = Mathf.Max(0.05f, dist / w.strikeSpeed);
            ProceduralSfx.Play(ProceduralSfx.Whoosh, 0.6f, 1.2f);
            float t = 0f;
            Vector3 end = impact + n * w.objectRadius * 0.4f;
            bool killedOnPath = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                _hand.position = Vector3.Lerp(start, end, t / dur);
                // wide/electrified weapons can catch the fly in the air along the swing
                if (!killedOnPath && fly != null && fly.Alive && fly.Airborne && Vector3.Distance(_hand.position, fly.transform.position) < w.lethalRadius)
                {
                    if (fly.TryKill(fly.transform.position, w.lethalRadius, w.killProbabilityInside, w.airborneKillFactor))
                    {
                        killedOnPath = true; kills++; Say(string.Format(L10n.T("msg.kill"), kills));
                        if (w.id == "racket") { Fx.Sparks(fly.transform.position, new Color(0.6f, 0.8f, 1f)); ProceduralSfx.Play(ProceduralSfx.Zap, 0.7f); }
                    }
                }
                yield return null;
            }
            if (!killedOnPath)
            {
                if (hitSurface)
                {
                    bool killed = Resolve(impact, w, L10n.T("msg.kill"), L10n.T("msg.miss"));
                    Fx.Dust(impact, n, w.lethalRadius / 0.06f);
                    if (killed && w.id == "racket") { Fx.Sparks(impact, new Color(0.6f, 0.8f, 1f)); ProceduralSfx.Play(ProceduralSfx.Zap, 0.7f); }
                    else if (!killed) ProceduralSfx.Play(ProceduralSfx.Splat, 0.25f, 0.6f);   // thud
                }
                else Say(L10n.T("msg.whiff"));   // swung at air: nothing to hit
            }
            t = 0f; float back = Mathf.Max(0.15f, w.retrySeconds * 0.4f);
            Vector3 from = _hand.position;
            while (t < back) { t += Time.deltaTime; _hand.position = Vector3.Lerp(from, cam.transform.TransformPoint(_restLocal), t / back); yield return null; }
            _hand.localPosition = _restLocal;
        }

        IEnumerator GunAttack(WeaponDefinition w)
        {
            Vector2 err = AimError(w.aimSigma / 3f);
            Vector3 dir = (cam.transform.forward + cam.transform.right * err.x + cam.transform.up * err.y).normalized;
            bool hitSomething = Physics.Raycast(cam.transform.position, dir, out var hit, 30f);
            Vector3 impact = hitSomething ? hit.point : cam.transform.position + dir * 30f;
            ProceduralSfx.Play(ProceduralSfx.Shot, 0.9f, w.lethalRadius > 0.05f ? 0.8f : 1.1f);
            Fx.MuzzleFlash(_hand.position + cam.transform.forward * 0.25f, cam.transform.forward);
            _hand.localPosition = _restLocal + new Vector3(0f, 0.02f, -0.06f);
            Resolve(impact, w, L10n.T("msg.kill"), L10n.T("msg.hole"));
            if (hitSomething) { Fx.Dust(impact, hit.normal, 1.5f); var hole = Fx.GlowSphere(impact, Mathf.Max(0.01f, w.lethalRadius * 0.4f), new Color(0.1f, 0.1f, 0.1f, 0.9f), 20f); hole.transform.localScale = new Vector3(hole.transform.localScale.x, 0.002f, hole.transform.localScale.z); }
            if (fly != null && fly.Alive) StartCoroutine(ScareFly());
            yield return new WaitForSeconds(0.08f);
            yield return new WaitForSeconds(Mathf.Max(0.2f, w.retrySeconds * 0.5f));
            _hand.localPosition = _restLocal;
        }

        IEnumerator ScareFly() { yield return new WaitForSeconds(0.12f); if (fly != null && fly.Alive) fly.Startle(); }

        IEnumerator RocketAttack(WeaponDefinition w)
        {
            Vector3 dir = cam.transform.forward;
            Vector3 impact = Physics.Raycast(cam.transform.position, dir, out var hit, 40f) ? hit.point : cam.transform.position + dir * 40f;
            var rocket = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(rocket.GetComponent<Collider>());
            rocket.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f);
            rocket.transform.position = _hand.position + cam.transform.forward * 0.3f; rocket.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            rocket.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(new Color(0.3f, 0.45f, 0.25f));
            var trail = Fx.Smoke(rocket.transform, new Vector3(0f, -0.15f, 0f), new Color(0.8f, 0.8f, 0.8f, 0.5f), 0.05f, 1.2f, 150f, 0.1f);
            ProceduralSfx.Play(ProceduralSfx.Whoosh, 1f, 0.6f);
            float dist = Vector3.Distance(rocket.transform.position, impact), t = 0f, dur = Mathf.Max(0.05f, dist / w.strikeSpeed);
            Vector3 s0 = rocket.transform.position;
            while (t < dur) { t += Time.deltaTime; rocket.transform.position = Vector3.Lerp(s0, impact, t / dur); yield return null; }
            trail.transform.SetParent(null, true); Destroy(trail.gameObject, 2f); var em = trail.emission; em.enabled = false;
            Destroy(rocket);
            ProceduralSfx.Play(ProceduralSfx.Boom, 1f);
            Fx.Explosion(impact, 1.2f);
            Resolve(impact, w, L10n.T("msg.house"), L10n.T("msg.rocketmiss"));
            StartCoroutine(Shake(0.7f, 0.05f));
            yield return new WaitForSeconds(1.5f);
        }

        IEnumerator Shake(float seconds, float amp)
        {
            var head = cam.transform;
            Vector3 basePos = head.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime) { head.localPosition = basePos + Random.insideUnitSphere * amp * (1f - t / seconds); yield return null; }
            head.localPosition = basePos;
        }

        IEnumerator SprayAttack(WeaponDefinition w)
        {
            Vector3 origin = _hand.position + cam.transform.forward * 0.12f;
            Vector3 dir = cam.transform.forward;
            ProceduralSfx.Play(ProceduralSfx.Spray, 0.8f);
            Fx.SprayCloud(origin, dir, 1.2f);
            Vector3 centre = origin + dir * w.startDistance;
            float exposure = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                float r = w.objectRadius + w.cloudGrowth * t;
                if (fly != null && fly.Alive && Vector3.Distance(fly.transform.position, centre) < r) exposure += Time.deltaTime * (fly.Airborne ? w.airborneKillFactor : 1f);
                if (fly != null) fly.SetThreat(centre, r, L10n.Pick("spray cloud", "ilaç bulutu"));
                yield return null;
            }
            if (exposure > 0.6f && fly != null && fly.Alive) { fly.Poison(8f); Say(L10n.T("msg.spray")); }
            else Say(L10n.T("msg.spraymiss"));
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator PlaceTrap(WeaponDefinition w)
        {
            Vector3 p = AimPoint(1.6f, out var n, out bool hit);
            if (!hit) { Say(L10n.T("msg.placeaim")); yield break; }
            var trap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(trap.GetComponent<Collider>());
            trap.transform.position = p + n * 0.002f; trap.transform.localScale = new Vector3(0.2f, 0.003f, 0.06f);
            trap.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.transform.forward, n), n);
            trap.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(new Color(0.95f, 0.7f, 0.2f), 0.85f);
            trap.AddComponent<StickyTrap>().Init(fly, this);
            _spawned.Add(trap);
            Say(L10n.T("msg.trap"));
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Place(WeaponDefinition w)
        {
            Vector3 p = AimPoint(2.2f, out var n, out bool hit);
            if (!hit) { Say(L10n.T("msg.placeaim")); yield break; }
            var item = FpsViewModels.Build(w, false);
            item.name = "Placed_" + w.id;
            item.transform.position = p; item.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
            var zone = item.AddComponent<RepellentZone>();
            zone.Init(w, this);
            _spawned.Add(item);
            cost += w.purchaseCost;
            Say(string.Format(L10n.T("msg.placed"), w.displayName));
            yield return new WaitForSeconds(0.25f);
        }

        IEnumerator CatAttack(WeaponDefinition w)
        {
            Vector3 impact = AimPoint(2.5f, out var n, out _);
            var cat = FpsViewModels.Build(w, false);
            cat.transform.localScale = Vector3.one * 0.7f;
            Vector3 s0 = _hand.position; float dist = Vector3.Distance(s0, impact), dur = dist / w.strikeSpeed;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                cat.transform.position = Vector3.Lerp(s0, impact, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.25f;
                cat.transform.rotation = Quaternion.LookRotation(impact - s0) * Quaternion.Euler(0f, 180f, u * 360f);
                if (fly != null) fly.SetThreat(cat.transform.position, 0.06f, w.displayName);
                yield return null;
            }
            cat.transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y + 180f, 0f);
            Resolve(impact, w, L10n.T("msg.cat"), L10n.T("msg.catmiss"));
            Fx.Dust(impact, n, 2f);
            Destroy(cat, 2.5f);
            yield return new WaitForSeconds(0.8f);
        }

        void Vacuum(bool held)
        {
            if (!held || fly == null || !fly.Alive) { _vacuumHold = 0f; return; }
            Vector3 nozzle = _hand.position + cam.transform.forward * 0.3f;
            float d = Vector3.Distance(nozzle, fly.transform.position);
            if (d < Current.lethalRadius * (fly.Airborne ? 1.6f : 1f))
            {
                _vacuumHold += Time.deltaTime;
                fly.transform.position = Vector3.MoveTowards(fly.transform.position, nozzle, Time.deltaTime * 0.8f);
                if (_vacuumHold > 0.3f) { attempts++; if (fly.TryKill(fly.transform.position, 1f, Current.killProbabilityInside, 1f)) { kills++; Say(L10n.T("msg.vacuum")); } _vacuumHold = 0f; }
            }
            else _vacuumHold = 0f;
        }

        public void CleanUp() { foreach (var g in _spawned) if (g != null) Destroy(g); _spawned.Clear(); if (_placePreview != null) Destroy(_placePreview); }
    }

    /// <summary>Fly paper: if the fly lands on it, it is stuck and dies.</summary>
    public class StickyTrap : MonoBehaviour
    {
        FlyAgent _fly; FpsWeaponController _owner;
        public void Init(FlyAgent f, FpsWeaponController o) { _fly = f; _owner = o; }
        void Update()
        {
            if (_fly == null || !_fly.Alive || _fly.state != FlyAgent.State.Resting) { if (_owner != null && _owner.fly != _fly) _fly = _owner.fly; return; }
            Vector3 local = transform.InverseTransformPoint(_fly.transform.position);
            if (Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.z) < 0.5f && Mathf.Abs(local.y) < 20f)
            {
                _fly.Stick(); _owner.kills++;
                _owner.Say(L10n.T("msg.trapkill"));
                _fly.Invoke(nameof(FlyAgent.KillStuck), 2.5f);
            }
        }
    }
}
