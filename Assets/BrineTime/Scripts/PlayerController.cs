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

        CharacterController _cc;
        Transform _rodPivot;
        Vector3 _velocity;
        float _stepDistance;
        bool _wasGrounded = true;

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
                Turn(Input.GetAxisRaw("Mouse X") * MouseSens, -Input.GetAxisRaw("Mouse Y") * MouseSens);
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
            float speed = !Automation && Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed;
            var wish = (transform.right * input.x + transform.forward * input.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            float accel = (grounded ? 12f : 3f) * speed * Time.deltaTime;
            _velocity.x = Mathf.MoveTowards(_velocity.x, wish.x * speed, accel);
            _velocity.z = Mathf.MoveTowards(_velocity.z, wish.z * speed, accel);
            if (grounded)
            {
                if (_velocity.y < 0) _velocity.y = -2f;  // stay glued to the ground/slopes
                if (canMove && !Automation && Input.GetKeyDown(KeyCode.Space))
                {
                    _velocity.y = JumpVelocity;
                    Sfx.Play("jump", 1f, -10f);
                }
            }
            _velocity.y -= Gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
            Footsteps(grounded);

            if (transform.position.y < WaterSurface.Level - 0.6f)
            {
                Sfx.PlayAt("splash_big", transform.position);
                NetSession.SendEvent("splash", 0, 0f, false, "");
                Fishing.ResetLine();
                Respawn();
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
            float target = 0f;
            if (Fishing.State == Fishing.FishState.Charging) target = -35f * Fishing.Charge;
            else if (Fishing.State == Fishing.FishState.Fight) target = 12f + Fishing.Tension * 10f;
            var e = _rodPivot.localEulerAngles;
            float cur = Mathf.DeltaAngle(0, e.x);
            _rodPivot.localRotation = Quaternion.Euler(Mathf.Lerp(cur, target, 10f * Time.deltaTime), 0, 0);
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
