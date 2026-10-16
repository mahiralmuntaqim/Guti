using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Faint ring on the water where the cast will land while you charge (orange if it would hit land).
    /// Reads Fishing.AimPoint / AimOnWater, which Fishing predicts with the real launch velocity.
    /// </summary>
    public class CastAim : MonoBehaviour
    {
        const int Segments = 32;
        const float Radius = 0.6f;
        static readonly Color WaterColor = new Color(0.92f, 1f, 1f, 0.8f);
        static readonly Color LandColor = new Color(1f, 0.6f, 0.25f, 0.85f);

        Fishing _fishing;
        LineRenderer _ring;
        Vector3 _shown;
        float _alpha;

        void Start()
        {
            _fishing = GetComponent<Fishing>();
            var go = new GameObject("CastAim");
            _ring = go.AddComponent<LineRenderer>();
            _ring.positionCount = Segments;
            _ring.loop = true;
            _ring.useWorldSpace = true;
            _ring.startWidth = _ring.endWidth = 0.05f;
            _ring.material = new Material(Shader.Find("Sprites/Default"));
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.enabled = false;
        }

        void LateUpdate()
        {
            if (_fishing == null || _ring == null) return;
            bool charging = _fishing.State == Fishing.FishState.Charging && _fishing.Charge > 0.02f;
            _alpha = Mathf.MoveTowards(_alpha, charging ? 1f : 0f, Time.deltaTime * (charging ? 4f : 8f));
            _ring.enabled = _alpha > 0.01f;
            if (!_ring.enabled) return;
            var target = _fishing.AimPoint;
            // glide rather than jump between fixed-step predictions
            _shown = (_shown - target).sqrMagnitude > 25f ? target : Vector3.Lerp(_shown, target, 1f - Mathf.Exp(-20f * Time.deltaTime));
            var c = _fishing.AimOnWater ? WaterColor : LandColor;
            c.a *= _alpha;
            _ring.startColor = _ring.endColor = c;
            // keep the ring readable far out: thicker with distance from the eye
            float dist = Vector3.Distance(Camera.main != null ? Camera.main.transform.position : transform.position, _shown);
            _ring.startWidth = _ring.endWidth = 0.04f + 0.006f * dist;
            float r = Radius * (1f + 0.08f * Mathf.Sin(Time.time * 5f));
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float x = _shown.x + Mathf.Cos(a) * r, z = _shown.z + Mathf.Sin(a) * r;
                float y = _fishing.AimOnWater ? WaterSurface.HeightAt(x, z) + 0.04f : _shown.y + 0.04f;
                _ring.SetPosition(i, new Vector3(x, y, z));
            }
        }

        void OnDestroy()
        {
            if (_ring != null) Destroy(_ring.gameObject);
        }
    }
}
