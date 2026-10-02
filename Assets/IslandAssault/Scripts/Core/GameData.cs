using System;
using System.Collections.Generic;
using UnityEngine;

namespace IslandAssault
{
    public enum BuildingType { HQ, GoldMine, Sawmill, Barracks, Cannon, MachineGun, Sniper, Mortar }
    public enum TroopType { Rifleman, Heavy, Rocketeer }

    public class BuildingDef
    {
        public BuildingType type;
        public string name;
        public string desc;
        public int size = 2;          // footprint in grid cells (size x size)
        public int maxLevel = 5;
        public int unlockHQ = 1;
        public bool isDefense;
        public bool isProducer;
        public bool producesGold;     // otherwise wood
        public float baseHp;
        public int baseGold;
        public int baseWood;

        // Defense stats
        public float range;
        public float minRange;
        public float fireInterval;
        public float damage;
        public float projectileSpeed;
        public float splash;
        public float turretHeight = 1.5f;
    }

    public class TroopDef
    {
        public TroopType type;
        public string name;
        public int space;
        public float hp;
        public float damage;
        public float range;
        public float speed;
        public float fireInterval;
        public int goldCost;
        public int unlockHQ = 1;
        public float projectileSpeed;
        public float splash;
        public Color color;
    }

    public static class GameData
    {
        public const float CellSize = 2f;
        public const int GridSize = 20;          // 20 x 20 cells buildable area
        public const int MaxHQ = 5;
        public const int Builders = 2;

        static Dictionary<BuildingType, BuildingDef> buildings;
        static Dictionary<TroopType, TroopDef> troops;

        static GameData()
        {
            buildings = new Dictionary<BuildingType, BuildingDef>();
            Add(new BuildingDef { type = BuildingType.HQ, name = "Headquarters", desc = "The heart of your island. Upgrade it to unlock more buildings and storage.", size = 3, baseHp = 2400, baseGold = 600, baseWood = 500 });
            Add(new BuildingDef { type = BuildingType.GoldMine, name = "Gold Mine", desc = "Digs up gold over time. Tap it to collect.", isProducer = true, producesGold = true, baseHp = 650, baseGold = 0, baseWood = 150 });
            Add(new BuildingDef { type = BuildingType.Sawmill, name = "Sawmill", desc = "Cuts wood over time. Tap it to collect.", isProducer = true, producesGold = false, baseHp = 650, baseGold = 150, baseWood = 0 });
            Add(new BuildingDef { type = BuildingType.Barracks, name = "Landing Craft", desc = "Each one carries a boat of troops into battle. Upgrade for more space.", baseHp = 900, baseGold = 220, baseWood = 160 });
            Add(new BuildingDef { type = BuildingType.Cannon, name = "Cannon", desc = "Heavy hitting, medium range. Great against Heavies.", isDefense = true, baseHp = 1000, baseGold = 260, baseWood = 220, range = 11f, fireInterval = 2.2f, damage = 110f, projectileSpeed = 24f, turretHeight = 1.4f });
            Add(new BuildingDef { type = BuildingType.MachineGun, name = "Machine Gun", desc = "Shreds groups of light troops at short range.", isDefense = true, baseHp = 850, baseGold = 240, baseWood = 200, range = 8f, fireInterval = 0.25f, damage = 10f, projectileSpeed = 45f, turretHeight = 1.2f });
            Add(new BuildingDef { type = BuildingType.Sniper, name = "Sniper Tower", desc = "Long range, precise shots.", isDefense = true, unlockHQ = 2, baseHp = 700, baseGold = 300, baseWood = 300, range = 14f, fireInterval = 1.8f, damage = 60f, projectileSpeed = 70f, turretHeight = 4.4f });
            Add(new BuildingDef { type = BuildingType.Mortar, name = "Mortar", desc = "Lobs explosive shells that hit groups. Can't fire up close.", isDefense = true, unlockHQ = 3, baseHp = 800, baseGold = 380, baseWood = 340, range = 16f, minRange = 5f, fireInterval = 4.0f, damage = 95f, projectileSpeed = 12f, splash = 2.6f, turretHeight = 1.0f });
            buildings[BuildingType.HQ].size = 3;

            troops = new Dictionary<TroopType, TroopDef>();
            troops[TroopType.Rifleman] = new TroopDef { type = TroopType.Rifleman, name = "Rifleman", space = 1, hp = 140, damage = 16, range = 5.5f, speed = 3.3f, fireInterval = 0.8f, goldCost = 15, projectileSpeed = 40f, color = new Color(0.25f, 0.5f, 0.95f) };
            troops[TroopType.Heavy] = new TroopDef { type = TroopType.Heavy, name = "Heavy", space = 4, hp = 720, damage = 22, range = 3.2f, speed = 2.3f, fireInterval = 0.9f, goldCost = 60, projectileSpeed = 40f, color = new Color(0.95f, 0.65f, 0.15f) };
            troops[TroopType.Rocketeer] = new TroopDef { type = TroopType.Rocketeer, name = "Rocketeer", space = 2, hp = 160, damage = 75, range = 9f, speed = 2.8f, fireInterval = 2.2f, goldCost = 40, unlockHQ = 2, projectileSpeed = 16f, splash = 0f, color = new Color(0.85f, 0.25f, 0.3f) };
        }

        static void Add(BuildingDef d) { buildings[d.type] = d; }

        public static BuildingDef Get(BuildingType t) { return buildings[t]; }
        public static TroopDef GetTroop(TroopType t) { return troops[t]; }
        public static IEnumerable<BuildingDef> AllBuildings { get { return buildings.Values; } }

        public static float Hp(BuildingType t, int lvl)
        {
            lvl = Mathf.Max(1, lvl);
            return Get(t).baseHp * (1f + 0.45f * (lvl - 1));
        }

        static int RoundCost(float v) { return Mathf.RoundToInt(v / 10f) * 10; }

        // Cost to build/upgrade TO level lvl
        public static int GoldCost(BuildingType t, int lvl) { return RoundCost(Get(t).baseGold * Mathf.Pow(2.1f, lvl - 1)); }
        public static int WoodCost(BuildingType t, int lvl) { return RoundCost(Get(t).baseWood * Mathf.Pow(2.1f, lvl - 1)); }

        public static float BuildTime(BuildingType t, int lvl)
        {
            float time = lvl <= 1 ? 6f : 20f * (lvl - 1) * (lvl - 1);
            if (t == BuildingType.HQ && lvl > 1) time *= 1.5f;
            return time;
        }

        public static float Damage(BuildingType t, int lvl) { return Get(t).damage * (1f + 0.3f * (Mathf.Max(1, lvl) - 1)); }

        public static float ProductionPerMinute(int lvl) { return 45f * lvl; }
        public static float ProducerCapacity(int lvl) { return 250f * lvl; }

        public static int StorageCap(int hqLevel) { return 3000 * Mathf.Max(1, hqLevel); }

        public static int BarracksCapacity(int lvl) { return 6 + 2 * Mathf.Max(1, lvl); }

        public static float TroopLevelMult(int hqLevel) { return 1f + 0.12f * (Mathf.Max(1, hqLevel) - 1); }

        public static int MaxCount(BuildingType t, int hq)
        {
            switch (t)
            {
                case BuildingType.HQ: return 1;
                case BuildingType.GoldMine:
                case BuildingType.Sawmill: return 1 + hq / 2;
                case BuildingType.Barracks: return Mathf.Min(hq, 4);
                case BuildingType.Cannon:
                case BuildingType.MachineGun: return 1 + hq / 2;
                case BuildingType.Sniper: return hq >= 2 ? hq / 2 : 0;
                case BuildingType.Mortar: return hq >= 3 ? Mathf.Min(2, hq - 2) : 0;
            }
            return 0;
        }

        public static string FormatTime(double seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            if (s >= 3600) return (s / 3600) + "h " + ((s % 3600) / 60) + "m";
            if (s >= 60) return (s / 60) + "m " + (s % 60) + "s";
            return s + "s";
        }

        public static double Now()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }

    // ---------------- Save data ----------------

    [Serializable]
    public class SavedBuilding
    {
        public int type;
        public int level;
        public int x;
        public int y;
        public bool upgrading;
        public double upgradeEnd;
        public float stored;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public float gold;
        public float wood;
        public int victories;
        public int seed;
        public double lastTime;
        public double homeTick;     // when home production was last applied
        public List<SavedBuilding> buildings = new List<SavedBuilding>();
        public List<int> boatTroops = new List<int>();

        public static SaveData NewGame()
        {
            var s = new SaveData();
            s.gold = 1500;
            s.wood = 1200;
            s.seed = UnityEngine.Random.Range(1, 99999);
            s.lastTime = GameData.Now();
            s.homeTick = s.lastTime;
            s.buildings.Add(new SavedBuilding { type = (int)BuildingType.HQ, level = 1, x = 8, y = 9 });
            s.buildings.Add(new SavedBuilding { type = (int)BuildingType.GoldMine, level = 1, x = 4, y = 12 });
            s.buildings.Add(new SavedBuilding { type = (int)BuildingType.Sawmill, level = 1, x = 13, y = 12 });
            s.buildings.Add(new SavedBuilding { type = (int)BuildingType.Barracks, level = 1, x = 9, y = 4 });
            s.buildings.Add(new SavedBuilding { type = (int)BuildingType.Cannon, level = 1, x = 5, y = 6 });
            s.boatTroops.Add((int)TroopType.Rifleman);
            return s;
        }
    }
}
