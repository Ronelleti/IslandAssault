using UnityEngine;

namespace IslandAssault
{
    public class ModelRefs
    {
        public Transform root;
        public Transform turret;   // rotates around Y to aim (faces +Z)
        public Transform muzzle;   // where projectiles spawn
        public float height = 3f;  // used for UI placement
    }

    /// <summary>Builds every building out of simple shapes. Level changes size and adds details.</summary>
    public static class BuildingModels
    {
        public static ModelRefs Create(Transform parent, BuildingType type, int level, bool enemy)
        {
            level = Mathf.Clamp(level, 1, 5);
            var refs = new ModelRefs();
            var root = new GameObject("Model").transform;
            root.SetParent(parent, false);
            refs.root = root;
            Color team = enemy ? Art.Enemy : Art.Player;
            int size = GameData.Get(type).size;
            float w = size * GameData.CellSize;

            // Ground pad
            Art.Box(root, new Vector3(0, 0.05f, 0), new Vector3(w - 0.25f, 0.1f, w - 0.25f), new Color(0.78f, 0.68f, 0.50f));

            // A real 3D model dropped into Resources/IslandAssaultModels replaces the generated one
            var custom = ModelLibrary.FindBuilding(type, level, enemy);
            if (custom != null)
            {
                CustomModel(root, refs, custom, type, w, team);
                LevelPips(root, level, w);
                return refs;
            }

            // the building itself is drawn 20% larger than its pad so it reads well from the camera
            const float BodyScale = 1.2f;
            var body = Art.Pivot(root, "Body", Vector3.zero);
            body.localScale = Vector3.one * BodyScale;
            switch (type)
            {
                case BuildingType.HQ: HQ(body, refs, level, team); break;
                case BuildingType.GoldMine: GoldMine(body, refs, level, team); break;
                case BuildingType.Sawmill: Sawmill(body, refs, level, team); break;
                case BuildingType.Barracks: Barracks(body, refs, level, team); break;
                case BuildingType.Cannon: Cannon(body, refs, level, team); break;
                case BuildingType.MachineGun: MachineGun(body, refs, level, team); break;
                case BuildingType.Sniper: Sniper(body, refs, level, team); break;
                case BuildingType.Mortar: Mortar(body, refs, level, team); break;
            }
            refs.height *= BodyScale;
            LevelPips(root, level, w);
            return refs;
        }

        static void CustomModel(Transform root, ModelRefs refs, GameObject prefab, BuildingType type, float w, Color team)
        {
            var def = GameData.Get(type);
            var body = Art.Pivot(root, "Body", Vector3.zero);
            float h;
            var inst = ModelLibrary.Spawn(prefab, body, w * 0.92f, type == BuildingType.HQ ? 9f : 7f, out h);
            refs.height = h;

            if (def.isDefense)
            {
                // Find the part that should aim. Put it under a clean pivot so it rotates around the vertical axis.
                var part = ModelLibrary.FindChild(inst, "turret", "weapon", "head", "barrel", "gun");
                var pivot = new GameObject("Turret").transform;
                pivot.SetParent(body, false);
                if (part != null)
                {
                    pivot.position = new Vector3(body.position.x, part.position.y, body.position.z);
                    part.SetParent(pivot, true);
                }
                else
                {
                    // no separate turret part: the whole model turns
                    pivot.localPosition = Vector3.zero;
                    inst.SetParent(pivot, true);
                }
                refs.turret = pivot;
                var muzzle = ModelLibrary.FindChild(inst, "muzzle");
                if (muzzle == null)
                {
                    float y = part != null ? 0.2f : h * 0.6f;
                    muzzle = Art.Pivot(pivot, "Muzzle", new Vector3(0, y, w * 0.4f));
                }
                refs.muzzle = muzzle;
            }

            // small team flag so you can tell your buildings from the enemy's
            Flag(root, new Vector3(w * 0.5f - 0.35f, 0.1f, w * 0.5f - 0.35f), Mathf.Clamp(h * 0.6f, 1.4f, 3f), team);
        }

        static void LevelPips(Transform root, int level, float w)
        {
            // little gold studs on the front edge of the pad show the level
            for (int i = 0; i < level; i++)
            {
                float x = (i - (level - 1) * 0.5f) * 0.32f;
                Art.Box(root, new Vector3(x, 0.14f, -w * 0.5f + 0.35f), new Vector3(0.2f, 0.08f, 0.2f), Art.Gold);
            }
        }

        static void Flag(Transform root, Vector3 pos, float height, Color team)
        {
            Art.Cyl(root, pos + new Vector3(0, height * 0.5f, 0), new Vector3(0.08f, height * 0.5f, 0.08f), Art.WoodDark);
            var flagPivot = Art.Pivot(root, "Flag", pos + new Vector3(0, height - 0.3f, 0));
            Art.Box(flagPivot, new Vector3(0.45f, 0, 0), new Vector3(0.85f, 0.5f, 0.04f), team);
            var s = flagPivot.gameObject.AddComponent<Sway>();
            s.amount = 9f; s.speed = 3f;
        }

        // ---------------- Buildings ----------------

        static void HQ(Transform r, ModelRefs refs, int lvl, Color team)
        {
            float s = 0.86f + lvl * 0.035f;
            var m = Art.Pivot(r, "HQ", Vector3.zero);
            m.localScale = Vector3.one * s;
            Art.Box(m, new Vector3(0, 0.25f, 0), new Vector3(5.0f, 0.3f, 5.0f), Art.Stone);
            Art.Box(m, new Vector3(0, 1.2f, 0), new Vector3(4.2f, 1.7f, 4.2f), Art.Cream);
            Art.Box(m, new Vector3(0, 2.1f, 0), new Vector3(4.5f, 0.22f, 4.5f), Art.WoodDark);
            float top = 2.2f;
            if (lvl >= 2)
            {
                Art.Box(m, new Vector3(0, 2.85f, 0), new Vector3(3.3f, 1.3f, 3.3f), Art.Cream);
                Art.Box(m, new Vector3(0, 3.55f, 0), new Vector3(3.6f, 0.2f, 3.6f), Art.WoodDark);
                top = 3.65f;
                for (int i = -1; i <= 1; i += 2)
                    Art.Box(m, new Vector3(i * 0.8f, 2.9f, -1.66f), new Vector3(0.5f, 0.6f, 0.05f), new Color(0.25f, 0.45f, 0.65f));
            }
            Art.MeshPart(m, Art.Pyramid, new Vector3(0, top, 0), new Vector3(lvl >= 2 ? 3.9f : 4.9f, 1.7f, lvl >= 2 ? 3.9f : 4.9f), Art.Mat(team, 0.3f));
            Art.Box(m, new Vector3(0, 0.95f, -2.11f), new Vector3(1.0f, 1.3f, 0.06f), Art.WoodDark);
            for (int i = -1; i <= 1; i += 2)
            {
                Art.Box(m, new Vector3(i * 1.4f, 1.4f, -2.11f), new Vector3(0.6f, 0.6f, 0.05f), new Color(0.25f, 0.45f, 0.65f));
                Art.Box(m, new Vector3(2.11f, 1.4f, i * 1.1f), new Vector3(0.05f, 0.6f, 0.6f), new Color(0.25f, 0.45f, 0.65f));
                Art.Box(m, new Vector3(-2.11f, 1.4f, i * 1.1f), new Vector3(0.05f, 0.6f, 0.6f), new Color(0.25f, 0.45f, 0.65f));
            }
            if (lvl >= 3)
            {
                // corner towers
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Art.Cyl(m, new Vector3(x * 2.25f, 1.4f, z * 2.25f), new Vector3(0.8f, 1.4f, 0.8f), Art.Stone);
                        Art.MeshPart(m, Art.Pyramid, new Vector3(x * 2.25f, 2.8f, z * 2.25f), new Vector3(1.0f, 0.9f, 1.0f), Art.Mat(team, 0.3f));
                    }
            }
            if (lvl >= 5) Art.Ball(m, new Vector3(0, top + 1.8f, 0), Vector3.one * 0.45f, Art.Gold);
            Flag(m, new Vector3(1.2f, top + 0.4f, 1.2f), 2.2f, team);
            refs.height = (top + 2.2f) * s;
        }

        static void GoldMine(Transform r, ModelRefs refs, int lvl, Color team)
        {
            Art.MeshPart(r, Art.MakeIco(11, 0.25f), new Vector3(0.3f, 0.7f, 0.6f), new Vector3(3.0f, 2.0f, 2.4f), Art.Mat(Art.StoneDark, 0.1f));
            Art.MeshPart(r, Art.MakeIco(12, 0.25f), new Vector3(-0.9f, 0.5f, 0.9f), new Vector3(1.6f, 1.3f, 1.6f), Art.Mat(Art.Stone, 0.1f));
            // mine entrance
            Art.Box(r, new Vector3(0.2f, 0.75f, -0.55f), new Vector3(1.2f, 1.2f, 0.3f), new Color(0.12f, 0.1f, 0.08f));
            Art.Box(r, new Vector3(-0.45f, 0.75f, -0.72f), new Vector3(0.18f, 1.4f, 0.18f), Art.Wood);
            Art.Box(r, new Vector3(0.85f, 0.75f, -0.72f), new Vector3(0.18f, 1.4f, 0.18f), Art.Wood);
            Art.Box(r, new Vector3(0.2f, 1.45f, -0.72f), new Vector3(1.6f, 0.2f, 0.22f), Art.Wood);
            // gold nuggets in the rock + a cart
            var gold = Art.Mat(Art.Gold, 0.8f, 0.6f);
            int n = 2 + lvl * 2;
            var rnd = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var p = new Vector3(0.3f + Mathf.Cos(a) * 1.2f, 0.6f + (float)rnd.NextDouble() * 0.9f, 0.6f + Mathf.Sin(a) * 1.0f);
                Art.MeshPart(r, Art.Ico, p, Vector3.one * (0.25f + (float)rnd.NextDouble() * 0.2f), gold);
            }
            var cart = Art.Pivot(r, "Cart", new Vector3(-1.1f, 0, -1.1f));
            Art.Box(cart, new Vector3(0, 0.55f, 0), new Vector3(0.9f, 0.5f, 0.7f), Art.WoodDark);
            Art.MeshPart(cart, Art.Ico, new Vector3(0, 0.85f, 0), new Vector3(0.7f, 0.35f, 0.55f), gold);
            for (int i = -1; i <= 1; i += 2)
                Art.Cyl(cart, new Vector3(i * 0.48f, 0.25f, 0), new Vector3(0.4f, 0.05f, 0.4f), Art.Metal, new Vector3(0, 0, 90));
            Flag(r, new Vector3(1.5f, 0.1f, -1.4f), 1.6f, team);
            refs.height = 2.4f;
        }

        static void Sawmill(Transform r, ModelRefs refs, int lvl, Color team)
        {
            Art.Box(r, new Vector3(0, 0.85f, 0.5f), new Vector3(3.0f, 1.5f, 2.0f), Art.Wood);
            Art.MeshPart(r, Art.Wedge, new Vector3(0, 1.6f, 0.5f), new Vector3(3.4f, 1.0f, 2.5f), Art.Mat(Art.Roof));
            Art.Box(r, new Vector3(-0.6f, 0.75f, -0.51f), new Vector3(0.8f, 1.1f, 0.05f), Art.WoodDark);
            // saw blade
            var saw = Art.Pivot(r, "Saw", new Vector3(0.9f, 0.9f, -0.75f));
            Art.Cyl(saw, Vector3.zero, new Vector3(0.9f, 0.03f, 0.9f), new Color(0.8f, 0.82f, 0.85f), new Vector3(0, 0, 90)).gameObject.AddComponent<Spin>().axis = new Vector3(0, 400f, 0);
            Art.Box(r, new Vector3(0.9f, 0.4f, -0.75f), new Vector3(0.9f, 0.5f, 0.5f), Art.WoodDark);
            // log pile grows with level
            int logs = 2 + lvl;
            for (int i = 0; i < logs; i++)
            {
                int row = i < 3 ? 0 : (i < 5 ? 1 : 2);
                int col = i < 3 ? i : (i < 5 ? i - 3 : i - 5);
                Art.Cyl(r, new Vector3(-1.45f + col * 0.42f + row * 0.21f, 0.3f + row * 0.36f, -1.1f), new Vector3(0.4f, 0.75f, 0.4f), i % 2 == 0 ? Art.Wood : new Color(0.7f, 0.5f, 0.3f), new Vector3(90, 0, 0));
            }
            Flag(r, new Vector3(1.5f, 0.1f, 1.5f), 1.8f, team);
            refs.height = 2.8f;
        }

        static void Barracks(Transform r, ModelRefs refs, int lvl, Color team)
        {
            // landing craft on a trailer
            var boat = Art.Pivot(r, "Boat", new Vector3(0.4f, 0.35f, 0));
            Art.Box(boat, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.7f, 3.0f), new Color(0.45f, 0.52f, 0.48f));
            Art.Box(boat, new Vector3(0, 0.82f, 0), new Vector3(1.7f, 0.08f, 3.1f), team);
            Art.Box(boat, new Vector3(0, 0.5f, 1.6f), new Vector3(1.5f, 0.6f, 0.15f), new Color(0.38f, 0.44f, 0.40f), new Vector3(-20, 0, 0));
            Art.Box(boat, new Vector3(0, 0.9f, -1.2f), new Vector3(0.6f, 0.5f, 0.5f), Art.Metal);
            for (int i = -1; i <= 1; i += 2)
                Art.Cyl(r, new Vector3(0.4f + i * 0.85f, 0.25f, 0), new Vector3(0.45f, 0.08f, 0.45f), Art.Metal, new Vector3(0, 0, 90));
            // tent
            Art.MeshPart(r, Art.Wedge, new Vector3(-1.25f, 0.1f, 0.5f), new Vector3(1.2f, 1.3f + lvl * 0.06f, 2.0f), Art.Mat(new Color(0.55f, 0.6f, 0.4f)), new Vector3(0, 90, 0));
            for (int i = 0; i < Mathf.Min(lvl, 4); i++)
                Art.Box(r, new Vector3(-1.4f + (i % 2) * 0.45f, 0.3f + (i / 2) * 0.4f, -1.3f), new Vector3(0.38f, 0.38f, 0.38f), Art.Wood);
            Flag(r, new Vector3(-1.5f, 0.1f, 1.5f), 2f, team);
            refs.height = 2.4f;
        }

        static void Cannon(Transform r, ModelRefs refs, int lvl, Color team)
        {
            Art.Cyl(r, new Vector3(0, 0.35f, 0), new Vector3(3.0f, 0.3f, 3.0f), Art.Stone);
            Art.Cyl(r, new Vector3(0, 0.68f, 0), new Vector3(3.15f, 0.05f, 3.15f), lvl >= 3 ? Art.Gold : Art.StoneDark);
            var t = Art.Pivot(r, "Turret", new Vector3(0, 0.7f, 0));
            Art.Cyl(t, new Vector3(0, 0.1f, 0), new Vector3(1.7f, 0.1f, 1.7f), Art.WoodDark);
            Art.Box(t, new Vector3(-0.55f, 0.5f, 0), new Vector3(0.25f, 0.7f, 1.0f), Art.Wood);
            Art.Box(t, new Vector3(0.55f, 0.5f, 0), new Vector3(0.25f, 0.7f, 1.0f), Art.Wood);
            float len = 0.95f + lvl * 0.08f;
            Art.Cyl(t, new Vector3(0, 0.62f, 0.55f), new Vector3(0.55f + lvl * 0.03f, len, 0.55f + lvl * 0.03f), Art.Metal, new Vector3(90, 0, 0));
            Art.Cyl(t, new Vector3(0, 0.62f, 0.55f + len - 0.1f), new Vector3(0.68f, 0.1f, 0.68f), lvl >= 4 ? Art.Gold : Art.StoneDark, new Vector3(90, 0, 0));
            Art.Ball(t, new Vector3(0, 0.62f, -0.4f), Vector3.one * 0.6f, Art.Metal);
            Art.Box(t, new Vector3(0, 0.3f, -0.7f), new Vector3(0.6f, 0.08f, 0.3f), team);
            refs.turret = t;
            refs.muzzle = Art.Pivot(t, "Muzzle", new Vector3(0, 0.62f, 0.55f + len + 0.1f));
            refs.height = 2.0f;
        }

        static void MachineGun(Transform r, ModelRefs refs, int lvl, Color team)
        {
            var bag = Art.Mat(new Color(0.78f, 0.70f, 0.50f), 0.05f);
            int bags = 10;
            for (int i = 0; i < bags; i++)
            {
                float a = i * Mathf.PI * 2f / bags;
                var p = new Vector3(Mathf.Cos(a) * 1.45f, 0.35f, Mathf.Sin(a) * 1.45f);
                Art.MeshPart(r, Art.Ico, p, new Vector3(0.95f, 0.5f, 0.6f), bag, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
                if (lvl >= 3) Art.MeshPart(r, Art.Ico, p + new Vector3(0, 0.38f, 0), new Vector3(0.85f, 0.45f, 0.55f), bag, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
            }
            var t = Art.Pivot(r, "Turret", new Vector3(0, 0.2f, 0));
            Art.Cyl(t, new Vector3(0, 0.4f, 0), new Vector3(0.25f, 0.4f, 0.25f), Art.Metal);
            Art.Box(t, new Vector3(0, 0.95f, 0.1f), new Vector3(0.7f, 0.4f, 0.9f), Art.Metal);
            Art.Box(t, new Vector3(0, 1.2f, -0.2f), new Vector3(0.75f, 0.12f, 0.4f), team);
            int barrels = lvl >= 4 ? 3 : 2;
            for (int i = 0; i < barrels; i++)
            {
                float x = (i - (barrels - 1) * 0.5f) * 0.2f;
                Art.Cyl(t, new Vector3(x, 0.98f, 0.95f), new Vector3(0.11f, 0.55f, 0.11f), new Color(0.2f, 0.2f, 0.22f), new Vector3(90, 0, 0));
            }
            // shield
            Art.Box(t, new Vector3(0, 1.0f, 0.62f), new Vector3(1.0f, 0.65f, 0.06f), new Color(0.35f, 0.42f, 0.36f));
            refs.turret = t;
            refs.muzzle = Art.Pivot(t, "Muzzle", new Vector3(0, 0.98f, 1.55f));
            refs.height = 1.8f;
        }

        static void Sniper(Transform r, ModelRefs refs, int lvl, Color team)
        {
            float h = 3.4f + lvl * 0.12f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Art.Box(r, new Vector3(x * 1.05f, h * 0.5f, z * 1.05f), new Vector3(0.22f, h, 0.22f), Art.Wood, new Vector3(-z * 4f, 0, x * 4f));
            Art.Box(r, new Vector3(0, h * 0.45f, 1.05f), new Vector3(2.2f, 0.12f, 0.12f), Art.WoodDark, new Vector3(0, 0, 35));
            Art.Box(r, new Vector3(0, h * 0.45f, -1.05f), new Vector3(2.2f, 0.12f, 0.12f), Art.WoodDark, new Vector3(0, 0, -35));
            Art.Box(r, new Vector3(0, h, 0), new Vector3(2.7f, 0.18f, 2.7f), Art.WoodDark);
            for (int i = 0; i < 4; i++)
            {
                var rail = Art.Pivot(r, "Rail", new Vector3(0, h + 0.35f, 0));
                rail.localRotation = Quaternion.Euler(0, i * 90, 0);
                Art.Box(rail, new Vector3(0, 0, 1.3f), new Vector3(2.7f, 0.5f, 0.08f), i == 0 ? team : Art.Wood);
            }
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Art.Box(r, new Vector3(x * 1.2f, h + 0.9f, z * 1.2f), new Vector3(0.12f, 1.8f, 0.12f), Art.Wood);
            Art.MeshPart(r, Art.Pyramid, new Vector3(0, h + 1.8f, 0), new Vector3(3.1f, 1.0f, 3.1f), Art.Mat(lvl >= 4 ? team : Art.Roof));
            var t = Art.Pivot(r, "Turret", new Vector3(0, h + 0.1f, 0));
            // little sniper
            Art.Box(t, new Vector3(0, 0.45f, -0.2f), new Vector3(0.45f, 0.6f, 0.35f), new Color(0.35f, 0.4f, 0.3f));
            Art.Ball(t, new Vector3(0, 0.95f, -0.2f), Vector3.one * 0.32f, new Color(0.95f, 0.78f, 0.62f));
            Art.Ball(t, new Vector3(0, 1.05f, -0.2f), new Vector3(0.36f, 0.18f, 0.36f), team);
            Art.Cyl(t, new Vector3(0.12f, 0.65f, 0.45f), new Vector3(0.07f, 0.7f, 0.07f), new Color(0.15f, 0.15f, 0.15f), new Vector3(90, 0, 0));
            refs.turret = t;
            refs.muzzle = Art.Pivot(t, "Muzzle", new Vector3(0.12f, 0.65f, 1.15f));
            refs.height = h + 2.8f;
        }

        static void Mortar(Transform r, ModelRefs refs, int lvl, Color team)
        {
            Art.Cyl(r, new Vector3(0, 0.2f, 0), new Vector3(3.1f, 0.2f, 3.1f), Art.StoneDark);
            var bag = Art.Mat(new Color(0.78f, 0.70f, 0.50f), 0.05f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                Art.MeshPart(r, Art.Ico, new Vector3(Mathf.Cos(a) * 1.5f, 0.5f, Mathf.Sin(a) * 1.5f), new Vector3(1.0f, 0.55f, 0.6f), bag, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
            }
            var t = Art.Pivot(r, "Turret", new Vector3(0, 0.4f, 0));
            Art.Cyl(t, new Vector3(0, 0.1f, 0), new Vector3(1.4f, 0.1f, 1.4f), Art.Metal);
            var tube = Art.Pivot(t, "Tube", new Vector3(0, 0.3f, 0));
            tube.localRotation = Quaternion.Euler(50f, 0, 0);
            float d = 0.75f + lvl * 0.05f;
            Art.Cyl(tube, new Vector3(0, 0.6f, 0), new Vector3(d, 0.75f, d), new Color(0.25f, 0.27f, 0.3f));
            Art.Cyl(tube, new Vector3(0, 1.3f, 0), new Vector3(d + 0.12f, 0.1f, d + 0.12f), lvl >= 3 ? Art.Gold : team);
            Art.Box(t, new Vector3(0, 0.4f, 0.3f), new Vector3(0.12f, 0.6f, 0.12f), Art.Metal, new Vector3(20, 0, 0));
            // ammo crates
            Art.Box(r, new Vector3(-1.0f, 0.45f, -0.9f), new Vector3(0.5f, 0.4f, 0.4f), Art.WoodDark);
            Art.Box(r, new Vector3(-0.5f, 0.45f, -1.1f), new Vector3(0.5f, 0.4f, 0.4f), Art.Wood);
            refs.turret = t;
            refs.muzzle = Art.Pivot(tube, "Muzzle", new Vector3(0, 1.45f, 0));
            refs.height = 2.0f;
        }

        // ---------------- Special states ----------------

        public static Transform Scaffold(Transform parent, int size)
        {
            float w = size * GameData.CellSize - 0.6f;
            var root = new GameObject("Scaffold").transform;
            root.SetParent(parent, false);
            float h = 2.4f + size * 0.3f;
            var wood = new Color(0.82f, 0.62f, 0.32f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Art.Box(root, new Vector3(x * w * 0.5f, h * 0.5f, z * w * 0.5f), new Vector3(0.14f, h, 0.14f), wood);
            for (int k = 1; k <= 2; k++)
            {
                float y = h * k / 2.2f;
                Art.Box(root, new Vector3(0, y, w * 0.5f), new Vector3(w, 0.1f, 0.1f), wood);
                Art.Box(root, new Vector3(0, y, -w * 0.5f), new Vector3(w, 0.1f, 0.1f), wood);
                Art.Box(root, new Vector3(w * 0.5f, y, 0), new Vector3(0.1f, 0.1f, w), wood);
                Art.Box(root, new Vector3(-w * 0.5f, y, 0), new Vector3(0.1f, 0.1f, w), wood);
            }
            Art.Box(root, new Vector3(0, h * 0.5f, -w * 0.5f), new Vector3(0.08f, h * 1.1f, 0.08f), wood, new Vector3(0, 0, 40));
            // hazard stripes board
            Art.Box(root, new Vector3(0, 0.55f, -w * 0.5f - 0.1f), new Vector3(1.4f, 0.35f, 0.06f), new Color(1f, 0.82f, 0.1f));
            Art.Box(root, new Vector3(0.6f, 0.3f, w * 0.25f), new Vector3(0.6f, 0.5f, 0.6f), Art.WoodDark);
            Art.Box(root, new Vector3(-0.5f, 0.25f, w * 0.15f), new Vector3(0.5f, 0.4f, 0.5f), Art.Wood);
            return root;
        }

        public static Transform Rubble(Transform parent, int size)
        {
            float w = size * GameData.CellSize;
            var root = new GameObject("Rubble").transform;
            root.SetParent(parent, false);
            Art.Box(root, new Vector3(0, 0.05f, 0), new Vector3(w - 0.4f, 0.1f, w - 0.4f), new Color(0.25f, 0.23f, 0.21f));
            var rnd = new System.Random(size * 31 + (int)(parent.position.x * 10));
            int n = 4 + size * 3;
            for (int i = 0; i < n; i++)
            {
                float s = 0.3f + (float)rnd.NextDouble() * 0.7f;
                var p = new Vector3(((float)rnd.NextDouble() - 0.5f) * (w - 1f), s * 0.25f, ((float)rnd.NextDouble() - 0.5f) * (w - 1f));
                Color c = rnd.NextDouble() < 0.5 ? new Color(0.30f, 0.28f, 0.26f) : new Color(0.42f, 0.36f, 0.30f);
                Art.MeshPart(root, Art.Ico, p, new Vector3(s, s * 0.6f, s), Art.Mat(c), new Vector3(0, (float)rnd.NextDouble() * 360f, 0));
            }
            return root;
        }
    }

    public class Spin : MonoBehaviour
    {
        public Vector3 axis = new Vector3(0, 90f, 0);
        void Update() { transform.Rotate(axis * Time.deltaTime, Space.Self); }
    }

    public class Bob : MonoBehaviour
    {
        public float amount = 0.15f;
        public float speed = 2f;
        Vector3 basePos;
        float phase;
        void Start() { basePos = transform.localPosition; phase = Random.value * 6f; }
        void Update() { transform.localPosition = basePos + new Vector3(0, Mathf.Sin(Time.time * speed + phase) * amount, 0); }
    }
}
