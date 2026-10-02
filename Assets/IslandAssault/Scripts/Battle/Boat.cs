using System.Collections.Generic;
using UnityEngine;

namespace IslandAssault
{
    /// <summary>Landing craft: sails to the beach, drops its ramp and unloads troops.</summary>
    public class LandingBoat : MonoBehaviour
    {
        public bool landed;
        public bool unloaded;
        Vector3 stopPos;
        float speed = 11f;
        Transform ramp;
        readonly Queue<TroopType> cargo = new Queue<TroopType>();
        float unloadTimer;
        Transform troopParent;
        float mult;
        Vector3 dir;

        public static LandingBoat Create(Transform parent, Vector3 start, Vector3 stop, TroopType type, int count, float mult, Transform troopParent)
        {
            var go = new GameObject("LandingBoat");
            go.transform.SetParent(parent, false);
            go.transform.position = start;
            var b = go.AddComponent<LandingBoat>();
            b.stopPos = stop;
            b.dir = (stop - start); b.dir.y = 0; b.dir.Normalize();
            go.transform.rotation = Quaternion.LookRotation(b.dir);
            for (int i = 0; i < count; i++) b.cargo.Enqueue(type);
            b.mult = mult;
            b.troopParent = troopParent;
            b.BuildModel(GameData.GetTroop(type).color);
            return b;
        }

        void BuildModel(Color accent)
        {
            var hullCol = new Color(0.42f, 0.5f, 0.46f);
            var m = Art.Pivot(transform, "Model", Vector3.zero);
            var custom = ModelLibrary.Find("LandingBoat");
            if (custom != null)
            {
                var sink = Art.Pivot(m, "Hull", new Vector3(0, -0.35f, 0));   // sit a little in the water
                float h;
                ModelLibrary.Spawn(custom, sink, 4.4f, 0f, out h);
                return;
            }
            Art.Box(m, new Vector3(0, 0.15f, 0), new Vector3(2.2f, 0.8f, 4.2f), hullCol);
            Art.Box(m, new Vector3(0, 0.58f, 0), new Vector3(2.3f, 0.1f, 4.3f), Art.Player);
            Art.Box(m, new Vector3(-1.05f, 0.75f, -0.2f), new Vector3(0.12f, 0.35f, 3.6f), hullCol);
            Art.Box(m, new Vector3(1.05f, 0.75f, -0.2f), new Vector3(0.12f, 0.35f, 3.6f), hullCol);
            Art.Box(m, new Vector3(0, 0.95f, -1.6f), new Vector3(0.8f, 0.6f, 0.7f), Art.Metal);
            Art.Box(m, new Vector3(0, 1.4f, -1.6f), new Vector3(0.5f, 0.3f, 0.5f), accent);
            ramp = Art.Pivot(m, "Ramp", new Vector3(0, 0.1f, 2.1f));
            Art.Box(ramp, new Vector3(0, 0.55f, 0), new Vector3(2.1f, 1.1f, 0.14f), new Color(0.36f, 0.43f, 0.40f));
            // little troops visible inside
            for (int i = 0; i < 4; i++)
            {
                Art.Ball(m, new Vector3((i % 2 - 0.5f) * 0.9f, 0.9f, (i / 2) * 0.9f - 0.2f), new Vector3(0.38f, 0.22f, 0.38f), accent);
            }
        }

        void Update()
        {
            if (!landed)
            {
                Vector3 p = transform.position;
                Vector3 to = stopPos - p; to.y = 0;
                float step = speed * Time.deltaTime;
                if (to.magnitude <= step)
                {
                    p = stopPos;
                    landed = true;
                    FX.Splash(p + dir * 2f);
                    FX.Dust(p + dir * 2.5f, 0.6f);
                }
                else p += to.normalized * step;
                // bob on the waves
                p.y = Mathf.Sin(Time.time * 2.2f + p.x) * 0.12f;
                transform.position = p;
                transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Mathf.Sin(Time.time * 1.7f) * 2.5f, 0, Mathf.Sin(Time.time * 1.3f) * 3f);
                return;
            }

            // ramp down
            if (ramp != null) ramp.localRotation = Quaternion.Slerp(ramp.localRotation, Quaternion.Euler(80f, 0, 0), Time.deltaTime * 6f);

            if (!unloaded)
            {
                unloadTimer -= Time.deltaTime;
                if (unloadTimer <= 0f && cargo.Count > 0)
                {
                    unloadTimer = 0.18f;
                    var type = cargo.Dequeue();
                    var side = Vector3.Cross(Vector3.up, dir);
                    Vector3 spawn = transform.position + dir * 3.2f + side * Random.Range(-1f, 1f);
                    var am = AttackMode.I;
                    if (am != null && am.island != null) spawn.y = am.island.GroundY(spawn);
                    var t = Troop.Spawn(troopParent, type, spawn, mult);
                    t.transform.rotation = Quaternion.LookRotation(dir);
                    t.landed = true;
                    if (am != null) am.troops.Add(t);
                }
                if (cargo.Count == 0) unloaded = true;
            }
        }
    }
}
