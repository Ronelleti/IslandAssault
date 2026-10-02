using UnityEngine;

namespace IslandAssault
{
    /// <summary>
    /// An attacking soldier. Goes for the nearest building, follows flares, shoots when in range.
    /// </summary>
    public class Troop : MonoBehaviour
    {
        public TroopType type;
        public TroopDef def;
        public float hp, maxHp, damage;
        public bool dead;
        public bool landed;
        public Vector3 Velocity { get; private set; }

        Building target;
        float cooldown;
        int followedFlare = -1;
        float walkT;
        float scale = 1f;
        Transform body, legL, legR, gun, muzzle;
        WorldUI.Bar bar;
        Vector3 flareOffset;

        public Vector3 AimPoint { get { return transform.position + Vector3.up * 0.9f * scale; } }

        public static Troop Spawn(Transform parent, TroopType type, Vector3 pos, float mult)
        {
            var def = GameData.GetTroop(type);
            var go = new GameObject(def.name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<Troop>();
            t.type = type;
            t.def = def;
            t.maxHp = t.hp = def.hp * mult;
            t.damage = def.damage * mult;
            t.cooldown = Random.Range(0f, def.fireInterval);
            t.flareOffset = new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-1.2f, 1.2f));
            t.BuildModel();
            return t;
        }

        void BuildModel()
        {
            scale = type == TroopType.Heavy ? 1.35f : (type == TroopType.Rocketeer ? 1.05f : 1f);
            var root = Art.Pivot(transform, "Model", Vector3.zero);
            root.localScale = Vector3.one * scale;
            Color uniform = new Color(0.30f, 0.42f, 0.28f);
            Color skin = new Color(0.96f, 0.78f, 0.62f);
            Color boots = new Color(0.2f, 0.17f, 0.14f);

            legL = Art.Pivot(root, "LegL", new Vector3(-0.13f, 0.5f, 0));
            Art.Box(legL, new Vector3(0, -0.25f, 0), new Vector3(0.17f, 0.5f, 0.2f), boots);
            legR = Art.Pivot(root, "LegR", new Vector3(0.13f, 0.5f, 0));
            Art.Box(legR, new Vector3(0, -0.25f, 0), new Vector3(0.17f, 0.5f, 0.2f), boots);

            body = Art.Pivot(root, "Body", new Vector3(0, 0.5f, 0));
            Art.Box(body, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.55f, 0.32f), uniform);
            Art.Box(body, new Vector3(0, 0.3f, -0.17f), new Vector3(0.36f, 0.4f, 0.12f), def.color); // backpack in troop color
            Art.Ball(body, new Vector3(0, 0.78f, 0), Vector3.one * 0.36f, skin);
            Art.Ball(body, new Vector3(0, 0.88f, 0), new Vector3(0.42f, 0.22f, 0.42f), def.color);

            gun = Art.Pivot(body, "Gun", new Vector3(0.2f, 0.32f, 0.2f));
            switch (type)
            {
                case TroopType.Rifleman:
                    Art.Box(gun, new Vector3(0, 0, 0.25f), new Vector3(0.08f, 0.1f, 0.65f), new Color(0.2f, 0.18f, 0.16f));
                    muzzle = Art.Pivot(gun, "Muzzle", new Vector3(0, 0, 0.6f));
                    break;
                case TroopType.Heavy:
                    Art.Cyl(gun, new Vector3(-0.05f, 0, 0.3f), new Vector3(0.2f, 0.35f, 0.2f), Art.Metal, new Vector3(90, 0, 0));
                    Art.Box(gun, new Vector3(-0.05f, -0.05f, 0.0f), new Vector3(0.25f, 0.25f, 0.3f), new Color(0.2f, 0.2f, 0.22f));
                    muzzle = Art.Pivot(gun, "Muzzle", new Vector3(-0.05f, 0, 0.7f));
                    break;
                case TroopType.Rocketeer:
                    gun.localPosition = new Vector3(0.22f, 0.62f, 0f);
                    Art.Cyl(gun, new Vector3(0, 0, 0.1f), new Vector3(0.16f, 0.5f, 0.16f), new Color(0.35f, 0.4f, 0.3f), new Vector3(90, 0, 0));
                    Art.Cyl(gun, new Vector3(0, 0, 0.6f), new Vector3(0.2f, 0.04f, 0.2f), def.color, new Vector3(90, 0, 0));
                    muzzle = Art.Pivot(gun, "Muzzle", new Vector3(0, 0, 0.7f));
                    break;
            }
            Art.SetLayerShadows(root.gameObject, true);
        }

        public void TakeDamage(float dmg)
        {
            if (dead) return;
            hp -= dmg;
            if (bar == null && WorldUI.I != null)
                bar = WorldUI.I.CreateBar(transform, Vector3.up * (1.9f * scale), new Color(0.35f, 0.75f, 1f), 44, 8);
            if (bar != null) bar.Set(hp / maxHp);
            if (hp <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (dead) return;
            hp = Mathf.Min(maxHp, hp + amount);
            if (bar != null) bar.Set(hp / maxHp);
        }

        void Die()
        {
            dead = true;
            Velocity = Vector3.zero;
            if (bar != null && WorldUI.I != null) WorldUI.I.Remove(bar);
            bar = null;
            FX.SmallHit(AimPoint);
            FX.Dust(transform.position, 0.4f);
            // a little tombstone-ish helmet left behind
            var h = Art.Ball(transform.parent, transform.position + Vector3.up * 0.1f, new Vector3(0.42f, 0.2f, 0.42f) * scale, def.color);
            Destroy(h.gameObject, 6f);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (bar != null && WorldUI.I != null) WorldUI.I.Remove(bar);
        }

        void Update()
        {
            if (dead || !landed) return;
            var am = AttackMode.I;
            if (am == null || !am.Fighting) { Animate(false); return; }
            float dt = Time.deltaTime;

            // Flares override everything until reached
            if (am.flareActive && followedFlare != am.flareId)
            {
                Vector3 goal = am.flarePos + flareOffset;
                Vector3 to = goal - transform.position; to.y = 0f;
                if (to.magnitude < 0.6f) { followedFlare = am.flareId; target = null; }
                else { Move(to.normalized, am, dt, null); return; }
            }

            if (target == null || target.destroyed) target = am.NearestBuilding(transform.position);
            if (target == null) { Velocity = Vector3.zero; Animate(false); return; }

            Vector3 d = target.Center - transform.position;
            d.y = 0f;
            float dist = d.magnitude - target.Radius;
            if (dist > def.range)
            {
                Move(d.normalized, am, dt, target);
            }
            else
            {
                Velocity = Vector3.zero;
                Face(d, dt);
                Animate(false);
                cooldown -= dt;
                if (cooldown <= 0f)
                {
                    cooldown = def.fireInterval * Random.Range(0.9f, 1.1f);
                    Shoot();
                }
            }
        }

        void Move(Vector3 dir, AttackMode am, float dt, Building ignore)
        {
            Vector3 pos = transform.position;
            Vector3 v = dir * def.speed;

            // separation from friends
            foreach (var o in am.troops)
            {
                if (o == null || o == this || o.dead) continue;
                Vector3 off = pos - o.transform.position; off.y = 0f;
                float m = off.magnitude;
                if (m < 0.75f && m > 0.0001f) v += off / m * (0.75f - m) * 4f;
            }
            pos += v * dt;

            // don't walk through buildings
            foreach (var b in am.buildings)
            {
                if (b == null || b.destroyed || b == ignore) continue;
                Vector3 off = pos - b.Center; off.y = 0f;
                float r = b.Radius + 0.35f;
                if (off.sqrMagnitude < r * r)
                {
                    if (off.sqrMagnitude < 0.0001f) off = Vector3.right;
                    // slide around: push out + bias sideways
                    Vector3 side = Vector3.Cross(Vector3.up, off.normalized);
                    if (Vector3.Dot(side, dir) < 0f) side = -side;
                    pos = new Vector3(b.Center.x, pos.y, b.Center.z) + off.normalized * r + side * def.speed * dt * 0.5f;
                }
            }

            pos.y = am.island != null ? am.island.GroundY(pos) : pos.y;
            Velocity = (pos - transform.position) / Mathf.Max(dt, 0.0001f);
            transform.position = pos;
            Face(dir, dt);
            Animate(true);
        }

        void Face(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * dt);
        }

        void Animate(bool walking)
        {
            if (legL == null) return;
            if (walking)
            {
                walkT += Time.deltaTime * def.speed * 3.2f;
                float s = Mathf.Sin(walkT) * 35f;
                legL.localRotation = Quaternion.Euler(s, 0, 0);
                legR.localRotation = Quaternion.Euler(-s, 0, 0);
                body.localPosition = new Vector3(0, 0.5f + Mathf.Abs(Mathf.Cos(walkT)) * 0.06f, 0);
            }
            else
            {
                legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0, 0.5f, 0), Time.deltaTime * 10f);
            }
        }

        void Shoot()
        {
            Vector3 from = muzzle != null ? muzzle.position : AimPoint;
            Vector3 to = target.AimPoint + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.4f), Random.Range(-0.4f, 0.4f));
            switch (type)
            {
                case TroopType.Rifleman:
                    FX.MuzzleFlash(from, transform.rotation, 0.25f);
                    Projectile.Fire(ProjectileKind.Bullet, from, to, null, target, damage, def.projectileSpeed, 0f, false);
                    break;
                case TroopType.Heavy:
                    FX.MuzzleFlash(from, transform.rotation, 0.35f);
                    Projectile.Fire(ProjectileKind.Bullet, from, to, null, target, damage, def.projectileSpeed, 0f, false);
                    break;
                case TroopType.Rocketeer:
                    FX.MuzzleFlash(from, transform.rotation, 0.4f);
                    Projectile.Fire(ProjectileKind.Rocket, from, to, null, target, damage, def.projectileSpeed, 0f, false, 0.8f);
                    break;
            }
            if (gun != null) gun.localRotation = Quaternion.Euler(-8f, 0, 0);
            CancelInvoke("ResetGun");
            Invoke("ResetGun", 0.08f);
        }

        void ResetGun() { if (gun != null) gun.localRotation = Quaternion.identity; }
    }
}
