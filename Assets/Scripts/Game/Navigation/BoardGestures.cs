using System;
using System.Numerics;

namespace Assets.Scripts.Game.Navigation
{
    public enum DragAxis { None, Horizontal, Vertical }

    /// <summary>
    /// Separates a tap from a drag for one pointer (VIEW-001). Positions are screen pixels. Once movement passes the
    /// threshold the gesture is a drag for the rest of its life and can never select.
    /// </summary>
    public sealed class PointerGesture
    {
        private Vector2 last;
        private float lastTime;
        public float Threshold { get; }
        public bool Active { get; private set; }
        public bool IsDrag { get; private set; }
        public DragAxis Axis { get; private set; }
        public Vector2 Start { get; private set; }
        /// <summary>Smoothed pointer velocity in pixels per second, for fling inertia.</summary>
        public Vector2 Velocity { get; private set; }

        public PointerGesture(float thresholdPixels) { Threshold = Math.Max(1f, thresholdPixels); }

        public void Begin(Vector2 position, float time)
        {
            Active = true; IsDrag = false; Axis = DragAxis.None;
            Start = last = position; lastTime = time; Velocity = Vector2.Zero;
        }

        /// <summary>Returns the movement since the previous call once this is a drag, otherwise zero.</summary>
        public Vector2 Move(Vector2 position, float time)
        {
            if (!Active) return Vector2.Zero;
            if (!IsDrag)
            {
                var total = position - Start;
                if (total.Length() < Threshold) return Vector2.Zero;
                IsDrag = true;
                Axis = Math.Abs(total.X) >= Math.Abs(total.Y) ? DragAxis.Horizontal : DragAxis.Vertical;
                // The threshold distance is applied too, so content does not jump when the drag starts.
                last = Start;
            }
            var delta = position - last;
            float dt = time - lastTime;
            if (dt > 1e-4f) Velocity = Vector2.Lerp(Velocity, delta / dt, 0.6f);
            last = position; lastTime = time;
            return delta;
        }

        /// <summary>Ends the gesture. True when it was a tap (never crossed the threshold).</summary>
        public bool End(Vector2 position, float time)
        {
            if (!Active) return false;
            // A pointer that stopped before lifting does not fling.
            bool rested = time - lastTime > 0.08f;
            Move(position, time);
            if (rested) Velocity = Vector2.Zero;
            Active = false;
            return !IsDrag;
        }

        public void Cancel() { Active = false; IsDrag = true; Velocity = Vector2.Zero; }
    }

    /// <summary>Recognises a second tap close in time and place to the first.</summary>
    public sealed class TapSequence
    {
        private Vector2 lastPosition;
        private float lastTime = float.NegativeInfinity;
        private int lastTarget = int.MinValue;
        public float MaxInterval { get; }
        public float MaxDistance { get; }

        public TapSequence(float maxIntervalSeconds, float maxDistancePixels) { MaxInterval = maxIntervalSeconds; MaxDistance = maxDistancePixels; }

        /// <summary>Registers a tap on <paramref name="target"/> (for example a row). True if it completes a double tap.</summary>
        public bool Tap(Vector2 position, float time, int target)
        {
            bool isDouble = target == lastTarget && time - lastTime <= MaxInterval && Vector2.Distance(position, lastPosition) <= MaxDistance;
            // A completed double tap is consumed, so a third tap starts a new sequence.
            if (isDouble) { lastTime = float.NegativeInfinity; lastTarget = int.MinValue; }
            else { lastTime = time; lastPosition = position; lastTarget = target; }
            return isDouble;
        }
    }

    public static class GestureMetrics
    {
        public const float ReferenceDpi = 160f;
        /// <summary>Drag threshold in pixels: about 8 px for a mouse and 14 px for touch at 160 dpi, scaled by the screen's dpi.</summary>
        public static float DragThreshold(float dpi, bool touch)
        {
            float scale = dpi > 0 ? Math.Max(1f, dpi / ReferenceDpi) : 1f;
            return (touch ? 14f : 8f) * scale;
        }

        /// <summary>A minimum on-screen size in pixels, scaled from the 160 dpi reference.</summary>
        public static float Physical(float referencePixels, float dpi) => referencePixels * (dpi > 0 ? Math.Max(1f, dpi / ReferenceDpi) : 1f);

        /// <summary>Converts a wheel delta to notches. Uniform scroll deltas are already one per notch; legacy ranges use 120.</summary>
        public static float WheelNotches(float delta, bool uniform) => uniform ? delta : delta / 120f;

        /// <summary>Zoom factor for a number of wheel notches: about 12% per notch, symmetric in and out.</summary>
        public static float WheelZoomFactor(float notches) => (float)Math.Pow(1.12, notches);

        /// <summary>Pinch zoom factor between two finger separations.</summary>
        public static float PinchFactor(float previousDistance, float distance) => previousDistance > 1f && distance > 1f ? distance / previousDistance : 1f;
    }

    /// <summary>
    /// The hand row (VIEW-001/U4). Spreads tiles across the width as before, but never closer than a minimum spacing;
    /// beyond that it becomes a horizontally scrollable strip. Positions are world x centred on the screen.
    /// </summary>
    public sealed class HandStrip
    {
        public int Count { get; private set; }
        public float ViewWidth { get; private set; }
        public float TileWidth { get; private set; }
        public float Margin { get; private set; }
        public float MinSpacing { get; private set; }
        public float Offset { get; private set; }

        public void Configure(int count, float viewWidth, float tileWidth, float margin, float minSpacing)
        {
            Count = Math.Max(0, count); ViewWidth = viewWidth; TileWidth = tileWidth; Margin = margin; MinSpacing = Math.Max(tileWidth, minSpacing);
            Offset = Clamp(Offset);
        }

        /// <summary>The spread spacing the original layout used across the full width.</summary>
        public float SpreadSpacing => Count <= 1 ? 0 : (ViewWidth - TileWidth - Margin) / (Count - 1);
        public bool Scrolls => Count > 1 && SpreadSpacing < MinSpacing;
        public float Spacing => Scrolls ? MinSpacing : SpreadSpacing;
        public float ContentWidth => Count <= 0 ? 0 : TileWidth + Spacing * (Count - 1);
        public float MinOffset => Scrolls ? Math.Min(0, (ViewWidth - Margin) - ContentWidth) : 0;

        public float X(int index)
        {
            if (Count <= 1) return 0f;
            if (!Scrolls) return index * SpreadSpacing - (ViewWidth - TileWidth - Margin) / 2;
            float left = -ViewWidth / 2 + Margin / 2 + TileWidth / 2;
            return left + index * Spacing + Offset;
        }

        /// <summary>Position without the scroll offset, for tiles under a hand root that applies the offset itself.</summary>
        public float LocalX(int index) => X(index) - Offset;

        public void Pan(float delta) { Offset = Clamp(Offset + delta); }

        /// <summary>Scrolls just enough to show the tile at <paramref name="index"/> fully.</summary>
        public void EnsureVisible(int index)
        {
            if (!Scrolls || index < 0 || index >= Count) return;
            float x = X(index), half = TileWidth / 2, left = -ViewWidth / 2 + Margin / 2, right = ViewWidth / 2 - Margin / 2;
            if (x - half < left) Offset += left - (x - half);
            else if (x + half > right) Offset -= (x + half) - right;
            Offset = Clamp(Offset);
        }

        public void Reset() { Offset = 0; }

        private float Clamp(float value) => value > 0 ? 0 : value < MinOffset ? MinOffset : value;
    }
}
