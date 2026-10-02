using UnityEngine;

namespace IslandAssault
{
    public enum ProjectileKind { Bullet, Cannonball, Rocket, Shell, Tracer }

    /// <summary>Everything that flies: bullets, cannonballs, rockets, mortar and gunboat shells.</summary>
    public class Projectile : MonoBehaviour
    {
        ProjectileKind kind;
        Vector3 start, end;
        float t, duration, arc;
        Troop targetTroop;
        Building targetBuilding;
        float damage, splash;
        bool hitsTroops;
        Vector3 lastPos;

        public static Projectile Fire(ProjectileKind kind, Vector3 from, Vector3 to, Troop troop, Building building,
            float damage, float speed, float splash, bool hitsTroops, float arcHeight = 0f)
        {
            var go = new GameObject("Projectile_" + kind);
            go.transform.position = from;
            var p = go.AddComponent<Projectile>();
            p.kind = kind;
            p.start = from;
            p.end = to;
            p.targetTroop = troop;
            p.targetBuilding = building;
            p.damage = damage;
            p.splash = splash;
            p.hitsTroops = hitsTroops;
            float dist = Vector3.Distance(from, to);
            p.duration = Mathf.Max(0.05f, dist / Mathf.Max(1f, speed));
            p.arc = arcHeight;
            p.lastPos = from;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            switch (kind)
            {
                case ProjectileKind.Bullet:
                    Art.MeshPart(transform, Art.Ico, Vector3.zero, new Vector3(0.12f, 0.12f, 0.6f), Art.Emissive(new Color(1f, 0.85f, 0.3f), 2.5f), default(Vector3), false);
                    break;
                case ProjectileKind.Tracer:
                    Art.MeshPart(transform, Art.Ico, Vector3.zero, new Vector3(0.1f, 0.1f, 1.4f), Art.Emissive(new Color(1f, 0.95f, 0.6f), 3f), default(Vector3), false);
                    break;
                case ProjectileKind.Cannonball:
                    Art.MeshPart(transform, Art.Ico, Vector3.zero, Vector3.one * 0.45f, Art.Mat(new Color(0.12f, 0.12f, 0.14f), 0.6f), default(Vector3), false);
                    AddTrail(new Color(0.85f, 0.85f, 0.85f), 0.3f, 0.25f);
                    break;
                case ProjectileKind.Rocket:
                    Art.MeshPart(transform, Art.GetMesh(PrimitiveType.Cylinder), Vector3.zero, new Vector3(0.18f, 0.35f, 0.18f), Art.Mat(new Color(0.85f, 0.25f, 0.2f)), new Vector3(90, 0, 0), false);
                    Art.MeshPart(transform, Art.Ico, new Vector3(0, 0, -0.4f), Vector3.one * 0.25f, Art.Emissive(new Color(1f, 0.6f, 0.2f), 3f), default(Vector3), false);
                    AddTrail(new Color(0.9f, 0.9f, 0.9f), 0.5f, 0.3f);
                    break;
                case ProjectileKind.Shell:
                    Art.MeshPart(transform, Art.Ico, Vector3.zero, new Vector3(0.45f, 0.45f, 0.7f), Art.Mat(new Color(0.18f, 0.2f, 0.18f), 0.4f), default(Vector3), false);
                    AddTrail(new Color(0.8f, 0.8f, 0.8f), 0.6f, 0.35f);
                    break;
            }
        }

        void AddTrail(Color c, float time, float width)
        {
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.startWidth = width;
            tr.endWidth = 0f;
            tr.sharedMaterial = Art.Mat(c, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.minVertexDistance = 0.2f;
        }

        void Update()
        {
            // home in on moving targets (except lobbed shells which land where they were aimed)
            if (arc <= 0.01f)
            {
                if (targetTroop != null && !targetTroop.dead) end = targetTroop.AimPoint;
            }

            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(start, end, k) + Vector3.up * (arc * 4f * k * (1f - k));
            transform.position = pos;
            Vector3 vel = pos - lastPos;
            if (vel.sqrMagnitude > 0.00001f) transform.rotation = Quaternion.LookRotation(vel);
            lastPos = pos;

            if (t >= 1f) Impact();
        }

        void Impact()
        {
            var am = AttackMode.I;
            if (splash > 0f && am != null)
            {
                if (hitsTroops)
                {
                    foreach (var tr in am.troops)
                    {
                        if (tr == null || tr.dead) continue;
                        Vector3 d = tr.transform.position - end; d.y = 0;
                        if (d.magnitude <= splash) tr.TakeDamage(damage);
                    }
                }
                else
                {
                    foreach (var b in am.buildings)
                    {
                        if (b == null || b.destroyed) continue;
                        Vector3 d = b.Center - end; d.y = 0;
                        if (d.magnitude <= splash + b.Radius * 0.7f) b.TakeDamage(damage);
                    }
                }
            }
            else
            {
                if (hitsTroops && targetTroop != null && !targetTroop.dead) targetTroop.TakeDamage(damage);
                if (!hitsTroops && targetBuilding != null && !targetBuilding.destroyed) targetBuilding.TakeDamage(damage);
            }

            switch (kind)
            {
                case ProjectileKind.Bullet:
                case ProjectileKind.Tracer:
                    FX.SmallHit(end);
                    break;
                case ProjectileKind.Rocket:
                case ProjectileKind.Cannonball:
                    FX.Explosion(end, 0.55f);
                    break;
                case ProjectileKind.Shell:
                    if (end.y < 0.05f && am != null && am.island != null && am.island.HeightAt(end.x, end.z) < 0f) FX.Splash(end);
                    else FX.Explosion(end, splash > 0f ? Mathf.Clamp(splash * 0.45f, 0.6f, 1.4f) : 0.7f);
                    break;
            }

            Destroy(gameObject);
        }
    }
}
