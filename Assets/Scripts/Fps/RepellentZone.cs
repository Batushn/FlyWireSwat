using System.Collections.Generic;
using FlyWireSwat.Sim;
using FlyWireSwat.Weapons;
using UnityEngine;

namespace FlyWireSwat.Fps
{
    /// <summary>A placed repellent/attractant in the FPS playground. FlyAgent consults all zones when choosing where to land.</summary>
    public class RepellentZone : MonoBehaviour
    {
        public static readonly List<RepellentZone> All = new List<RepellentZone>();
        public WeaponDefinition item;
        public FpsWeaponController owner;
        Light _light;
        float _zapCooldown;

        public void Init(WeaponDefinition def, FpsWeaponController o)
        {
            item = def; owner = o;
            if (def.lightColor.a > 0f)
            {
                _light = new GameObject("ZoneLight").AddComponent<Light>();
                _light.transform.SetParent(transform, false); _light.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                _light.type = LightType.Point; _light.color = def.lightColor; _light.intensity = 1.6f; _light.range = 1.4f;
            }
            if (def.emitsSmoke) Fx.Smoke(transform, new Vector3(0f, 0.03f, 0f), new Color(0.6f, 0.6f, 0.6f, 0.4f), 0.03f, 2.5f, 12f, 0.12f);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (_light != null) _light.intensity = 1.5f + Mathf.Sin(Time.time * 60f) * 0.3f;   // UV tube flicker
            _zapCooldown -= Time.deltaTime;
            if (item.killsOnContact && owner != null && owner.fly != null && owner.fly.Alive && _zapCooldown <= 0f)
            {
                var fly = owner.fly;
                float d = Vector3.Distance(fly.transform.position, transform.position + Vector3.up * 0.12f);
                if (d < item.contactRadius + 0.06f && (fly.state == FlyAgent.State.Resting || fly.Airborne))
                {
                    _zapCooldown = 1f;
                    if (item.lightColor.a > 0f) { Fx.Sparks(fly.transform.position, new Color(0.8f, 0.6f, 1f), 50); ProceduralSfx.Play(ProceduralSfx.Zap, 0.9f); owner.Say(L10n.T("msg.zap")); }
                    else owner.Say(L10n.T("msg.trapkill"));
                    fly.Kill(false); owner.kills++;
                }
            }
        }

        /// <summary>Wind: push airborne flies away from a fan.</summary>
        public Vector3 WindAt(Vector3 p)
        {
            if (item.windPush <= 0f) return Vector3.zero;
            Vector3 d = p - transform.position; d.y *= 0.3f;
            float dist = d.magnitude;
            if (dist > item.repelRadius || dist < 1e-3f) return Vector3.zero;
            return d.normalized * item.windPush * (1f - dist / item.repelRadius);
        }
    }
}
