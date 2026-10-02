using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandAssault
{
    /// <summary>
    /// Your home island: place, move and upgrade buildings, collect resources, set up your army.
    /// </summary>
    public class BaseMode : MonoBehaviour
    {
        public static BaseMode I;

        Island island;
        readonly List<Building> buildings = new List<Building>();
        Building selected;
        Transform selectRing;
        bool active;

        // placement
        bool placing;
        BuildingType placeType;
        Building moving;
        int ghostX, ghostY;
        Transform ghost;
        Transform footprint;
        bool draggingGhost;
        int dragOffX, dragOffY;

        // UI
        RectTransform ui, buildPanel, armyPanel, infoPanel, placeBar, bottomBar;
        Text infoTitle, infoBody, buildersText, victoriesText, upgradeText;
        Button upgradeBtn, moveBtn, confirmBtn;
        float resetArmedUntil;

        GameManager GM { get { return GameManager.I; } }
        SaveData Save { get { return GameManager.I.save; } }

        // =========================================================
        // Enter / exit
        // =========================================================

        public void Enter()
        {
            I = this;
            active = true;
            island = Island.Create(Save.seed, false);

            foreach (var sb in Save.buildings)
            {
                var type = (BuildingType)sb.type;
                var b = Building.Create(island.transform, type, sb.level, sb.x, sb.y, false);
                b.upgrading = sb.upgrading;
                b.upgradeEnd = sb.upgradeEnd;
                b.upgradeStart = sb.upgradeEnd - GameData.BuildTime(type, sb.level + 1);
                b.stored = sb.stored;
                b.Rebuild();
                buildings.Add(b);
            }

            // Offline production since we were last home
            double now = GameData.Now();
            double elapsed = Save.homeTick > 0 ? now - Save.homeTick : 0;
            if (elapsed > 1)
            {
                float total = 0;
                foreach (var b in buildings)
                {
                    if (!b.Def.isProducer || b.level < 1 || b.upgrading) continue;
                    float before = b.stored;
                    b.stored = Mathf.Min(GameData.ProducerCapacity(b.level), b.stored + GameData.ProductionPerMinute(b.level) / 60f * (float)elapsed);
                    total += b.stored - before;
                }
                if (total > 10) UIManager.I.Toast("Welcome back! Your mines produced while you were away.");
            }
            Save.homeTick = now;

            BuildUI();
            CameraController.I.Focus(new Vector3(0, 0, -3f), 44f);
            CameraController.I.InputEnabled = true;
        }

        public void Exit()
        {
            if (!active) return;
            WriteToSave();
            CancelPlacement();
            Deselect();
            foreach (var b in buildings) if (b != null) Destroy(b.gameObject);
            buildings.Clear();
            if (island != null) Destroy(island.gameObject);
            if (WorldUI.I != null) WorldUI.I.Clear();
            active = false;
        }

        public void WriteToSave()
        {
            if (!active) return;
            Save.buildings.Clear();
            foreach (var b in buildings)
            {
                Save.buildings.Add(new SavedBuilding
                {
                    type = (int)b.type, level = b.level, x = b.gx, y = b.gy,
                    upgrading = b.upgrading, upgradeEnd = b.upgradeEnd, stored = b.stored
                });
            }
            Save.homeTick = GameData.Now();
        }

        // =========================================================
        // Queries
        // =========================================================

        Building BuildingAt(int x, int y)
        {
            foreach (var b in buildings) if (b.Occupies(x, y)) return b;
            return null;
        }

        int CountOf(BuildingType t)
        {
            int n = 0;
            foreach (var b in buildings) if (b.type == t) n++;
            return n;
        }

        int BuildersBusy
        {
            get { int n = 0; foreach (var b in buildings) if (b.upgrading) n++; return n; }
        }

        bool AreaFree(int x, int y, int size, Building ignore)
        {
            if (x < 0 || y < 0 || x + size > GameData.GridSize || y + size > GameData.GridSize) return false;
            foreach (var b in buildings)
            {
                if (b == ignore) continue;
                if (x < b.gx + b.Size && x + size > b.gx && y < b.gy + b.Size && y + size > b.gy) return false;
            }
            return true;
        }

        public List<Building> Barracks()
        {
            var list = new List<Building>();
            foreach (var b in buildings) if (b.type == BuildingType.Barracks && b.level >= 1) list.Add(b);
            return list;
        }

        // =========================================================
        // Update loop
        // =========================================================

        void Update()
        {
            if (!active) return;
            double now = GameData.Now();
            float dt = Time.deltaTime;

            foreach (var b in buildings)
            {
                if (b.upgrading && now >= b.upgradeEnd) FinishUpgrade(b);
                b.UpdateConstructionUI(now);

                if (b.Def.isProducer && b.level >= 1 && !b.upgrading)
                {
                    float cap = GameData.ProducerCapacity(b.level);
                    b.stored = Mathf.Min(cap, b.stored + GameData.ProductionPerMinute(b.level) / 60f * dt);
                    bool show = b.stored >= Mathf.Max(5f, cap * 0.08f);
                    if (show && b.bubble == null)
                    {
                        var target = b;
                        b.bubble = WorldUI.I.CreateBubble(b.transform, Vector3.up * (b.Height + 1.4f),
                            b.Def.producesGold ? UIKit.GoldText : UIKit.WoodText, b.Def.producesGold ? "G" : "W", () => Collect(target));
                    }
                    else if (!show && b.bubble != null) { WorldUI.I.Remove(b.bubble); b.bubble = null; }
                }
            }

            if (placing) HandlePlacementInput();
            else HandleSelectInput();

            if (selectRing != null && selected != null)
            {
                selectRing.position = selected.transform.position + Vector3.up * 0.08f;
                float s = selected.Size * GameData.CellSize * (1.0f + Mathf.Sin(Time.time * 5f) * 0.02f);
                selectRing.localScale = new Vector3(s, 0.02f, s);
            }

            RefreshTopTexts();
            if (selected != null && infoPanel.gameObject.activeSelf) RefreshInfo();
        }

        /// <summary>Finds the building under the pointer using its 3D box, so tapping a tall roof works.</summary>
        Building BuildingUnderPointer(Vector2 screen)
        {
            Ray ray = CameraController.I.cam.ScreenPointToRay(screen);
            Building best = null;
            float bestDist = float.MaxValue;
            foreach (var b in buildings)
            {
                if (b == null || !b.gameObject.activeSelf) continue;
                float w = b.Size * GameData.CellSize * 0.9f;
                var bounds = new Bounds(b.transform.position + Vector3.up * (b.Height * 0.5f), new Vector3(w, b.Height, w));
                float d;
                if (bounds.IntersectRay(ray, out d) && d < bestDist) { bestDist = d; best = b; }
            }
            if (best != null) return best;
            Vector3 p;
            if (!island.ScreenToGround(CameraController.I.cam, screen, out p)) return null;
            int x, y;
            Island.WorldToCell(p, out x, out y);
            return BuildingAt(x, y);
        }

        void HandleSelectInput()
        {
            if (!GameInput.Tap) return;
            var b = BuildingUnderPointer(GameInput.Pos);
            if (b != null)
            {
                if (b.bubble != null) Collect(b);
                Select(b);
            }
            else Deselect();
        }

        // =========================================================
        // Selection, upgrades, collecting
        // =========================================================

        void Select(Building b)
        {
            selected = b;
            b.Pop();
            if (selectRing == null)
            {
                selectRing = Art.MeshPart(null, Art.GetMesh(PrimitiveType.Cylinder), Vector3.zero, Vector3.one, Art.Emissive(new Color(1f, 0.9f, 0.3f), 0.6f), default(Vector3), false);
                selectRing.name = "SelectRing";
            }
            selectRing.gameObject.SetActive(true);
            buildPanel.gameObject.SetActive(false);
            armyPanel.gameObject.SetActive(false);
            bottomBar.gameObject.SetActive(false);
            infoPanel.gameObject.SetActive(true);
            RefreshInfo();
        }

        void Deselect()
        {
            selected = null;
            if (selectRing != null) selectRing.gameObject.SetActive(false);
            if (infoPanel != null) infoPanel.gameObject.SetActive(false);
            if (bottomBar != null && !placing) bottomBar.gameObject.SetActive(true);
        }

        void Collect(Building b)
        {
            if (b == null || b.stored < 1f) return;
            float got = b.Def.producesGold ? GM.AddGold(b.stored) : GM.AddWood(b.stored);
            if (got < 1f)
            {
                UIManager.I.Toast("Storage is full! Upgrade your Headquarters.", new Color(1f, 0.6f, 0.5f));
                return;
            }
            b.stored -= got;
            WorldUI.I.FloatText(b.transform.position + Vector3.up * (b.Height + 1f), "+" + Mathf.FloorToInt(got), b.Def.producesGold ? UIKit.GoldText : UIKit.WoodText, 40);
            b.Pop();
            if (b.bubble != null) { WorldUI.I.Remove(b.bubble); b.bubble = null; }
        }

        string UpgradeBlocker(Building b, out int gold, out int wood, out float time)
        {
            int next = b.level + 1;
            gold = GameData.GoldCost(b.type, next);
            wood = GameData.WoodCost(b.type, next);
            time = GameData.BuildTime(b.type, next);
            if (b.upgrading) return "Under construction";
            if (b.level >= b.Def.maxLevel) return "Max level";
            if (b.type != BuildingType.HQ && b.level >= GM.HQLevel) return "Needs HQ level " + (b.level + 1);
            if (BuildersBusy >= GameData.Builders) return "All builders are busy";
            return null;
        }

        void TryUpgrade()
        {
            var b = selected;
            if (b == null) return;
            int gold, wood; float time;
            string blocker = UpgradeBlocker(b, out gold, out wood, out time);
            if (blocker != null) { UIManager.I.Toast(blocker, new Color(1f, 0.6f, 0.5f)); return; }
            if (gold > GM.StorageCap || wood > GM.StorageCap) { UIManager.I.Toast("Costs more than your storage. Upgrade the Headquarters.", new Color(1f, 0.6f, 0.5f)); return; }
            if (!GM.Spend(gold, wood)) { UIManager.I.Toast("Not enough resources!", new Color(1f, 0.6f, 0.5f)); return; }
            double now = GameData.Now();
            b.upgrading = true;
            b.upgradeStart = now;
            b.upgradeEnd = now + time;
            b.Rebuild();
            b.Pop();
            FX.Dust(b.transform.position, b.Size * 0.6f);
            RefreshInfo();
            GM.Save();
        }

        void FinishUpgrade(Building b)
        {
            b.upgrading = false;
            b.level++;
            b.hp = GameData.Hp(b.type, b.level);
            b.Rebuild();
            b.Pop();
            b.ClearConstructionUI();
            FX.Confetti(b.transform.position);
            WorldUI.I.FloatText(b.transform.position + Vector3.up * (b.Height + 1.5f), b.level == 1 ? "Built!" : "Level " + b.level + "!", Color.white, 40);
            UIManager.I.Toast(b.Def.name + (b.level == 1 ? " is ready!" : " upgraded to level " + b.level + "!"), new Color(0.7f, 1f, 0.6f));
            if (b.type == BuildingType.Barracks) SyncBoatList();
            if (selected == b) RefreshInfo();
            GM.Save();
        }

        // =========================================================
        // Placement (new buildings + moving)
        // =========================================================

        void StartPlacement(BuildingType type, Building move)
        {
            CancelPlacement();
            Deselect();
            placing = true;
            island.ShowGrid(true);
            placeType = type;
            moving = move;
            buildPanel.gameObject.SetActive(false);
            armyPanel.gameObject.SetActive(false);
            bottomBar.gameObject.SetActive(false);
            placeBar.gameObject.SetActive(true);

            int size = GameData.Get(type).size;
            ghost = new GameObject("Ghost").transform;
            BuildingModels.Create(ghost, type, move != null ? move.level : 1, false);
            Art.SetLayerShadows(ghost.gameObject, false);
            footprint = Art.MeshPart(ghost, Art.Cube, new Vector3(0, 0.12f, 0), new Vector3(size * GameData.CellSize, 0.1f, size * GameData.CellSize), Art.Mat(Color.green), default(Vector3), false);

            if (move != null)
            {
                move.gameObject.SetActive(false);
                SetGhost(move.gx, move.gy);
            }
            else
            {
                // first free spot closest to the middle of the screen
                Vector3 c;
                int cx = GameData.GridSize / 2, cy = GameData.GridSize / 2;
                if (island.ScreenToGround(CameraController.I.cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out c))
                    Island.WorldToCell(c, out cx, out cy);
                int bestX = cx, bestY = cy; float best = float.MaxValue;
                for (int x = 0; x <= GameData.GridSize - size; x++)
                    for (int y = 0; y <= GameData.GridSize - size; y++)
                    {
                        if (!AreaFree(x, y, size, null)) continue;
                        float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        if (d < best) { best = d; bestX = x; bestY = y; }
                    }
                SetGhost(bestX, bestY);
            }
        }

        void SetGhost(int x, int y)
        {
            int size = GameData.Get(placeType).size;
            ghostX = Mathf.Clamp(x, 0, GameData.GridSize - size);
            ghostY = Mathf.Clamp(y, 0, GameData.GridSize - size);
            ghost.position = Island.CellToWorld(ghostX, ghostY, size) + Vector3.up * 0.15f;
            bool ok = AreaFree(ghostX, ghostY, size, moving);
            footprint.GetComponent<MeshRenderer>().sharedMaterial = Art.Mat(ok ? new Color(0.35f, 0.95f, 0.35f) : new Color(0.95f, 0.25f, 0.2f), 0.2f);
            if (confirmBtn != null) confirmBtn.interactable = ok;
        }

        void HandlePlacementInput()
        {
            var cam = CameraController.I.cam;
            int size = GameData.Get(placeType).size;
            Vector3 p;
            if (GameInput.Down && !GameInput.OverUI && island.ScreenToGround(cam, GameInput.Pos, out p))
            {
                int x, y;
                Island.WorldToCell(p, out x, out y);
                if (x >= ghostX && x < ghostX + size && y >= ghostY && y < ghostY + size)
                {
                    draggingGhost = true;
                    dragOffX = x - ghostX;
                    dragOffY = y - ghostY;
                    CameraController.I.PanBlocked = true;
                }
            }
            if (draggingGhost && GameInput.Held && island.ScreenToGround(cam, GameInput.Pos, out p))
            {
                int x, y;
                Island.WorldToCell(p, out x, out y);
                if (x - dragOffX != ghostX || y - dragOffY != ghostY) SetGhost(x - dragOffX, y - dragOffY);
            }
            if (GameInput.Up)
            {
                bool wasDragging = draggingGhost;
                draggingGhost = false;
                CameraController.I.PanBlocked = false;
                if (GameInput.Tap && !wasDragging && island.ScreenToGround(cam, GameInput.Pos, out p))
                {
                    int x, y;
                    Island.WorldToCell(p, out x, out y);
                    SetGhost(x - size / 2, y - size / 2);
                }
            }
            if (ghost != null)
            {
                float bob = Mathf.Sin(Time.time * 4f) * 0.08f;
                ghost.position = Island.CellToWorld(ghostX, ghostY, size) + Vector3.up * (0.15f + bob + (draggingGhost ? 0.4f : 0f));
            }
        }

        void ConfirmPlacement()
        {
            int size = GameData.Get(placeType).size;
            if (!AreaFree(ghostX, ghostY, size, moving)) { UIManager.I.Toast("Can't place it there", new Color(1f, 0.6f, 0.5f)); return; }

            if (moving != null)
            {
                var m = moving;
                moving.gameObject.SetActive(true);
                moving.SetCell(ghostX, ghostY);
                moving = null;
                FX.Dust(m.transform.position, size * 0.5f);
                m.Pop();
                CancelPlacement();
                GM.Save();
                return;
            }

            int gold = GameData.GoldCost(placeType, 1), wood = GameData.WoodCost(placeType, 1);
            if (BuildersBusy >= GameData.Builders) { UIManager.I.Toast("All builders are busy", new Color(1f, 0.6f, 0.5f)); return; }
            if (!GM.Spend(gold, wood)) { UIManager.I.Toast("Not enough resources!", new Color(1f, 0.6f, 0.5f)); return; }

            var b = Building.Create(island.transform, placeType, 0, ghostX, ghostY, false);
            double now = GameData.Now();
            b.upgrading = true;
            b.upgradeStart = now;
            b.upgradeEnd = now + GameData.BuildTime(placeType, 1);
            b.Rebuild();
            buildings.Add(b);
            FX.Dust(b.transform.position, size * 0.6f);
            CancelPlacement();
            GM.Save();
        }

        void CancelPlacement()
        {
            if (moving != null) { moving.gameObject.SetActive(true); moving = null; }
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            footprint = null;
            placing = false;
            if (island != null) island.ShowGrid(false);
            draggingGhost = false;
            if (CameraController.I != null) CameraController.I.PanBlocked = false;
            if (placeBar != null) placeBar.gameObject.SetActive(false);
            if (bottomBar != null) bottomBar.gameObject.SetActive(true);
        }

        // =========================================================
        // Army
        // =========================================================

        void SyncBoatList()
        {
            int boats = Barracks().Count;
            while (Save.boatTroops.Count < boats) Save.boatTroops.Add((int)TroopType.Rifleman);
        }

        void CycleTroop(int boat, int dir)
        {
            SyncBoatList();
            int hq = GM.HQLevel;
            int n = System.Enum.GetValues(typeof(TroopType)).Length;
            int t = Save.boatTroops[boat];
            for (int i = 0; i < n; i++)
            {
                t = (t + dir + n) % n;
                if (GameData.GetTroop((TroopType)t).unlockHQ <= hq) break;
            }
            Save.boatTroops[boat] = t;
            RebuildArmyPanel();
        }

        // =========================================================
        // UI
        // =========================================================

        void BuildUI()
        {
            ui = UIManager.I.NewModeRoot();

            // top right info
            var tr = UIKit.Panel(ui, "TopRight", UIKit.PanelDark, false);
            UIKit.Place(tr.rectTransform, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(330, 110));
            victoriesText = UIKit.Label(tr.transform, "", 28, Color.white, TextAnchor.UpperLeft);
            UIKit.Stretch(victoriesText.rectTransform, 18, 18, 12, 50);
            buildersText = UIKit.Label(tr.transform, "", 26, new Color(1f, 0.85f, 0.5f), TextAnchor.LowerLeft);
            UIKit.Stretch(buildersText.rectTransform, 18, 18, 50, 12);
            var reset = UIKit.Button(ui, "New game", UIKit.Grey, OnResetPressed, 20);
            UIKit.Place((RectTransform)reset.transform, new Vector2(1, 1), new Vector2(-24, -140), new Vector2(150, 50));

            // bottom bar
            bottomBar = UIKit.Rect(ui, "BottomBar");
            UIKit.Stretch(bottomBar);
            var build = UIKit.Button(bottomBar, "BUILD", UIKit.Green, ToggleBuildPanel, 38);
            UIKit.Place((RectTransform)build.transform, new Vector2(0, 0), new Vector2(30, 30), new Vector2(220, 120));
            var army = UIKit.Button(bottomBar, "ARMY", UIKit.Blue, ToggleArmyPanel, 38);
            UIKit.Place((RectTransform)army.transform, new Vector2(0, 0), new Vector2(270, 30), new Vector2(220, 120));
            var attack = UIKit.Button(bottomBar, "ATTACK!", UIKit.Orange, OnAttackPressed, 48);
            UIKit.Place((RectTransform)attack.transform, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(300, 150));

            // placement bar
            placeBar = UIKit.Rect(ui, "PlaceBar");
            UIKit.Stretch(placeBar);
            var hint = UIKit.Label(placeBar, "Drag the building or tap a spot, then press PLACE", 30, Color.white);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(1100, 50));
            confirmBtn = UIKit.Button(placeBar, "PLACE", UIKit.Green, ConfirmPlacement, 40);
            UIKit.Place((RectTransform)confirmBtn.transform, new Vector2(0.5f, 0), new Vector2(140, 40), new Vector2(240, 120));
            var cancel = UIKit.Button(placeBar, "CANCEL", UIKit.Red, CancelPlacement, 40);
            UIKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0), new Vector2(-140, 40), new Vector2(240, 120));
            placeBar.gameObject.SetActive(false);

            BuildBuildPanel();
            BuildArmyPanel();
            BuildInfoPanel();
        }

        void RefreshTopTexts()
        {
            victoriesText.text = "Victories: " + Save.victories + "\nHQ level " + GM.HQLevel;
            buildersText.text = "Builders: " + (GameData.Builders - BuildersBusy) + " / " + GameData.Builders + " free";
        }

        void OnResetPressed()
        {
            if (Time.unscaledTime < resetArmedUntil) { resetArmedUntil = 0; GM.ResetGame(); }
            else { resetArmedUntil = Time.unscaledTime + 3f; UIManager.I.Toast("Tap 'New game' again to erase your island", new Color(1f, 0.7f, 0.5f)); }
        }

        void OnAttackPressed()
        {
            SyncBoatList();
            if (Barracks().Count == 0) { UIManager.I.Toast("Build a Landing Craft first!", new Color(1f, 0.6f, 0.5f)); return; }
            GM.EnterAttack();
        }

        // ---------- Build panel ----------

        void BuildBuildPanel()
        {
            var panel = UIKit.Panel(ui, "BuildPanel", UIKit.PanelDark, true);
            buildPanel = panel.rectTransform;
            UIKit.Place(buildPanel, new Vector2(0.5f, 0), new Vector2(0, 175), new Vector2(1640, 330));
            var title = UIKit.Label(buildPanel, "BUILD", 34, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(400, 46));
            var close = UIKit.Button(buildPanel, "X", UIKit.Red, () => buildPanel.gameObject.SetActive(false), 30);
            UIKit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(60, 60));
            var row = UIKit.Rect(buildPanel, "Row");
            UIKit.Stretch(row, 20, 20, 64, 20);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            buildPanel.gameObject.SetActive(false);
        }

        void RefreshBuildPanel()
        {
            var row = buildPanel.Find("Row");
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            int hq = GM.HQLevel;
            foreach (var def in GameData.AllBuildings)
            {
                if (def.type == BuildingType.HQ) continue;
                var d = def;
                int count = CountOf(d.type), max = GameData.MaxCount(d.type, hq);
                string lockText = null;
                if (hq < d.unlockHQ) lockText = "Needs HQ " + d.unlockHQ;
                else if (count >= max) lockText = "Built " + count + "/" + max;
                Color c = lockText == null ? (d.isDefense ? new Color(0.62f, 0.32f, 0.26f) : new Color(0.25f, 0.45f, 0.62f)) : UIKit.Grey;
                var card = UIKit.Button(row, d.name, c, () => OnBuildCard(d.type), 26);
                var label = UIKit.ButtonText(card);
                label.alignment = TextAnchor.UpperCenter;
                int g = GameData.GoldCost(d.type, 1), w = GameData.WoodCost(d.type, 1);
                string cost = (g > 0 ? "<color=#ffd84a>" + g + " gold</color>\n" : "") + (w > 0 ? "<color=#e6a46a>" + w + " wood</color>" : "");
                var info = UIKit.Label(card.transform, lockText ?? (cost + "\n<size=20>" + count + "/" + max + " built</size>"), 22, Color.white);
                UIKit.Stretch(info.rectTransform, 6, 6, 70, 14);
                card.interactable = lockText == null;
            }
        }

        void OnBuildCard(BuildingType t)
        {
            int g = GameData.GoldCost(t, 1), w = GameData.WoodCost(t, 1);
            if (!GM.CanAfford(g, w)) { UIManager.I.Toast("Not enough resources!", new Color(1f, 0.6f, 0.5f)); return; }
            if (BuildersBusy >= GameData.Builders) { UIManager.I.Toast("All builders are busy", new Color(1f, 0.6f, 0.5f)); return; }
            StartPlacement(t, null);
        }

        void ToggleBuildPanel()
        {
            bool show = !buildPanel.gameObject.activeSelf;
            Deselect();
            armyPanel.gameObject.SetActive(false);
            buildPanel.gameObject.SetActive(show);
            if (show) RefreshBuildPanel();
        }

        // ---------- Army panel ----------

        void BuildArmyPanel()
        {
            var panel = UIKit.Panel(ui, "ArmyPanel", UIKit.PanelDark, true);
            armyPanel = panel.rectTransform;
            UIKit.Place(armyPanel, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(980, 640));
            armyPanel.gameObject.SetActive(false);
        }

        void ToggleArmyPanel()
        {
            bool show = !armyPanel.gameObject.activeSelf;
            Deselect();
            buildPanel.gameObject.SetActive(false);
            armyPanel.gameObject.SetActive(show);
            if (show) RebuildArmyPanel();
        }

        void RebuildArmyPanel()
        {
            for (int i = armyPanel.childCount - 1; i >= 0; i--) Destroy(armyPanel.GetChild(i).gameObject);
            SyncBoatList();
            var title = UIKit.Label(armyPanel, "YOUR ARMY", 36, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(600, 50));
            var close = UIKit.Button(armyPanel, "X", UIKit.Red, () => armyPanel.gameObject.SetActive(false), 30);
            UIKit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-12, -12), new Vector2(60, 60));

            var boats = Barracks();
            int totalCost = 0;
            for (int i = 0; i < boats.Count; i++)
            {
                int boat = i;
                var tdef = GameData.GetTroop((TroopType)Save.boatTroops[i]);
                int cap = GameData.BarracksCapacity(boats[i].level);
                int count = Mathf.Max(1, cap / tdef.space);
                totalCost += count * tdef.goldCost;

                var rowImg = UIKit.Panel(armyPanel, "Boat" + i, new Color(1, 1, 1, 0.08f), false);
                UIKit.Place(rowImg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -80 - i * 115), new Vector2(920, 100));
                var lbl = UIKit.Label(rowImg.transform, "Boat " + (i + 1) + "  <size=22>(level " + boats[i].level + ", space " + cap + ")</size>", 28, Color.white, TextAnchor.MiddleLeft);
                UIKit.Stretch(lbl.rectTransform, 20, 520, 0, 0);
                var icon = UIKit.Icon(rowImg.transform, tdef.color, tdef.name.Substring(0, 1), 64);
                UIKit.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(420, 0), new Vector2(64, 64), new Vector2(0.5f, 0.5f));
                var tl = UIKit.Label(rowImg.transform, tdef.name + " x" + count, 30, Color.white, TextAnchor.MiddleLeft);
                UIKit.Place(tl.rectTransform, new Vector2(0, 0.5f), new Vector2(465, 0), new Vector2(260, 60), new Vector2(0, 0.5f));
                var prev = UIKit.Button(rowImg.transform, "<", UIKit.Blue, () => CycleTroop(boat, -1), 36);
                UIKit.Place((RectTransform)prev.transform, new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(80, 80));
                var next = UIKit.Button(rowImg.transform, ">", UIKit.Blue, () => CycleTroop(boat, 1), 36);
                UIKit.Place((RectTransform)next.transform, new Vector2(1, 0.5f), new Vector2(-15, 0), new Vector2(80, 80));
            }
            string footer = boats.Count == 0
                ? "Build a Landing Craft to carry troops."
                : "Training cost per attack: <color=#ffd84a>" + totalCost + " gold</color>\n<size=22>Rifleman: cheap and fast.  Heavy: tanky, short range.  Rocketeer (HQ 2): long range, big damage.</size>";
            var f = UIKit.Label(armyPanel, footer, 28, Color.white);
            UIKit.Place(f.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(920, 110));
        }

        // ---------- Info panel ----------

        void BuildInfoPanel()
        {
            var panel = UIKit.Panel(ui, "InfoPanel", UIKit.PanelDark, true);
            infoPanel = panel.rectTransform;
            UIKit.Place(infoPanel, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1000, 250));
            infoTitle = UIKit.Label(infoPanel, "", 36, Color.white, TextAnchor.UpperLeft);
            UIKit.Stretch(infoTitle.rectTransform, 24, 380, 16, 180);
            infoBody = UIKit.Label(infoPanel, "", 24, new Color(0.88f, 0.9f, 0.95f), TextAnchor.UpperLeft, false);
            UIKit.Stretch(infoBody.rectTransform, 24, 380, 70, 16);
            upgradeBtn = UIKit.Button(infoPanel, "UPGRADE", UIKit.Green, TryUpgrade, 28);
            UIKit.Place((RectTransform)upgradeBtn.transform, new Vector2(1, 0.5f), new Vector2(-20, 30), new Vector2(330, 150));
            upgradeText = UIKit.ButtonText(upgradeBtn);
            moveBtn = UIKit.Button(infoPanel, "MOVE", UIKit.Blue, () => { if (selected != null) StartPlacement(selected.type, selected); }, 24);
            UIKit.Place((RectTransform)moveBtn.transform, new Vector2(1, 0), new Vector2(-195, 14), new Vector2(155, 58));
            var close = UIKit.Button(infoPanel, "CLOSE", UIKit.Grey, Deselect, 24);
            UIKit.Place((RectTransform)close.transform, new Vector2(1, 0), new Vector2(-20, 14), new Vector2(155, 58));
            infoPanel.gameObject.SetActive(false);
        }

        void RefreshInfo()
        {
            var b = selected;
            if (b == null) return;
            var d = b.Def;
            infoTitle.text = d.name + "  <size=26><color=#ffd84a>Level " + b.level + "</color></size>";
            string body = d.desc + "\n";
            int lvl = Mathf.Max(1, b.level);
            body += "Health: " + Mathf.RoundToInt(GameData.Hp(b.type, lvl));
            if (d.isDefense)
                body += "   Damage: " + Mathf.RoundToInt(GameData.Damage(b.type, lvl)) + "   Range: " + d.range;
            if (d.isProducer)
                body += "   Makes " + Mathf.RoundToInt(GameData.ProductionPerMinute(lvl)) + "/min   Holding " + Mathf.FloorToInt(b.stored) + "/" + Mathf.RoundToInt(GameData.ProducerCapacity(lvl));
            if (b.type == BuildingType.Barracks)
                body += "   Troop space: " + GameData.BarracksCapacity(lvl);
            if (b.type == BuildingType.HQ)
                body += "   Storage: " + GM.StorageCap;
            infoBody.text = body;

            int gold, wood; float time;
            string blocker = UpgradeBlocker(b, out gold, out wood, out time);
            if (b.upgrading)
            {
                upgradeText.text = "Building...\n" + GameData.FormatTime(b.upgradeEnd - GameData.Now());
                upgradeBtn.interactable = false;
            }
            else if (blocker != null)
            {
                upgradeText.text = "UPGRADE\n<size=22>" + blocker + "</size>";
                upgradeBtn.interactable = false;
            }
            else
            {
                bool afford = GM.CanAfford(gold, wood);
                string g = gold > 0 ? "<color=" + (Save.gold >= gold ? "#ffe066" : "#ff7766") + ">" + gold + "g</color> " : "";
                string w = wood > 0 ? "<color=" + (Save.wood >= wood ? "#f0b27a" : "#ff7766") + ">" + wood + "w</color>" : "";
                upgradeText.text = "UPGRADE\n<size=24>" + g + w + "</size>\n<size=20>" + GameData.FormatTime(time) + "</size>";
                upgradeBtn.interactable = afford;
            }
            moveBtn.interactable = !b.upgrading;
        }
    }
}
