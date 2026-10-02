using UnityEngine;
using UnityEngine.UI;

namespace IslandAssault
{
    /// <summary>Owns the main canvas, the resource counters and toast messages. Each mode builds its own HUD under ModeRoot.</summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager I;

        public Canvas canvas;
        public RectTransform root;
        public RectTransform ModeRoot { get; private set; }

        Text goldText, woodText;
        Image goldFill, woodFill;
        RectTransform resourcePanel;
        Text toastText;
        CanvasGroup toastGroup;
        float toastTimer;
        float shownGold, shownWood;

        public static UIManager Create()
        {
            UIKit.EnsureEventSystem();
            var canvas = UIKit.CreateCanvas("UI", 10);
            var ui = canvas.gameObject.AddComponent<UIManager>();
            ui.canvas = canvas;
            ui.root = (RectTransform)canvas.transform;
            I = ui;
            WorldUI.Create(canvas.transform);
            ui.BuildCommon();
            return ui;
        }

        void BuildCommon()
        {
            resourcePanel = UIKit.Rect(root, "Resources");
            UIKit.Place(resourcePanel, new Vector2(0, 1), new Vector2(24, -20), new Vector2(380, 150));
            goldText = ResourcePill(resourcePanel, 0, UIKit.GoldText, "G", out goldFill);
            woodText = ResourcePill(resourcePanel, 1, UIKit.WoodText, "W", out woodFill);

            var toast = UIKit.Panel(root, "Toast", new Color(0.05f, 0.08f, 0.14f, 0.85f), false);
            UIKit.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 70));
            toastText = UIKit.Label(toast.transform, "", 32, Color.white);
            UIKit.Stretch(toastText.rectTransform, 12, 12, 6, 6);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
        }

        Text ResourcePill(RectTransform parent, int index, Color color, string letter, out Image fill)
        {
            var pill = UIKit.Panel(parent, "Pill", UIKit.PanelDark, false);
            UIKit.Place(pill.rectTransform, new Vector2(0, 1), new Vector2(30, -index * 74), new Vector2(340, 62));
            Image f;
            var bar = UIKit.Bar(pill.transform, "Cap", new Color(0, 0, 0, 0.35f), color * 0.85f, out f);
            UIKit.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(46, 8), new Vector2(280, 12), new Vector2(0, 0));
            fill = f;
            var t = UIKit.Label(pill.transform, "0", 30, Color.white, TextAnchor.MiddleLeft);
            UIKit.Stretch(t.rectTransform, 50, 10, 2, 18);
            var icon = UIKit.Icon(parent, color, letter, 66);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.pivot = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(0, -index * 74 + 2);
            return t;
        }

        public RectTransform NewModeRoot()
        {
            if (ModeRoot != null) Destroy(ModeRoot.gameObject);
            ModeRoot = UIKit.Rect(root, "ModeUI");
            UIKit.Stretch(ModeRoot);
            ModeRoot.SetSiblingIndex(1); // above WorldUI, below resources/toast
            resourcePanel.SetAsLastSibling();
            toastGroup.transform.SetAsLastSibling();
            return ModeRoot;
        }

        public void Toast(string msg) { Toast(msg, Color.white); }

        public void Toast(string msg, Color color)
        {
            toastText.text = msg;
            toastText.color = color;
            toastTimer = 2.6f;
            toastGroup.alpha = 1f;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.save == null) return;
            int cap = gm.StorageCap;
            shownGold = Mathf.Lerp(shownGold, gm.save.gold, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            shownWood = Mathf.Lerp(shownWood, gm.save.wood, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            if (Mathf.Abs(shownGold - gm.save.gold) < 1f) shownGold = gm.save.gold;
            if (Mathf.Abs(shownWood - gm.save.wood) < 1f) shownWood = gm.save.wood;
            goldText.text = Mathf.FloorToInt(shownGold).ToString("N0") + "  <size=20><color=#bbbbbb>/ " + cap.ToString("N0") + "</color></size>";
            woodText.text = Mathf.FloorToInt(shownWood).ToString("N0") + "  <size=20><color=#bbbbbb>/ " + cap.ToString("N0") + "</color></size>";
            UIKit.SetBar(goldFill, shownGold / cap);
            UIKit.SetBar(woodFill, shownWood / cap);

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f);
            }
        }
    }
}
