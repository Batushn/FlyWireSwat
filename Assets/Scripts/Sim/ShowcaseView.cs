using FlyWireSwat.Fly;
using FlyWireSwat.Weapons;
using Unity.Mathematics;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Animates one attempt in slow motion: weapon looming in, fly reacting, impact.</summary>
    public class ShowcaseView : MonoBehaviour
    {
        public FlyVisual fly;
        public Transform flyRoot;
        public Transform weaponRoot;
        public Transform impactFx;
        public Transform cloudFx;

        WeaponDefinition _weapon;
        AttemptResult _res;
        GameObject _weaponGo;
        Material _fxMat, _cloudMat;
        float _replayStartMs, _replayEndMs;
        bool _deadShown, _impactShown, _sprayShown;
        public float ReplayMs { get; private set; }
        public bool Finished => ReplayMs >= _replayEndMs;
        public bool Playing = true;
        public float TimeScale = 0.04f;  // 25x slow motion

        public void Build()
        {
            flyRoot = new GameObject("FlyRoot").transform;
            flyRoot.SetParent(transform, false);
            fly = flyRoot.gameObject.AddComponent<FlyVisual>();
            fly.Build();
            fly.ResetVisual();

            weaponRoot = new GameObject("WeaponRoot").transform;
            weaponRoot.SetParent(transform, false);

            impactFx = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            impactFx.name = "ImpactFx";
            Destroy(impactFx.GetComponent<Collider>());
            impactFx.SetParent(transform, false);
            _fxMat = MakeTransparent(new Color(1f, 0.6f, 0.2f, 0.45f));
            impactFx.GetComponent<Renderer>().sharedMaterial = _fxMat;
            impactFx.gameObject.SetActive(false);

            cloudFx = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            cloudFx.name = "CloudFx";
            Destroy(cloudFx.GetComponent<Collider>());
            cloudFx.SetParent(transform, false);
            _cloudMat = Fx.UnlitTransparent(new Color(0.75f, 1f, 0.75f, 0.18f));
            cloudFx.GetComponent<Renderer>().sharedMaterial = _cloudMat;
            cloudFx.gameObject.SetActive(false);
        }

        public static Material MakeTransparent(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        static Vector3 ThreatDirection(in StimulusSpec s)
        {
            float az = math.radians(s.azimuthDeg), el = math.radians(s.elevationDeg);
            return new Vector3(math.sin(az) * math.cos(el), math.sin(el), math.cos(az) * math.cos(el));
        }

        public void Load(WeaponDefinition w, in AttemptResult res)
        {
            _weapon = w; _res = res; _repel = null;
            if (_weaponGo != null) Destroy(_weaponGo);
            _weaponGo = w.behaviour == Weapons.ItemBehaviour.Gun || w.behaviour == Weapons.ItemBehaviour.Rocket ? BuildProjectile(w) : Fps.FpsViewModels.Build(w, false);
            _weaponGo.transform.SetParent(weaponRoot, false);
            if (_repelItem != null) { Destroy(_repelItem); _repelItem = null; }
            fly.ResetVisual();
            flyRoot.localPosition = Vector3.zero;
            flyRoot.localRotation = Quaternion.identity;
            impactFx.gameObject.SetActive(false);
            cloudFx.gameObject.SetActive(w.stimulusKind == StimulusKind.ExpandingCloud);
            _sprayShown = false;
            _replayStartMs = math.min(res.simStartMs, res.impactMs) - 120f;
            if (w.stimulusKind == StimulusKind.Approach && w.windupSpeed > 0f) _replayStartMs = math.min(_replayStartMs, 0f);
            _replayEndMs = res.impactMs + (w.stimulusKind == StimulusKind.ExpandingCloud ? 900f : 450f);
            ReplayMs = _replayStartMs;
            Playing = true;
            Apply(ReplayMs);
        }

        /// <summary>Advance the replay by a fixed amount of simulated time (used by the video recorder).</summary>
        public void Step(float ms)
        {
            if (_repel != null) { UpdateRepellent(ms * 0.001f / Mathf.Max(0.001f, TimeScale)); return; }
            ReplayMs = Mathf.Min(_replayEndMs, ReplayMs + ms); Apply(ReplayMs);
        }

        GameObject _repelItem;
        WeaponDefinition _repel;
        float _repelT;
        bool _repelDecided, _repelVeer, _repelKill;
        Vector3 _repelStart, _repelEnd;
        System.Random _repelRng = new System.Random();
        public bool RepellentDecided => _repelDecided;
        public bool RepellentVeered => _repelVeer;
        public bool RepellentKilled => _repelKill;

        /// <summary>Repellent showcase: the item sits 25 cm from the fly spot; the fly comes in to land and reacts.</summary>
        public void LoadRepellent(WeaponDefinition item, float protection, float killChance)
        {
            _weapon = item; _repel = item;
            if (_weaponGo != null) { Destroy(_weaponGo); _weaponGo = null; }
            if (_repelItem != null) Destroy(_repelItem);
            _repelItem = Fps.FpsViewModels.Build(item, false);
            _repelItem.transform.SetParent(transform, false);
            _repelItem.transform.localPosition = new Vector3(0.18f, 0f, 0.08f);
            _repelItem.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
            if (item.emitsSmoke) Fx.Smoke(_repelItem.transform, new Vector3(0f, 0.03f, 0f), new Color(0.6f, 0.6f, 0.6f, 0.4f), 0.03f, 2.5f, 12f, 0.12f);
            if (item.lightColor.a > 0f) { var l = new GameObject("UV").AddComponent<Light>(); l.transform.SetParent(_repelItem.transform, false); l.transform.localPosition = new Vector3(0f, 0.16f, 0f); l.type = LightType.Point; l.color = item.lightColor; l.intensity = 0.9f; l.range = 0.7f; }
            impactFx.gameObject.SetActive(false); cloudFx.gameObject.SetActive(false);
            fly.ResetVisual();
            float zr = item.attractStrength > 0f ? item.attractRadius : item.repelRadius;
            var zc = item.attractStrength > 0f ? new Color(0.75f, 0.45f, 1f, 0.10f) : item.repelStrength > 0.05f ? new Color(0.45f, 1f, 0.6f, 0.10f) : new Color(0.7f, 0.7f, 0.7f, 0.06f);
            Fx.ZoneDome(_repelItem.transform, zr, zc);
            _repelDecided = false; _repelT = 0f;
            _repelVeer = _repelRng.NextDouble() < protection;
            _repelKill = !_repelVeer && item.killsOnContact && _repelRng.NextDouble() < Mathf.Clamp01(killChance);
            _repelStart = new Vector3(-0.6f, 0.35f, -0.4f);
            _repelEnd = _repelKill ? _repelItem.transform.localPosition + new Vector3(0f, 0.12f, -0.03f) : Vector3.zero;
            flyRoot.localPosition = _repelStart;
            fly.wingsFlapping = true;
            Playing = true;
            _replayStartMs = 0f; _replayEndMs = 5000f; ReplayMs = 0f;
        }

        void UpdateRepellent(float dtSeconds)
        {
            if (_repel == null) return;
            _repelT += dtSeconds;
            ReplayMs = _repelT * 1000f;
            float u = Mathf.Clamp01(_repelT / 2.2f);
            Vector3 p;
            if (_repelVeer)
            {
                // approach, then bank away before the zone
                Vector3 mid = Vector3.Lerp(_repelStart, _repelEnd, 0.7f) + Vector3.up * 0.08f;
                Vector3 away = new Vector3(-0.7f, 0.4f, 0.5f);
                if (u < 0.7f) p = Vector3.Lerp(_repelStart, mid, u / 0.7f);
                else p = Vector3.Lerp(mid, away, (u - 0.7f) / 0.3f);
                if (u >= 0.7f) _repelDecided = true;
                fly.wingsFlapping = true;
            }
            else
            {
                p = Vector3.Lerp(_repelStart, _repelEnd, Mathf.SmoothStep(0f, 1f, u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.12f;
                if (u >= 1f)
                {
                    if (!_repelDecided)
                    {
                        _repelDecided = true;
                        if (_repelKill) { fly.SetDead(false); Fx.Sparks(flyRoot.position, new Color(0.8f, 0.6f, 1f), 40); ProceduralSfx.Play(ProceduralSfx.Zap, 0.6f); }
                        else fly.wingsFlapping = false;
                    }
                }
            }
            flyRoot.localPosition = p;
            Vector3 d = (_repelEnd - _repelStart); d.y = 0f;
            if (u < 1f && d.sqrMagnitude > 1e-4f) flyRoot.localRotation = Quaternion.LookRotation(_repelVeer && u > 0.7f ? new Vector3(-1f, 0f, 0.5f) : d.normalized, Vector3.up);
        }

        public void Restart() { ReplayMs = _replayStartMs; fly.ResetVisual(); Playing = true; Apply(ReplayMs); }

        void Update()
        {
            if (_repel != null) { if (Playing) UpdateRepellent(Time.unscaledDeltaTime); return; }
            if (_weaponGo == null) return;
            if (Playing && ReplayMs < _replayEndMs)
            {
                ReplayMs += Time.unscaledDeltaTime * 1000f * TimeScale;
                if (ReplayMs > _replayEndMs) ReplayMs = _replayEndMs;
            }
            Apply(ReplayMs);
        }

        void Apply(float t)
        {
            var s = _res.stimulus;
            Vector3 dir = ThreatDirection(s);
            float tt = math.max(0f, t);

            // weapon
            if (s.kind == StimulusKind.ExpandingCloud)
            {
                if (!_sprayShown && t >= 0f) { _sprayShown = true; ProceduralSfx.Play(ProceduralSfx.Spray, 0.7f); Fx.SprayCloud(transform.TransformPoint(dir * (s.startDistance + 0.05f)), -dir, 1.5f); }
                float r = LoomGeometry.ObjectRadius(s, tt);
                cloudFx.localPosition = dir * s.startDistance;
                cloudFx.localScale = Vector3.one * r * 2f;
                _weaponGo.transform.localPosition = dir * (s.startDistance + 0.05f);
                _weaponGo.transform.LookAt(flyRoot.position);
            }
            else
            {
                float d = LoomGeometry.Distance(s, tt);
                bool passedImpact = t >= _res.impactMs;
                Vector3 aim = new Vector3(_res.aimOffset.x, 0f, _res.aimOffset.y);
                Vector3 pos = aim + dir * (d + s.objectRadius);
                if (passedImpact && (_weapon.shape == WeaponShape.Bullet || _weapon.shape == WeaponShape.Rocket))
                    pos = aim + dir * 0.0f - dir * (t - _res.impactMs) * 0.001f * s.strikeSpeed * 0.2f;
                _weaponGo.transform.localPosition = pos;
                _weaponGo.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            }

            // fly
            float3 disp = EscapeModel.Displacement(_res.decision, tt);
            flyRoot.localPosition = new Vector3(disp.x, disp.y, disp.z);
            bool airborne = _res.decision.takeoffMs >= 0f && tt >= _res.decision.takeoffMs;
            fly.wingsFlapping = airborne && !(_res.killed && t >= _res.impactMs);
            if (airborne)
            {
                var d3 = new Vector3(_res.decision.direction.x, 0f, _res.decision.direction.z);
                if (d3.sqrMagnitude > 1e-4f) flyRoot.localRotation = Quaternion.LookRotation(d3, Vector3.up);
            }
            else flyRoot.localRotation = Quaternion.identity;

            // impact effects
            float evalMs = s.kind == StimulusKind.ExpandingCloud ? _res.impactMs + 400f : _res.impactMs;
            if (t >= _res.impactMs && s.kind != StimulusKind.ExpandingCloud)
            {
                impactFx.gameObject.SetActive(true);
                float age = (t - _res.impactMs) / 200f;
                float radius = _weapon.lethalRadius * math.min(1f, 0.3f + age);
                impactFx.localPosition = new Vector3(_res.aimOffset.x, 0.002f, _res.aimOffset.y);
                impactFx.localScale = Vector3.one * radius * 2f;
                var c = _weapon.shape == WeaponShape.Rocket ? new Color(1f, 0.35f, 0.05f, 0.5f * math.saturate(1.5f - age)) : new Color(1f, 0.6f, 0.2f, 0.35f * math.saturate(1.5f - age));
                _fxMat.SetColor("_BaseColor", c);
            }
            else impactFx.gameObject.SetActive(false);

            if (_res.killed && t >= evalMs) { if (!_deadShown) { _deadShown = true; bool splat = _weapon.behaviour == Weapons.ItemBehaviour.Melee || _weapon.behaviour == Weapons.ItemBehaviour.Gun; if (splat) Fx.Splat(flyRoot.position, Vector3.up); ProceduralSfx.Play(ProceduralSfx.Splat, 0.7f); } fly.SetDead(_weapon.behaviour == Weapons.ItemBehaviour.Melee || _weapon.behaviour == Weapons.ItemBehaviour.Gun); }
            else if (t < evalMs) { fly.ResetVisual(); _deadShown = false; }
            if (t >= _res.impactMs && !_impactShown && s.kind != StimulusKind.ExpandingCloud)
            {
                _impactShown = true;
                Vector3 ip = transform.TransformPoint(new Vector3(_res.aimOffset.x, 0f, _res.aimOffset.y));
                if (_weapon.behaviour == Weapons.ItemBehaviour.Rocket) { Fx.Explosion(ip, 1.0f); ProceduralSfx.Play(ProceduralSfx.Boom, 0.9f); }
                else if (_weapon.behaviour == Weapons.ItemBehaviour.Gun) { Fx.Dust(ip, Vector3.up, 1.5f); ProceduralSfx.Play(ProceduralSfx.Shot, 0.8f); }
                else if (_weapon.behaviour == Weapons.ItemBehaviour.Melee) { Fx.Dust(ip, Vector3.up, _weapon.lethalRadius / 0.06f); ProceduralSfx.Play(ProceduralSfx.Whoosh, 0.5f); if (_weapon.id == "racket" && _res.killed) Fx.Sparks(ip, new Color(0.6f, 0.8f, 1f)); }
            }
            if (t < _res.impactMs) _impactShown = false;
        }

        static GameObject BuildProjectile(WeaponDefinition w)
        {
            var pivot = new GameObject("Projectile");
            var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(pivot.transform, false);
            g.transform.localScale = w.visualScale;
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(w.color, 0.5f, 0.7f);
            if (w.behaviour == Weapons.ItemBehaviour.Rocket)
            {
                var fin = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(fin.GetComponent<Collider>());
                fin.transform.SetParent(pivot.transform, false); fin.transform.localScale = new Vector3(0.2f, 0.01f, 0.15f); fin.transform.localPosition = new Vector3(0f, 0f, -0.35f);
                fin.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(w.color * 0.8f);
                Fx.Smoke(pivot.transform, new Vector3(0f, 0f, -0.5f), new Color(0.85f, 0.85f, 0.85f, 0.5f), 0.08f, 1.5f, 120f, 0.05f);
            }
            return pivot;
        }

        static GameObject BuildHand(WeaponDefinition w)
        {
            var pivot = new GameObject("Pivot");
            var skin = FlyVisual.MakeMat(w.color, 0.25f);
            var palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(palm.GetComponent<Collider>());
            palm.transform.SetParent(pivot.transform, false);
            palm.transform.localScale = new Vector3(0.085f, 0.1f, 0.03f);
            palm.GetComponent<Renderer>().sharedMaterial = skin;
            for (int i = 0; i < 4; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(f.GetComponent<Collider>());
                f.transform.SetParent(pivot.transform, false);
                f.transform.localScale = new Vector3(0.017f, 0.038f, 0.017f);
                f.transform.localPosition = new Vector3(-0.032f + i * 0.021f, 0.075f + (i == 1 || i == 2 ? 0.006f : 0f), 0f);
                f.GetComponent<Renderer>().sharedMaterial = skin;
            }
            var thumb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(thumb.GetComponent<Collider>());
            thumb.transform.SetParent(pivot.transform, false);
            thumb.transform.localScale = new Vector3(0.018f, 0.03f, 0.018f);
            thumb.transform.localPosition = new Vector3(0.052f, 0.02f, 0f);
            thumb.transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
            thumb.GetComponent<Renderer>().sharedMaterial = skin;
            pivot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var outer = new GameObject("Hand");
            pivot.transform.SetParent(outer.transform, false);
            return outer;
        }

        public static GameObject BuildPrimitiveWeapon(WeaponDefinition w)
        {
            GameObject g;
            switch (w.shape)
            {
                case WeaponShape.Sphere: g = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                case WeaponShape.Rod: g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case WeaponShape.Cone: g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case WeaponShape.Bullet: g = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                case WeaponShape.Rocket: g = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                case WeaponShape.Cloud: g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                default: g = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
            }
            if (w.shape == WeaponShape.Flat && w.displayName.Contains("El"))
            {
                Destroy(g);
                return BuildHand(w);
            }
            g.name = "Weapon_" + w.displayName;
            Destroy(g.GetComponent<Collider>());
            var mat = FlyVisual.MakeMat(w.color, 0.5f, w.shape == WeaponShape.Bullet || w.shape == WeaponShape.Rocket ? 0.7f : 0.05f);
            g.GetComponent<Renderer>().sharedMaterial = mat;
            // Child pivot so the mesh's long axis points along the local +Z (toward the fly).
            var pivot = new GameObject("Pivot");
            g.transform.SetParent(pivot.transform, false);
            g.transform.localScale = w.visualScale;
            if (w.shape == WeaponShape.Rod || w.shape == WeaponShape.Cone || w.shape == WeaponShape.Bullet || w.shape == WeaponShape.Rocket || w.shape == WeaponShape.Cloud)
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (w.shape == WeaponShape.Slab || w.shape == WeaponShape.Flat)
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (w.shape == WeaponShape.Slab)
            {
                // handle
                var h = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(h.GetComponent<Collider>());
                h.transform.SetParent(pivot.transform, false);
                h.transform.localScale = new Vector3(0.012f, 0.18f, 0.012f);
                h.transform.localPosition = new Vector3(0f, -w.visualScale.x * 0.5f - 0.18f, 0f);
                h.GetComponent<Renderer>().sharedMaterial = FlyVisual.MakeMat(w.color * 0.7f);
            }
            return pivot;
        }
    }
}
