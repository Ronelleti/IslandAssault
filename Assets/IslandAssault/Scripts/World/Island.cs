using System.Collections.Generic;
using UnityEngine;

namespace IslandAssault
{
    /// <summary>
    /// A procedural low-poly tropical island with a flat buildable plateau in the middle (the grid),
    /// a sandy beach all around, palms, rocks and bushes.
    /// </summary>
    public class Island : MonoBehaviour
    {
        public const float PlateauHeight = 0.8f;
        public const float Extent = 48f;

        public int seed;
        public bool enemy;
        float noiseOffset;
        MeshCollider meshCollider;
        public readonly List<Transform> decorations = new List<Transform>();

        public static Island Create(int seed, bool enemy)
        {
            var go = new GameObject(enemy ? "Enemy Island" : "Home Island");
            var island = go.AddComponent<Island>();
            island.seed = seed;
            island.enemy = enemy;
            island.noiseOffset = (seed % 1000) * 1.731f + 10f;
            island.BuildTerrain();
            island.BuildDecor();
            return island;
        }

        // ---------------- Height function ----------------

        public float EdgeDistance(float x, float z)
        {
            // Rounded-square distance so the square grid fits nicely inside
            float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 4f) + Mathf.Pow(Mathf.Abs(z), 4f), 0.25f);
            float n = Mathf.PerlinNoise(x * 0.045f + noiseOffset, z * 0.045f + noiseOffset * 0.7f);
            float edge = 27.5f + (n - 0.5f) * 6f;
            return d - edge;   // < 0 = on the plateau
        }

        public float HeightAt(float x, float z)
        {
            float s = EdgeDistance(x, z);
            float h;
            if (s <= 0f) return PlateauHeight;
            if (s < 2f) h = Mathf.Lerp(PlateauHeight, 0.32f, Mathf.SmoothStep(0f, 1f, s / 2f));
            else if (s < 9f) h = Mathf.Lerp(0.32f, -0.5f, (s - 2f) / 7f);
            else h = Mathf.Max(-3.2f, -0.5f - (s - 9f) * 0.38f);
            float bump = (Mathf.PerlinNoise(x * 0.23f + noiseOffset, z * 0.23f) - 0.5f) * 0.25f;
            return h + bump * Mathf.Clamp01(s / 2f);
        }

        public float GroundY(Vector3 p) { return Mathf.Max(HeightAt(p.x, p.z), -0.35f); }

        // ---------------- Grid helpers ----------------

        public static Vector3 CellToWorld(int gx, int gy, int size)
        {
            float half = GameData.GridSize * 0.5f;
            float x = (gx + size * 0.5f - half) * GameData.CellSize;
            float z = (gy + size * 0.5f - half) * GameData.CellSize;
            return new Vector3(x, PlateauHeight, z);
        }

        public static void WorldToCell(Vector3 p, out int gx, out int gy)
        {
            float half = GameData.GridSize * 0.5f;
            gx = Mathf.FloorToInt(p.x / GameData.CellSize + half);
            gy = Mathf.FloorToInt(p.z / GameData.CellSize + half);
        }

        public static bool InGrid(int gx, int gy) { return gx >= 0 && gy >= 0 && gx < GameData.GridSize && gy < GameData.GridSize; }

        /// <summary>Ray from the screen onto the island (or the sea at y=0 if it misses).</summary>
        public bool ScreenToGround(Camera cam, Vector2 screen, out Vector3 point)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            RaycastHit hit;
            if (meshCollider != null && meshCollider.Raycast(ray, out hit, 1000f))
            {
                point = hit.point;
                return true;
            }
            var plane = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        // ---------------- Terrain mesh ----------------

        void BuildTerrain()
        {
            int res = 84;
            float size = Extent * 2f;
            float step = size / res;
            var mb = new MeshBuilder(6, MeshBuilder.Orient.Up);
            var h = new float[res + 1, res + 1];
            var pts = new Vector3[res + 1, res + 1];
            for (int i = 0; i <= res; i++)
                for (int j = 0; j <= res; j++)
                {
                    float x = -Extent + i * step;
                    float z = -Extent + j * step;
                    // jitter the off-plateau vertices a bit for a hand-made low-poly look
                    float s = EdgeDistance(x, z);
                    if (s > 1f && i > 0 && j > 0 && i < res && j < res)
                    {
                        x += (Mathf.PerlinNoise(i * 0.7f + noiseOffset, j * 0.7f) - 0.5f) * step * 0.6f;
                        z += (Mathf.PerlinNoise(i * 0.7f, j * 0.7f + noiseOffset) - 0.5f) * step * 0.6f;
                    }
                    h[i, j] = HeightAt(x, z);
                    pts[i, j] = new Vector3(x, h[i, j], z);
                }

            for (int i = 0; i < res; i++)
                for (int j = 0; j < res; j++)
                {
                    Vector3 a = pts[i, j], b = pts[i + 1, j], c = pts[i + 1, j + 1], d = pts[i, j + 1];
                    bool alt = ((i + j) & 1) == 0;
                    if (alt) { AddTri(mb, a, c, b); AddTri(mb, a, d, c); }
                    else { AddTri(mb, a, d, b); AddTri(mb, b, d, c); }
                }

            var mesh = mb.Build("IslandTerrain");
            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            float e = enemy ? 0.93f : 1f;
            r.sharedMaterials = new Material[]
            {
                Art.Mat(new Color(0.98f, 0.88f, 0.64f), 0.05f),                 // 0 dry sand
                Art.Mat(new Color(0.50f * e, 0.74f, 0.34f), 0.05f),             // 1 grass
                Art.Mat(new Color(0.80f, 0.72f, 0.48f), 0.05f),                 // 2 grass edge
                Art.Mat(new Color(0.45f * e, 0.69f, 0.31f), 0.05f),             // 3 grass darker
                Art.Mat(new Color(0.55f * e, 0.77f, 0.37f), 0.05f),             // 4 grass lighter
                Art.Mat(new Color(0.86f, 0.80f, 0.58f), 0.1f)                   // 5 wet sand / seabed
            };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;

            // Grid overlay: only shown while placing or moving a building
            var plate = new GameObject("GridOverlay");
            plate.transform.SetParent(transform, false);
            var pmb = new MeshBuilder(2, MeshBuilder.Orient.Up);
            float half = GameData.GridSize * 0.5f * GameData.CellSize;
            for (int gx = 0; gx < GameData.GridSize; gx++)
                for (int gy = 0; gy < GameData.GridSize; gy++)
                {
                    float x0 = -half + gx * GameData.CellSize + 0.06f, z0 = -half + gy * GameData.CellSize + 0.06f;
                    float x1 = x0 + GameData.CellSize - 0.12f, z1 = z0 + GameData.CellSize - 0.12f;
                    float y = PlateauHeight + 0.03f;
                    pmb.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), (gx + gy) % 2);
                }
            plate.AddComponent<MeshFilter>().sharedMesh = pmb.Build("GridOverlay");
            var pr = plate.AddComponent<MeshRenderer>();
            pr.sharedMaterials = new Material[] { Art.Transparent(new Color(1f, 1f, 1f, 0.16f), 0f), Art.Transparent(new Color(1f, 1f, 1f, 0.09f), 0f) };
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gridOverlay = plate;
            plate.SetActive(false);
        }

        public GameObject gridOverlay;
        public void ShowGrid(bool show) { if (gridOverlay != null) gridOverlay.SetActive(show); }

        void AddTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c)
        {
            float avg = (a.y + b.y + c.y) / 3f;
            float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            int sub;
            if (avg > 0.62f)
            {
                // patchy low-poly grass: big soft patches + a little per-triangle variation
                Vector3 m = (a + b + c) / 3f;
                float patch = Mathf.PerlinNoise(m.x * 0.09f + noiseOffset, m.z * 0.09f - noiseOffset);
                float jitter = (Mathf.Sin(m.x * 12.9898f + m.z * 78.233f) * 43758.5453f) % 1f;
                float v = patch + Mathf.Abs(jitter) * 0.25f;
                sub = v < 0.45f ? 3 : (v < 0.78f ? 1 : 4);
            }
            else if (maxY > 0.5f) sub = 2;      // grass/sand transition
            else if (avg < 0.02f) sub = 5;      // wet sand + seabed (looks turquoise under the water)
            else sub = 0;                       // dry beach
            mb.Tri(a, b, c, sub);
        }

        // ---------------- Decorations ----------------

        void BuildDecor()
        {
            var rnd = new System.Random(seed * 7 + 3);
            var root = new GameObject("Decor").transform;
            root.SetParent(transform, false);

            // Palms around the edge of the plateau and on the beach
            int palms = 0, tries = 0;
            while (palms < 26 && tries < 2000)
            {
                tries++;
                float x = Rand(rnd, -40f, 40f), z = Rand(rnd, -40f, 40f);
                float s = EdgeDistance(x, z);
                if (s < -4f || s > 5.5f) continue;
                if (InsideGridArea(x, z, 1.5f)) continue;
                var p = Palm(root, new Vector3(x, HeightAt(x, z) - 0.05f, z), rnd);
                decorations.Add(p);
                palms++;
            }

            // Rocks on the beach and in the shallows
            for (int i = 0; i < 22; i++)
            {
                float x = Rand(rnd, -44f, 44f), z = Rand(rnd, -44f, 44f);
                float s = EdgeDistance(x, z);
                if (s < 1f || s > 12f || InsideGridArea(x, z, 1f)) { i--; tries++; if (tries > 4000) break; continue; }
                float sc = Rand(rnd, 0.6f, 2.2f);
                var mesh = Art.MakeIco(rnd.Next(), 0.35f);
                var rock = Art.MeshPart(root, mesh, new Vector3(x, HeightAt(x, z) + sc * 0.15f, z),
                    new Vector3(sc * Rand(rnd, 0.9f, 1.6f), sc * Rand(rnd, 0.6f, 1.0f), sc * Rand(rnd, 0.9f, 1.5f)),
                    Art.Mat(rnd.NextDouble() < 0.5 ? Art.Stone : Art.StoneDark, 0.1f), new Vector3(0, Rand(rnd, 0, 360), 0));
                decorations.Add(rock);
            }

            // Bushes and flowers on the grass strip around the grid
            for (int i = 0; i < 40; i++)
            {
                float x = Rand(rnd, -30f, 30f), z = Rand(rnd, -30f, 30f);
                if (EdgeDistance(x, z) > -1f || InsideGridArea(x, z, 1.2f)) { tries++; if (tries > 6000) break; i--; continue; }
                var b = Bush(root, new Vector3(x, PlateauHeight, z), rnd);
                decorations.Add(b);
            }

            // Wooden pier on the south side (home only)
            if (!enemy)
            {
                float z = -26f;
                while (z > -50f && HeightAt(6f, z) > -0.2f) z -= 1f;
                var pier = Art.Pivot(root, "Pier", new Vector3(6f, 0f, z));
                for (int k = 0; k < 6; k++)
                {
                    Art.Box(pier, new Vector3(0, 0.45f, -k * 1.4f + 3f), new Vector3(2.2f, 0.15f, 1.25f), k % 2 == 0 ? Art.Wood : Art.WoodDark);
                    Art.Cyl(pier, new Vector3(-1f, -0.2f, -k * 1.4f + 3f), new Vector3(0.22f, 0.7f, 0.22f), Art.WoodDark);
                    Art.Cyl(pier, new Vector3(1f, -0.2f, -k * 1.4f + 3f), new Vector3(0.22f, 0.7f, 0.22f), Art.WoodDark);
                }
            }
        }

        static bool InsideGridArea(float x, float z, float margin)
        {
            float half = GameData.GridSize * 0.5f * GameData.CellSize + margin;
            return Mathf.Abs(x) < half && Mathf.Abs(z) < half;
        }

        static float Rand(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        public static Transform Palm(Transform parent, Vector3 pos, System.Random rnd)
        {
            var root = Art.Pivot(parent, "Palm", pos);
            root.localRotation = Quaternion.Euler(0, Rand(rnd, 0, 360), 0);
            float lean = Rand(rnd, 5f, 22f);
            int segs = 5;
            float segH = Rand(rnd, 0.75f, 0.95f);
            Vector3 p = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            var trunkCol = new Color(0.55f, 0.40f, 0.25f);
            var trunkCol2 = new Color(0.47f, 0.34f, 0.21f);
            for (int i = 0; i < segs; i++)
            {
                rot = Quaternion.Euler(lean * (i + 1) / segs, 0, 0);
                float w = Mathf.Lerp(0.42f, 0.28f, i / (float)segs);
                Art.MeshPart(root, Art.GetMesh(PrimitiveType.Cylinder), p + rot * new Vector3(0, segH * 0.5f, 0), new Vector3(w, segH * 0.5f, w), Art.Mat(i % 2 == 0 ? trunkCol : trunkCol2), rot.eulerAngles);
                p += rot * new Vector3(0, segH, 0);
            }
            var crown = Art.Pivot(root, "Crown", p);
            var leaf = new Color(0.22f, 0.62f, 0.22f);
            var leaf2 = new Color(0.30f, 0.70f, 0.26f);
            int leaves = 7;
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * 360f / leaves + Rand(rnd, -10, 10);
                var l = Art.Pivot(crown, "Leaf", Vector3.zero);
                l.localRotation = Quaternion.Euler(0, yaw, 0);
                Art.Box(l, new Vector3(0, -0.05f, 0.9f), new Vector3(0.55f, 0.07f, 1.8f), i % 2 == 0 ? leaf : leaf2, new Vector3(18f, 0, 0));
                Art.Box(l, new Vector3(0, -0.55f, 2.3f), new Vector3(0.4f, 0.06f, 1.3f), i % 2 == 0 ? leaf2 : leaf, new Vector3(40f, 0, 0));
            }
            Art.Ball(crown, new Vector3(0.15f, -0.25f, 0.1f), Vector3.one * 0.32f, new Color(0.45f, 0.30f, 0.15f));
            Art.Ball(crown, new Vector3(-0.18f, -0.28f, 0.05f), Vector3.one * 0.3f, new Color(0.40f, 0.27f, 0.14f));
            root.gameObject.AddComponent<Sway>().amount = 1.5f;
            return root;
        }

        static Transform Bush(Transform parent, Vector3 pos, System.Random rnd)
        {
            var root = Art.Pivot(parent, "Bush", pos);
            int n = rnd.Next(2, 4);
            for (int i = 0; i < n; i++)
            {
                float s = Rand(rnd, 0.7f, 1.3f);
                Art.Ball(root, new Vector3(Rand(rnd, -0.5f, 0.5f), s * 0.35f, Rand(rnd, -0.5f, 0.5f)), new Vector3(s, s * 0.8f, s), i % 2 == 0 ? Art.GrassDark : new Color(0.25f, 0.55f, 0.22f));
            }
            if (rnd.NextDouble() < 0.5)
            {
                Color fc = rnd.NextDouble() < 0.5 ? new Color(1f, 0.4f, 0.5f) : new Color(1f, 0.9f, 0.3f);
                for (int i = 0; i < 3; i++)
                    Art.Ball(root, new Vector3(Rand(rnd, -0.6f, 0.6f), Rand(rnd, 0.5f, 0.8f), Rand(rnd, -0.6f, 0.6f)), Vector3.one * 0.18f, fc);
            }
            return root;
        }
    }

    /// <summary>Gentle wind sway for palms.</summary>
    public class Sway : MonoBehaviour
    {
        public float amount = 2f;
        public float speed = 0.8f;
        Quaternion baseRot;
        float phase;

        void Start()
        {
            baseRot = transform.localRotation;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t) * amount, 0, Mathf.Cos(t * 0.7f) * amount * 0.6f);
        }
    }
}
