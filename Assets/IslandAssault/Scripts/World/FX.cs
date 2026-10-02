using UnityEngine;
using UnityEngine.Rendering;

namespace IslandAssault
{
    /// <summary>Explosions, smoke, muzzle flashes, heals. Chunky low-poly particles to match the art style.</summary>
    public static class FX
    {
        static readonly Color Fire1 = new Color(1f, 0.75f, 0.2f);
        static readonly Color Fire2 = new Color(1f, 0.38f, 0.08f);
        static readonly Color Smoke = new Color(0.32f, 0.31f, 0.30f);
        static readonly Color SmokeLight = new Color(0.75f, 0.74f, 0.72f);

        static ParticleSystem MakeSystem(Vector3 pos, Material mat, int count, float lifeMin, float lifeMax, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float gravity, float radius, float upBias, float destroyAfter = 4f)
        {
            var go = new GameObject("FX");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(count, 10);

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = upBias > 0.5f ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            if (upBias > 0.5f) shape.rotation = new Vector3(-90f, 0f, 0f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Art.Ico;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            Object.Destroy(go, destroyAfter);
            return ps;
        }

        public static void Explosion(Vector3 pos, float scale = 1f)
        {
            MakeSystem(pos + Vector3.up * 0.3f, Art.Emissive(Fire1, 2.5f), Mathf.RoundToInt(10 * scale), 0.25f, 0.5f, 2f * scale, 5f * scale, 0.5f * scale, 1.1f * scale, 0f, 0.3f * scale, 1f);
            MakeSystem(pos + Vector3.up * 0.3f, Art.Emissive(Fire2, 2f), Mathf.RoundToInt(8 * scale), 0.3f, 0.6f, 1.5f * scale, 4f * scale, 0.6f * scale, 1.3f * scale, 0f, 0.4f * scale, 1f);
            MakeSystem(pos + Vector3.up * 0.5f, Art.Mat(Smoke, 0f), Mathf.RoundToInt(9 * scale), 0.8f, 1.6f, 0.8f, 2.2f * scale, 0.8f * scale, 1.6f * scale, -0.25f, 0.6f * scale, 1f);
            MakeSystem(pos + Vector3.up * 0.3f, Art.Mat(new Color(0.35f, 0.25f, 0.18f)), Mathf.RoundToInt(7 * scale), 0.8f, 1.3f, 4f * scale, 8f * scale, 0.12f, 0.3f * scale, 2.2f, 0.3f, 1f);
            if (CameraController.I != null) CameraController.I.Shake(0.25f * scale);
        }

        public static void SmallHit(Vector3 pos)
        {
            MakeSystem(pos, Art.Emissive(Fire1, 2f), 4, 0.12f, 0.25f, 1.5f, 3f, 0.15f, 0.3f, 0f, 0.1f, 0f, 1.5f);
            MakeSystem(pos, Art.Mat(SmokeLight, 0f), 3, 0.3f, 0.6f, 0.5f, 1.2f, 0.2f, 0.4f, -0.2f, 0.1f, 0f, 1.5f);
        }

        public static void Dust(Vector3 pos, float scale = 1f)
        {
            MakeSystem(pos, Art.Mat(new Color(0.85f, 0.78f, 0.62f), 0f), Mathf.RoundToInt(10 * scale), 0.5f, 1.0f, 1.5f * scale, 3f * scale, 0.4f * scale, 0.9f * scale, -0.1f, 0.8f * scale, 1f);
        }

        public static void Splash(Vector3 pos)
        {
            MakeSystem(new Vector3(pos.x, 0.1f, pos.z), Art.Mat(new Color(0.85f, 0.95f, 1f), 0.8f), 10, 0.5f, 0.9f, 3f, 6f, 0.2f, 0.45f, 2f, 0.3f, 1f);
        }

        public static void Heal(Vector3 pos, float radius)
        {
            MakeSystem(pos + Vector3.up * 0.3f, Art.Emissive(new Color(0.3f, 1f, 0.45f), 1.8f), 26, 0.8f, 1.4f, 1f, 2.5f, 0.2f, 0.45f, -0.4f, radius, 1f);
        }

        public static void Confetti(Vector3 pos)
        {
            MakeSystem(pos + Vector3.up * 2f, Art.Mat(Art.Gold, 0.6f, 0.5f), 14, 0.8f, 1.4f, 3f, 6f, 0.15f, 0.3f, 1.2f, 0.5f, 1f);
            MakeSystem(pos + Vector3.up * 2f, Art.Mat(new Color(0.3f, 0.7f, 1f), 0.5f), 10, 0.8f, 1.4f, 3f, 6f, 0.15f, 0.3f, 1.2f, 0.5f, 1f);
            MakeSystem(pos + Vector3.up * 0.3f, Art.Mat(SmokeLight, 0f), 8, 0.6f, 1.0f, 1f, 2.5f, 0.5f, 1.0f, -0.1f, 1.2f, 1f);
        }

        public static void MuzzleFlash(Vector3 pos, Quaternion rot, float size = 0.5f)
        {
            var t = Art.MeshPart(null, Art.Ico, pos, Vector3.one * size, Art.Emissive(Fire1, 3f), rot.eulerAngles, false);
            Object.Destroy(t.gameObject, 0.06f);
        }

        /// <summary>Persistent smoke column on destroyed buildings.</summary>
        public static GameObject SmokeColumn(Vector3 pos, float scale = 1f)
        {
            var go = new GameObject("SmokeColumn");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.1f * scale);
            main.gravityModifier = -0.06f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 5f * scale;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f * scale;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Art.Ico;
            r.sharedMaterial = Art.Mat(new Color(0.22f, 0.21f, 0.21f), 0f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return go;
        }
    }
}
