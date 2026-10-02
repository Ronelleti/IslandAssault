using UnityEngine;

namespace IslandAssault
{
    /// <summary>Turret logic for Cannon, Machine Gun, Sniper Tower and Mortar.</summary>
    public class Defense : MonoBehaviour
    {
        Building building;
        Troop target;
        float cooldown;
        float retarget;
        float recoil;
        float idlePhase;
        Transform cachedTurret;
        Vector3 turretBasePos;

        public void Init(Building b)
        {
            building = b;
            idlePhase = Random.value * 10f;
            cooldown = Random.Range(0.2f, 1f);
        }

        void Update()
        {
            if (building == null || building.destroyed || building.model == null || building.model.turret == null) return;
            var def = building.Def;
            var turret = building.model.turret;
            float dt = Time.deltaTime;

            if (turret != cachedTurret) { cachedTurret = turret; turretBasePos = turret.localPosition; recoil = 0f; }

            // recoil kick (backwards along the barrel)
            if (recoil > 0f)
            {
                recoil = Mathf.Max(0f, recoil - dt * 4f);
                turret.localPosition = turretBasePos - turret.localRotation * Vector3.forward * (recoil * 0.25f);
            }

            var am = AttackMode.I;
            bool combat = building.enemy && am != null && am.Fighting && building.level >= 1;
            if (!combat)
            {
                // idle scan
                float yaw = Mathf.Sin(Time.time * 0.35f + idlePhase) * 70f;
                turret.localRotation = Quaternion.Slerp(turret.localRotation, Quaternion.Euler(0, yaw, 0), dt * 2f);
                return;
            }

            retarget -= dt;
            if (target == null || target.dead || !InRange(target) || retarget <= 0f)
            {
                target = FindTarget(am);
                retarget = 0.5f;
            }
            cooldown -= dt;
            if (target == null) return;

            Vector3 dir = target.transform.position - turret.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion want = Quaternion.LookRotation(dir);
            turret.rotation = Quaternion.RotateTowards(turret.rotation, want, 260f * dt);
            if (cooldown <= 0f && Quaternion.Angle(turret.rotation, want) < 10f)
            {
                Fire(def);
                cooldown = def.fireInterval;
            }
        }

        bool InRange(Troop t)
        {
            var def = building.Def;
            Vector3 d = t.transform.position - building.Center;
            d.y = 0f;
            float dist = d.magnitude;
            return dist <= def.range && dist >= def.minRange;
        }

        Troop FindTarget(AttackMode am)
        {
            Troop best = null;
            float bestD = float.MaxValue;
            foreach (var t in am.troops)
            {
                if (t == null || t.dead || !t.landed) continue;
                if (!InRange(t)) continue;
                float d = (t.transform.position - building.Center).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        void Fire(BuildingDef def)
        {
            Vector3 from = building.model.muzzle != null ? building.model.muzzle.position : building.AimPoint;
            float dmg = GameData.Damage(building.type, building.level);
            recoil = 1f;
            switch (building.type)
            {
                case BuildingType.Cannon:
                    FX.MuzzleFlash(from, Quaternion.identity, 0.8f);
                    Projectile.Fire(ProjectileKind.Cannonball, from, target.AimPoint, target, null, dmg, def.projectileSpeed, 0f, true);
                    break;
                case BuildingType.MachineGun:
                    FX.MuzzleFlash(from, Quaternion.identity, 0.4f);
                    Projectile.Fire(ProjectileKind.Bullet, from, target.AimPoint, target, null, dmg, def.projectileSpeed, 0f, true);
                    recoil = 0.4f;
                    break;
                case BuildingType.Sniper:
                    FX.MuzzleFlash(from, Quaternion.identity, 0.35f);
                    Projectile.Fire(ProjectileKind.Tracer, from, target.AimPoint, target, null, dmg, def.projectileSpeed, 0f, true);
                    break;
                case BuildingType.Mortar:
                    FX.MuzzleFlash(from, Quaternion.identity, 0.9f);
                    // aim slightly ahead of where the troop is walking
                    Vector3 aim = target.transform.position + target.Velocity * 1.2f;
                    Projectile.Fire(ProjectileKind.Shell, from, aim, null, null, dmg, def.projectileSpeed, def.splash, true, 6f);
                    break;
            }
        }
    }
}
