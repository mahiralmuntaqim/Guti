using System.Collections.Generic;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Builds low-poly creature models in code from each creature's ModelDef.
    /// Models face +X (head forward); 1 unit = 1 metre at size 1.0; centred on the origin.
    /// </summary>
    public static class FishModel
    {
        const int Rings = 12, Sides = 10;

        class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                C.Add(col); C.Add(col); C.Add(col);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public void TriC(Vector3 a, Color ca, Vector3 b, Color cb, Vector3 c, Color cc)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                C.Add(ca); C.Add(cb); C.Add(cc);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public Mesh Build()
            {
                var m = new Mesh { name = "Creature" };
                m.SetVertices(V);
                m.SetColors(C);
                m.SetTriangles(T, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        public static GameObject Build(string id, float size = 1f, bool golden = false)
        {
            var c = Data.Creatures[id];
            var m = c.Model;
            Color col1 = c.Color, col2 = m.Color2;
            if (golden)
            {
                col1 = new Color(1f, 0.78f, 0.25f);
                col2 = new Color(1f, 0.92f, 0.55f);
            }
            var root = new GameObject("Fish_" + id);
            float L = c.Length;
            switch (m.Shape)
            {
                case "eel": Eel(root.transform, L, m, col1, col2); break;
                case "ray": Ray(root.transform, L, col1, col2); break;
                case "crab": Crab(root.transform, L, m, col1, col2); break;
                case "squid": Squid(root.transform, L, col1, col2); break;
                case "boot": Boot(root.transform, L, col1, col2); break;
                default: Fish(root.transform, L, m, col1, col2); break;
            }
            ApplyMaterials(root, golden, m.Extra == "glow");
            root.transform.localScale = Vector3.one * size;
            return root;
        }

        // ------------------------------------------------------------------ shapes

        static void Fish(Transform root, float L, ModelDef m, Color col1, Color col2)
        {
            float h = m.H * L, w = m.W * L;
            var b = new Builder();
            Lathe(b, L, h, w, m.Pattern, col1, col2, false);
            float tx = -L * 0.5f;
            Color fin = col1 * 0.85f;
            fin.a = 1f;
            if (m.Tail == "fork")
            {
                DoubleTri(b, new Vector3(tx + L * 0.04f, 0, 0), new Vector3(tx - L * 0.26f, h * 0.62f, 0), new Vector3(tx - L * 0.14f, 0, 0), fin);
                DoubleTri(b, new Vector3(tx + L * 0.04f, 0, 0), new Vector3(tx - L * 0.14f, 0, 0), new Vector3(tx - L * 0.26f, -h * 0.62f, 0), fin);
            }
            else if (m.Tail == "round")
            {
                var prev = new Vector3(tx - L * 0.05f, h * 0.5f, 0);
                for (int k = 1; k <= 6; k++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, k / 6f);
                    var p = new Vector3(tx - L * 0.05f - Mathf.Cos(a) * L * 0.16f, Mathf.Sin(a) * h * 0.5f, 0);
                    DoubleTri(b, new Vector3(tx + L * 0.04f, 0, 0), prev, p, fin);
                    prev = p;
                }
            }
            DoubleTri(b, new Vector3(-L * 0.12f, h * 0.46f, 0), new Vector3(L * 0.14f, h * 0.46f, 0), new Vector3(-L * 0.08f, h * 0.85f, 0), fin);
            foreach (float side in new[] { -1f, 1f })
                DoubleTri(b, new Vector3(L * 0.16f, -h * 0.15f, side * w * 0.45f), new Vector3(L * 0.02f, -h * 0.25f, side * w * 0.5f),
                    new Vector3(0, -h * 0.4f, side * w * 1.1f), fin);
            Finish(root, b);
            Eyes(root, new Vector3(L * 0.34f, h * 0.12f, w * 0.36f), L * 0.035f);

            switch (m.Extra)
            {
                case "whiskers":
                    foreach (float side in new[] { -1f, 1f })
                        for (int k = 0; k < 2; k++)
                            Gfx.Stick(root, new Vector3(L * 0.46f, -h * 0.05f, side * w * 0.2f),
                                new Vector3(L * 0.62f, -h * (0.35f + 0.25f * k), side * w * (1.2f + k)), L * 0.008f, col2 * 0.7f);
                    break;
                case "crown":
                    for (int k = 0; k < 5; k++)
                    {
                        float x = Mathf.Lerp(-L * 0.08f, L * 0.2f, k / 4f);
                        Gfx.Cylinder(root, 0f, L * 0.03f, L * 0.12f, new Vector3(x, h * 0.48f + L * 0.06f, 0), new Color(1f, 0.85f, 0.2f), false, 6);
                    }
                    break;
                case "lure":
                    var tip = new Vector3(L * 0.7f, h * 0.75f, 0);
                    Gfx.Stick(root, new Vector3(L * 0.3f, h * 0.42f, 0), tip, L * 0.01f, col1);
                    var bulb = Gfx.Sphere(root, tip, L * 0.06f, new Color(0.6f, 1f, 0.9f));
                    bulb.GetComponent<MeshRenderer>().sharedMaterial = Gfx.NewMat(new Color(0.6f, 1f, 0.9f), emission: new Color(0.4f, 1f, 0.85f) * 2.5f);
                    bulb.name = "KeepMaterial";
                    break;
                case "frown":
                    Gfx.Stick(root, new Vector3(L * 0.42f, -h * 0.02f, w * 0.3f), new Vector3(L * 0.47f, -h * 0.12f, w * 0.12f), L * 0.008f, new Color(0.1f, 0.1f, 0.1f));
                    break;
            }
        }

        static float Wiggle(float x, float L) => Mathf.Sin(x / L * Mathf.PI * 2f * 1.2f) * L * 0.05f;

        static void Eel(Transform root, float L, ModelDef m, Color col1, Color col2)
        {
            var b = new Builder();
            float h = m.H * L, w = m.W * L;
            Lathe(b, L, h, w, m.Pattern, col1, col2, true);
            for (int k = 0; k < 8; k++)
            {
                float x0 = Mathf.Lerp(-L * 0.5f, L * 0.25f, k / 8f), x1 = Mathf.Lerp(-L * 0.5f, L * 0.25f, (k + 1) / 8f), xm = (x0 + x1) * 0.5f;
                DoubleTri(b, new Vector3(x0, h * 0.45f, Wiggle(x0, L)), new Vector3(x1, h * 0.45f, Wiggle(x1, L)), new Vector3(xm, h * 1.1f, Wiggle(xm, L)), col2);
            }
            Finish(root, b);
            Eyes(root, new Vector3(L * 0.44f, h * 0.15f, w * 0.4f + Wiggle(L * 0.44f, L)), L * 0.015f);
        }

        static void Ray(Transform root, float L, Color col1, Color col2)
        {
            var b = new Builder();
            float span = L * 0.55f;
            var nose = new Vector3(L * 0.35f, 0, 0);
            var back = new Vector3(-L * 0.2f, 0, 0);
            var top = new Vector3(0.05f * L, L * 0.07f, 0);
            var bottom = new Vector3(0.05f * L, -L * 0.03f, 0);
            foreach (var wing in new[] { new Vector3(0, 0.01f, -span), new Vector3(0, 0.01f, span) })
            {
                foreach (var pair in new[] { (nose, wing), (wing, back) })
                {
                    DoubleTri(b, top, pair.Item1, pair.Item2, col1);
                    DoubleTri(b, bottom, pair.Item2, pair.Item1, col2);
                }
            }
            Finish(root, b);
            Gfx.Stick(root, back, back + new Vector3(-L * 0.55f, 0, 0), L * 0.012f, col1 * 0.8f);
            Eyes(root, new Vector3(L * 0.16f, L * 0.06f, L * 0.06f), L * 0.02f);
        }

        static void Crab(Transform root, float L, ModelDef m, Color col1, Color col2)
        {
            var body = Gfx.Sphere(root, Vector3.zero, L * 0.5f, col1);
            body.transform.localScale = new Vector3(0.8f, 0.45f, 1f) * L;
            Color leg = col1 * 0.85f;
            leg.a = 1f;
            foreach (float side in new[] { -1f, 1f })
            {
                for (int k = 0; k < 3; k++)
                {
                    var bs = new Vector3(Mathf.Lerp(-0.18f, 0.12f, k / 2f) * L, 0, side * L * 0.38f);
                    var knee = bs + new Vector3(0, L * 0.12f, side * L * 0.18f);
                    Gfx.Stick(root, bs, knee, L * 0.025f, leg);
                    Gfx.Stick(root, knee, knee + new Vector3(-0.04f * L, -L * 0.25f, side * L * 0.1f), L * 0.022f, leg);
                }
                var arm = new Vector3(L * 0.3f, 0, side * L * 0.3f);
                var claw = arm + new Vector3(L * 0.25f, L * 0.08f, side * L * 0.12f);
                Gfx.Stick(root, arm, claw, L * 0.035f, col1);
                var pincer = Gfx.Sphere(root, claw + new Vector3(L * 0.06f, 0, 0), L * 0.11f, col2);
                pincer.transform.localScale = new Vector3(1.4f, 0.8f, 0.8f) * L * 0.22f;
                Gfx.Stick(root, new Vector3(L * 0.25f, L * 0.1f, side * L * 0.08f), new Vector3(L * 0.3f, L * 0.24f, side * L * 0.1f), L * 0.015f, col1);
                Gfx.Sphere(root, new Vector3(L * 0.3f, L * 0.26f, side * L * 0.1f), L * 0.035f, new Color(0.05f, 0.05f, 0.05f));
            }
            if (m.Extra == "snorkel")
            {
                var blue = new Color(0.2f, 0.75f, 0.95f);
                Gfx.Stick(root, new Vector3(0, L * 0.2f, 0), new Vector3(0, L * 0.55f, 0), L * 0.03f, blue);
                Gfx.Stick(root, new Vector3(0, L * 0.55f, 0), new Vector3(L * 0.1f, L * 0.6f, 0), L * 0.03f, blue);
            }
        }

        static void Squid(Transform root, float L, Color col1, Color col2)
        {
            var mantle = Gfx.Cylinder(root, 0f, L * 0.16f, L * 0.55f, new Vector3(-L * 0.12f, 0, 0), col1, false, 10);
            mantle.transform.localRotation = Quaternion.Euler(0, 0, 90);  // point the tip backwards (-X)
            Gfx.Sphere(root, new Vector3(L * 0.2f, 0, 0), L * 0.14f, col1 * 1.1f);
            Eyes(root, new Vector3(L * 0.26f, L * 0.05f, L * 0.1f), L * 0.04f);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 2f / 8f;
                var start = new Vector3(L * 0.3f, Mathf.Cos(a) * L * 0.07f, Mathf.Sin(a) * L * 0.07f);
                var mid = start + new Vector3(L * 0.22f, Mathf.Cos(a) * L * 0.07f, Mathf.Sin(a) * L * 0.07f);
                var tip = mid + new Vector3(L * 0.2f, Mathf.Cos(a + 0.6f) * L * 0.05f, Mathf.Sin(a + 0.6f) * L * 0.05f);
                Gfx.Stick(root, start, mid, L * 0.022f, col2);
                Gfx.Stick(root, mid, tip, L * 0.014f, col2);
            }
        }

        static void Boot(Transform root, float L, Color col1, Color col2)
        {
            Gfx.Box(root, new Vector3(L * 0.3f, L * 0.55f, L * 0.3f), new Vector3(-L * 0.15f, L * 0.25f, 0), col1, false, new Vector3(0, 0, 4.5f));
            Gfx.Box(root, new Vector3(L * 0.75f, L * 0.18f, L * 0.32f), new Vector3(L * 0.12f, 0, 0), col1, false);
            Gfx.Box(root, new Vector3(L * 0.8f, L * 0.05f, L * 0.34f), new Vector3(L * 0.1f, -L * 0.1f, 0), col2, false);
            Gfx.Stick(root, new Vector3(-L * 0.15f, L * 0.5f, 0), new Vector3(-L * 0.05f, L * 0.75f, L * 0.05f), L * 0.02f, new Color(0.2f, 0.55f, 0.2f));
        }

        // ------------------------------------------------------------------ mesh helpers

        static void DoubleTri(Builder b, Vector3 a, Vector3 c1, Vector3 c2, Color col)
        {
            b.Tri(a, c1, c2, col);
            b.Tri(a, c2, c1, col);
        }

        static void Lathe(Builder b, float L, float h, float w, string pattern, Color col1, Color col2, bool curl)
        {
            var pts = new Vector3[Rings + 1, Sides];
            var cols = new Color[Rings + 1, Sides];
            for (int r = 0; r <= Rings; r++)
            {
                float u = (float)r / Rings;  // 0 = tail, 1 = nose
                float x = Mathf.Lerp(-L * 0.5f, L * 0.5f, u);
                float prof = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.04f, 0.96f, u)), 0.7f) * (0.55f + 0.45f * Mathf.SmoothStep(0f, 1f, u / 0.7f));
                for (int s = 0; s < Sides; s++)
                {
                    float a = Mathf.PI * 2f * s / Sides;
                    float y = Mathf.Cos(a) * h * 0.5f * prof;
                    float z = Mathf.Sin(a) * w * 0.5f * prof + (curl ? Wiggle(x, L) : 0f);
                    var col = col1;
                    if (pattern == "stripes" && Mathf.Sin(u * 5f * Mathf.PI * 2f) > 0.35f) col = col2;
                    if (pattern == "spots")
                    {
                        float hsh = Mathf.Abs(Mathf.Sin(r * 12.9898f + s * 78.233f) * 43758.5453f);
                        if (hsh - Mathf.Floor(hsh) > 0.72f) col = col2;
                    }
                    if (Mathf.Cos(a) < -0.35f && pattern != "plain") col = Color.Lerp(col, new Color(0.95f, 0.95f, 0.9f), 0.55f);
                    pts[r, s] = new Vector3(x, y, z);
                    cols[r, s] = col;
                }
            }
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int s2 = (s + 1) % Sides;
                    b.TriC(pts[r, s], cols[r, s], pts[r, s2], cols[r, s2], pts[r + 1, s], cols[r + 1, s]);
                    b.TriC(pts[r, s2], cols[r, s2], pts[r + 1, s2], cols[r + 1, s2], pts[r + 1, s], cols[r + 1, s]);
                }
        }

        static void Finish(Transform root, Builder b)
        {
            var go = Gfx.MeshObject(root, "Body", b.Build(), null, Vector3.zero, false);
            go.name = "VertexColored";
        }

        static void Eyes(Transform root, Vector3 pos, float radius)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var p = new Vector3(pos.x, pos.y, pos.z * side);
                Gfx.Sphere(root, p, radius, new Color(0.97f, 0.97f, 0.95f));
                Gfx.Sphere(root, p + new Vector3(radius * 0.35f, 0, side * radius * 0.6f), radius * 0.6f, new Color(0.03f, 0.03f, 0.05f));
            }
        }

        static void ApplyMaterials(GameObject root, bool golden, bool glow)
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.gameObject.name == "KeepMaterial") continue;
                bool vertexColored = mr.gameObject.name == "VertexColored";
                Color tint = vertexColored || mr.sharedMaterial == null ? Color.white : mr.sharedMaterial.color;
                Color? emission = null;
                if (golden) emission = new Color(1f, 0.7f, 0.2f) * 0.25f;
                else if (glow) emission = (vertexColored ? new Color(0.3f, 0.9f, 0.85f) : tint) * 0.35f;
                mr.sharedMaterial = Gfx.NewMat(tint, golden ? 0.65f : 0.45f, golden ? 0.75f : 0.05f, emission, vertexColored, true);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var col in root.GetComponentsInChildren<Collider>()) Object.Destroy(col);
        }
    }
}
