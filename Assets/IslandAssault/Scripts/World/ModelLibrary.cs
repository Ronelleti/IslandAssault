using System.Collections.Generic;
using UnityEngine;

namespace IslandAssault
{
    /// <summary>
    /// Lets you replace any generated model with a real 3D model.
    ///
    /// HOW TO USE: put a model (.fbx / .obj / .glb with a glTF importer / or a prefab) in
    ///     Assets/IslandAssault/Resources/IslandAssaultModels/
    /// and name it after the thing it replaces, e.g. "Cannon.fbx", "HQ.fbx", "Palm.fbx".
    ///
    /// Optional, more specific names (the most specific one found wins):
    ///     Cannon_3          -> only for level 3 (levels without their own file use the closest lower one)
    ///     Cannon_Enemy      -> only on enemy islands
    ///     Cannon_3_Enemy    -> level 3 on enemy islands
    ///
    /// Models are automatically scaled to fit their spot, centered, and placed on the ground.
    /// Defenses: if the model has a child object whose name contains "Turret" (or "Weapon"/"Head"),
    /// only that part rotates to aim; otherwise the whole model rotates. A child named "Muzzle"
    /// marks where shots come out. Models should face +Z (Unity's forward / blue arrow).
    /// </summary>
    public static class ModelLibrary
    {
        public const string Folder = "IslandAssaultModels/";
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Find(string name)
        {
            GameObject g;
            if (!cache.TryGetValue(name, out g))
            {
                g = Resources.Load<GameObject>(Folder + name);
                cache[name] = g;
            }
            return g;
        }

        public static GameObject FindBuilding(BuildingType type, int level, bool enemy)
        {
            string n = type.ToString();
            GameObject g;
            if (enemy)
            {
                for (int l = level; l >= 1; l--)
                    if ((g = Find(n + "_" + l + "_Enemy")) != null) return g;
                if ((g = Find(n + "_Enemy")) != null) return g;
            }
            for (int l = level; l >= 1; l--)
                if ((g = Find(n + "_" + l)) != null) return g;
            return Find(n);
        }

        /// <summary>
        /// Instantiates a model under parent, scales it to fit 'width' (footprint) and optionally 'maxHeight',
        /// centers it on the parent and puts its bottom on the parent's height. Returns the instance.
        /// </summary>
        public static Transform Spawn(GameObject prefab, Transform parent, float width, float maxHeight, out float height)
        {
            var inst = Object.Instantiate(prefab, parent, false);
            inst.name = prefab.name;
            var t = inst.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>()) a.applyRootMotion = false;

            Bounds b;
            if (!WorldBounds(inst, out b)) { height = 1f; return t; }

            float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
            Vector3 size = b.size / parentScale;
            float fit = 1f;
            float footprint = Mathf.Max(size.x, size.z);
            if (width > 0f && footprint > 0.0001f) fit = width / footprint;
            if (maxHeight > 0f && size.y * fit > maxHeight) fit = maxHeight / size.y;
            t.localScale = Vector3.one * fit;

            WorldBounds(inst, out b);
            Vector3 p = parent.position;
            t.position += new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
            height = b.size.y / parentScale;

            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return t;
        }

        static bool WorldBounds(GameObject go, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Finds a child whose name contains any of the keywords (case-insensitive).</summary>
        public static Transform FindChild(Transform root, params string[] keywords)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                string n = t.name.ToLowerInvariant();
                foreach (var k in keywords)
                    if (n.Contains(k)) return t;
            }
            return null;
        }
    }
}
