using System;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Builds every island from Data.Islands: terrain, dock, Fish Market, General Store, Ferry and
    /// theme props (palms / coral / storm rocks + lighthouse). Islands sit far apart on one ocean.
    /// </summary>
    public class World : MonoBehaviour
    {
        public const float IslandRadius = 20f;
        public const float DockStart = 18f;
        public const float DockEnd = 36f;

        System.Random _rng;

        public static Vector3 SpawnPoint(int island) => Data.Islands[island].Origin + new Vector3(0, 1.3f, DockEnd - 2f);

        public static int IslandAt(Vector3 pos)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < Data.Islands.Length; i++)
            {
                float d = Vector3.Distance(pos, Data.Islands[i].Origin);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public void Build()
        {
            BuildSky();
            for (int i = 0; i < Data.Islands.Length; i++) BuildIsland(i);
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        void BuildSky()
        {
            var sky = Resources.Load<Material>("SkyMaterial");
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null)
                {
                    sky = new Material(shader);
                    sky.SetFloat("_AtmosphereThickness", 0.8f);
                    sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 0.9f));
                    sky.SetColor("_GroundColor", new Color(0.25f, 0.4f, 0.5f));
                    sky.SetFloat("_Exposure", 1.25f);
                }
            }
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.62f, 0.66f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.35f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.83f, 0.92f);
            RenderSettings.fogDensity = 0.006f;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform);
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            QualitySettings.shadowDistance = 80f;
        }

        void BuildIsland(int index)
        {
            var cfg = Data.Islands[index];
            var root = new GameObject(cfg.Name).transform;
            root.SetParent(transform);
            root.position = cfg.Origin;
            _rng = new System.Random(7 + index * 101);

            // terrain: sand body (top at y = 1), grass cap, a hill
            Gfx.Cylinder(root, IslandRadius, IslandRadius + 4f, 7f, new Vector3(0, -2.5f, 0), cfg.Sand, true, 48);
            Gfx.Cylinder(root, IslandRadius - 4.5f, IslandRadius - 3.5f, 0.3f, new Vector3(0, 1.1f, 0), cfg.Grass, true, 48);
            Gfx.Cylinder(root, 5f, 9f, 2.2f, new Vector3(-9, 2.3f, -4), cfg.Grass, true, 32);
            for (int i = 0; i < 7; i++)
            {
                float a = Rand(0, Mathf.PI * 2f), d = Rand(30f, 55f), r = Rand(0.8f, 2.5f) * (cfg.Theme == "storm" ? 1.6f : 1f);
                Gfx.Cylinder(root, r * 0.6f, r, r * 1.6f, new Vector3(Mathf.Cos(a) * d, r * 0.3f - 0.4f, Mathf.Sin(a) * d), cfg.Rock, true, 7);
            }

            BuildDock(root);
            BuildStall(root, Stall.Kind.Market, new Vector3(-6.5f, 1f, 10f), new Color(0.2f, 0.55f, 0.85f), "FISH MARKET");
            BuildStall(root, Stall.Kind.Store, new Vector3(6.5f, 1f, 10f), new Color(0.85f, 0.45f, 0.2f), "GENERAL STORE");
            BuildStall(root, Stall.Kind.Ferry, new Vector3(7f, 1f, -9f), new Color(0.9f, 0.9f, 0.95f), "FERRY");
            BuildFerryBoat(root, new Vector3(9f, 0f, -24f));
            Sign(root, cfg.Name.ToUpperInvariant(), new Vector3(0, 7.5f, 0), 1.8f);

            switch (cfg.Theme)
            {
                case "coral":
                    Scatter(root, 7, p => Palm(root, p, new Color(0.3f, 0.68f, 0.35f)));
                    for (int i = 0; i < 14; i++)
                    {
                        float a = Rand(0, Mathf.PI * 2f), d = Rand(IslandRadius + 1f, IslandRadius + 6f);
                        Coral(root, new Vector3(Mathf.Cos(a) * d, -0.3f, Mathf.Sin(a) * d));
                    }
                    Hut(root, new Vector3(-12, 1f, -12), new Color(1f, 0.75f, 0.8f));
                    break;
                case "storm":
                    Scatter(root, 8, p => Pine(root, p));
                    Lighthouse(root, new Vector3(10, 1f, -2));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Rand(0, Mathf.PI * 2f), d = Rand(IslandRadius - 6f, IslandRadius - 2f), r = Rand(1f, 2.2f);
                        Gfx.Cylinder(root, r * 0.5f, r, r * 1.5f, new Vector3(Mathf.Cos(a) * d, 1f + r * 0.6f, Mathf.Sin(a) * d), cfg.Rock, true, 6);
                    }
                    break;
                default:
                    Scatter(root, 9, p => Palm(root, p, new Color(0.2f, 0.55f, 0.22f)));
                    break;
            }
            Gfx.Box(root, Vector3.one * 0.8f, new Vector3(-9f, 1.4f, 11f), new Color(0.6f, 0.45f, 0.25f), true, new Vector3(0, 23, 0));
            Gfx.Box(root, Vector3.one * 0.6f, new Vector3(-9.1f, 2.1f, 11.1f), new Color(0.65f, 0.5f, 0.3f), true, new Vector3(0, -17, 0));
            Gfx.Cylinder(root, 0.35f, 0.35f, 0.9f, new Vector3(9.2f, 1.45f, 11f), new Color(0.35f, 0.25f, 0.15f), true, 12);
        }

        static readonly Vector3[] Reserved =
        {
            new Vector3(-6.5f, 1, 10), new Vector3(6.5f, 1, 10), new Vector3(7, 1, -9), new Vector3(-9, 1, -4),
            new Vector3(10, 1, -2), new Vector3(-12, 1, -12),
        };

        void Scatter(Transform root, int count, Action<Vector3> place)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries++ < 300)
            {
                float a = Rand(0, Mathf.PI * 2f), d = Rand(6f, IslandRadius - 3f);
                var p = new Vector3(Mathf.Cos(a) * d, 1f, Mathf.Sin(a) * d);
                if (Mathf.Abs(p.x) < 3f && p.z > 4f) continue;  // dock path
                bool blocked = false;
                foreach (var r in Reserved) if (Vector3.Distance(p, r) < 4.5f) blocked = true;
                if (blocked) continue;
                place(p);
                placed++;
            }
        }

        void BuildDock(Transform root)
        {
            float length = DockEnd - DockStart, mid = (DockStart + DockEnd) * 0.5f;
            var deck = Gfx.Box(root, new Vector3(3f, 0.25f, length), new Vector3(0, 1f, mid), new Color(0.55f, 0.38f, 0.22f));
            deck.name = "Dock";
            deck.AddComponent<Surface>().Kind = "wood";
            for (float z = DockStart + 1f; z <= DockEnd; z += 3f)
                foreach (float x in new[] { -1.35f, 1.35f })
                    Gfx.Cylinder(root, 0.12f, 0.12f, 3f, new Vector3(x, -0.4f, z), new Color(0.4f, 0.27f, 0.15f), false, 8);
            Gfx.Box(root, new Vector3(1.6f, 0.12f, 0.5f), new Vector3(-0.8f, 1.5f, DockEnd - 0.8f), new Color(0.45f, 0.3f, 0.18f), false);
            var lampPos = new Vector3(1.35f, 2.8f, DockStart + 1f);
            Gfx.Cylinder(root, 0.06f, 0.06f, 1.8f, new Vector3(1.35f, 2f, DockStart + 1f), new Color(0.2f, 0.2f, 0.22f), false, 8);
            var bulb = Gfx.Sphere(root, lampPos, 0.14f, Color.white);
            bulb.GetComponent<MeshRenderer>().sharedMaterial = Gfx.NewMat(new Color(1f, 0.85f, 0.6f), emission: new Color(1f, 0.85f, 0.6f) * 2f);
            var lamp = Gfx.Child(root, "Lamp").AddComponent<Light>();
            lamp.transform.localPosition = lampPos;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.85f, 0.6f);
            lamp.range = 8f;
        }

        void BuildStall(Transform root, Stall.Kind kind, Vector3 pos, Color roof, string title)
        {
            var counter = Gfx.Box(root, new Vector3(3.2f, 1.1f, 1.2f), pos + new Vector3(0, 0.55f, 0), new Color(0.62f, 0.45f, 0.28f));
            counter.name = title;
            counter.AddComponent<Stall>().Type = kind;
            foreach (float x in new[] { -1.5f, 1.5f })
                Gfx.Box(root, new Vector3(0.15f, 2.8f, 0.15f), pos + new Vector3(x, 1.4f, -0.9f), new Color(0.45f, 0.32f, 0.2f), false);
            Gfx.Box(root, new Vector3(3.8f, 0.15f, 2.4f), pos + new Vector3(0, 2.85f, -0.4f), roof, false, new Vector3(-12, 0, 0));
            switch (kind)
            {
                case Stall.Kind.Market:
                    Gfx.Box(root, new Vector3(1f, 0.5f, 0.7f), pos + new Vector3(0.8f, 1.35f, -0.1f), new Color(0.8f, 0.92f, 1f), false);
                    break;
                case Stall.Kind.Store:
                    for (int i = 0; i < 4; i++)
                        Gfx.Cylinder(root, 0.12f, 0.12f, 0.3f, pos + new Vector3(-1.1f + i * 0.6f, 1.25f, 0), Color.HSVToRGB(i * 0.2f, 0.6f, 0.9f), false, 10);
                    break;
                case Stall.Kind.Ferry:
                    var ring = Gfx.Cylinder(root, 0.34f, 0.34f, 0.08f, pos + new Vector3(0, 1.9f, -0.82f), new Color(1f, 0.35f, 0.25f), false, 16);
                    ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    break;
            }
            Sign(root, title, pos + new Vector3(0, 3.4f, 0), 0.6f);
        }

        void Sign(Transform root, string text, Vector3 localPos, float scale)
        {
            var go = Gfx.Child(root, "Sign " + text);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 96;
            tm.characterSize = 0.05f * scale;
            tm.color = new Color(1f, 0.97f, 0.85f);
            tm.font = Ui.Font;
            go.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            go.AddComponent<Billboard>().YawOnly = true;
        }

        void BuildFerryBoat(Transform root, Vector3 pos)
        {
            var boat = Gfx.Child(root, "Ferry Boat");
            boat.transform.localPosition = pos;
            Gfx.Box(boat.transform, new Vector3(3.2f, 1f, 7f), new Vector3(0, 0.3f, 0), new Color(0.9f, 0.9f, 0.92f), false);
            Gfx.Box(boat.transform, new Vector3(3.25f, 0.2f, 7.05f), new Vector3(0, 0.55f, 0), new Color(0.2f, 0.4f, 0.75f), false);
            Gfx.Box(boat.transform, new Vector3(2.2f, 1.4f, 2.4f), new Vector3(0, 1.5f, -0.8f), new Color(0.95f, 0.95f, 0.85f), false);
            Gfx.Cylinder(boat.transform, 0.25f, 0.3f, 1.2f, new Vector3(0, 2.6f, -1.2f), new Color(0.85f, 0.25f, 0.2f), false, 12);
            boat.AddComponent<Bob>();
        }

        void Palm(Transform root, Vector3 b, Color leaf)
        {
            float h = Rand(4.5f, 6.5f);
            var lean = new Vector3(Rand(-8f, 8f), 0, Rand(-8f, 8f));
            var trunk = Gfx.Cylinder(root, 0.18f, 0.28f, h, b + new Vector3(0, h * 0.5f, 0), new Color(0.5f, 0.36f, 0.22f), true, 8);
            trunk.transform.localRotation = Quaternion.Euler(lean);
            var top = trunk.transform.localPosition + trunk.transform.localRotation * new Vector3(0, h * 0.5f, 0);
            for (int i = 0; i < 6; i++)
            {
                var pivot = Gfx.Child(root, "Leaf");
                pivot.transform.localPosition = top;
                pivot.transform.localRotation = Quaternion.Euler(0, i * 60f, 0) * Quaternion.Euler(25f, 0, 0);
                Gfx.Box(pivot.transform, new Vector3(0.5f, 0.05f, 2.6f), new Vector3(0, 0, 1.1f), leaf, false);
            }
        }

        void Pine(Transform root, Vector3 b)
        {
            float h = Rand(4f, 6f);
            Gfx.Cylinder(root, 0.15f, 0.22f, h * 0.4f, b + new Vector3(0, h * 0.2f, 0), new Color(0.35f, 0.25f, 0.18f), true, 8);
            for (int k = 0; k < 3; k++)
                Gfx.Cylinder(root, 0f, 1.6f - k * 0.4f, h * 0.45f, b + new Vector3(0, h * (0.45f + k * 0.2f), 0), new Color(0.18f, 0.32f, 0.25f), false, 8);
        }

        void Coral(Transform root, Vector3 b)
        {
            var col = Color.HSVToRGB(Mathf.Repeat(Rand(0.85f, 1.05f), 1f), 0.55f, 0.95f);
            int n = 3 + _rng.Next(3);
            for (int k = 0; k < n; k++)
            {
                float h = Rand(0.8f, 1.8f);
                var br = Gfx.Cylinder(root, 0.08f, 0.18f, h, b + new Vector3(Rand(-0.4f, 0.4f), h * 0.4f, Rand(-0.4f, 0.4f)), col, false, 6);
                br.transform.localRotation = Quaternion.Euler(Rand(-28f, 28f), 0, Rand(-28f, 28f));
            }
        }

        void Hut(Transform root, Vector3 b, Color wall)
        {
            Gfx.Box(root, new Vector3(4, 2.6f, 4), b + new Vector3(0, 1.3f, 0), wall);
            var roof = Gfx.Cylinder(root, 0f, 3.4f, 2f, b + new Vector3(0, 3.6f, 0), new Color(0.75f, 0.6f, 0.35f), false, 4);
            roof.transform.localRotation = Quaternion.Euler(0, 45, 0);
        }

        void Lighthouse(Transform root, Vector3 b)
        {
            for (int k = 0; k < 4; k++)
            {
                var col = k % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.85f, 0.2f, 0.2f);
                Gfx.Cylinder(root, 1.3f - k * 0.15f, 1.45f - k * 0.15f, 2.5f, b + new Vector3(0, 1.25f + k * 2.5f, 0), col, true, 16);
            }
            var glass = Gfx.Sphere(root, b + new Vector3(0, 11f, 0), 0.8f, Color.white);
            glass.GetComponent<MeshRenderer>().sharedMaterial = Gfx.NewMat(new Color(1f, 0.9f, 0.6f), emission: new Color(1f, 0.9f, 0.6f) * 3f);
            var lamp = Gfx.Child(root, "Lighthouse Lamp").AddComponent<Light>();
            lamp.transform.localPosition = b + new Vector3(0, 11f, 0);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.9f, 0.6f);
            lamp.intensity = 3f;
            lamp.range = 25f;
        }
    }

    /// <summary>Marks a collider's footstep surface ("wood", default sand).</summary>
    public class Surface : MonoBehaviour
    {
        public string Kind = "sand";
    }

    /// <summary>Makes a decorative object bob and sway on the waves.</summary>
    public class Bob : MonoBehaviour
    {
        float _baseY;
        void Start() => _baseY = transform.localPosition.y;

        void Update()
        {
            var p = transform.position;
            var lp = transform.localPosition;
            transform.localPosition = new Vector3(lp.x, _baseY + WaterSurface.HeightAt(p.x, p.z) * 0.8f, lp.z);
            float pitch = (WaterSurface.HeightAt(p.x, p.z + 2f) - WaterSurface.HeightAt(p.x, p.z - 2f)) * 0.15f * Mathf.Rad2Deg;
            float roll = (WaterSurface.HeightAt(p.x + 1.5f, p.z) - WaterSurface.HeightAt(p.x - 1.5f, p.z)) * 0.15f * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(pitch, 0, roll);
        }
    }

    /// <summary>Keeps text facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        public bool YawOnly;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var dir = transform.position - cam.transform.position;
            if (YawOnly) dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
