using System.Collections.Generic;
using UnityEngine;

namespace BrineTime
{
    /// <summary>Materials, procedural meshes and primitive builders shared by the world, fish and avatars.</summary>
    public static class Gfx
    {
        static Shader _lit;
        static readonly Dictionary<Color, Material> _cache = new Dictionary<Color, Material>();

        public static Shader LitShader => _lit != null ? _lit : (_lit = Resources.Load<Shader>("Shaders/BrineLit") ?? Shader.Find("BrineTime/Lit"));

        /// <summary>Shared opaque material for a flat colour (cached).</summary>
        public static Material Mat(Color c, float smooth = 0.15f)
        {
            if (_cache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(LitShader) { color = c };
            m.SetFloat("_Glossiness", smooth);
            _cache[c] = m;
            return m;
        }

        public static Material NewMat(Color c, float smooth = 0.15f, float metallic = 0f, Color? emission = null, bool vertexColors = false, bool doubleSided = false)
        {
            var m = new Material(LitShader) { color = c };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_UseVertexColor", vertexColors ? 1f : 0f);
            m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            if (emission.HasValue) m.SetColor("_EmissionColor", emission.Value);
            return m;
        }

        public static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>A mesh object with an optional matching collider.</summary>
        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, bool collide)
        {
            var go = Child(parent, name);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 size, Vector3 localPos, Color c, bool collide = true, Vector3? eulerDeg = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Box";
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (eulerDeg.HasValue) go.transform.localEulerAngles = eulerDeg.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return go;
        }

        public static GameObject Sphere(Transform parent, Vector3 localPos, float radius, Color c, bool collide = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Sphere";
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * radius * 2f;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return go;
        }

        /// <summary>Tapered cylinder (rTop 0 = cone), centred on localPos, along +Y.</summary>
        public static GameObject Cylinder(Transform parent, float rTop, float rBottom, float height, Vector3 localPos, Color c, bool collide = true, int segments = 24)
        {
            var mesh = CylinderMesh(rTop, rBottom, height, segments);
            var go = MeshObject(parent, "Cylinder", mesh, Mat(c), localPos, false);
            if (collide)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            return go;
        }

        /// <summary>A thin cylinder from a to b (local space of parent).</summary>
        public static GameObject Stick(Transform parent, Vector3 a, Vector3 b, float radius, Color c)
        {
            float len = Mathf.Max(Vector3.Distance(a, b), 0.001f);
            var go = MeshObject(parent, "Stick", CylinderMesh(radius, radius, len, 6), Mat(c), (a + b) * 0.5f, false);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return go;
        }

        static readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();

        public static Mesh CylinderMesh(float rTop, float rBottom, float height, int segments)
        {
            string key = $"{rTop:F3}|{rBottom:F3}|{height:F3}|{segments}";
            if (_meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float h = height * 0.5f;
            // sides (duplicated seam vertices so normals stay sharp at caps)
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cx = Mathf.Cos(a), sz = Mathf.Sin(a);
                verts.Add(new Vector3(cx * rBottom, -h, sz * rBottom));
                verts.Add(new Vector3(cx * rTop, h, sz * rTop));
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tris.AddRange(new[] { b0, t0, b1, b1, t0, t1 });
            }
            // caps
            void Cap(float y, float r, bool up)
            {
                if (r <= 0f) return;
                int centre = verts.Count;
                verts.Add(new Vector3(0, y, 0));
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
                for (int i = 0; i < segments; i++)
                {
                    if (up) tris.AddRange(new[] { centre, centre + 2 + i, centre + 1 + i });
                    else tris.AddRange(new[] { centre, centre + 1 + i, centre + 2 + i });
                }
            }
            Cap(h, rTop, true);
            Cap(-h, rBottom, false);
            var mesh = new Mesh { name = "Cyl" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat grid in XZ, centred on the origin (used for the ocean).</summary>
        public static Mesh PlaneMesh(float size, int cells)
        {
            var verts = new Vector3[(cells + 1) * (cells + 1)];
            var tris = new int[cells * cells * 6];
            float step = size / cells;
            for (int z = 0, v = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++, v++)
                    verts[v] = new Vector3(-size * 0.5f + x * step, 0, -size * 0.5f + z * step);
            for (int z = 0, t = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int i = z * (cells + 1) + x;
                    tris[t++] = i; tris[t++] = i + cells + 1; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + cells + 1; tris[t++] = i + cells + 2;
                }
            var mesh = new Mesh { name = "Ocean", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 4f, size));
            return mesh;
        }

        /// <summary>Removes all colliders under a hierarchy (for purely visual objects).</summary>
        public static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }
    }
}
