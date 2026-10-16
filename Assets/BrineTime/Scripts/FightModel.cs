namespace BrineTime
{
    /// <summary>How a hooked creature fights. See FightModel.Styles for the numbers.</summary>
    public enum FightStyle
    {
        /// <summary>Predictable, slow surges; rare, well-telegraphed lunges.</summary>
        Steady,
        /// <summary>Fast, jittery pull with frequent short lunges.</summary>
        Thrasher,
        /// <summary>Calm while you reel, but sprints away harder the longer you leave the line slack; long lunges.</summary>
        Runner,
    }

    /// <summary>
    /// Pure, deterministic tension-fight simulation (no Unity objects), stepped on the fixed tick by Fishing.
    /// Holding builds tension and reels line in; releasing relaxes tension while the fish takes line.
    /// Lunges are telegraphed by <see cref="LungeWarning"/> ramping 0..1 before they hit, so a snap is
    /// always the result of holding through one. The fish tires (<see cref="Fatigue"/>) while you keep
    /// pressure on it. Mirrored line-by-line by docs/gameplay/sim/fightmodel.py: keep the two in sync.
    /// </summary>
    public sealed class FightModel
    {
        public const float FishPull = 3.8f;      // m/s a fish swims away per unit strength when you're not reeling
        public const float HeldPull = 0.6f;      // fraction of that pull that still drags line while you reel
        public const float TensionGain = 1.5f;   // tension build-up per unit strength while reeling (/ rod line)
        public const float TensionBase = 0.3f;   // tension build-up from reeling itself (/ rod line)
        public const float TensionRelax = 1.0f;  // tension recovery per second when you let go
        public const float LandDistance = 1.8f, EscapeMargin = 10.8f, StartTension = 0.15f;
        public const float FatigueSlow = 0.35f;     // fraction of strength lost when fully tired
        public const float FatigueRecover = 0.3f;   // released: fatigue recovers at this fraction of the drain rate

        public struct StyleDef
        {
            public float Base, Amp, Freq0, FreqS;   // thrash = Base + Amp * sin(t * (Freq0 + FreqS * strength))
            public float LungeMin, LungeMax;        // seconds between lunges
            public float Warn, Duration, Power;     // telegraph time, lunge time, extra thrash during a lunge
            public float Run;                       // extra pull (fraction) after 1.2 s of slack line
        }

        static readonly StyleDef[] Styles =
        {
            new StyleDef { Base = 0.72f, Amp = 0.18f, Freq0 = 1.4f, FreqS = 1.2f, LungeMin = 6f, LungeMax = 10f, Warn = 0.75f, Duration = 0.6f, Power = 0.55f, Run = 0f },
            new StyleDef { Base = 0.55f, Amp = 0.42f, Freq0 = 2.4f, FreqS = 2.4f, LungeMin = 3f, LungeMax = 5.5f, Warn = 0.55f, Duration = 0.35f, Power = 0.9f, Run = 0f },
            new StyleDef { Base = 0.60f, Amp = 0.25f, Freq0 = 1.6f, FreqS = 1.4f, LungeMin = 5f, LungeMax = 8f, Warn = 0.8f, Duration = 1.1f, Power = 0.7f, Run = 0.7f },
        };

        public static StyleDef Style(FightStyle s) => Styles[(int)s];

        public enum Outcome { None, Snap, Escape, Land }

        public readonly float Strength, Stamina, Reel, Line;
        public readonly FightStyle Kind;
        public float Tension, Distance, MaxDistance, Time, Fatigue, Thrash, MaxTension;
        /// <summary>0 normally; ramps 0..1 during the telegraph right before a lunge.</summary>
        public float LungeWarning { get; private set; }
        public bool Lunging { get; private set; }
        /// <summary>Seconds of slack line so far (runners speed up with it).</summary>
        public float SlackTime { get; private set; }
        public Outcome Result { get; private set; }

        readonly StyleDef _s;
        readonly System.Random _rng;
        float _lungeTimer, _lungeLeft;

        public FightModel(float strength, FightStyle style, float stamina, float reel, float line, float startDistance, System.Random rng)
        {
            Strength = strength; Kind = style; Stamina = stamina; Reel = reel; Line = line;
            _s = Styles[(int)style];
            _rng = rng;
            Distance = startDistance;
            MaxDistance = startDistance + EscapeMargin;
            Tension = MaxTension = StartTension;
            Time = (float)_rng.NextDouble() * 10f;
            _lungeTimer = NextLunge();
        }

        float NextLunge() => _s.LungeMin + (float)_rng.NextDouble() * (_s.LungeMax - _s.LungeMin);

        public Outcome Step(bool holding, float dt)
        {
            float s = Strength;
            Time += dt;
            // lunge cycle: wait -> telegraph (warning ramps 0..1) -> lunge
            Lunging = false;
            LungeWarning = 0f;
            if (_lungeLeft > 0f)
            {
                _lungeLeft -= dt;
                Lunging = true;
                if (_lungeLeft <= 0f) _lungeTimer = NextLunge();
            }
            else
            {
                _lungeTimer -= dt;
                if (_lungeTimer <= 0f) { _lungeLeft = _s.Duration; Lunging = true; }
                else if (_lungeTimer < _s.Warn) LungeWarning = 1f - _lungeTimer / _s.Warn;
            }
            float th = _s.Base + _s.Amp * (float)System.Math.Sin(Time * (_s.Freq0 + _s.FreqS * s));
            if (_s.Amp > 0.4f) th += 0.12f * (float)System.Math.Sin(Time * 7.3f);  // thrashers jitter
            if (Lunging) th += _s.Power;
            SlackTime = holding ? 0f : SlackTime + dt;
            if (_s.Run > 0f) th *= 1f + _s.Run * System.Math.Min(SlackTime / 1.2f, 1f);
            Thrash = th;
            float eff = s * (1f - FatigueSlow * Fatigue);
            float pull = eff * th * FishPull;
            if (holding)
            {
                Fatigue = System.Math.Min(1f, Fatigue + dt / Stamina);
                Tension += (eff * th * TensionGain + TensionBase) * dt / Line;
                Distance += (pull * HeldPull - Reel) * dt;
            }
            else
            {
                Fatigue = System.Math.Max(0f, Fatigue - FatigueRecover * dt / Stamina);
                Tension -= TensionRelax * dt;
                Distance += pull * dt;
            }
            Tension = System.Math.Min(System.Math.Max(Tension, 0f), 1.2f);
            if (Tension > MaxTension) MaxTension = Tension;
            if (Tension >= 1f) Result = Outcome.Snap;
            else if (Distance >= MaxDistance) Result = Outcome.Escape;
            else if (Distance <= LandDistance) Result = Outcome.Land;
            return Result;
        }
    }
}
