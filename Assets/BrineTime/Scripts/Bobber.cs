using UnityEngine;

namespace BrineTime
{
    /// <summary>The float at the end of the line. Rides the CPU wave and reports open water vs land.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Bobber : MonoBehaviour
    {
        public const float Radius = 0.11f;

        public bool InWater { get; private set; }
        public bool OnLand { get; private set; }
        public Rigidbody Body { get; private set; }

        int _contacts;

        public static Bobber Create(Vector3 pos)
        {
            var go = new GameObject("Bobber");
            go.transform.position = pos;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;  // smooth between physics ticks
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var col = go.AddComponent<SphereCollider>();
            col.radius = Radius;
            Gfx.Sphere(go.transform, Vector3.zero, Radius, new Color(0.9f, 0.15f, 0.1f));
            var band = Gfx.Cylinder(go.transform, Radius * 1.05f, Radius * 1.05f, Radius * 0.6f, Vector3.zero, new Color(0.95f, 0.95f, 0.95f), false, 12);
            band.name = "Band";
            return go.AddComponent<Bobber>();
        }

        void Awake() => Body = GetComponent<Rigidbody>();

        void OnCollisionEnter(Collision c) => _contacts++;
        void OnCollisionExit(Collision c) => _contacts = Mathf.Max(0, _contacts - 1);

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            OnLand = _contacts > 0;
            var p = transform.position;
            float depth = WaterSurface.HeightAt(p.x, p.z) - p.y + Radius * 0.4f;
            InWater = depth > 0f && !OnLand;
            if (depth > 0f)
            {
                // buoyancy grows with depth; heavy damping makes it settle like a real float
                Body.AddForce(Vector3.up * Body.mass * 9.81f * (1f + Mathf.Min(depth, 0.5f) * 14f));
                Body.linearDamping = 5f;
                Body.angularDamping = 5f;
            }
            else
            {
                Body.linearDamping = 0.05f;
                Body.angularDamping = 0.5f;
            }
        }

        public void Tug(float strength = 0.5f) => Body.AddForce(Vector3.down * strength, ForceMode.Impulse);

        public void ReelToward(Vector3 target, float speed)
        {
            var to = target - transform.position;
            to.y = 0;
            if (to.sqrMagnitude < 0.0001f) return;
            var v = to.normalized * speed;
            Body.linearVelocity = new Vector3(v.x, Body.linearVelocity.y, v.z);
        }

        /// <summary>During a fight the bobber is driven directly (kinematic, still interpolated).</summary>
        public void Pin()
        {
            Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
        }
    }
}
