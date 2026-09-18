using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Tiny synthesized sound effects so the project needs no audio assets.</summary>
    public static class ProceduralSfx
    {
        const int Rate = 44100;
        static AudioClip _whoosh, _splat, _shot, _boom, _spray, _zap;
        static AudioSource _src;

        static AudioSource Source
        {
            get
            {
                if (_src == null)
                {
                    var go = new GameObject("SFX");
                    Object.DontDestroyOnLoad(go);
                    _src = go.AddComponent<AudioSource>();
                    _src.spatialBlend = 0f;
                }
                return _src;
            }
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                data[i] = Mathf.Clamp(f(t, noise), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float a, float d) => t < a ? t / a : Mathf.Exp(-(t - a) / d);

        public static AudioClip Whoosh { get { if (_whoosh == null) _whoosh = Make("whoosh", 0.35f, (t, n) => n * Env(t, 0.12f, 0.08f) * 0.5f * Mathf.Sin(t * 40f)); return _whoosh; } }
        public static AudioClip Splat { get { if (_splat == null) _splat = Make("splat", 0.25f, (t, n) => (n * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 90f) * 0.5f) * Env(t, 0.005f, 0.05f)); return _splat; } }
        public static AudioClip Shot { get { if (_shot == null) _shot = Make("shot", 0.6f, (t, n) => n * Env(t, 0.002f, 0.09f) * 0.9f + Mathf.Sin(t * 2f * Mathf.PI * 60f) * Env(t, 0.002f, 0.2f) * 0.4f); return _shot; } }
        public static AudioClip Boom { get { if (_boom == null) _boom = Make("boom", 2.5f, (t, n) => n * Env(t, 0.01f, 0.5f) * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * 35f) * Env(t, 0.01f, 0.9f) * 0.8f); return _boom; } }
        public static AudioClip Spray { get { if (_spray == null) _spray = Make("spray", 1.2f, (t, n) => n * 0.25f * Env(t, 0.05f, 0.9f)); return _spray; } }
        public static AudioClip Zap { get { if (_zap == null) _zap = Make("zap", 0.3f, (t, n) => Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 4200f)) * 0.3f * Env(t, 0.005f, 0.1f) + n * 0.2f * Env(t, 0.005f, 0.06f)); return _zap; } }

        /// <summary>Looping 200 Hz wing hum with a little vibrato.</summary>
        public static AudioClip Buzz(float hz = 190f)
        {
            var clip = Make("buzz", 1.0f, (t, n) =>
            {
                float f = hz * (1f + 0.02f * Mathf.Sin(t * 2f * Mathf.PI * 7f));
                float ph = t * f;
                float saw = 2f * (ph - Mathf.Floor(ph + 0.5f));
                return saw * 0.25f + Mathf.Sin(ph * 2f * Mathf.PI * 2f) * 0.1f + n * 0.03f;
            });
            return clip;
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            var s = Source;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }
    }
}
