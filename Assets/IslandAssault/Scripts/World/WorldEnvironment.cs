using UnityEngine;
using UnityEngine.Rendering;

namespace IslandAssault
{
    /// <summary>
    /// Sun, sky, fog, animated ocean, sea floor and drifting clouds. Created once and kept between modes.
    /// </summary>
    public class WorldEnvironment : MonoBehaviour
    {
        public static WorldEnvironment I;
        public Light sun;

        public static WorldEnvironment Create()
        {
            var go = new GameObject("Environment");
            var env = go.AddComponent<WorldEnvironment>();
            I = env;
            env.Setup();
            return env;
        }

        void Setup()
        {
            // --- Sun ---
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var lg = new GameObject("Sun");
                sun = lg.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;

            // --- Ambient & fog ---
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.80f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.70f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.34f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.84f, 0.95f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 320f;
            QualitySettings.shadowDistance = 150f;

            // --- Ocean ---
            var water = new GameObject("Ocean");
            water.transform.SetParent(transform, false);
            water.AddComponent<Water>();

            // --- Sea floor (seen through the transparent water) ---
            var floor = Art.MeshPart(transform, Art.Cube, new Vector3(0, -3.4f, 0), new Vector3(600f, 0.2f, 600f), Art.Mat(new Color(0.05f, 0.30f, 0.45f), 0f), default(Vector3), false);
            floor.name = "SeaFloor";

            // --- Clouds ---
            var clouds = new GameObject("Clouds").transform;
            clouds.SetParent(transform, false);
            var rnd = new System.Random(42);
            for (int i = 0; i < 14; i++)
            {
                var c = Art.Pivot(clouds, "Cloud", new Vector3((float)rnd.NextDouble() * 260f - 130f, 26f + (float)rnd.NextDouble() * 8f, (float)rnd.NextDouble() * 260f - 130f));
                int puffs = rnd.Next(3, 6);
                for (int k = 0; k < puffs; k++)
                {
                    float s = 3f + (float)rnd.NextDouble() * 4f;
                    Art.MeshPart(c, Art.Ico, new Vector3(k * 3.2f - puffs * 1.6f, (float)rnd.NextDouble() * 1.5f, (float)rnd.NextDouble() * 3f), new Vector3(s * 1.3f, s * 0.75f, s), Art.Mat(new Color(1f, 1f, 1f), 0f), default(Vector3), true);
                }
                c.gameObject.AddComponent<Drift>();
            }
        }
    }

    public class Drift : MonoBehaviour
    {
        public float speed = 1.2f;
        void Update()
        {
            var p = transform.position;
            p.x += speed * Time.deltaTime;
            if (p.x > 140f) p.x = -140f;
            transform.position = p;
        }
    }

    /// <summary>Low-poly animated ocean. Vertices are moved on the CPU, so it works with any shader.</summary>
    public class Water : MonoBehaviour
    {
        public float size = 280f;
        public int segments = 56;
        public float amplitude = 0.13f;

        Mesh mesh;
        Vector3[] basePos;
        Vector3[] verts;

        void Start()
        {
            var mb = new MeshBuilder(1, MeshBuilder.Orient.Up);
            float step = size / segments;
            float h = size * 0.5f;
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < segments; j++)
                {
                    var a = new Vector3(-h + i * step, 0, -h + j * step);
                    var b = new Vector3(a.x + step, 0, a.z);
                    var c = new Vector3(a.x + step, 0, a.z + step);
                    var d = new Vector3(a.x, 0, a.z + step);
                    if (((i + j) & 1) == 0) mb.Quad(a, d, c, b, 0); else { mb.Tri(a, d, b, 0); mb.Tri(b, d, c, 0); }
                }
            mesh = mb.Build("Ocean");
            mesh.MarkDynamic();
            basePos = mesh.vertices;
            verts = new Vector3[basePos.Length];
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = Art.Transparent(new Color(0.10f, 0.66f, 0.82f, 0.60f), 0.92f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        void Update()
        {
            if (mesh == null) return;
            float t = Time.time;
            for (int i = 0; i < basePos.Length; i++)
            {
                var p = basePos[i];
                float y = Mathf.Sin(p.x * 0.18f + t * 1.1f) * amplitude
                        + Mathf.Sin(p.z * 0.23f + t * 0.8f) * amplitude * 0.8f
                        + Mathf.Sin((p.x + p.z) * 0.41f + t * 1.7f) * amplitude * 0.35f;
                verts[i] = new Vector3(p.x, y, p.z);
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }
    }
}
