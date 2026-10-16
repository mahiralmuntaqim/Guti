using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Rod state machine: charge -> cast -> wait -> bite -> tension fight -> catch.
    /// Hold LMB to charge, release to cast. While waiting, hold to reel the bobber back. When something
    /// bites, click quickly, then reel in bursts: holding builds tension (snap at 100%), letting go lets
    /// the fish swim away. Input is sampled every frame; the fight simulation runs on the fixed tick.
    /// </summary>
    public class Fishing : MonoBehaviour
    {
        public enum FishState { Idle, Charging, Waiting, Bite, Fight }

        const float BiteWindow = 1.1f, RetrieveSpeed = 9f, LandDistance = 1.8f, EscapeMargin = 8f;
        // Fight tuning (simulated so each rod tier opens up the next bait tier):
        const float FishPull = 2.8f;      // m/s a fish swims away per unit strength when you're not reeling
        const float HeldPull = 0.6f;      // fraction of that pull that still drags line while you reel
        const float TensionGain = 1.5f;   // tension build-up per unit strength while reeling (/ rod line)
        const float TensionRelax = 0.9f;  // tension recovery per second when you let go

        public FishState State { get; private set; } = FishState.Idle;
        public float Charge { get; private set; }
        public float Tension { get; private set; }
        public float Distance { get; private set; }
        public float MaxDistance { get; private set; }
        public Bobber Bobber { get; private set; }
        /// <summary>-1 = read the mouse; 0/1 = forced input (automated tests).</summary>
        public int AutoReel = -1;

        PlayerController _player;
        System.Random _rng = new System.Random();
        float _waitTimer = -1f, _biteTimer, _fightT, _tickTimer, _leapTimer, _trophyTimer;
        string _hookedId;
        float _hookedSize;
        bool _hookedGolden, _clean, _landHintShown, _splashedDown;
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
                    if (jp) { State = FishState.Charging; Charge = 0; }
                    break;
                case FishState.Charging:
                    Charge = Mathf.Min(Charge + dt * 0.8f, 1f);
                    if (jr) Cast();
                    break;
                case FishState.Waiting:
                    ProcessWaiting(dt);
                    break;
                case FishState.Bite:
                    _biteTimer -= dt;
                    if (jp) StartFight();
                    else if (_biteTimer <= 0)
                    {
                        GameState.Notify("It nibbled the bait and left...", new Color(0.8f, 0.8f, 0.8f));
                        State = FishState.Waiting;
                        _waitTimer = NewWaitTime();
                    }
                    break;
                case FishState.Fight:
                    ProcessFight(dt);
                    break;
            }
        }

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

        void ReelTicks(float dt, float speedFactor)
        {
            _tickTimer -= dt;
            if (_tickTimer > 0) return;
            _tickTimer = 0.09f / Mathf.Max(speedFactor, 0.3f);
            Sfx.Play("tick", (0.9f + (float)_rng.NextDouble() * 0.2f) * Mathf.Clamp(speedFactor, 0.8f, 1.4f), -8f);
        }

        void Cast()
        {
            if (!GameState.HasBait(GameState.CurrentBait)) GameState.CurrentBait = 0;
            Bobber = Bobber.Create(_player.RodTip.position);
            Physics.IgnoreCollision(Bobber.GetComponent<Collider>(), _player.GetComponent<Collider>());
            Bobber.Body.linearVelocity = _player.Cam.transform.forward * (5f + 13f * Charge) + Vector3.up * (2f + 2f * Charge);
            State = FishState.Waiting;
            _waitTimer = -1f;
            _landHintShown = _splashedDown = false;
            Sfx.Play("whoosh", 0.8f + Charge * 0.5f);
        }

        void ProcessWaiting(float dt)
        {
            if (Bobber == null) { ResetLine(); return; }
            if (Bobber.InWater && !_splashedDown)
            {
                _splashedDown = true;
                Sfx.PlayAt("plop", Bobber.transform.position);
                Sfx.PlayAt("splash", Bobber.transform.position, 1.3f, -10f);
            }
            if (_pressed)
            {
                Bobber.ReelToward(_player.RodTip.position, RetrieveSpeed);
                ReelTicks(dt, 1.6f);
                if (FlatDistance() < 2.5f) ResetLine();
                return;
            }
            if (Bobber.InWater)
            {
                if (_waitTimer < 0) _waitTimer = NewWaitTime();
                _waitTimer -= dt;
                if (_waitTimer <= 0) Bite();
            }
            else if (Bobber.OnLand && Bobber.Body.linearVelocity.magnitude < 0.3f && !_landHintShown)
            {
                _landHintShown = true;
                GameState.Notify("That's dry land. Hold click to reel in and cast into the water.", new Color(1f, 0.8f, 0.5f));
            }
        }

        void Bite()
        {
            _hookedId = Data.RollCreature(GameState.CurrentBait, GameState.CurrentIsland, _rng);
            _hookedSize = Data.RollSize(_rng);
            _hookedGolden = !Data.Creatures[_hookedId].Junk && _rng.NextDouble() < Data.GoldenChance;
            State = FishState.Bite;
            _biteTimer = BiteWindow;
            Bobber.Tug(0.5f);
            Sfx.PlayAt("plop", Bobber.transform.position, 0.8f);
            Sfx.PlayAt("splash", Bobber.transform.position, 1.1f, -4f);
            GameState.Notify("!  Something bit. CLICK!", new Color(1f, 0.95f, 0.4f));
        }

        void StartFight()
        {
            State = FishState.Fight;
            Distance = FlatDistance();
            MaxDistance = Distance + EscapeMargin;
            Tension = 0.15f;
            _clean = true;
            _fightT = (float)_rng.NextDouble() * 10f;
            _leapTimer = 1.5f + (float)_rng.NextDouble() * 1.5f;
            Bobber.Pin();
            _fightFish = FishModel.Build(_hookedId, _hookedSize, _hookedGolden);
            _fightFish.transform.position = Bobber.transform.position + Vector3.down * 0.35f;
            Sfx.PlayAt("splash", Bobber.transform.position);
        }

        void ProcessFight(float dt)
        {
            if (Bobber == null) { ResetLine(); return; }
            float strength = Data.FightStrength(_hookedId, _hookedSize, _hookedGolden);
            _fightT += dt;
            float thrash = 0.55f + 0.45f * Mathf.Sin(_fightT * (2f + strength * 2f));
            bool lunge = _rng.NextDouble() < 0.03;
            if (lunge) thrash += (float)_rng.NextDouble() * 0.6f;
            var rod = GameState.Rod;
            float pull = strength * thrash * FishPull;
            if (_pressed)
            {
                Tension += (strength * thrash * TensionGain + 0.3f) * dt / rod.Line;
                Distance += (pull * HeldPull - rod.Reel) * dt;
                ReelTicks(dt, rod.Reel / 2.2f);
            }
            else
            {
                Tension -= TensionRelax * dt;
                Distance += pull * dt;
            }
            Tension = Mathf.Clamp(Tension, 0f, 1.2f);
            if (Tension > Data.CleanTensionLimit) _clean = false;

            // drag the (kinematic, interpolated) bobber along the line toward/away from the player
            var tip = _player.RodTip.position;
            var dir = Bobber.transform.position - tip;
            dir.y = 0;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : _player.transform.forward;
            var pos = new Vector3(tip.x, 0, tip.z) + dir * Distance;
            pos += Vector3.Cross(Vector3.up, dir) * Mathf.Sin(_fightT * 6f) * 0.25f * strength;
            pos.y = WaterSurface.HeightAt(pos.x, pos.z) - 0.05f - thrash * 0.06f;
            Bobber.Body.MovePosition(pos);
            UpdateFightFish(dt, dir, pos, strength, lunge);

            if (Tension >= 1f)
            {
                Sfx.Play("snap");
                GameState.Notify(GameState.Bait.Infinite ? "SNAP! The line broke." : "SNAP! The line broke and took your bait.", new Color(1f, 0.4f, 0.3f));
                if (!GameState.Bait.Infinite) GameState.ConsumeBait();
                ResetLine();
            }
            else if (Distance >= MaxDistance)
            {
                Sfx.PlayAt("splash", Bobber.transform.position);
                Sfx.Play("escape", 1f, -6f);
                GameState.Notify("It got away!", new Color(1f, 0.6f, 0.4f));
                ResetLine();
            }
            else if (Distance <= LandDistance)
            {
                LandCatch();
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
            else if (lunge) Sfx.PlayAt("splash", p, 1.4f, -10f);
            p.y = WaterSurface.HeightAt(p.x, p.z) + depth;
            // smooth toward the target so the fish moves fluidly between ticks
            _fightFish.transform.position = Vector3.Lerp(_fightFish.transform.position, p, 0.5f);
            // model faces +X; turn it so +X points along dir (away from the player), with a tail wag
            float heading = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
            _fightFish.transform.rotation = Quaternion.Euler(0, heading + Mathf.Sin(_fightT * 14f) * 20f, 0);
        }

        void LandCatch()
        {
            var c = Data.Creatures[_hookedId];
            int value = Data.CatchValue(_hookedId, _hookedSize, _hookedGolden, _clean);
            int cm = Mathf.RoundToInt(c.Length * _hookedSize * 100f);
            string title = Data.CatchTitle(_hookedId, _hookedGolden);
            Sfx.PlayAt("splash_big", Bobber != null ? Bobber.transform.position : transform.position);
            if (GameState.AddCatch(_hookedId, _hookedSize, _hookedGolden, _clean))
            {
                var col = _hookedGolden ? new Color(1f, 0.85f, 0.2f) : Data.RarityColors[c.Rarity];
                GameState.Notify($"Caught {title}  ({cm} cm, {Data.RarityNames[c.Rarity]})  worth ${value}{(_clean ? "  Clean catch!" : "")}", col);
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
            State = FishState.Idle;
            Charge = Tension = 0;
            _waitTimer = -1f;
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
