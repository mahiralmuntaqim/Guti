using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Procedural sound: every effect is synthesized at startup from oscillators and filtered noise,
    /// so the game ships with no audio files. Sfx.Play("name") or Sfx.PlayAt("name", position).
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 22050;
        const int Pool2D = 10, Pool3D = 16;

        static Sfx _instance;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _p2 = new List<AudioSource>();
        readonly List<AudioSource> _p3 = new List<AudioSource>();
        int _n2, _n3;
        float _gullTimer = 6f;
        System.Random _rng = new System.Random(1234);

        public static readonly string[] Names =
        {
            "splash", "splash_big", "plop", "whoosh", "tick", "snap", "jingle", "golden", "escape", "cash",
            "blip", "deny", "horn", "step_wood", "step_sand", "jump", "gull", "waves",
        };

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Sfx>();
        }

        public static bool Has(string name) => _instance != null && _instance._clips.ContainsKey(name);

        public static void Play(string name, float pitch = 1f, float volumeDb = 0f)
        {
            if (_instance == null || !_instance._clips.TryGetValue(name, out var clip)) return;
            var s = _instance._p2[_instance._n2];
            _instance._n2 = (_instance._n2 + 1) % Pool2D;
            s.pitch = pitch;
            s.volume = DbToLinear(volumeDb);
            s.clip = clip;
            s.Play();
        }

        public static void PlayAt(string name, Vector3 pos, float pitch = 1f, float volumeDb = 0f)
        {
            if (_instance == null || !_instance._clips.TryGetValue(name, out var clip)) return;
            var s = _instance._p3[_instance._n3];
            _instance._n3 = (_instance._n3 + 1) % Pool3D;
            s.transform.position = pos;
            s.pitch = pitch;
            s.volume = DbToLinear(volumeDb);
            s.clip = clip;
            s.Play();
        }

        static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

        void Awake()
        {
            _clips["splash"] = Splash(0.55f, 7f, 0.7f);
            _clips["splash_big"] = Splash(1.1f, 3.5f, 1f);
            _clips["plop"] = Plop();
            _clips["whoosh"] = Whoosh();
            _clips["tick"] = Tick();
            _clips["snap"] = Snap();
            _clips["jingle"] = Arpeggio(new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.085f, 0.45f);
            _clips["golden"] = Arpeggio(new[] { 784f, 987.8f, 1174.7f, 1568f, 1975.5f, 2349.3f }, 0.06f, 0.4f);
            _clips["escape"] = Arpeggio(new[] { 392f, 349.2f, 293.7f }, 0.12f, 0.35f);
            _clips["cash"] = Cash();
            _clips["blip"] = Tone(880f, 0.07f, 0.35f, "sine");
            _clips["deny"] = Tone(140f, 0.16f, 0.25f, "square");
            _clips["horn"] = Horn();
            _clips["step_wood"] = Step(true);
            _clips["step_sand"] = Step(false);
            _clips["jump"] = Tone(330f, 0.08f, 0.15f, "sine", 1.6f);
            _clips["gull"] = Gull();
            _clips["waves"] = Waves();

            for (int i = 0; i < Pool2D; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _p2.Add(s);
            }
            for (int i = 0; i < Pool3D; i++)
            {
                var go = new GameObject("Sfx3D");
                go.transform.SetParent(transform);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 6f;
                s.maxDistance = 60f;
                _p3.Add(s);
            }
            var amb = gameObject.AddComponent<AudioSource>();
            amb.clip = _clips["waves"];
            amb.loop = true;
            amb.volume = DbToLinear(-13f);
            amb.Play();
        }

        void Update()
        {
            _gullTimer -= Time.deltaTime;
            if (_gullTimer <= 0f)
            {
                _gullTimer = 8f + (float)_rng.NextDouble() * 12f;
                Play("gull", 0.85f + (float)_rng.NextDouble() * 0.35f, -16f);
            }
        }

        // ------------------------------------------------------------------ synthesis

        float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Clip(string name, float[] samples, bool loop = false)
        {
            var c = AudioClip.Create(name, samples.Length, 1, Rate, false);
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            c.SetData(samples, 0);
            return c;
        }

        static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

        static float Osc(string kind, float phase)
        {
            float f = phase - Mathf.Floor(phase);
            switch (kind)
            {
                case "square": return f < 0.5f ? 1f : -1f;
                case "saw": return f * 2f - 1f;
                default: return Mathf.Sin(phase * Mathf.PI * 2f);
            }
        }

        AudioClip Tone(float freq, float dur, float vol, string kind, float sweep = 1f)
        {
            var b = Buf(dur);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                phase += freq * Mathf.Lerp(1f, sweep, t / dur) / Rate;
                float env = Mathf.Min(t / 0.005f, 1f) * Mathf.Exp(-t * 6f / dur);
                b[i] = Osc(kind, phase) * env * vol;
            }
            return Clip("tone", b);
        }

        AudioClip Splash(float dur, float decay, float vol)
        {
            var b = Buf(dur);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                lp += Mathf.Lerp(0.55f, 0.08f, Mathf.Min(t / dur, 1f)) * (n - lp);
                lp2 += 0.02f * (n - lp2);
                float env = Mathf.Min(t / 0.008f, 1f) * Mathf.Exp(-t * decay);
                float frac = t * 13f - Mathf.Floor(t * 13f);
                float bubble = Mathf.Sin(t * Mathf.PI * 2f * (300f + 900f * frac)) * 0.15f * Mathf.Exp(-t * decay * 1.5f);
                b[i] = ((lp - lp2 * 0.5f) * 1.4f + bubble) * env * vol;
            }
            return Clip("splash", b);
        }

        AudioClip Plop()
        {
            var b = Buf(0.18f);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                phase += Mathf.Lerp(950f, 220f, Mathf.Min(t / 0.12f, 1f)) / Rate;
                b[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 22f) * 0.6f + Noise() * Mathf.Exp(-t * 60f) * 0.08f;
            }
            return Clip("plop", b);
        }

        AudioClip Whoosh()
        {
            float dur = 0.38f;
            var b = Buf(dur);
            float lp1 = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                float c = Mathf.Lerp(0.05f, 0.35f, t / dur);
                lp1 += c * (n - lp1);
                lp2 += c * 0.3f * (n - lp2);
                b[i] = (lp1 - lp2) * Mathf.Sin(Mathf.PI * t / dur) * 1.6f;
            }
            return Clip("whoosh", b);
        }

        AudioClip Tick()
        {
            var b = Buf(0.025f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                b[i] = (Noise() * 0.5f + Mathf.Sin(t * Mathf.PI * 2f * 2600f) * 0.5f) * Mathf.Exp(-t * 350f) * 0.5f;
            }
            return Clip("tick", b);
        }

        AudioClip Snap()
        {
            var b = Buf(0.35f);
            float prev = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                float hp = n - prev;
                prev = n;
                b[i] = hp * Mathf.Exp(-t * 45f) * 0.8f + Mathf.Sin(t * Mathf.PI * 2f * 95f) * Mathf.Exp(-t * 14f) * 0.5f;
            }
            return Clip("snap", b);
        }

        AudioClip Arpeggio(float[] notes, float step, float vol)
        {
            var b = Buf(step * notes.Length + 0.45f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < b.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Min(t / 0.004f, 1f) * Mathf.Exp(-t * 7f);
                    float f = notes[n];
                    float w = Mathf.PI * 2f * t;
                    b[i] += (Mathf.Sin(w * f) + 0.3f * Mathf.Sin(w * f * 2f) + 0.1f * Mathf.Sin(w * f * 3f)) * env * vol * 0.6f;
                }
            }
            return Clip("arp", b);
        }

        AudioClip Cash()
        {
            var b = Buf(0.8f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float click = Noise() * Mathf.Exp(-t * 90f) * 0.5f;
                float bell = 0;
                if (t > 0.06f)
                {
                    float t2 = t - 0.06f, w = Mathf.PI * 2f * t2;
                    bell = (Mathf.Sin(w * 1318.5f) + Mathf.Sin(w * 1760f) * 0.8f + Mathf.Sin(w * 2637f) * 0.3f) * Mathf.Exp(-t2 * 5.5f) * 0.28f;
                }
                b[i] = click + bell;
            }
            return Clip("cash", b);
        }

        AudioClip Horn()
        {
            float dur = 1.4f;
            var b = Buf(dur);
            float p1 = 0, p2 = 0, lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                p1 += 110f / Rate;
                p2 += 165f / Rate;
                float raw = Osc("saw", p1) * 0.6f + Osc("saw", p2) * 0.4f;
                lp += 0.08f * (raw - lp);
                b[i] = lp * Mathf.Min(t / 0.15f, 1f) * Mathf.Min((dur - t) / 0.35f, 1f) * 0.7f;
            }
            return Clip("horn", b);
        }

        AudioClip Step(bool wood)
        {
            var b = Buf(0.09f);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                lp += (wood ? 0.25f : 0.12f) * (Noise() - lp);
                float body = wood ? Mathf.Sin(t * Mathf.PI * 2f * 150f) * Mathf.Exp(-t * 55f) * 0.5f : 0f;
                b[i] = (lp * (wood ? 0.6f : 0.9f) + body) * Mathf.Exp(-t * (wood ? 40f : 28f)) * 0.6f;
            }
            return Clip("step", b);
        }

        AudioClip Gull()
        {
            float dur = 0.65f;
            var b = Buf(dur);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate, u = t / dur;
                float f = 1100f + 500f * Mathf.Sin(u * Mathf.PI) - 350f * u + 40f * Mathf.Sin(t * Mathf.PI * 2f * 28f);
                phase += f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(u * 1.3f, 1f)) * (1f - u * 0.4f);
                b[i] = (Osc("saw", phase) * 0.35f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f) * env * 0.35f;
            }
            return Clip("gull", b);
        }

        AudioClip Waves()
        {
            // 8 s of brown-ish noise with slow swells; the tail is cross-faded into the head to loop cleanly
            int len = 8 * Rate, fade = Rate;
            var b = new float[len + fade];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                lp += 0.06f * (n - lp);
                lp2 += 0.004f * (n - lp2);
                float swell = 0.45f + 0.35f * Mathf.Sin(t * Mathf.PI * 2f / 4f) + 0.2f * Mathf.Sin(t * Mathf.PI * 2f / 2.67f + 1f);
                b[i] = (lp * 0.9f + lp2 * 3f) * swell * 0.8f;
            }
            var o = new float[len];
            Array.Copy(b, o, len);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                o[i] = b[i] * w + b[len + i] * (1f - w);
            }
            return Clip("waves", o, true);
        }
    }
}
