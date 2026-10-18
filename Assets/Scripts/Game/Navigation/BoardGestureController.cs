using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Num = System.Numerics;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Assets.Scripts.Game.Navigation
{
    /// <summary>
    /// Pointer and touch gestures for board navigation (VIEW-001, D1–D3). A press that stays under the drag threshold
    /// is reported as <see cref="Tapped"/> on release; a drag never selects. Gestures that start on overlay UI are ignored.
    /// Navigation is local presentation state: nothing here sends commands or judges legality.
    /// </summary>
    public sealed class BoardGestureController : MonoBehaviour
    {
        private enum Region { None, Board, Hand }

        /// <summary>A click or tap at a screen position (pixels), raised on release.</summary>
        public event Action<Vector2> Tapped;
        public float WheelScrollPixels = 48f;

        private BoardNavigator nav;
        private PointerGesture gesture;
        private Region region;
        private int pressRow = -1;
        private bool touchActive, multiTouch, pinching;
        private float lastPinchDistance;
        private Vector2 lastPinchMid;
        private readonly TapSequence clicks = new TapSequence(0.35f, 24f);
        private readonly TapSequence taps = new TapSequence(0.35f, 40f);
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public void Initialize(BoardNavigator navigator) { nav = navigator; }

        private void OnEnable() { EnhancedTouchSupport.Enable(); }
        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            gesture = null; multiTouch = pinching = touchActive = false;
        }

        private void Update()
        {
            if (nav == null) return;
            float now = Time.unscaledTime;
            var touches = Touch.activeTouches;
            if (touches.Count > 0 || multiTouch) { UpdateTouch(touches, now); return; }
            UpdateMouse(now);
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) nav.Fit();
        }

        private void UpdateMouse(float now)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Begin(position, now, false);
            else if (mouse.leftButton.isPressed && gesture != null && gesture.Active) Drag(position, now);
            if (mouse.leftButton.wasReleasedThisFrame && gesture != null && gesture.Active) End(position, now, false);
            var scroll = mouse.scroll.ReadValue();
            if (scroll != Vector2.zero) Wheel(position, scroll);
        }

        private void UpdateTouch(UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch> touches, float now)
        {
            if (touches.Count >= 2)
            {
                // A second finger turns the gesture into a pinch; no tap or drag fires until every finger lifts.
                multiTouch = true;
                if (gesture != null && gesture.Active) gesture.Cancel();
                Pinch(touches[0].screenPosition, touches[1].screenPosition);
                return;
            }
            if (touches.Count == 0) { multiTouch = pinching = touchActive = false; return; }
            pinching = false;
            if (multiTouch) return;
            var touch = touches[0];
            switch (touch.phase)
            {
                case TouchPhase.Began: Begin(touch.screenPosition, now, true); touchActive = true; break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary: if (touchActive && gesture != null && gesture.Active) Drag(touch.screenPosition, now); break;
                case TouchPhase.Ended: if (touchActive && gesture != null && gesture.Active) End(touch.screenPosition, now, true); touchActive = false; break;
                case TouchPhase.Canceled: gesture?.Cancel(); touchActive = false; break;
            }
        }

        private void Begin(Vector2 position, float now, bool touch)
        {
            nav.State.StopInertia();
            if (OverUi(position)) { gesture = null; return; }
            gesture = new PointerGesture(GestureMetrics.DragThreshold(Screen.dpi, touch));
            gesture.Begin(ToNum(position), now);
            region = nav.InHand(position) ? Region.Hand : nav.InBoard(position) ? Region.Board : Region.None;
            pressRow = region == Region.Board ? nav.RowAtScreen(position) : -1;
        }

        private void Drag(Vector2 position, float now)
        {
            var delta = gesture.Move(ToNum(position), now);
            if (!gesture.IsDrag || delta == Num.Vector2.Zero) return;
            if (region == Region.Hand) { if (gesture.Axis == DragAxis.Horizontal) nav.PanHand(delta.X); }
            else if (region == Region.Board)
            {
                if (gesture.Axis == DragAxis.Vertical && nav.State.OverflowsVertically) nav.State.PanBoard(nav.PixelsToView(delta.Y));
                else if (gesture.Axis == DragAxis.Horizontal && pressRow >= 0) nav.State.PanRow(pressRow, nav.PixelsToView(delta.X));
            }
            nav.SnapVisible(); // direct manipulation follows the pointer without smoothing lag
        }

        private void End(Vector2 position, float now, bool touch)
        {
            bool tap = gesture.End(ToNum(position), now);
            if (tap)
            {
                Tapped?.Invoke(position);
                if (region == Region.Board && pressRow >= 0 && (touch ? taps : clicks).Tap(ToNum(position), now, pressRow))
                {
                    if (touch) nav.ToggleEndOrFit(pressRow); // D2: double-tap toggles end and overview
                    else nav.JumpToEnd(pressRow);            // D1: double-click returns to the playable end
                }
                return;
            }
            if (!touch || region != Region.Board) return;
            // Fling inertia after a touch drag (D2).
            var velocity = gesture.Velocity;
            if (gesture.Axis == DragAxis.Horizontal && pressRow >= 0) nav.State.FlingRow(pressRow, nav.PixelsToView(velocity.X));
            else if (gesture.Axis == DragAxis.Vertical && nav.State.OverflowsVertically) nav.State.FlingBoard(nav.PixelsToView(velocity.Y));
        }

        private void Wheel(Vector2 position, Vector2 scroll)
        {
            if (OverUi(position)) return;
            bool uniform = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            float x = GestureMetrics.WheelNotches(scroll.x, uniform), y = GestureMetrics.WheelNotches(scroll.y, uniform);
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            // Shift+wheel and sideways trackpad swipes scroll; only a plain vertical wheel zooms (D1).
            bool sideways = shift || Mathf.Abs(x) > Mathf.Abs(y);
            float sideAmount = shift && Mathf.Abs(x) <= Mathf.Abs(y) ? y : x;
            if (nav.InHand(position)) { nav.PanHand(sideAmount * WheelScrollPixels); nav.SnapVisible(); return; }
            if (!nav.InBoard(position)) return;
            int row = nav.RowAtScreen(position);
            if (sideways) { if (row >= 0) nav.State.PanRow(row, nav.PixelsToView(sideAmount * WheelScrollPixels)); }
            else nav.ZoomAtScreen(GestureMetrics.WheelZoomFactor(y), position, row);
        }

        private void Pinch(Vector2 a, Vector2 b)
        {
            float distance = Vector2.Distance(a, b);
            var mid = (a + b) / 2;
            if (!pinching)
            {
                pinching = true; lastPinchDistance = distance; lastPinchMid = mid;
                nav.State.StopInertia();
                return;
            }
            nav.ZoomAtScreen(GestureMetrics.PinchFactor(lastPinchDistance, distance), mid, nav.RowAtScreen(mid));
            // Two-finger drag scrolls the board vertically when the rows overflow (D3).
            if (nav.State.OverflowsVertically) nav.State.PanBoard(nav.PixelsToView(mid.y - lastPinchMid.y));
            lastPinchDistance = distance; lastPinchMid = mid;
            nav.SnapVisible();
        }

        /// <summary>True when overlay UI (buttons, panels, the hand-scroll strip) is under the point.</summary>
        public bool OverUi(Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            uiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        private static Num.Vector2 ToNum(Vector2 v) => new Num.Vector2(v.x, v.y);
    }
}
