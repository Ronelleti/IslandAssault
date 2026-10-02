using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandAssault
{
    /// <summary>
    /// Materials and procedural meshes. Works with URP and the Built-in pipeline because it clones
    /// the pipeline's own default material instead of looking shaders up by name.
    /// </summary>
    public static class Art
    {
        static Material baseMat;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Mesh cubeMesh, sphereMesh, cylinderMesh, icoMesh, pyramidMesh, wedgeMesh;

        // Palette
        public static readonly Color Sand = new Color(0.96f, 0.86f, 0.62f);
        public static readonly Color Grass = new Color(0.42f, 0.74f, 0.30f);
        public static readonly Color GrassDark = new Color(0.30f, 0.60f, 0.24f);
        public static readonly Color Wood = new Color(0.62f, 0.42f, 0.24f);
        public static readonly Color WoodDark = new Color(0.42f, 0.27f, 0.15f);
        public static readonly Color Stone = new Color(0.62f, 0.62f, 0.60f);
        public static readonly Color StoneDark = new Color(0.40f, 0.41f, 0.42f);
        public static readonly Color Metal = new Color(0.30f, 0.32f, 0.36f);
        public static readonly Color Gold = new Color(1f, 0.80f, 0.18f);
        public static readonly Color Roof = new Color(0.80f, 0.36f, 0.24f);
        public static readonly Color Cream = new Color(0.95f, 0.91f, 0.80f);
        public static readonly Color Player = new Color(0.20f, 0.50f, 0.95f);
        public static readonly Color Enemy = new Color(0.85f, 0.18f, 0.18f);
        public static readonly Color Dirt = new Color(0.66f, 0.52f, 0.36f);

        static Material BaseMaterial
        {
            get
            {
                if (baseMat == null)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMat = g.GetComponent<Renderer>().sharedMaterial;
                    cubeMesh = g.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(g);
                }
                return baseMat;
            }
        }

        public static Material Mat(Color c, float smooth = 0.15f, float metal = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + "_" + smooth.ToString("0.00") + "_" + metal.ToString("0.00");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseMaterial);
            m.name = "IA_" + key;
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        public static Material Emissive(Color c, float intensity = 2f)
        {
            string key = "EM_" + ColorUtility.ToHtmlStringRGBA(c) + intensity.ToString("0.0");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseMaterial);
            SetColor(m, c);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
            cache[key] = m;
            return m;
        }

        public static Material Transparent(Color c, float smooth = 0.6f)
        {
            string key = "TR_" + ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseMaterial);
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            MakeTransparent(m);
            cache[key] = m;
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface"))
            {
                // URP Lit
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                // Built-in Standard
                m.SetFloat("_Mode", 3f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        // ---------------- Meshes ----------------

        public static Mesh Cube { get { if (cubeMesh == null) { var b = BaseMaterial; } return cubeMesh; } }

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t);
            var mesh = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
            return mesh;
        }

        public static Mesh GetMesh(PrimitiveType t)
        {
            switch (t)
            {
                case PrimitiveType.Cube: return Cube;
                case PrimitiveType.Sphere: if (sphereMesh == null) sphereMesh = PrimitiveMesh(t); return sphereMesh;
                case PrimitiveType.Cylinder: if (cylinderMesh == null) cylinderMesh = PrimitiveMesh(t); return cylinderMesh;
                default: return PrimitiveMesh(t);
            }
        }

        /// <summary>Low-poly ball (icosahedron), flat shaded. Size 1 (radius 0.5).</summary>
        public static Mesh Ico
        {
            get
            {
                if (icoMesh != null) return icoMesh;
                icoMesh = MakeIco(0, 0f);
                return icoMesh;
            }
        }

        public static Mesh MakeIco(int seed, float jitter)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            var rnd = new System.Random(seed);
            for (int i = 0; i < v.Count; i++)
            {
                float j = 1f + ((float)rnd.NextDouble() * 2f - 1f) * jitter;
                v[i] = v[i].normalized * 0.5f * j;
            }
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var mb = new MeshBuilder(1);
            for (int i = 0; i < f.Length; i += 3) mb.Tri(v[f[i]], v[f[i + 1]], v[f[i + 2]], 0);
            return mb.Build("Ico");
        }

        /// <summary>Square pyramid, base 1x1 at y=0, apex at y=1.</summary>
        public static Mesh Pyramid
        {
            get
            {
                if (pyramidMesh != null) return pyramidMesh;
                var mb = new MeshBuilder(1, MeshBuilder.Orient.Outward, new Vector3(0, 0.3f, 0));
                Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f);
                Vector3 top = new Vector3(0, 1, 0);
                mb.Tri(a, top, b, 0); mb.Tri(b, top, c, 0); mb.Tri(c, top, d, 0); mb.Tri(d, top, a, 0);
                mb.Tri(a, b, c, 0); mb.Tri(a, c, d, 0);
                pyramidMesh = mb.Build("Pyramid");
                return pyramidMesh;
            }
        }

        /// <summary>Gable roof (triangular prism), base 1x1 at y=0, ridge along X at y=1.</summary>
        public static Mesh Wedge
        {
            get
            {
                if (wedgeMesh != null) return wedgeMesh;
                var mb = new MeshBuilder(1, MeshBuilder.Orient.Outward, new Vector3(0, 0.4f, 0));
                Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f);
                Vector3 r1 = new Vector3(-0.5f, 1, 0), r2 = new Vector3(0.5f, 1, 0);
                mb.Quad(a, r1, r2, b, 0);       // front slope
                mb.Quad(c, r2, r1, d, 0);       // back slope
                mb.Tri(a, d, r1, 0);            // left gable
                mb.Tri(b, r2, c, 0);            // right gable
                mb.Quad(a, b, c, d, 0);         // bottom
                wedgeMesh = mb.Build("Wedge");
                return wedgeMesh;
            }
        }

        // ---------------- Part helpers ----------------

        public static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3), float smooth = 0.15f)
        {
            return MeshPart(parent, GetMesh(type), pos, scale, Mat(c, smooth), euler);
        }

        public static Transform MeshPart(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default(Vector3), bool shadows = true)
        {
            var g = new GameObject(mesh != null ? mesh.name : "Part");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return g.transform;
        }

        public static Transform Box(Transform p, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3))
        { return Part(p, PrimitiveType.Cube, pos, scale, c, euler); }

        public static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3))
        { return Part(p, PrimitiveType.Cylinder, pos, scale, c, euler); }

        public static Transform Ball(Transform p, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3))
        { return MeshPart(p, Ico, pos, scale, Mat(c), euler); }

        public static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g.transform;
        }

        public static void SetLayerShadows(GameObject root, bool cast)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        public static void Tint(GameObject root, Material mat)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        }
    }

    /// <summary>Builds flat-shaded meshes (every triangle gets its own vertices).</summary>
    public class MeshBuilder
    {
        public enum Orient { Outward, Up, None }

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int>[] subs;
        readonly Orient orient;
        readonly Vector3 center;

        /// <param name="orient">Outward: faces point away from 'center' (for closed convex shapes). Up: faces point up (terrain).</param>
        public MeshBuilder(int submeshes, Orient orient = Orient.Outward, Vector3 center = default(Vector3))
        {
            this.orient = orient;
            this.center = center;
            subs = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) subs[i] = new List<int>();
        }

        public int VertexCount { get { return verts.Count; } }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, int sub)
        {
            // In Unity the visible (front) side of a triangle has normal Cross(b - a, c - a).
            Vector3 n = Vector3.Cross(b - a, c - a);
            bool flip = false;
            if (orient == Orient.Up) flip = n.y < 0f;
            else if (orient == Orient.Outward) flip = Vector3.Dot(n, (a + b + c) / 3f - center) < 0f;
            if (flip) { Vector3 t = b; b = c; c = t; }

            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int sub)
        {
            Tri(a, b, c, sub);
            Tri(a, c, d, sub);
        }

        public Mesh Build(string name)
        {
            var m = new Mesh();
            m.name = name;
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
