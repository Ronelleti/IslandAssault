using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IslandAssault
{
    /// <summary>
    /// One place that reads mouse + touch, for both the new Input System and the old Input Manager.
    /// Call Tick() once per frame (GameManager does it). Everything else reads the static fields.
    /// </summary>
    public static class GameInput
    {
        public static Vector2 Pos;          // current pointer position (screen pixels)
        public static Vector2 DownPos;      // where the current press started
        public static bool Down;            // pressed this frame
        public static bool Held;            // currently pressed
        public static bool Up;              // released this frame
        public static bool Tap;             // released this frame without dragging, not on UI
        public static bool OverUI;          // the current press started over UI
        public static bool Dragging;        // moved beyond threshold since press
        public static float Scroll;         // mouse wheel (notches)
        public static float PinchRatio = 1; // >1 = fingers moving apart this frame
        public static int Touches;

        static bool prevHeld;
        static bool multiTouch;
        static float prevPinchDist;
        static readonly List<Vector2> touchPos = new List<Vector2>();
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        static int lastFrame = -1;

        public static float DragThreshold { get { return Mathf.Max(10f, Screen.height * 0.012f); } }

        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            bool held; Vector2 pos; float scroll;
            touchPos.Clear();
            ReadRaw(out held, out pos, out scroll);
            Touches = touchPos.Count;
            Scroll = scroll;

            Down = held && !prevHeld;
            Up = !held && prevHeld;

            if (held) Pos = pos;
            else if (Touches == 0 && !Up) Pos = pos;

            if (Down)
            {
                DownPos = pos;
                OverUI = IsOverUI(pos);
                Dragging = false;
                multiTouch = false;
            }
            if (held && !Dragging && (pos - DownPos).magnitude > DragThreshold) Dragging = true;
            if (Touches >= 2) multiTouch = true;

            // Pinch
            PinchRatio = 1f;
            if (Touches >= 2)
            {
                float d = Vector2.Distance(touchPos[0], touchPos[1]);
                if (prevPinchDist > 0.01f) PinchRatio = d / prevPinchDist;
                prevPinchDist = d;
            }
            else prevPinchDist = 0f;

            Tap = Up && !Dragging && !OverUI && !multiTouch;
            Held = held;
            prevHeld = held;
        }

        public static bool IsOverUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es);
            ped.position = pos;
            uiHits.Clear();
            es.RaycastAll(ped, uiHits);
            return uiHits.Count > 0;
        }

#if ENABLE_INPUT_SYSTEM
        static void ReadRaw(out bool held, out Vector2 pos, out float scroll)
        {
            held = false; pos = Pos; scroll = 0f;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                    if (t.press.isPressed) touchPos.Add(t.position.ReadValue());
            }
            if (touchPos.Count > 0)
            {
                held = true;
                pos = touchPos[0];
                return;
            }
            var m = Mouse.current;
            if (m != null)
            {
                pos = m.position.ReadValue();
                held = m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed;
                float y = m.scroll.ReadValue().y;
                if (Mathf.Abs(y) > 5f) y /= 120f;   // Windows reports 120 per notch
                scroll = y;
            }
        }
#else
        static void ReadRaw(out bool held, out Vector2 pos, out float scroll)
        {
            held = false; pos = Pos; scroll = 0f;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) touchPos.Add(t.position);
            }
            if (touchPos.Count > 0)
            {
                held = true;
                pos = touchPos[0];
                return;
            }
            pos = Input.mousePosition;
            held = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            scroll = Input.mouseScrollDelta.y;
        }
#endif
    }
}
