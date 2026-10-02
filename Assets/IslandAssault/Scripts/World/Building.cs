using System;
using UnityEngine;

namespace IslandAssault
{
    /// <summary>A building on a grid (yours or the enemy's). Handles model, health, construction state and destruction.</summary>
    public class Building : MonoBehaviour
    {
        public BuildingType type;
        public int level;
        public int gx, gy;
        public bool enemy;

        public float hp, maxHp;
        public bool destroyed;

        public bool upgrading;
        public double upgradeEnd;
        public double upgradeStart;
        public float stored;

        public ModelRefs model;
        Transform scaffold;
        public Transform rubble;
        GameObject smoke;

        WorldUI.Bar hpBar, progressBar;
        WorldUI.Element timerLabel;
        public WorldUI.Element bubble;

        float bounce;
        public Action<Building> onDestroyed;

        public BuildingDef Def { get { return GameData.Get(type); } }
        public int Size { get { return Def.size; } }
        public float Radius { get { return Size * GameData.CellSize * 0.5f * 0.85f; } }
        public Vector3 Center { get { return transform.position; } }
        public bool Functional { get { return !destroyed && level >= 1; } }
        public float Height { get { return model != null ? model.height : 2.2f; } }
        public Vector3 AimPoint { get { return transform.position + Vector3.up * Mathf.Min(Height * 0.45f, 1.6f); } }

        public static Building Create(Transform parent, BuildingType type, int level, int gx, int gy, bool enemy)
        {
            var go = new GameObject(GameData.Get(type).name);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Building>();
            b.type = type;
            b.level = level;
            b.enemy = enemy;
            b.SetCell(gx, gy);
            b.maxHp = b.hp = GameData.Hp(type, Mathf.Max(1, level));
            b.Rebuild();
            return b;
        }

        public void SetCell(int x, int y)
        {
            gx = x; gy = y;
            transform.position = Island.CellToWorld(gx, gy, Size);
        }

        public bool Occupies(int x, int y)
        {
            return x >= gx && x < gx + Size && y >= gy && y < gy + Size;
        }

        public void Rebuild()
        {
            if (model != null && model.root != null) Destroy(model.root.gameObject);
            if (scaffold != null) Destroy(scaffold.gameObject);
            model = null; scaffold = null;

            if (destroyed) return;

            if (level >= 1)
            {
                model = BuildingModels.Create(transform, type, level, enemy);
                if (Def.isDefense)
                {
                    var d = GetComponent<Defense>();
                    if (d == null) d = gameObject.AddComponent<Defense>();
                    d.Init(this);
                }
            }
            else
            {
                // brand new building: just a pad
                var pad = new ModelRefs { root = new GameObject("Model").transform, height = 2f };
                pad.root.SetParent(transform, false);
                float w = Size * GameData.CellSize;
                Art.Box(pad.root, new Vector3(0, 0.05f, 0), new Vector3(w - 0.25f, 0.1f, w - 0.25f), new Color(0.78f, 0.68f, 0.50f));
                model = pad;
            }
            if (upgrading) scaffold = BuildingModels.Scaffold(transform, Size);
            maxHp = GameData.Hp(type, Mathf.Max(1, level));
            hp = Mathf.Min(hp, maxHp);
            if (hp <= 0f) hp = maxHp;
        }

        // ---------------- Construction UI ----------------

        public void UpdateConstructionUI(double now)
        {
            if (WorldUI.I == null) return;
            if (upgrading && !destroyed)
            {
                if (progressBar == null)
                {
                    progressBar = WorldUI.I.CreateBar(transform, Vector3.up * (Height + 1.2f), new Color(1f, 0.78f, 0.15f), 110, 16);
                    timerLabel = WorldUI.I.CreateLabel(transform, Vector3.up * (Height + 2.2f), "", 26, Color.white);
                }
                double total = Math.Max(0.01, upgradeEnd - upgradeStart);
                progressBar.Set((float)((now - upgradeStart) / total));
                timerLabel.text.text = GameData.FormatTime(upgradeEnd - now);
            }
            else ClearConstructionUI();
        }

        public void ClearConstructionUI()
        {
            if (WorldUI.I == null) return;
            if (progressBar != null) { WorldUI.I.Remove(progressBar); progressBar = null; }
            if (timerLabel != null) { WorldUI.I.Remove(timerLabel); timerLabel = null; }
        }

        // ---------------- Combat ----------------

        public void TakeDamage(float dmg)
        {
            if (destroyed || dmg <= 0f) return;
            hp -= dmg;
            bounce = Mathf.Max(bounce, 0.35f);
            if (hpBar == null && WorldUI.I != null)
                hpBar = WorldUI.I.CreateBar(transform, Vector3.up * (Height + 0.8f), enemy ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.3f, 0.85f, 0.3f), 90, 12);
            if (hpBar != null) hpBar.Set(hp / maxHp);
            if (hp <= 0f) Explode();
        }

        public void Explode()
        {
            if (destroyed) return;
            destroyed = true;
            hp = 0f;
            if (hpBar != null && WorldUI.I != null) { WorldUI.I.Remove(hpBar); hpBar = null; }
            ClearConstructionUI();
            if (bubble != null && WorldUI.I != null) { WorldUI.I.Remove(bubble); bubble = null; }
            FX.Explosion(transform.position + Vector3.up * 0.8f, Size >= 3 ? 2.2f : 1.5f);
            if (model != null && model.root != null) Destroy(model.root.gameObject);
            if (scaffold != null) Destroy(scaffold.gameObject);
            model = null;
            var d = GetComponent<Defense>();
            if (d != null) d.enabled = false;
            rubble = BuildingModels.Rubble(transform, Size);
            smoke = FX.SmokeColumn(transform.position + Vector3.up * 0.5f, Size >= 3 ? 1.6f : 1f);
            smoke.transform.SetParent(transform, true);
            if (onDestroyed != null) onDestroyed(this);
        }

        public void Pop() { bounce = 1f; }

        void Update()
        {
            if (bounce > 0f && model != null && model.root != null)
            {
                bounce = Mathf.Max(0f, bounce - Time.deltaTime * 3.5f);
                float s = 1f + Mathf.Sin(bounce * Mathf.PI * 3f) * 0.07f * bounce;
                model.root.localScale = new Vector3(s, 2f - s, s);
                if (bounce <= 0f) model.root.localScale = Vector3.one;
            }
        }

        void OnDestroy()
        {
            if (WorldUI.I == null) return;
            if (hpBar != null) WorldUI.I.Remove(hpBar);
            if (bubble != null) WorldUI.I.Remove(bubble);
            ClearConstructionUI();
        }
    }
}
