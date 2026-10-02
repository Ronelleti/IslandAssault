using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandAssault
{
    /// <summary>
    /// Screen-space UI that follows 3D objects: health bars, timers, collect bubbles, floating "+100" text.
    /// </summary>
    public class WorldUI : MonoBehaviour
    {
        public static WorldUI I;

        public class Element
        {
            public Transform target;
            public Vector3 worldPos;
            public Vector3 offset;
            public RectTransform rt;
            public bool visible = true;
            public float life = -1f;      // >0 = floating text
            public float age;
            public Text text;
            public CanvasGroup group;

            public void SetVisible(bool v) { visible = v; }
        }

        public class Bar : Element
        {
            public Image fill;
            public void Set(float v) { UIKit.SetBar(fill, v); }
            public void SetColor(Color c) { fill.color = c; }
        }

        RectTransform layer;
        Camera cam;
        readonly List<Element> elements = new List<Element>();

        public static WorldUI Create(Transform canvas)
        {
            var rt = UIKit.Rect(canvas, "WorldUI");
            UIKit.Stretch(rt);
            rt.SetAsFirstSibling();
            var w = rt.gameObject.AddComponent<WorldUI>();
            w.layer = rt;
            I = w;
            return w;
        }

        public Bar CreateBar(Transform target, Vector3 offset, Color fill, float width = 70f, float height = 12f)
        {
            Image f;
            var bg = UIKit.Bar(layer, "HP", new Color(0.05f, 0.05f, 0.08f, 0.75f), fill, out f);
            bg.rectTransform.sizeDelta = new Vector2(width, height);
            var b = new Bar { target = target, offset = offset, rt = bg.rectTransform, fill = f };
            UIKit.SetBar(f, 1f);
            elements.Add(b);
            return b;
        }

        public Element CreateLabel(Transform target, Vector3 offset, string text, int size, Color color)
        {
            var t = UIKit.Label(layer, text, size, color);
            t.rectTransform.sizeDelta = new Vector2(260, 40);
            var e = new Element { target = target, offset = offset, rt = t.rectTransform, text = t };
            elements.Add(e);
            return e;
        }

        public Element CreateBubble(Transform target, Vector3 offset, Color color, string letter, Action onClick)
        {
            var img = UIKit.Panel(layer, "Bubble", new Color(1, 1, 1, 0.95f), true);
            img.sprite = UIKit.Circle;
            img.type = Image.Type.Simple;
            img.rectTransform.sizeDelta = new Vector2(64, 64);
            var icon = UIKit.Icon(img.transform, color, letter, 48);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var e = new Element { target = target, offset = offset, rt = img.rectTransform };
            elements.Add(e);
            return e;
        }

        public void FloatText(Vector3 worldPos, string text, Color color, int size = 34)
        {
            var t = UIKit.Label(layer, text, size, color);
            t.rectTransform.sizeDelta = new Vector2(300, 50);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            var g = t.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            elements.Add(new Element { worldPos = worldPos, rt = t.rectTransform, text = t, life = 1.4f, group = g });
        }

        public void Remove(Element e)
        {
            if (e == null) return;
            if (e.rt != null) Destroy(e.rt.gameObject);
            elements.Remove(e);
        }

        public void Clear()
        {
            foreach (var e in elements) if (e.rt != null) Destroy(e.rt.gameObject);
            elements.Clear();
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var e = elements[i];
                if (e.rt == null) { elements.RemoveAt(i); continue; }
                Vector3 wp;
                if (e.life > 0f)
                {
                    e.age += Time.deltaTime;
                    if (e.age >= e.life) { Destroy(e.rt.gameObject); elements.RemoveAt(i); continue; }
                    wp = e.worldPos + Vector3.up * (e.age * 2.2f);
                    if (e.group != null) e.group.alpha = 1f - Mathf.Clamp01((e.age - e.life * 0.6f) / (e.life * 0.4f));
                }
                else
                {
                    if (e.target == null) { Destroy(e.rt.gameObject); elements.RemoveAt(i); continue; }
                    wp = e.target.position + e.offset;
                }
                Vector3 sp = cam.WorldToScreenPoint(wp);
                bool show = e.visible && sp.z > 0f;
                if (e.rt.gameObject.activeSelf != show) e.rt.gameObject.SetActive(show);
                if (show) e.rt.position = new Vector3(sp.x, sp.y, 0f);
            }
        }
    }
}
