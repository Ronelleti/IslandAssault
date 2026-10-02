using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace IslandAssault
{
    /// <summary>Tiny helper library to build uGUI from code (no prefabs needed).</summary>
    public static class UIKit
    {
        static Font font;
        static Sprite rounded, circle;

        public static readonly Color PanelDark = new Color(0.08f, 0.12f, 0.20f, 0.88f);
        public static readonly Color PanelLight = new Color(0.96f, 0.93f, 0.85f, 0.97f);
        public static readonly Color Green = new Color(0.30f, 0.72f, 0.25f);
        public static readonly Color Orange = new Color(0.98f, 0.55f, 0.12f);
        public static readonly Color Red = new Color(0.86f, 0.24f, 0.20f);
        public static readonly Color Blue = new Color(0.20f, 0.52f, 0.92f);
        public static readonly Color Grey = new Color(0.55f, 0.57f, 0.60f);
        public static readonly Color GoldText = new Color(1f, 0.85f, 0.25f);
        public static readonly Color WoodText = new Color(0.86f, 0.62f, 0.38f);

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { } }
                if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return font;
            }
        }

        /// <summary>Procedural rounded-rectangle sprite with 9-slice borders.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                int size = 64, rad = 22;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, rad, size - rad);
                        float cy = Mathf.Clamp(y + 0.5f, rad, size - rad);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float a = Mathf.Clamp01(rad - d + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(rad, rad, rad, rad));
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        float a = Mathf.Clamp01(size / 2f - d);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return circle;
            }
        }

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Position a rect: anchor = where on the parent (0..1), pos = offset in pixels, size in pixels.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool blocksClicks = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = blocksClicks;
            return img;
        }

        public static Image Bar(Transform parent, string name, Color back, Color fill, out Image fillImage)
        {
            var bg = Panel(parent, name, back, false);
            var f = Panel(bg.transform, "Fill", fill, false);
            Stretch(f.rectTransform, 3, 3, 3, 3);
            f.rectTransform.pivot = new Vector2(0, 0.5f);
            fillImage = f;
            return bg;
        }

        public static void SetBar(Image fill, float value)
        {
            value = Mathf.Clamp01(value);
            var rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(value, 1);
            fill.enabled = value > 0.001f;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Button Button(Transform parent, string label, Color color, Action onClick, int fontSize = 30)
        {
            var img = Panel(parent, "Button_" + label, color, true);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            btn.colors = colors;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            // bottom "3D" lip
            var lip = Panel(img.transform, "Lip", new Color(0, 0, 0, 0.22f), false);
            lip.rectTransform.anchorMin = new Vector2(0, 0);
            lip.rectTransform.anchorMax = new Vector2(1, 0);
            lip.rectTransform.pivot = new Vector2(0.5f, 0);
            lip.rectTransform.offsetMin = new Vector2(0, 0);
            lip.rectTransform.offsetMax = new Vector2(0, 8);
            var t = Label(img.transform, label, fontSize, Color.white);
            Stretch(t.rectTransform, 8, 8, 4, 8);
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.35f);
            return btn;
        }

        public static Text ButtonText(Button b) { return b.GetComponentInChildren<Text>(); }

        public static Image Icon(Transform parent, Color color, string letter, int size = 40)
        {
            var rt = Rect(parent, "Icon");
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.color = color;
            img.raycastTarget = false;
            rt.sizeDelta = new Vector2(size, size);
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.4f);
            ol.effectDistance = new Vector2(2, -2);
            if (!string.IsNullOrEmpty(letter))
            {
                var t = Label(rt, letter, (int)(size * 0.55f), Color.white);
                Stretch(t.rectTransform);
            }
            return img;
        }
    }
}
