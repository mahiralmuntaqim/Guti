using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Rod state machine: charge -> cast -> wait (nibbles) -> bite -> tension fight -> catch.
    /// Hold LMB to charge, release to cast. While waiting, hold to reel the bobber back. A fish nibbles
    /// once or twice (small twitches - don't strike yet!) before it really bites (the float dunks under):
    /// click quickly, then reel in bursts: holding builds tension (snap at 100%), letting go lets the
    /// fish swim away. Watch for the lunge telegraph (<see cref="LungeWarning"/>) and ease off before it
    /// hits. Input is sampled every frame; the fight simulation (FightModel) runs on the fixed tick.
    /// </summary>
    public class Fishing : MonoBehaviour
    {
        public enum FishState { Idle, Charging, Waiting, Bite, Fight }

        const float RetrieveSpeed = 9f;
        // Bite window: 1.1 s for a minnow, up to ~1.45 s for the heaviest fish (they take the bait slower).
        const float BiteWindowMin = 1.1f, BiteWindowExtra = 0.35f;
        // Charge: ease-in over ChargeTime; holding past full makes the power wobble slightly below max.
        const float ChargeTime = 1.0f, ChargeEase = 1.35f, OverchargeDip = 0.07f, OverchargeHz = 1.3f;
        const float SpookWindow = 0.6f;  // pressing this soon after a nibble scares the fish off

        public FishState State { get; private set; } = FishState.Idle;
        /// <summary>Cast power 0..1 shown on the charge bar (eased; wobbles a little when held at full).</summary>
        public float Charge { get; private set; }
        public float Tension { get; private set; }
        public float Distance { get; private set; }
        public float MaxDistance { get; private set; }
        public Bobber Bobber { get; private set; }
        /// <summary>-1 = read the mouse; 0/1 = forced input (automated tests).</summary>
        public int AutoReel = -1;

        // ---- read-only cues for HUD / audio / camera (all 0/false outside a fight) ----
        /// <summary>0..1 ramp during the telegraph right before a lunge. Ease off the reel when it rises!</summary>
        public float LungeWarning => State == FishState.Fight && _fight != null ? _fight.LungeWarning : 0f;
        /// <summary>True while the fish is lunging (holding now builds tension fast).</summary>
        public bool Lunging => State == FishState.Fight && _fight != null && _fight.Lunging;
        /// <summary>0..1 how tired the hooked fish is (it pulls up to 35% weaker when fully tired).</summary>
        public float Fatigue => State == FishState.Fight && _fight != null ? _fight.Fatigue : 0f;
        /// <summary>Fight personality of the hooked creature (valid during Bite/Fight).</summary>
        public FightStyle HookedStyle => _hookedId != null && Data.Creatures.TryGetValue(_hookedId, out var c) ? c.Style : FightStyle.Steady;
        /// <summary>True for a moment after a nibble (a fake tug before the real bite).</summary>
        public bool Nibbling => State == FishState.Waiting && _sinceNibble < 0.35f;
        /// <summary>Seconds left to strike during a bite (0 otherwise).</summary>
        public float BiteTimeLeft => State == FishState.Bite ? Mathf.Max(0f, _biteTimer) : 0f;
        /// <summary>Where the cast would land right now (valid while Charging).</summary>
        public Vector3 AimPoint { get; private set; }
        /// <summary>True if <see cref="AimPoint"/> is open water.</summary>
        public bool AimOnWater { get; private set; }

        PlayerController _player;
        System.Random _rng = new System.Random();
        FightModel _fight;
        float _waitTimer = -1f, _biteTimer, _tickTimer, _leapTimer, _trophyTimer, _chargeT, _biteTugTimer;
        float _nibble1 = -1f, _nibble2 = -1f, _sinceNibble = 99f;
        string _hookedId;
        float _hookedSize, _hookedStrength;
        bool _hookedGolden, _landHintShown, _splashedDown;
        bool _pressed, _justPressed, _justReleased, _wasPressed;
        LineRenderer _line;
        GameObject _fightFish, _trophy;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            var lineGo = new GameObject("FishingLine");
            _line = lineGo.AddComponent<LineRenderer>();
            _line.positionCount = 17;
            _line.startWidth = _line.endWidth = 0.012f;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = _line.endColor = new Color(0.95f, 0.95f, 0.9f);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.enabled = false;
            gameObject.AddComponent<CastAim>();
        }

        bool ReelInput()
        {
            if (AutoReel >= 0) return AutoReel == 1;
            if (_player.UiOpen || Cursor.lockState != CursorLockMode.Locked) return false;
            return Input.GetMouseButton(0);
        }

        void Update()
        {
            // sample input every frame; edges are latched until the next fixed tick consumes them
            _pressed = ReelInput();
            if (_pressed && !_wasPressed) _justPressed = true;
            if (!_pressed && _wasPressed) _justReleased = true;
            _wasPressed = _pressed;
            UpdateLine();
            if (_trophy != null)
            {
                _trophyTimer -= Time.deltaTime;
                _trophy.transform.Rotate(0, 115f * Time.deltaTime, 0, Space.Self);
                if (_trophyTimer <= 0) { Destroy(_trophy); _trophy = null; }
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool jp = _justPressed, jr = _justReleased;
            _justPressed = _justReleased = false;
            switch (State)
            {
                case FishState.Idle:
                    if (jp) { State = FishState.Charging; Charge = 0; _chargeT = 0; }
                    break;
                case FishState.Charging:
                    _chargeT += dt;
                    Charge = ChargeCurve(_chargeT);
                    UpdateAim();
                    if (jr) Cast();
                    break;
                case FishState.Waiting:
                    ProcessWaiting(dt, jp);
                    break;
                case FishState.Bite:
                    _biteTimer -= dt;
                    _biteTugTimer -= dt;
                    if (_biteTugTimer <= 0 && Bobber != null) { Bobber.Tug(0.12f); _biteTugTimer = 0.4f; }
                    if (jp) StartFight();
                    else if (_biteTimer <= 0)
                    {
                        GameState.Notify("Too slow - it stole the bait and left...", new Color(0.8f, 0.8f, 0.8f));
                        State = FishState.Waiting;
                        StartWait();
                    }
                    break;
                case FishState.Fight:
                    ProcessFight(dt);
                    break;
            }
        }

        /// <summary>Ease-in over ChargeTime, then a slight wobble below max if you keep holding.</summary>
        public static float ChargeCurve(float t)
        {
            if (t < ChargeTime) return Mathf.Pow(t / ChargeTime, ChargeEase);
            float over = t - ChargeTime;
            return 1f - OverchargeDip * 0.5f * (1f - Mathf.Cos(over * OverchargeHz * 2f * Mathf.PI));
        }

        /// <summary>Initial bobber velocity for a cast at this power, looking along <paramref name="forward"/>.</summary>
        public static Vector3 LaunchVelocity(Vector3 forward, float power, float rodCast) =>
            forward * (5f + 13f * power) * rodCast + Vector3.up * (2f + 2f * power);

        /// <summary>Snapshot other players need to draw our rod.</summary>
        public void NetState(out bool hasBobber, out Vector3 bobberPos, out float tension)
        {
            hasBobber = Bobber != null;
            bobberPos = hasBobber ? Bobber.transform.position : Vector3.zero;
            tension = State == FishState.Fight ? Tension : 0f;
        }

        float NewWaitTime()
        {
            var b = GameState.Bait.Bite;
            return b.x + (float)_rng.NextDouble() * (b.y - b.x);
        }

        /// <summary>Roll the wait and 0-2 nibbles; nibbles fire when the remaining wait drops below their marks.</summary>
        void StartWait()
        {
            _waitTimer = NewWaitTime();
            _nibble1 = _nibble2 = -1f;
            _sinceNibble = 99f;
            double r = _rng.NextDouble();
            int n = r < 0.25 ? 0 : r < 0.7 ? 1 : 2;
            if (n >= 1) _nibble1 = 0.5f + (float)_rng.NextDouble() * 0.5f;              // 0.5-1.0 s before the bite
            if (n >= 2) _nibble2 = _nibble1 + 0.6f + (float)_rng.NextDouble() * 0.5f;   // a beat earlier
            float latest = _waitTimer - 0.4f;  // never right on splashdown
            if (_nibble2 > latest) _nibble2 = -1f;
            if (_nibble1 > latest) _nibble1 = -1f;
        }

        void ReelTicks(float dt, float speedFactor)
        {
            _tickTimer -= dt;
            if (_tickTimer > 0) return;
            _tickTimer = 0.09f / Mathf.Max(speedFactor, 0.3f);
            Sfx.Play("tick", (0.9f + (float)_rng.NextDouble() * 0.2f) * Mathf.Clamp(speedFactor, 0.8f, 1.4f), -8f);
        }

        void UpdateAim()
        {
            var tip = _player.RodTip.position;
            var v = LaunchVelocity(_player.Cam.transform.forward, Charge, GameState.Rod.Cast);
            AimOnWater = PredictLanding(tip, v, _player.GetComponent<Collider>(), out var p);
            AimPoint = p;
        }

        /// <summary>
        /// Integrates the bobber's flight the way PhysX will (gravity + 0.05 air damping at the fixed
        /// step) and returns where it first meets ground or the water surface. True if that's water.
        /// </summary>
        public static bool PredictLanding(Vector3 pos, Vector3 vel, Collider ignore, out Vector3 point)
        {
            float dt = Time.fixedDeltaTime;
            var g = Physics.gravity;
            for (int i = 0; i < 300; i++)
            {
                vel += g * dt;
                vel *= 1f / (1f + dt * 0.05f);
                var next = pos + vel * dt;
                var seg = next - pos;
                if (Physics.SphereCast(pos, Bobber.Radius * 0.8f, seg.normalized, out var hit, seg.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider != ignore && hit.collider.GetComponentInParent<PlayerController>() == null)
                {
                    point = hit.point;
                    return false;
                }
                float h = WaterSurface.HeightAt(next.x, next.z);
                if (next.y <= h)
                {
                    float f = Mathf.InverseLerp(pos.y - WaterSurface.HeightAt(pos.x, pos.z), next.y - h, 0f);
                    point = Vector3.Lerp(pos, next, f);
                    point.y = h;
                    return true;
                }
                pos = next;
            }
            point = pos;
            return false;
        }

        void Cast()
        {
            if (!GameState.HasBait(GameState.CurrentBait)) GameState.CurrentBait = 0;
            Bobber = Bobber.Create(_player.RodTip.position);
            Physics.IgnoreCollision(Bobber.GetComponent<Collider>(), _player.GetComponent<Collider>());
            Bobber.Body.linearVelocity = LaunchVelocity(_player.Cam.transform.forward, Charge, GameState.Rod.Cast);
            State = FishState.Waiting;
            _waitTimer = -1f;
            _landHintShown = _splashedDown = false;
            Sfx.Play("whoosh", 0.8f + Charge * 0.5f);
            _player.AddShake(0.15f + 0.25f * Charge);
        }

        void ProcessWaiting(float dt, bool justPressed)
        {
            if (Bobber == null) { ResetLine(); return; }
            _sinceNibble += dt;
            if (Bobber.InWater && !_splashedDown)
            {
                _splashedDown = true;
                Sfx.PlayAt("plop", Bobber.transform.position);
                Sfx.PlayAt("splash", Bobber.transform.position, 1.3f, -10f);
            }
            if (_pressed)
            {
                if (justPressed && _sinceNibble < SpookWindow)
                {
                    GameState.Notify("Too early - that was just a nibble. You spooked it!", new Color(1f, 0.75f, 0.5f));
                    _waitTimer = -1f;  // a fresh wait (and fresh nibbles) once the float settles again
                    _sinceNibble = 99f;
                }
                Bobber.ReelToward(_player.RodTip.position, RetrieveSpeed);
                ReelTicks(dt, 1.6f);
                if (FlatDistance() < 2.5f) ResetLine();
                return;
            }
            if (Bobber.InWater)
            {
                if (_waitTimer < 0) StartWait();
                _waitTimer -= dt;
                if (_nibble2 > 0 && _waitTimer <= _nibble2) { _nibble2 = -1f; Nibble(); }
                else if (_nibble1 > 0 && _waitTimer <= _nibble1) { _nibble1 = -1f; Nibble(); }
                if (_waitTimer <= 0) Bite();
            }
            else if (Bobber.OnLand && Bobber.Body.linearVelocity.magnitude < 0.3f && !_landHintShown)
            {
                _landHintShown = true;
                GameState.Notify("That's dry land. Hold click to reel in and cast into the water.", new Color(1f, 0.8f, 0.5f));
            }
        }

        void Nibble()
        {
            _sinceNibble = 0f;
            Bobber.Tug(0.1f + (float)_rng.NextDouble() * 0.05f);
            Sfx.PlayAt("plop", Bobber.transform.position, 1.5f + (float)_rng.NextDouble() * 0.3f, -16f);
        }

        void Bite()
        {
            _hookedId = Data.RollCreature(GameState.CurrentBait, GameState.CurrentIsland, _rng);
            _hookedSize = Data.RollSize(_rng);
            _hookedGolden = !Data.Creatures[_hookedId].Junk && _rng.NextDouble() < Data.GoldenChance;
            _hookedStrength = Data.FightStrength(_hookedId, _hookedSize, _hookedGolden);
            State = FishState.Bite;
            _biteTimer = BiteWindowMin + BiteWindowExtra * Mathf.InverseLerp(0.4f, 1.5f, _hookedStrength);
            _biteTugTimer = 0.45f;
            Bobber.Tug(0.6f);
            Bobber.Dunk(0.6f, 4.5f);  // a clear, strong dip - unlike the little nibbles
            Sfx.PlayAt("plop", Bobber.transform.position, 0.8f);
            Sfx.PlayAt("splash", Bobber.transform.position, 1.1f, -4f);
            _player.AddShake(0.25f);
            GameState.Notify("!  Something bit. CLICK!", new Color(1f, 0.95f, 0.4f));
        }

        void StartFight()
        {
            State = FishState.Fight;
            var rod = GameState.Rod;
            _fight = new FightModel(_hookedStrength, Data.Creatures[_hookedId].Style, Data.FightStamina(_hookedId),
                rod.Reel, rod.Line, FlatDistance(), _rng);
            Distance = _fight.Distance;
            MaxDistance = _fight.MaxDistance;
            Tension = _fight.Tension;
            _leapTimer = 1.5f + (float)_rng.NextDouble() * 1.5f;
            Bobber.Pin();
            _fightFish = FishModel.Build(_hookedId, _hookedSize, _hookedGolden);
            _fightFish.transform.position = Bobber.transform.position + Vector3.down * 0.35f;
            Sfx.PlayAt("splash", Bobber.transform.position);
        }

        void ProcessFight(float dt)
        {
            if (Bobber == null || _fight == null) { ResetLine(); return; }
            bool wasLunging = _fight.Lunging;
            var result = _fight.Step(_pressed, dt);
            Tension = _fight.Tension;
            Distance = _fight.Distance;
            if (_pressed) ReelTicks(dt, _fight.Reel / 2.95f);
            bool lungeStart = _fight.Lunging && !wasLunging;
            if (lungeStart) _player.AddShake(0.35f);

            // drag the (kinematic, interpolated) bobber along the line toward/away from the player
            float strength = _hookedStrength;
            var tip = _player.RodTip.position;
            var dir = Bobber.transform.position - tip;
            dir.y = 0;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : _player.transform.forward;
            var pos = new Vector3(tip.x, 0, tip.z) + dir * Distance;
            float sway = 0.25f * strength * (1f + (_fight.Lunging ? 1.2f : 0f));
            pos += Vector3.Cross(Vector3.up, dir) * Mathf.Sin(_fight.Time * 6f) * sway;
            // telegraph: the float twitches faster and faster right before a lunge
            float warn = _fight.LungeWarning;
            float twitch = warn > 0 ? Mathf.Sin(_fight.Time * (20f + 25f * warn)) * 0.06f * warn : 0f;
            pos.y = WaterSurface.HeightAt(pos.x, pos.z) - 0.05f - _fight.Thrash * 0.06f + twitch - (_fight.Lunging ? 0.08f : 0f);
            Bobber.Body.MovePosition(pos);
            UpdateFightFish(dt, dir, pos, strength, lungeStart);

            if (result == FightModel.Outcome.Snap)
            {
                Sfx.Play("snap");
                _player.AddShake(1f);
                GameState.Notify(GameState.Bait.Infinite ? "SNAP! You held on too hard - the line broke." : "SNAP! You held on too hard - the line broke and took your bait.", new Color(1f, 0.4f, 0.3f));
                if (!GameState.Bait.Infinite) GameState.ConsumeBait();
                ResetLine();
            }
            else if (result == FightModel.Outcome.Escape)
            {
                Sfx.PlayAt("splash", Bobber.transform.position);
                Sfx.Play("escape", 1f, -6f);
                GameState.Notify("It got away! Reel more often so it can't run.", new Color(1f, 0.6f, 0.4f));
                ResetLine();
            }
            else if (result == FightModel.Outcome.Land)
            {
                _player.AddShake(0.4f);
                LandCatch(_fight.MaxTension <= Data.CleanTensionLimit);
            }
        }

        /// <summary>The hooked creature swims under the bobber, wagging away from you, and now and then leaps.</summary>
        void UpdateFightFish(float dt, Vector3 dir, Vector3 bobPos, float strength, bool lunge)
        {
            if (_fightFish == null) return;
            _leapTimer -= dt;
            var p = bobPos + dir * 0.4f;
            float depth = -0.35f;
            if (_leapTimer < 0)
            {
                float t = -_leapTimer / 0.6f;
                depth = Mathf.Sin(t * Mathf.PI) * (0.8f + strength * 0.5f);
                if (t >= 1f)
                {
                    _leapTimer = (2f + (float)_rng.NextDouble() * 2f) / Mathf.Max(strength, 0.5f);
                    Sfx.PlayAt("splash", p, 0.9f + (float)_rng.NextDouble() * 0.3f, -2f);
                }
            }
            else if (lunge) Sfx.PlayAt("splash", p, 1.4f, -6f);
            p.y = WaterSurface.HeightAt(p.x, p.z) + depth;
            // smooth toward the target so the fish moves fluidly between ticks
            _fightFish.transform.position = Vector3.Lerp(_fightFish.transform.position, p, 0.5f);
            // model faces +X; turn it so +X points along dir (away from the player), with a tail wag
            float heading = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
            float wagHz = _fight != null && _fight.Lunging ? 24f : 14f;
            _fightFish.transform.rotation = Quaternion.Euler(0, heading + Mathf.Sin(_fight != null ? _fight.Time * wagHz : 0f) * 20f, 0);
        }

        void LandCatch(bool clean)
        {
            var c = Data.Creatures[_hookedId];
            int value = GameState.CatchValueNow(_hookedId, _hookedSize, _hookedGolden, clean);
            bool featured = GameState.IsCatchOfTheDay(_hookedId);
            int cm = Mathf.RoundToInt(c.Length * _hookedSize * 100f);
            string title = Data.CatchTitle(_hookedId, _hookedGolden);
            Sfx.PlayAt("splash_big", Bobber != null ? Bobber.transform.position : transform.position);
            if (GameState.AddCatch(_hookedId, _hookedSize, _hookedGolden, clean))
            {
                var col = _hookedGolden ? new Color(1f, 0.85f, 0.2f) : Data.RarityColors[c.Rarity];
                GameState.Notify($"Caught {title}  ({cm} cm, {Data.RarityNames[c.Rarity]})  worth ${value}{(featured ? "  Catch of the Day!" : "")}{(clean ? "  Clean catch!" : "")}", col);
                Sfx.Play(_hookedGolden || c.Rarity >= 2 ? "golden" : "jingle");
                ShowTrophy();
                NetSession.SendEvent("catch", value, _hookedSize, _hookedGolden, _hookedId);
            }
            else
            {
                Sfx.Play("deny");
                GameState.Notify($"Your {Data.Coolers[GameState.CoolerLevel].Name} is full! You let the {title} go. Sell at the Market.", new Color(1f, 0.6f, 0.4f));
            }
            if (_rng.NextDouble() < GameState.Bait.Consume) GameState.ConsumeBait();
            ResetLine();
        }

        void ShowTrophy()
        {
            if (_trophy != null) Destroy(_trophy);
            _trophy = FishModel.Build(_hookedId, 1f, _hookedGolden);
            float scale = Mathf.Clamp(0.55f / Data.Creatures[_hookedId].Length, 0.3f, 3f);
            _trophy.transform.SetParent(_player.Cam.transform, false);
            _trophy.transform.localScale = Vector3.one * scale;
            _trophy.transform.localPosition = new Vector3(0, -0.1f, 1.1f);
            _trophy.transform.localRotation = Quaternion.Euler(-10f, 0, 0);
            _trophyTimer = 2.8f;
        }

        float FlatDistance()
        {
            var a = _player.RodTip.position;
            var b = Bobber.transform.position;
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        public void ResetLine()
        {
            if (Bobber != null) Destroy(Bobber.gameObject);
            Bobber = null;
            if (_fightFish != null) Destroy(_fightFish);
            _fightFish = null;
            _fight = null;
            State = FishState.Idle;
            Charge = Tension = 0;
            _waitTimer = -1f;
            _sinceNibble = 99f;
        }

        void UpdateLine()
        {
            _line.enabled = Bobber != null;
            if (Bobber == null) return;
            var a = _player.RodTip.position;
            var b = Bobber.transform.position;  // Rigidbody interpolation keeps this smooth per frame
            float sag = State == FishState.Fight ? 0f : Mathf.Min(Vector3.Distance(a, b) * 0.06f, 1.2f);
            for (int i = 0; i < 17; i++)
            {
                float t = i / 16f;
                var p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                _line.SetPosition(i, p);
            }
        }

        void OnDestroy()
        {
            if (_line != null) Destroy(_line.gameObject);
        }
    }
}
