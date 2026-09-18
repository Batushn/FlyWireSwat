using System.Collections.Generic;
using FlyWireSwat.Fly;
using FlyWireSwat.Sim;
using Unity.Mathematics;
using UnityEngine;

namespace FlyWireSwat.Fps
{
    /// <summary>
    /// A living fly: rests on the table, watches the world through the real connectome (LiveBrain),
    /// jumps when the Giant Fiber says so, buzzes around, lands again. Can be killed.
    /// </summary>
    public class FlyAgent : MonoBehaviour
    {
        public enum State { Resting, Escaping, Flying, Landing, Dead, Poisoned, Stuck }

        public LiveBrain brain;
        public LandingSurface surface;
        public FlyVisual visual;
        public State state = State.Resting;
        public EscapeMode lastEscape = EscapeMode.None;
        public float lastGfTime = -100f;
        public float restTimer;
        public string threatName = "";

        Vector3 _vel;
        Vector3 _target;
        float _stateTime;
        float _flyDuration;
        readonly System.Random _rng = new System.Random();
        AudioSource _buzz;
        float _longWindow;          // accumulated long-mode DN spikes with decay
        float _poisonTimer;
        float _prevAngle = -1f;
        float _angVelSmoothed;
        Vector3 _threatPos;
        float _threatRadius;
        bool _hasThreat;

        public bool Alive => state != State.Dead;
        public bool Airborne => state == State.Escaping || state == State.Flying || state == State.Landing;

        public void Init(LiveBrain b, LandingSurface s)
        {
            brain = b; surface = s;
            visual = gameObject.AddComponent<FlyVisual>();
            visual.Build();
            visual.ResetVisual();
            _buzz = gameObject.AddComponent<AudioSource>();
            _buzz.clip = ProceduralSfx.Buzz(); _buzz.loop = true; _buzz.spatialBlend = 1f; _buzz.minDistance = 0.3f; _buzz.maxDistance = 6f; _buzz.volume = 0.5f;
            _buzz.Play(); _buzz.mute = true;
        }

        /// <summary>Tell the fly what the biggest threat looks like this frame (world position + half-size).</summary>
        public void SetThreat(Vector3 worldPos, float radius, string name)
        {
            _threatPos = worldPos; _threatRadius = radius; _hasThreat = true; threatName = name;
        }
        public void ClearThreat() { _hasThreat = false; threatName = ""; }

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            if (state == State.Dead) return;

            // --- perception -> brain (only meaningful while sitting)
            float angle = 0f, angVel = 0f, az = 0f;
            if (_hasThreat && (state == State.Resting || state == State.Poisoned))
            {
                Vector3 d = _threatPos - transform.position;
                float dist = math.max(0.001f, d.magnitude - _threatRadius);
                angle = math.degrees(2f * math.atan(_threatRadius / dist));
                if (_prevAngle >= 0f && dt > 0f)
                {
                    float raw = math.max(0f, (angle - _prevAngle) / dt);
                    _angVelSmoothed = math.lerp(_angVelSmoothed, raw, math.saturate(dt * 25f));
                }
                _prevAngle = angle;
                angVel = _angVelSmoothed;
                Vector3 local = transform.InverseTransformDirection(d.normalized);
                az = math.degrees(math.atan2(local.x, local.z));
            }
            else { _prevAngle = -1f; _angVelSmoothed = 0f; }
            brain.Tick(dt, angle, angVel, az, _hasThreat ? 1f : 0f);

            _longWindow = _longWindow * math.exp(-dt / 0.12f) + brain.LongDnSpikesThisFrame;
            if (brain.GfFiredThisFrame) lastGfTime = Time.time;

            switch (state)
            {
                case State.Stuck:
                    break;
                case State.Resting:
                case State.Poisoned:
                    restTimer += dt;
                    if (state == State.Poisoned)
                    {
                        _poisonTimer -= dt;
                        if (_poisonTimer <= 0f) { Kill(false); return; }
                    }
                    if (brain.GfFiredThisFrame) StartCoroutine(TakeoffAfter(EscapeMode.ShortGiantFiber, EscapeModel.ShortModeLatencyMs * 0.001f));
                    else if (_longWindow >= 3f) { _longWindow = 0f; StartCoroutine(TakeoffAfter(EscapeMode.LongCoordinated, EscapeModel.LongModeLatencyMs * 0.001f)); }
                    else if (restTimer > 25f + (float)_rng.NextDouble() * 30f) { restTimer = 0f; Takeoff(EscapeMode.None, RandomDir(0.6f), 0.4f); } // gets bored
                    break;
                case State.Escaping:
                    _vel += Vector3.up * 2f * dt;
                    _vel = Vector3.ClampMagnitude(_vel + (_vel.normalized) * EscapeModel.FlightAccel * dt, EscapeModel.FlightSpeed);
                    transform.position += _vel * dt;
                    if (_stateTime > 0.35f) { state = State.Flying; _stateTime = 0f; _flyDuration = 3f + (float)_rng.NextDouble() * 6f; _target = WanderPoint(); }
                    break;
                case State.Flying:
                    if ((transform.position - _target).magnitude < 0.15f || _stateTime % 1.5f < dt) _target = WanderPoint();
                    Steer(_target, 1.2f, dt);
                    if (_stateTime > _flyDuration) { state = State.Landing; _stateTime = 0f; _target = PickLandingSpot(); }
                    break;
                case State.Landing:
                    Steer(_target + Vector3.up * 0.02f, 0.6f, dt);
                    if ((transform.position - _target).magnitude < 0.03f)
                    {
                        transform.position = _target; _vel = Vector3.zero;
                        transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
                        state = State.Resting; _stateTime = 0f; restTimer = 0f;
                        visual.wingsFlapping = false; _buzz.mute = true;
                        brain.Reset();
                    }
                    break;
            }
            if (Airborne)
            {
                visual.wingsFlapping = true; _buzz.mute = false;
                if (_vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(_vel.x, 0f, _vel.z).normalized, Vector3.up), dt * 8f);
            }
        }

        System.Collections.IEnumerator TakeoffAfter(EscapeMode mode, float delay)
        {
            var s = state;
            yield return new WaitForSeconds(delay);
            if (state != s) yield break;
            Vector3 away = _hasThreat ? (transform.position - _threatPos) : transform.forward;
            away.y = 0f; away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
            float jitter = mode == EscapeMode.ShortGiantFiber ? 110f : 30f;
            Vector3 dir = Quaternion.Euler(0f, ((float)_rng.NextDouble() * 2f - 1f) * jitter, 0f) * away;
            dir = Vector3.Slerp(dir, Vector3.up, mode == EscapeMode.ShortGiantFiber ? 0.55f : 0.4f);
            Takeoff(mode, dir, mode == EscapeMode.ShortGiantFiber ? EscapeModel.ShortModeSpeed : EscapeModel.LongModeSpeed);
        }

        void Takeoff(EscapeMode mode, Vector3 dir, float speed)
        {
            lastEscape = mode;
            state = State.Escaping; _stateTime = 0f;
            _vel = dir.normalized * speed;
            transform.position += Vector3.up * 0.005f;
        }

        /// <summary>Landing choice honouring placed repellents (avoid) and attractants (lure).</summary>
        public Vector3 PickLandingSpot()
        {
            for (int tries = 0; tries < 12; tries++)
            {
                Vector3 p = surface.RandomPoint(_rng);
                bool rejected = false;
                foreach (var z in RepellentZone.All)
                {
                    if (z.item == null) continue;
                    Vector3 zp = z.transform.position; zp.y = p.y;
                    float d = Vector3.Distance(p, zp);
                    if (z.item.attractStrength > 0f && d < z.item.attractRadius && _rng.NextDouble() < z.item.attractStrength)
                    {
                        float a = (float)_rng.NextDouble() * Mathf.PI * 2f, r = (float)_rng.NextDouble() * Mathf.Max(0.02f, z.item.contactRadius * 0.9f);
                        return zp + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    }
                    if (z.item.repelStrength > 0f && d < z.item.repelRadius && _rng.NextDouble() < z.item.repelStrength) { rejected = true; break; }
                }
                if (!rejected) return p;
            }
            // everything near is repelled: land at the far edge of the table
            return surface.RandomPoint(_rng, 0.05f);
        }

        Vector3 RandomDir(float up) => Vector3.Slerp(new Vector3((float)_rng.NextDouble() - 0.5f, 0f, (float)_rng.NextDouble() - 0.5f).normalized, Vector3.up, up);

        Vector3 WanderPoint()
        {
            var b = surface.worldBounds;
            return new Vector3(math.lerp(b.min.x - 0.3f, b.max.x + 0.3f, (float)_rng.NextDouble()),
                               b.center.y + 0.15f + (float)_rng.NextDouble() * 0.9f,
                               math.lerp(b.min.z - 0.3f, b.max.z + 0.3f, (float)_rng.NextDouble()));
        }

        void Steer(Vector3 target, float speed, float dt)
        {
            Vector3 desired = (target - transform.position).normalized * speed;
            _vel = Vector3.Lerp(_vel, desired, dt * 4f);
            foreach (var z in RepellentZone.All) _vel += z.WindAt(transform.position) * dt;
            transform.position += _vel * dt;
        }

        /// <summary>Applies an attack. Returns true if the fly died.</summary>
        public bool TryKill(Vector3 impactPoint, float lethalRadius, float killProbInside, float airborneFactor)
        {
            if (state == State.Dead) return false;
            float dist = Vector3.Distance(transform.position, impactPoint);
            if (dist > lethalRadius + 0.003f) return false;
            float p = killProbInside * (Airborne ? airborneFactor : 1f);
            if ((float)_rng.NextDouble() < p) { Kill(true); return true; }
            return false;
        }

        /// <summary>Loud noise: the fly leaves even if it saw nothing.</summary>
        public void Startle()
        {
            if (state == State.Resting || state == State.Poisoned) { restTimer = 0f; Takeoff(EscapeMode.None, RandomDir(0.5f), 0.5f); }
        }

        public void Poison(float seconds)
        {
            if (state == State.Resting && _poisonTimer <= 0f) { state = State.Poisoned; _poisonTimer = seconds; }
            else if (Airborne) { _poisonTimer = seconds; state = State.Landing; _target = PickLandingSpot(); }
        }

        public void Kill(bool splat)
        {
            state = State.Dead;
            _vel = Vector3.zero;
            visual.SetDead(splat);
            _buzz.mute = true;
            if (splat) { ProceduralSfx.Play(ProceduralSfx.Splat, 0.8f); Fx.Splat(transform.position, Vector3.up); }
        }

        public void KillStuck() { if (state == State.Stuck) Kill(false); }

        public void Stick() { if (Alive) { state = State.Stuck; _vel = Vector3.zero; visual.wingsFlapping = true; _buzz.mute = false; } }
    }
}
