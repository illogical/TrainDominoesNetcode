using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Game.Navigation
{
    /// <summary>Latest-only local hand resize job. Gameplay always preempts this cosmetic owner.</summary>
    public sealed class HandReflowController : MonoBehaviour
    {
        private LayoutManager layout;
        private BoardNavigator nav;
        private Func<List<Transform>> hand;
        private Func<int> selectedIndex;
        private Func<bool> busy;
        private readonly HandReflowSchedule schedule = new HandReflowSchedule();
        private Transform[] tiles;
        private Vector3[] starts, ends, startScales;
        private float started, rootStart, rootEnd, amplitude, progress, slideSeconds, settleSeconds;
        private float[] bounces, phases;
        private Vector3 endScale;
        public float WaveSpreadSeconds { get; private set; }
        public float PhaseSpread { get; private set; }
        public int WaveSafetyReductions { get; private set; }
        private float inputGuardUntil;
        private bool manuallyInspected, guardingHandGesture;
        public bool Moving { get; private set; }
        public bool CanSelect => nav != null && nav.HandUsable && !Moving && Time.unscaledTime >= inputGuardUntil;
        public int RecoverySnaps { get; private set; }

        public void Initialize(LayoutManager owner)
        {
            layout = owner; nav = owner.Navigator;
            nav.HandGeometryChanged += GeometryChanged;
            nav.HandPanning += Pan;
        }

        public void Bind(Func<List<Transform>> currentHand, Func<int> selection, Func<bool> gameplayBusy)
        { hand = currentHand; selectedIndex = selection; busy = gameplayBusy; Invalidate(); }

        private void GeometryChanged()
        {
            Invalidate(); nav.HandMotionOwned = true;
            // A wider viewport can expose formerly clipped side faces that already intersect.
            // Recover before rendering that invalid row; ordinary valid resizes remain debounced.
            if (hand != null && nav.HandUsable && !busy() && ProjectedOverlap(hand())) Begin();
        }

        private bool ProjectedOverlap(List<Transform> row)
        {
            float previousRight = float.NegativeInfinity;
            foreach (var tile in row)
            {
                if (tile == null) continue;
                var body = tile.GetComponent<Renderer>().bounds;
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = nav.WorldToScreen(body.center + Vector3.Scale(body.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x);
                }
                if (Mathf.Min(previousRight, nav.Camera.pixelRect.xMax) - Mathf.Max(left, nav.Camera.pixelRect.xMin) > .5f) return true;
                previousRight = right;
            }
            return false;
        }

        public void SelectionChanged() { manuallyInspected = false; Invalidate(); }

        public void Invalidate() { if (Moving) Cancel(false); schedule.Dirty(Time.unscaledTime); }

        // Leave actual poses intact, removing only this job's shared cosmetic offset.
        public void Cancel(bool retry = true)
        {
            if (Moving)
            {
                for (int i = 0; i < tiles.Length; i++) if (tiles[i] != null) tiles[i].localPosition -= Vector3.up * bounces[i];
                Array.Clear(bounces, 0, bounces.Length);
                PhaseSpread = 0;
                // Adopt the actual interpolated strip before any gesture or replacement takes over.
                float pitch = tiles.Length > 1 && tiles[0] != null && tiles[tiles.Length - 1] != null ? (tiles[tiles.Length - 1].localPosition.x - tiles[0].localPosition.x) / (tiles.Length - 1) : nav.Hand.TileWidth;
                nav.Hand.Configure(tiles.Length, nav.HandViewWidth, Mathf.Min(nav.Hand.TileWidth, pitch), layout.HandSideMargin, pitch, pitch, nav.HandCenter);
                if (tiles.Length > 0 && tiles[0] != null) nav.Hand.Anchor(0, tiles[0].position.x);
                float offset = nav.Hand.Offset;
                for (int i = 0; i < tiles.Length; i++) if (tiles[i] != null) tiles[i].localPosition -= Vector3.right * (offset - nav.PresentedHandOffset);
                nav.SetPresentedHandOffset(offset);
                Moving = false; nav.HandMotionOwned = false;
            }
            nav.HandMotionOwned = false;
            if (retry) Invalidate(); else schedule.Clear();
        }

        private void Pan()
        {
            if (Moving) Cancel();
            manuallyInspected = true;
            guardingHandGesture |= nav.HandGestureActive;
            inputGuardUntil = Time.unscaledTime + 0.18f;
            Invalidate();
        }

        private void LateUpdate()
        {
            if (hand == null) return;
            if (nav.HandGestureActive)
            {
                if (Moving) Pan();
                if (guardingHandGesture) inputGuardUntil = Time.unscaledTime + 0.18f;
                Invalidate();
                return;
            }
            guardingHandGesture = false;
            if (!nav.HandUsable || busy()) { if (Moving) Cancel(); return; }
            if (Moving)
            {
                float elapsed = Time.realtimeSinceStartup - started;
                float rootT = Mathf.Clamp01(elapsed / (slideSeconds + WaveSpreadSeconds));
                progress = Mathf.Max(progress, Ease(rootT));
                nav.SetPresentedHandOffset(Mathf.Lerp(rootStart, rootEnd, progress));
                float low = 1, high = 0;
                for (int i = 0; i < tiles.Length; i++)
                {
                    float delay = HandReflowSchedule.WaveDelay(i, tiles.Length, WaveSpreadSeconds);
                    phases[i] = Mathf.Max(phases[i], Ease(Mathf.Clamp01((elapsed - delay) / slideSeconds)));
                    SamplePose(i, phases[i], elapsed - delay, out var world, out var scale, out bounces[i]);
                    if (tiles[i] != null) { tiles[i].localPosition = world - Vector3.right * nav.PresentedHandOffset; tiles[i].localScale = scale; }
                    low = Mathf.Min(low, phases[i]); high = Mathf.Max(high, phases[i]);
                }
                PhaseSpread = high - low;
                if (elapsed >= slideSeconds + WaveSpreadSeconds + settleSeconds)
                {
                    for (int i = 0; i < tiles.Length; i++) if (tiles[i] != null) { tiles[i].localPosition = ends[i]; tiles[i].localScale = endScale; }
                    Array.Clear(bounces, 0, bounces.Length);
                    PhaseSpread = 0; Moving = false; nav.HandMotionOwned = false;
                }
            }
            else if (schedule.Ready(Time.unscaledTime, Mathf.Max(0, layout.HandResizeQuietSeconds))) Begin();
        }

        private void Begin()
        {
            schedule.Clear();
            var current = hand();
            if (current.Count == 0) { nav.ConfigureHand(0); nav.HandMotionOwned = false; return; }
            tiles = current.ToArray(); starts = new Vector3[tiles.Length]; ends = new Vector3[tiles.Length]; startScales = new Vector3[tiles.Length]; bounces = new float[tiles.Length]; phases = new float[tiles.Length];
            int anchor = manuallyInspected ? -1 : selectedIndex();
            if (anchor < 0 || anchor >= tiles.Length)
            {
                anchor = 0;
                float left = nav.Hand.Center - nav.Hand.ViewWidth / 2 + nav.Hand.Margin / 2 + nav.Hand.TileWidth / 2;
                for (int i = 0; i < tiles.Length; i++) if (tiles[i].position.x >= left) { anchor = i; break; }
            }
            float anchorX = tiles[anchor].position.x;
            for (int i = 0; i < tiles.Length; i++) { starts[i] = tiles[i].localPosition; startScales[i] = tiles[i].localScale; }
            rootStart = nav.PresentedHandOffset;
            var targets = layout.GetHandLocalPositions(tiles.Length);
            endScale = layout.HandTileScale;
            nav.Hand.Anchor(anchor, anchorX);
            rootEnd = nav.Hand.Offset;
            bool safe = !ProjectedOverlap(current);
            for (int i = 0; i < tiles.Length; i++)
            {
                ends[i] = targets[i] + (i == selectedIndex() ? Vector3.up * layout.SelectionRaise : Vector3.zero);
                if (tiles[i].parent != nav.HandRoot || Quaternion.Angle(tiles[i].localRotation, LayoutManager.HandRotation) > 0.1f) safe = false;
                if (i > 0 && starts[i].x <= starts[i - 1].x) safe = false;
            }
            bool unchanged = true;
            for (int i = 0; i < tiles.Length; i++) unchanged &= Vector3.Distance(starts[i] + Vector3.right * rootStart,
                ends[i] + Vector3.right * rootEnd) < 1e-6f && Vector3.Distance(startScales[i], endScale) < 1e-4f;
            if (unchanged)
            {
                // Coordinate rebasing is invisible and must not create a wave/input mask.
                nav.SetPresentedHandOffset(rootEnd);
                for (int i = 0; i < tiles.Length; i++) { tiles[i].localPosition = ends[i]; tiles[i].localScale = endScale; }
                nav.HandMotionOwned = false;
                return;
            }
            if (!safe)
            {
                // Already-invalid/in-flight starting poses cannot claim a separated slide.
                RecoverySnaps++;
                nav.HandMotionOwned = false;
                nav.SetPresentedHandOffset(rootEnd);
                for (int i = 0; i < tiles.Length; i++) layout.SnapToHand(tiles[i], ends[i]);
                return;
            }
            started = Time.realtimeSinceStartup; progress = 0; PhaseSpread = 0;
            // The padded hand band reserves selection lift. Skip decorative motion if its clearance is used.
            float clearance = float.PositiveInfinity;
            for (int i = 0; i < tiles.Length; i++)
                clearance = Mathf.Min(clearance, layout.HandLocalY + layout.SelectionRaise + layout.TileSize.y * .04f - Mathf.Max(starts[i].y, ends[i].y));
            amplitude = layout.ReduceHandMotion ? 0 : Mathf.Max(0, Mathf.Min(layout.TileSize.y * 0.04f, 3f * Mathf.Max(1, nav.UiScale) / nav.PixelsPerWorld, clearance));
            slideSeconds = layout.ReduceHandMotion ? .08f : Mathf.Max(.01f, layout.HandResizeSlideSeconds);
            settleSeconds = layout.ReduceHandMotion ? 0 : Mathf.Max(0, layout.HandResizeSettleSeconds);
            WaveSpreadSeconds = layout.ReduceHandMotion || float.IsNaN(layout.HandResizeWaveSeconds) ? 0 : Mathf.Clamp(layout.HandResizeWaveSeconds, 0, .2f);
            while (!SeparatedWave(WaveSpreadSeconds))
            {
                if (WaveSpreadSeconds == 0)
                {
                    RecoverySnaps++; nav.SetPresentedHandOffset(rootEnd); nav.HandMotionOwned = false;
                    for (int i = 0; i < tiles.Length; i++) layout.SnapToHand(tiles[i], ends[i]);
                    return;
                }
                WaveSpreadSeconds *= .5f;
                if (WaveSpreadSeconds < .0075f) WaveSpreadSeconds = 0;
                WaveSafetyReductions++;
            }
            // Preflight time does not consume any of the visible animation.
            started = Time.realtimeSinceStartup;
            Moving = true; nav.HandMotionOwned = true;
        }

        private float Ease(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return Mathf.Clamp01(layout.HandResizeCurve != null ? layout.HandResizeCurve.Evaluate(t) : t * t * (3 - 2 * t));
        }

        private void SamplePose(int index, float phase, float localElapsed, out Vector3 world, out Vector3 scale, out float bounce)
        {
            world = Vector3.Lerp(starts[index] + Vector3.right * rootStart, ends[index] + Vector3.right * rootEnd, phase);
            scale = Vector3.Lerp(startScales[index], endScale, phase);
            bounce = layout.ReduceHandMotion ? 0 : amplitude * HandReflowSchedule.Settle(localElapsed, slideSeconds, settleSeconds);
            world += Vector3.up * bounce;
        }

        private Rect BodyAt(Vector3 world, Vector3 scale)
        {
            // Stable hand cohorts have the authored hand rotation and uniform scale ratios.
            float ratio = scale.x / layout.HandNativeScale.x;
            var center = world + layout.HandBodyCenter * ratio;
            var extents = layout.TileSize * (ratio / 2);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                var p = nav.WorldToScreen(center + Vector3.Scale(extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool SeparatedWave(float spread)
        {
            // Project actual body corners at 240 Hz before accepting independent phases.
            // This diagnostic samples trajectories; rendered-frame assertions remain a separate check.
            int samples = Mathf.Max(2, Mathf.CeilToInt((slideSeconds + settleSeconds + spread) * 240));
            var bounds = new Rect[tiles.Length];
            var trialPhases = new float[tiles.Length];
            var viewport = nav.Camera.pixelRect;
            for (int sample = 0; sample <= samples; sample++)
            {
                float elapsed = sample * (slideSeconds + settleSeconds + spread) / samples;
                float previousX = float.NegativeInfinity;
                for (int i = 0; i < tiles.Length; i++)
                {
                    float local = elapsed - HandReflowSchedule.WaveDelay(i, tiles.Length, spread);
                    trialPhases[i] = Mathf.Max(trialPhases[i], Ease(Mathf.Clamp01(local / slideSeconds)));
                    SamplePose(i, trialPhases[i], local, out var world, out var scale, out _);
                    if (world.x <= previousX) return false;
                    previousX = world.x;
                    bounds[i] = BodyAt(world, scale);
                    if (bounds[i].yMin < nav.HandUsableRect.yMin - .5f || bounds[i].yMax > nav.HandTop + .5f) return false;
                    for (int j = 0; j < i; j++)
                    {
                        float overlapX = Mathf.Min(bounds[j].xMax, bounds[i].xMax, viewport.xMax) - Mathf.Max(bounds[j].xMin, bounds[i].xMin, viewport.xMin);
                        float overlapY = Mathf.Min(bounds[j].yMax, bounds[i].yMax, viewport.yMax) - Mathf.Max(bounds[j].yMin, bounds[i].yMin, viewport.yMin);
                        if (overlapX > .1f && overlapY > .1f) return false;
                    }
                }
            }
            return true;
        }

        private void OnDisable() { if (nav != null) Cancel(false); }
        private void OnDestroy()
        {
            if (nav == null) return;
            nav.HandGeometryChanged -= GeometryChanged; nav.HandPanning -= Pan;
        }
    }
}
