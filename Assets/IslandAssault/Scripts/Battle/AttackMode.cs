using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandAssault
{
    /// <summary>
    /// Scout an enemy island, land your boats, use gunboat abilities, destroy the HQ to win.
    /// </summary>
    public class AttackMode : MonoBehaviour
    {
        public static AttackMode I;

        public enum Phase { None, Scout, Deploy, Landing, Battle, Ended }
        public enum Ability { None, Flare, Barrage, Medkit }

        public Phase phase = Phase.None;
        public Island island;
        public readonly List<Building> buildings = new List<Building>();
        public readonly List<Troop> troops = new List<Troop>();
        readonly List<LandingBoat> boats = new List<LandingBoat>();
        Transform troopRoot, boatRoot, gunboat;

        // enemy
        int enemySeed, enemyLevel, enemyHQ;
        int lootGold, lootWood, gainedGold, gainedWood;
        string enemyName;

        // gunboat
        public float energy;
        int flareUses, barrageUses, medkitUses;
        Ability selected = Ability.Barrage;
        public bool flareActive;
        public Vector3 flarePos;
        public int flareId;
        float flareTimer;
        GameObject flareObj;

        float timeLeft;
        const float BattleTime = 180f;

        // UI
        RectTransform ui, scoutUI, battleUI, resultUI;
        Text timerText, energyText, troopsText, hintText, lootText;
        Button[] abilityButtons;
        Text[] abilityTexts;
        Image energyFill;

        public bool Fighting { get { return phase == Phase.Landing || phase == Phase.Battle; } }

        static readonly string[] Names = { "Dr. Kraken's Outpost", "Coral Fortress", "Sandbar Stronghold", "Rusty Cove", "Iron Lagoon", "Skull Reef Base", "Palm Bunker", "Smugglers' Atoll", "Volcano Watch", "Lighthouse Point" };

        // =========================================================
        // Enter / exit
        // =========================================================

        public void Enter()
        {
            I = this;
            StartScout();
        }

        public void Exit()
        {
            StopAllCoroutines();
            ClearWorld();
            phase = Phase.None;
            if (I == this) I = null;
        }

        void ClearWorld()
        {
            foreach (var t in troops) if (t != null) Destroy(t.gameObject);
            troops.Clear();
            buildings.Clear();
            boats.Clear();
            if (island != null) Destroy(island.gameObject);
            if (troopRoot != null) Destroy(troopRoot.gameObject);
            if (boatRoot != null) Destroy(boatRoot.gameObject);
            if (gunboat != null) Destroy(gunboat.gameObject);
            if (flareObj != null) Destroy(flareObj);
            island = null;
            flareActive = false;
            if (WorldUI.I != null) WorldUI.I.Clear();
        }

        // =========================================================
        // Enemy generation
        // =========================================================

        void StartScout()
        {
            ClearWorld();
            phase = Phase.Scout;
            GenerateEnemy();
            CameraController.I.Focus(new Vector3(0, 0, -4f), 58f);
            CameraController.I.InputEnabled = true;
            BuildUI();
        }

        void GenerateEnemy()
        {
            var save = GameManager.I.save;
            enemySeed = Random.Range(1, 999999);
            var rnd = new System.Random(enemySeed);
            enemyLevel = 1 + save.victories + (save.victories > 0 ? rnd.Next(0, 2) : 0);
            enemyHQ = Mathf.Clamp(1 + (enemyLevel - 1) / 2, 1, 5);
            enemyName = Names[rnd.Next(Names.Length)];
            lootGold = 250 + enemyLevel * 230 + rnd.Next(0, 160);
            lootWood = 200 + enemyLevel * 210 + rnd.Next(0, 160);
            gainedGold = gainedWood = 0;

            island = Island.Create(enemySeed, true);
            troopRoot = new GameObject("Troops").transform;
            boatRoot = new GameObject("Boats").transform;
            var occupied = new bool[GameData.GridSize, GameData.GridSize];

            // HQ toward the middle/back so attackers must fight through
            int hqX = rnd.Next(7, 11), hqY = rnd.Next(8, 12);
            PlaceEnemy(BuildingType.HQ, hqX, hqY, enemyHQ, occupied);

            var plan = new List<BuildingType>();
            int defenses = Mathf.Min(1 + enemyLevel, 14);
            for (int i = 0; i < defenses; i++)
            {
                var options = new List<BuildingType> { BuildingType.Cannon, BuildingType.MachineGun, BuildingType.MachineGun, BuildingType.Cannon };
                if (enemyHQ >= 2) { options.Add(BuildingType.Sniper); options.Add(BuildingType.Sniper); }
                if (enemyHQ >= 3) options.Add(BuildingType.Mortar);
                plan.Add(options[rnd.Next(options.Count)]);
            }
            int mines = 2 + rnd.Next(0, 2);
            for (int i = 0; i < mines; i++) plan.Add(i % 2 == 0 ? BuildingType.GoldMine : BuildingType.Sawmill);
            plan.Add(BuildingType.Barracks);

            foreach (var type in plan)
            {
                int lvl = Mathf.Clamp(enemyHQ - rnd.Next(0, 2), 1, 5);
                int size = GameData.Get(type).size;
                bool placed = false;
                // first try with a 1-cell gap around, then without
                for (int pass = 0; pass < 2 && !placed; pass++)
                {
                    for (int attempt = 0; attempt < 300 && !placed; attempt++)
                    {
                        int x = rnd.Next(0, GameData.GridSize - size + 1);
                        int y = rnd.Next(0, GameData.GridSize - size + 1);
                        if (GameData.Get(type).isDefense)
                        {
                            // defenses prefer to sit around the HQ
                            float dx = x - hqX, dy = y - hqY;
                            if (dx * dx + dy * dy > 60f && attempt < 200) continue;
                        }
                        if (!Free(occupied, x, y, size, pass == 0 ? 1 : 0)) continue;
                        PlaceEnemy(type, x, y, lvl, occupied);
                        placed = true;
                    }
                }
            }
        }

        static bool Free(bool[,] occ, int x, int y, int size, int gap)
        {
            for (int i = x - gap; i < x + size + gap; i++)
                for (int j = y - gap; j < y + size + gap; j++)
                {
                    if (i < 0 || j < 0 || i >= GameData.GridSize || j >= GameData.GridSize)
                    {
                        if (i >= x && i < x + size && j >= y && j < y + size) return false;
                        continue;
                    }
                    if (occ[i, j]) return false;
                }
            return true;
        }

        void PlaceEnemy(BuildingType type, int x, int y, int lvl, bool[,] occ)
        {
            var b = Building.Create(island.transform, type, lvl, x, y, true);
            b.onDestroyed = OnBuildingDestroyed;
            buildings.Add(b);
            int size = b.Size;
            for (int i = x; i < x + size; i++)
                for (int j = y; j < y + size; j++)
                    occ[i, j] = true;
        }

        // =========================================================
        // Army
        // =========================================================

        struct BoatLoad { public TroopType type; public int count; }

        List<BoatLoad> Army()
        {
            var save = GameManager.I.save;
            int hq = GameManager.I.HQLevel;
            var list = new List<BoatLoad>();
            int idx = 0;
            foreach (var sb in save.buildings)
            {
                if (sb.type != (int)BuildingType.Barracks || sb.level < 1) continue;
                var type = idx < save.boatTroops.Count ? (TroopType)save.boatTroops[idx] : TroopType.Rifleman;
                if (GameData.GetTroop(type).unlockHQ > hq) type = TroopType.Rifleman;
                var def = GameData.GetTroop(type);
                list.Add(new BoatLoad { type = type, count = Mathf.Max(1, GameData.BarracksCapacity(sb.level) / def.space) });
                idx++;
            }
            return list;
        }

        int TrainingCost()
        {
            int c = 0;
            foreach (var l in Army()) c += l.count * GameData.GetTroop(l.type).goldCost;
            return c;
        }

        // =========================================================
        // Update
        // =========================================================

        void Update()
        {
            if (phase == Phase.None) return;
            float dt = Time.deltaTime;

            if (gunboat != null)
            {
                gunboat.position = new Vector3(gunboat.position.x, Mathf.Sin(Time.time * 1.3f) * 0.15f, gunboat.position.z);
            }

            switch (phase)
            {
                case Phase.Deploy:
                    if (GameInput.Tap)
                    {
                        Vector3 p;
                        if (island.ScreenToGround(CameraController.I.cam, GameInput.Pos, out p)) LaunchBoats(p);
                    }
                    break;

                case Phase.Landing:
                case Phase.Battle:
                    TickBattle(dt);
                    break;
            }
        }

        void TickBattle(float dt)
        {
            timeLeft -= dt;
            troops.RemoveAll(t => t == null);

            if (phase == Phase.Landing)
            {
                bool allDone = true;
                foreach (var b in boats) if (!b.unloaded) allDone = false;
                if (allDone) phase = Phase.Battle;
            }

            if (flareActive)
            {
                flareTimer -= dt;
                if (flareTimer <= 0f)
                {
                    flareActive = false;
                    if (flareObj != null) Destroy(flareObj, 1f);
                    flareObj = null;
                }
            }

            if (GameInput.Tap && phase == Phase.Battle)
            {
                Vector3 p;
                if (island.ScreenToGround(CameraController.I.cam, GameInput.Pos, out p)) UseAbility(p);
            }

            int alive = 0;
            foreach (var t in troops) if (t != null && !t.dead) alive++;

            // UI
            int secs = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            timerText.text = (secs / 60) + ":" + (secs % 60).ToString("00");
            timerText.color = secs <= 30 ? new Color(1f, 0.45f, 0.4f) : Color.white;
            troopsText.text = "Troops: " + alive;
            energyText.text = "Energy " + Mathf.FloorToInt(energy);
            UIKit.SetBar(energyFill, energy / 60f);
            lootText.text = "Loot so far:  <color=#ffd84a>" + gainedGold + " gold</color>   <color=#e6a46a>" + gainedWood + " wood</color>";
            RefreshAbilityButtons();

            if (phase == Phase.Battle && alive == 0) { EndBattle(false, "All your troops were defeated."); return; }
            if (timeLeft <= 0f) { EndBattle(false, "Time ran out."); return; }
        }

        public Building NearestBuilding(Vector3 pos)
        {
            Building best = null;
            float bestD = float.MaxValue;
            foreach (var b in buildings)
            {
                if (b == null || b.destroyed) continue;
                Vector3 d = b.Center - pos; d.y = 0f;
                float dist = d.magnitude - b.Radius;
                if (dist < bestD) { bestD = dist; best = b; }
            }
            return best;
        }

        // =========================================================
        // Landing
        // =========================================================

        void OnAttackPressed()
        {
            int cost = TrainingCost();
            if (!GameManager.I.Spend(cost, 0)) { UIManager.I.Toast("Not enough gold to train your troops (" + cost + ")", new Color(1f, 0.6f, 0.5f)); return; }
            phase = Phase.Deploy;
            energy = 15 + 2 * GameManager.I.HQLevel;
            flareUses = barrageUses = medkitUses = 0;
            timeLeft = BattleTime;
            SpawnGunboat();
            scoutUI.gameObject.SetActive(false);
            battleUI.gameObject.SetActive(true);
            hintText.text = "Tap a beach to land your boats!";
            hintText.gameObject.SetActive(true);
        }

        void LaunchBoats(Vector3 tap)
        {
            Vector3 dir = new Vector3(tap.x, 0, tap.z);
            if (dir.magnitude < 1f) dir = Vector3.back;
            dir.Normalize();
            Vector3 inward = -dir;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            var army = Army();
            float mult = GameData.TroopLevelMult(GameManager.I.HQLevel);
            for (int i = 0; i < army.Count; i++)
            {
                float lateral = (i - (army.Count - 1) * 0.5f) * 3.6f;
                Vector3 farPoint = dir * 80f + side * lateral;
                // march from the sea toward the island until we hit shallow sand
                Vector3 stop = farPoint;
                for (float d = 0; d < 80f; d += 0.5f)
                {
                    Vector3 p = farPoint + inward * d;
                    if (island.HeightAt(p.x, p.z) > -0.45f) { stop = p - inward * 1.5f; break; }
                }
                Vector3 start = stop + dir * (28f + i * 2f);
                boats.Add(LandingBoat.Create(boatRoot, start, stop, army[i].type, army[i].count, mult, troopRoot));
            }
            phase = Phase.Landing;
            hintText.text = "Select a gunboat ability, then tap the island to use it";
            CameraController.I.FlyTo(tap * 0.6f);
            StartCoroutine(HideHint(5f));
        }

        IEnumerator HideHint(float t)
        {
            yield return new WaitForSeconds(t);
            if (hintText != null && phase == Phase.Battle) hintText.gameObject.SetActive(false);
            else if (hintText != null && phase == Phase.Landing) { yield return new WaitForSeconds(3f); if (hintText != null) hintText.gameObject.SetActive(false); }
        }

        void SpawnGunboat()
        {
            gunboat = new GameObject("Gunboat").transform;
            gunboat.position = new Vector3(-14f, 0f, -62f);
            gunboat.rotation = Quaternion.Euler(0, 75f, 0);
            var hull = new Color(0.38f, 0.45f, 0.52f);
            Art.Box(gunboat, new Vector3(0, 0.3f, 0), new Vector3(4.5f, 1.6f, 13f), hull);
            Art.MeshPart(gunboat, Art.Pyramid, new Vector3(0, 0.3f, 6.5f), new Vector3(4.5f, 3.5f, 1.6f), Art.Mat(hull), new Vector3(90, 0, 0));
            Art.Box(gunboat, new Vector3(0, 1.15f, 0), new Vector3(4.6f, 0.12f, 13.1f), Art.Player);
            Art.Box(gunboat, new Vector3(0, 2.0f, -2.5f), new Vector3(3f, 1.8f, 3.5f), Art.Cream);
            Art.Box(gunboat, new Vector3(0, 3.1f, -2.5f), new Vector3(2.4f, 0.5f, 2.6f), Art.Metal);
            Art.Cyl(gunboat, new Vector3(0, 4.0f, -3f), new Vector3(0.5f, 1.2f, 0.5f), Art.Metal);
            var turret = Art.Pivot(gunboat, "Turret", new Vector3(0, 1.6f, 3f));
            Art.Cyl(turret, Vector3.zero, new Vector3(2f, 0.4f, 2f), Art.Metal);
            Art.Cyl(turret, new Vector3(0, 0.3f, 1.4f), new Vector3(0.4f, 1.2f, 0.4f), Art.StoneDark, new Vector3(75, 0, 0));
            Flag(gunboat, new Vector3(0, 5.2f, -3f));
        }

        static void Flag(Transform parent, Vector3 pos)
        {
            var p = Art.Pivot(parent, "Flag", pos);
            Art.Box(p, new Vector3(0, 0, -0.5f), new Vector3(0.05f, 0.6f, 1f), Art.Player);
            var s = p.gameObject.AddComponent<Sway>();
            s.amount = 10f; s.speed = 3f;
        }

        // =========================================================
        // Gunboat abilities
        // =========================================================

        int Cost(Ability a)
        {
            switch (a)
            {
                case Ability.Flare: return 2 + flareUses;
                case Ability.Barrage: return 3 + barrageUses * 2;
                case Ability.Medkit: return 3 + medkitUses * 2;
            }
            return 0;
        }

        void SelectAbility(Ability a) { selected = a; RefreshAbilityButtons(); }

        void UseAbility(Vector3 p)
        {
            if (selected == Ability.None) { UIManager.I.Toast("Pick a gunboat ability first"); return; }
            int cost = Cost(selected);
            if (energy < cost) { UIManager.I.Toast("Not enough energy. Destroy buildings to earn more!", new Color(1f, 0.6f, 0.5f)); return; }
            energy -= cost;
            switch (selected)
            {
                case Ability.Flare:
                    flareUses++;
                    flareId++;
                    flareActive = true;
                    flarePos = new Vector3(p.x, island.GroundY(p), p.z);
                    flareTimer = 9f;
                    if (flareObj != null) Destroy(flareObj);
                    flareObj = new GameObject("Flare");
                    flareObj.transform.position = flarePos;
                    Art.Cyl(flareObj.transform, new Vector3(0, 0.4f, 0), new Vector3(0.2f, 0.4f, 0.2f), new Color(0.9f, 0.9f, 0.9f));
                    var glow = Art.MeshPart(flareObj.transform, Art.Ico, new Vector3(0, 0.95f, 0), Vector3.one * 0.6f, Art.Emissive(new Color(1f, 0.15f, 0.1f), 4f), default(Vector3), false);
                    glow.gameObject.AddComponent<Bob>().amount = 0.08f;
                    var smoke = FX.SmokeColumn(flarePos + Vector3.up, 0.6f);
                    smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = Art.Mat(new Color(1f, 0.45f, 0.4f), 0f);
                    smoke.transform.SetParent(flareObj.transform, true);
                    break;

                case Ability.Barrage:
                    barrageUses++;
                    StartCoroutine(Barrage(p));
                    break;

                case Ability.Medkit:
                    medkitUses++;
                    FX.Heal(new Vector3(p.x, island.GroundY(p), p.z), 2.5f);
                    foreach (var t in troops)
                    {
                        if (t == null || t.dead) continue;
                        Vector3 d = t.transform.position - p; d.y = 0;
                        if (d.magnitude <= 4.5f)
                        {
                            t.Heal(t.maxHp * 0.6f);
                            WorldUI.I.FloatText(t.AimPoint + Vector3.up, "+", new Color(0.4f, 1f, 0.5f), 30);
                        }
                    }
                    break;
            }
            RefreshAbilityButtons();
        }

        IEnumerator Barrage(Vector3 p)
        {
            float dmg = 170f * (1f + 0.3f * (GameManager.I.HQLevel - 1));
            Vector3 from = gunboat != null ? gunboat.position + Vector3.up * 2.5f + gunboat.forward * 4f : p + new Vector3(0, 10, -40);
            for (int i = 0; i < 5; i++)
            {
                Vector2 r = Random.insideUnitCircle * 2.6f;
                Vector3 target = new Vector3(p.x + r.x, 0, p.z + r.y);
                target.y = island.GroundY(target);
                FX.MuzzleFlash(from, Quaternion.identity, 1.4f);
                Projectile.Fire(ProjectileKind.Shell, from, target, null, null, dmg, 45f, 2.2f, false, 14f);
                yield return new WaitForSeconds(0.14f);
            }
        }

        // =========================================================
        // Destruction / results
        // =========================================================

        void OnBuildingDestroyed(Building b)
        {
            if (phase == Phase.Ended) return;
            if (b.type == BuildingType.HQ)
            {
                EndBattle(true, "Enemy Headquarters destroyed!");
                return;
            }
            energy += 3f;
            // resource buildings drop some loot right away
            if (b.type == BuildingType.GoldMine) GrantLoot(Mathf.RoundToInt(lootGold * 0.12f), 0, b);
            else if (b.type == BuildingType.Sawmill) GrantLoot(0, Mathf.RoundToInt(lootWood * 0.12f), b);
            if (WorldUI.I != null) WorldUI.I.FloatText(b.Center + Vector3.up * 3f, "+3 energy", new Color(0.5f, 0.85f, 1f), 28);
        }

        void GrantLoot(int gold, int wood, Building at)
        {
            int g = Mathf.RoundToInt(GameManager.I.AddGold(gold));
            int w = Mathf.RoundToInt(GameManager.I.AddWood(wood));
            gainedGold += g;
            gainedWood += w;
            if (at != null && WorldUI.I != null)
            {
                if (g > 0) WorldUI.I.FloatText(at.Center + Vector3.up * 4f, "+" + g + " gold", UIKit.GoldText, 34);
                if (w > 0) WorldUI.I.FloatText(at.Center + Vector3.up * 4f, "+" + w + " wood", UIKit.WoodText, 34);
            }
        }

        void EndBattle(bool victory, string reason)
        {
            if (phase == Phase.Ended) return;
            phase = Phase.Ended;
            flareActive = false;
            hintText.gameObject.SetActive(false);
            if (victory)
            {
                GrantLoot(lootGold - gainedGold, lootWood - gainedWood, null);
                GameManager.I.save.victories++;
                StartCoroutine(ChainExplode());
            }
            GameManager.I.Save();
            StartCoroutine(ShowResult(victory, reason, victory ? 2.2f : 1.0f));
        }

        IEnumerator ChainExplode()
        {
            var remaining = new List<Building>();
            foreach (var b in buildings) if (b != null && !b.destroyed) remaining.Add(b);
            foreach (var b in remaining)
            {
                yield return new WaitForSeconds(0.12f);
                if (b != null) b.Explode();
            }
        }

        IEnumerator ShowResult(bool victory, string reason, float delay)
        {
            yield return new WaitForSeconds(delay);
            battleUI.gameObject.SetActive(false);
            resultUI.gameObject.SetActive(true);
            var title = resultUI.Find("Title").GetComponent<Text>();
            var body = resultUI.Find("Body").GetComponent<Text>();
            title.text = victory ? "VICTORY!" : "DEFEAT";
            title.color = victory ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.45f, 0.4f);
            body.text = reason + "\n\nLoot: <color=#ffd84a>" + gainedGold + " gold</color>   <color=#e6a46a>" + gainedWood + " wood</color>"
                      + (victory ? "\nVictories: " + GameManager.I.save.victories : "");
        }

        // =========================================================
        // UI
        // =========================================================

        void BuildUI()
        {
            ui = UIManager.I.NewModeRoot();

            // ---- Scout ----
            scoutUI = UIKit.Rect(ui, "Scout");
            UIKit.Stretch(scoutUI);
            var info = UIKit.Panel(scoutUI, "EnemyInfo", UIKit.PanelDark, false);
            UIKit.Place(info.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(760, 150));
            var t = UIKit.Label(info.transform, enemyName + "  <size=26><color=#ff8877>Level " + enemyLevel + "</color></size>", 38, Color.white);
            UIKit.Stretch(t.rectTransform, 10, 10, 10, 80);
            var lt = UIKit.Label(info.transform, "Available loot:  <color=#ffd84a>" + lootGold + " gold</color>   <color=#e6a46a>" + lootWood + " wood</color>", 28, Color.white);
            UIKit.Stretch(lt.rectTransform, 10, 10, 75, 15);

            var home = UIKit.Button(scoutUI, "HOME", UIKit.Grey, () => GameManager.I.EnterHome(), 36);
            UIKit.Place((RectTransform)home.transform, new Vector2(0, 0), new Vector2(30, 30), new Vector2(220, 120));
            var next = UIKit.Button(scoutUI, "FIND ANOTHER\n<size=22>50 gold</size>", UIKit.Blue, OnNextPressed, 30);
            UIKit.Place((RectTransform)next.transform, new Vector2(1, 0), new Vector2(-360, 30), new Vector2(280, 120));
            int cost = TrainingCost();
            var atk = UIKit.Button(scoutUI, "ATTACK!\n<size=24>Train troops: " + cost + " gold</size>", UIKit.Orange, OnAttackPressed, 44);
            UIKit.Place((RectTransform)atk.transform, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(310, 150));

            // ---- Battle ----
            battleUI = UIKit.Rect(ui, "Battle");
            UIKit.Stretch(battleUI);
            var top = UIKit.Panel(battleUI, "Top", UIKit.PanelDark, false);
            UIKit.Place(top.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(560, 120));
            timerText = UIKit.Label(top.transform, "3:00", 46, Color.white);
            UIKit.Stretch(timerText.rectTransform, 10, 10, 6, 50);
            lootText = UIKit.Label(top.transform, "", 22, Color.white);
            UIKit.Stretch(lootText.rectTransform, 10, 10, 70, 8);

            var tr = UIKit.Panel(battleUI, "TroopCount", UIKit.PanelDark, false);
            UIKit.Place(tr.rectTransform, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(260, 70));
            troopsText = UIKit.Label(tr.transform, "Troops: 0", 30, Color.white);
            UIKit.Stretch(troopsText.rectTransform);

            hintText = UIKit.Label(battleUI, "", 36, Color.white);
            UIKit.Place(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1300, 60));
            hintText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);

            var end = UIKit.Button(battleUI, "END BATTLE", UIKit.Red, () => EndBattle(false, "You retreated."), 26);
            UIKit.Place((RectTransform)end.transform, new Vector2(0, 0), new Vector2(30, 30), new Vector2(220, 90));

            // energy + abilities
            var abil = UIKit.Panel(battleUI, "Abilities", UIKit.PanelDark, true);
            UIKit.Place(abil.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(760, 200));
            var eb = UIKit.Bar(abil.transform, "Energy", new Color(0, 0, 0, 0.4f), new Color(0.35f, 0.75f, 1f), out energyFill);
            UIKit.Place(eb.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(700, 34));
            energyText = UIKit.Label(eb.transform, "Energy", 24, Color.white);
            UIKit.Stretch(energyText.rectTransform);
            abilityButtons = new Button[3];
            abilityTexts = new Text[3];
            string[] names = { "FLARE", "BARRAGE", "MEDKIT" };
            Color[] cols = { new Color(0.9f, 0.3f, 0.25f), new Color(0.95f, 0.55f, 0.15f), new Color(0.3f, 0.75f, 0.35f) };
            for (int i = 0; i < 3; i++)
            {
                var a = (Ability)(i + 1);
                var b = UIKit.Button(abil.transform, names[i], cols[i], () => SelectAbility(a), 28);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2((i - 1) * 240, 14), new Vector2(220, 120));
                abilityButtons[i] = b;
                abilityTexts[i] = UIKit.ButtonText(b);
            }
            battleUI.gameObject.SetActive(false);

            // ---- Result ----
            var res = UIKit.Panel(ui, "Result", UIKit.PanelDark, true);
            resultUI = res.rectTransform;
            UIKit.Place(resultUI, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 520));
            var title = UIKit.Label(resultUI, "", 80, Color.white);
            title.name = "Title";
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(800, 110));
            var body = UIKit.Label(resultUI, "", 32, Color.white);
            body.name = "Body";
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(800, 200));
            var back = UIKit.Button(resultUI, "RETURN HOME", UIKit.Green, () => GameManager.I.EnterHome(), 38);
            UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(400, 110));
            resultUI.gameObject.SetActive(false);
        }

        void RefreshAbilityButtons()
        {
            if (abilityButtons == null) return;
            string[] names = { "FLARE", "BARRAGE", "MEDKIT" };
            for (int i = 0; i < 3; i++)
            {
                var a = (Ability)(i + 1);
                int c = Cost(a);
                bool sel = selected == a;
                abilityTexts[i].text = (sel ? "> " + names[i] + " <" : names[i]) + "\n<size=22>" + c + " energy</size>";
                var rt = (RectTransform)abilityButtons[i].transform;
                rt.localScale = Vector3.one * (sel ? 1.08f : 1f);
                abilityButtons[i].image.color = new Color(abilityButtons[i].image.color.r, abilityButtons[i].image.color.g, abilityButtons[i].image.color.b, energy >= c ? 1f : 0.45f);
            }
        }

        void OnNextPressed()
        {
            if (!GameManager.I.Spend(50, 0)) { UIManager.I.Toast("Not enough gold", new Color(1f, 0.6f, 0.5f)); return; }
            StartScout();
        }
    }
}
