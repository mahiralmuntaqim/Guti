using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// The ocean. The same two-sine wave runs on the GPU (BrineTime/Water shader) and the CPU
    /// (HeightAt), both driven by _BrineWaveTime, so floating objects ride the waves you see.
    /// </summary>
    public class WaterSurface : MonoBehaviour
    {
        public const float Level = 0f;
        static readonly Vector2 Dir1 = new Vector2(0.857f, 0.514f);
        static readonly Vector2 Dir2 = new Vector2(-0.371f, 0.928f);
        const float Freq1 = 0.35f, Freq2 = 0.8f, Speed1 = 1.1f, Speed2 = 1.7f, Amp1 = 0.22f, Amp2 = 0.08f;
        const float Size = 360f;
        const float GridStep = 2f;

        static readonly int WaveTimeId = Shader.PropertyToID("_BrineWaveTime");

        public Transform Follow;

        /// <summary>Wave clock. Time.time is the fixed-step time inside FixedUpdate and the frame time
        /// inside Update, so physics and rendering each sample the wave at the right moment.</summary>
        public static float WaveTime => Time.time;

        public static float HeightAt(float x, float z)
        {
            float t = WaveTime;
            return Level + Amp1 * Mathf.Sin((Dir1.x * x + Dir1.y * z) * Freq1 + t * Speed1)
                         + Amp2 * Mathf.Sin((Dir2.x * x + Dir2.y * z) * Freq2 + t * Speed2);
        }

        void Awake()
        {
            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = Gfx.PlaneMesh(Size, Mathf.RoundToInt(Size / GridStep));
            var shader = Resources.Load<Shader>("Shaders/BrineWater") ?? Shader.Find("BrineTime/Water");
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(shader);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.position = new Vector3(0, Level, 0);
        }

        void Update()
        {
            Shader.SetGlobalFloat(WaveTimeId, WaveTime);
            if (Follow != null)
            {
                var p = Follow.position;
                transform.position = new Vector3(Mathf.Round(p.x / GridStep) * GridStep, Level, Mathf.Round(p.z / GridStep) * GridStep);
            }
        }
    }
}
