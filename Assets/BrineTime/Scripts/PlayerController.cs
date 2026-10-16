using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// First-person castaway: walk, look, jump, interact (E), switch bait (1-4), journal (Tab), menu (Esc).
    /// Movement uses a CharacterController updated every rendered frame, so motion is smooth on any
    /// refresh rate; only the bobber/fish use fixed-step physics (with Rigidbody interpolation).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        const float WalkSpeed = 4.5f, SprintSpeed = 7f, JumpVelocity = 5.2f, Gravity = 9.81f;
        const float MouseSens = 2.2f, InteractRange = 3.5f, EyeHeight = 1.6f;
        // Movement feel: ~0.15 s to reach walk speed, ~0.1 s to stop; limited steering in the air.
        const float GroundAccel = 32f, GroundDecel = 48f, AirAccel = 9f, AirDrag = 1.5f;
        const float CoyoteTime = 0.12f, JumpBuffer = 0.13f;
        const float SprintFovKick = 5f, BobAmount = 0.035f;

        public Camera Cam { get; private set; }
        public Transform RodTip { get; private set; }
        public Fishing Fishing { get; private set; }
        public Hud Hud;
        public bool UiOpen;
        /// <summary>Tests drive the player; real keyboard/mouse are ignored.</summary>
        public bool Automation;
        public Vector2 SimMove;
        public float Pitch;
        public float SpawnYaw = 0f;
        // Player-facing options (defaults until Settings drives them; see docs/gameplay/NOTES.md).
        /// <summary>Degrees per legacy mouse-axis unit.</summary>
        public float LookSensitivity = MouseSens;
        public bool InvertY;
        /// <summary>Vertical field of view when not sprinting.</summary>
        public float BaseFov = 70f;
        /// <summary>Subtle walking head-bob. Off by default.</summary>
        public bool HeadBob;

        CharacterController _cc;
        Transform _rodPivot;
        Vector3 _velocity;
        float _stepDistance;
        bool _wasGrounded = true;
        float _lastGrounded = -1f, _jumpQueued = -1f, _shake, _rodPitch, _bobPhase, _bobOffset;
        bool _sprinting;

        public static PlayerController Create()
        {
            var go = new GameObject("Player");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.875f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 45f;
            return go.AddComponent<PlayerController>();
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0, EyeHeight, 0);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = 70f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            BuildRod();
            Fishing = gameObject.AddComponent<Fishing>();
        }

        void BuildRod()
        {
            _rodPivot = Gfx.Child(Cam.transform, "RodPivot").transform;
            _rodPivot.localPosition = new Vector3(0.32f, -0.32f, 0.45f);
            var rod = Gfx.Cylinder(_rodPivot, 0.008f, 0.02f, 1.3f, new Vector3(0, 0.3f, 0.38f), new Color(0.45f, 0.3f, 0.15f), false, 8);
            rod.transform.localRotation = Quaternion.Euler(60f, 0, 0);
            var reel = Gfx.Cylinder(_rodPivot, 0.05f, 0.05f, 0.04f, new Vector3(0.04f, 0, 0), new Color(0.25f, 0.25f, 0.28f), false, 16);
            reel.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            foreach (var r in _rodPivot.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            RodTip = Gfx.Child(_rodPivot, "RodTip").transform;
            // end of the 1.3 m rod: centre (0, 0.3, 0.38) + 0.65 * (0, sin 30, cos 30)
            RodTip.localPosition = new Vector3(0, 0.625f, 0.943f);
        }

        public void CaptureMouse()
        {
            if (Automation) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReleaseMouse()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Turn(float yawDeg, float pitchDeg)
        {
            transform.Rotate(0, yawDeg, 0);
            Pitch = Mathf.Clamp(Pitch + pitchDeg, -83f, 83f);
            Cam.transform.localRotation = Quaternion.Euler(Pitch, 0, 0);
        }

        public void Respawn()
        {
            _cc.enabled = false;
            transform.position = World.SpawnPoint(GameState.CurrentIsland);
            transform.rotation = Quaternion.Euler(0, SpawnYaw, 0);
            _cc.enabled = true;
            Pitch = 0;
            Cam.transform.localRotation = Quaternion.identity;
            _velocity = Vector3.zero;
            _stepDistance = 0;
        }

        void Update()
        {
            bool canControl = !UiOpen && !Automation && Cursor.lockState == CursorLockMode.Locked;
            if (canControl)
            {
                // raw axes: no smoothing / acceleration, just sensitivity
                float my = Input.GetAxisRaw("Mouse Y") * LookSensitivity;
                Turn(Input.GetAxisRaw("Mouse X") * LookSensitivity, InvertY ? my : -my);
                if (Input.GetKeyDown(KeyCode.E)) TryInteract();
                if (Input.GetKeyDown(KeyCode.Tab)) Hud.TogglePanel(Hud.Panel.Journal);
                if (Input.GetKeyDown(KeyCode.Escape)) Hud.OpenPanel(Hud.Panel.Pause);
                for (int i = 0; i < 4; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        if (Fishing.State == Fishing.FishState.Idle) GameState.SelectBait(i);
                        else GameState.Notify("Reel in before changing bait.", new Color(0.9f, 0.9f, 0.9f));
                    }
            }
            else if (!UiOpen && !Automation && Input.GetMouseButtonDown(0))
            {
                CaptureMouse();  // click back into the game after alt-tab
            }

            Move();
            UpdateRodPose();
            if (Hud != null) Hud.SetPrompt(CurrentPrompt());
        }

        /// <summary>Add camera/rod shake "trauma" (0..1); it decays on its own. Never touches Pitch/yaw.</summary>
        public void AddShake(float amount) => _shake = Mathf.Clamp01(_shake + amount);

        void LateUpdate()
        {
            _shake = Mathf.MoveTowards(_shake, 0f, 1.8f * Time.deltaTime);
            // camera shake is layered on top of the look angles, so mouse look never drifts
            float cont = 0f;
            if (Fishing.State == Fishing.FishState.Fight)
                cont = Fishing.Tension * Fishing.Tension * 0.35f + Fishing.LungeWarning * 0.15f + (Fishing.Lunging ? 0.3f : 0f);
            float amp = _shake * _shake * 2.2f + cont;  // degrees
            var euler = new Vector3(Pitch, 0, 0);
            if (amp > 0.001f)
            {
                float t = Time.time * 23f;
                euler.x += (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * amp;
                euler.y += (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 1.2f * amp;
                euler.z += (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 1.6f * amp;
            }
            Cam.transform.localRotation = Quaternion.Euler(euler);

            // sprint FOV kick and optional head-bob
            float flat = new Vector2(_velocity.x, _velocity.z).magnitude;
            float fov = BaseFov + (_sprinting && flat > WalkSpeed + 0.5f ? SprintFovKick : 0f);
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov, 1f - Mathf.Exp(-8f * Time.deltaTime));
            float bobTarget = 0f;
            if (HeadBob && _cc.isGrounded && flat > 0.5f)
            {
                _bobPhase += flat * Time.deltaTime * 2.1f;
                bobTarget = Mathf.Sin(_bobPhase * 2f) * BobAmount * Mathf.Clamp01(flat / WalkSpeed);
            }
            _bobOffset = Mathf.Lerp(_bobOffset, bobTarget, 1f - Mathf.Exp(-12f * Time.deltaTime));
            Cam.transform.localPosition = new Vector3(0, EyeHeight + _bobOffset, 0);
        }

        void Move()
        {
            bool canMove = !UiOpen && Fishing.State != Fishing.FishState.Fight;
            Vector2 input = Vector2.zero;
            if (Automation) input = SimMove;
            else if (canMove)
            {
                input.x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
                input.y = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            }
            bool grounded = _cc.isGrounded;
            float now = Time.time;
            if (grounded) _lastGrounded = now;
            _sprinting = !Automation && canMove && Input.GetKey(KeyCode.LeftShift) && input.y > 0;
            float speed = _sprinting ? SprintSpeed : WalkSpeed;
            var wish = (transform.right * input.x + transform.forward * input.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // accelerate toward the wished velocity; stop a bit quicker than you start; little air control
            var flat = new Vector3(_velocity.x, 0, _velocity.z);
            var target = wish * speed;
            bool steering = wish.sqrMagnitude > 0.01f;
            float rate;
            if (grounded) rate = steering && Vector3.Dot(target, flat) >= 0f ? GroundAccel : GroundDecel;
            else rate = steering ? AirAccel : AirDrag;
            if (!grounded && steering && flat.magnitude > speed)
                target = target.normalized * flat.magnitude;  // air steering never adds speed beyond what you had
            flat = Vector3.MoveTowards(flat, target, rate * Time.deltaTime);
            _velocity.x = flat.x;
            _velocity.z = flat.z;

            // jump with coyote time (just walked off an edge) and buffering (pressed just before landing)
            if (canMove && !Automation && Input.GetKeyDown(KeyCode.Space)) _jumpQueued = now;
            if (grounded && _velocity.y < 0) _velocity.y = -2f;  // stay glued to the ground/slopes
            if (_jumpQueued >= 0f && now - _jumpQueued <= JumpBuffer && now - _lastGrounded <= CoyoteTime && _velocity.y <= 0.5f)
            {
                _velocity.y = JumpVelocity;
                _jumpQueued = -1f;
                _lastGrounded = -1f;
                Sfx.Play("jump", 1f, -10f);
            }
            _velocity.y -= Gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
            Footsteps(grounded);

            if (transform.position.y < WaterSurface.Level - 0.6f)
            {
                var fellAt = transform.position;
                Sfx.PlayAt("splash_big", fellAt);
                NetSession.SendEvent("splash", 0, 0f, false, "");
                Fishing.ResetLine();
                Respawn();
                // climb out on the nearest bit of dock instead of always at the far end
                var o = Data.Islands[GameState.CurrentIsland].Origin;
                _cc.enabled = false;
                transform.position = o + new Vector3(Mathf.Clamp(fellAt.x - o.x, -0.8f, 0.8f), 1.3f,
                    Mathf.Clamp(fellAt.z - o.z, World.DockStart + 1.5f, World.DockEnd - 2f));
                _cc.enabled = true;
                AddShake(0.5f);
                GameState.Notify("Splash! You climb back onto the dock, dripping.", new Color(0.6f, 0.85f, 1f));
            }
        }

        string _surface = "sand";

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f)
                _surface = hit.collider.TryGetComponent<Surface>(out var s) ? s.Kind : "sand";
        }

        void Footsteps(bool grounded)
        {
            bool nowGrounded = _cc.isGrounded;
            if (nowGrounded && !_wasGrounded)
            {
                Sfx.Play(_surface == "wood" ? "step_wood" : "step_sand", 0.8f, -4f);
                _stepDistance = 0;
            }
            _wasGrounded = nowGrounded;
            if (!nowGrounded) return;
            float flat = new Vector2(_velocity.x, _velocity.z).magnitude;
            _stepDistance += flat * Time.deltaTime;
            float stride = flat > WalkSpeed + 0.5f ? 2.2f : 1.7f;
            if (_stepDistance >= stride)
            {
                _stepDistance = 0;
                Sfx.Play(_surface == "wood" ? "step_wood" : "step_sand", Random.Range(0.9f, 1.1f), -6f);
            }
        }

        void UpdateRodPose()
        {
            float target = 0f, shake = 0f;
            if (Fishing.State == Fishing.FishState.Charging) target = -35f * Fishing.Charge;
            else if (Fishing.State == Fishing.FishState.Fight)
            {
                target = 12f + Fishing.Tension * 10f + (Fishing.Lunging ? 6f : 0f);
                // the rod trembles with tension, flickers during a lunge telegraph and jerks in a lunge
                shake = Fishing.Tension * Fishing.Tension * 3.5f + Fishing.LungeWarning * 2.5f + (Fishing.Lunging ? 4f : 0f);
            }
            else if (Fishing.State == Fishing.FishState.Bite) { target = 4f; shake = 2f; }
            else if (Fishing.Nibbling) shake = 1.2f;
            shake += _shake * _shake * 4f;
            _rodPitch = Mathf.Lerp(_rodPitch, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            float t = Time.time * 31f;
            float nx = (Mathf.PerlinNoise(t, 1.7f) - 0.5f) * 2f * shake;
            float ny = (Mathf.PerlinNoise(3.3f, t) - 0.5f) * 1.5f * shake;
            _rodPivot.localRotation = Quaternion.Euler(_rodPitch + nx, ny, ny * 0.5f);
        }

        Stall LookedAtStall()
        {
            if (Physics.Raycast(Cam.transform.position, Cam.transform.forward, out var hit, InteractRange, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<Stall>();
            return null;
        }

        string CurrentPrompt()
        {
            if (UiOpen) return "";
            var s = LookedAtStall();
            return s != null ? "[E] " + s.Prompt() : "";
        }

        void TryInteract()
        {
            var s = LookedAtStall();
            if (s != null) s.Interact(this);
        }
    }
}
