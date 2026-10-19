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
        private float started, rootStart, rootEnd, bounce, amplitude, progress;
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
                foreach (var tile in tiles) if (tile != null) tile.localPosition -= Vector3.up * bounce;
                bounce = 0;
                // Adopt the actual interpolated strip before any gesture or replacement takes over.
                float pitch = tiles.Length > 1 && tiles[0] != null && tiles[1] != null ? tiles[1].localPosition.x - tiles[0].localPosition.x : nav.Hand.TileWidth;
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
                float slide = layout.ReduceHandMotion ? 0.08f : Mathf.Max(0.01f, layout.HandResizeSlideSeconds);
                float settle = layout.ReduceHandMotion ? 0 : Mathf.Max(0, layout.HandResizeSettleSeconds);
                float elapsed = Time.unscaledTime - started;
                float t = Mathf.Clamp01(elapsed / slide);
                // A shared monotonic phase keeps every adjacent separation a convex combination.
                progress = Mathf.Max(progress, Mathf.Clamp01(layout.HandResizeCurve != null ? layout.HandResizeCurve.Evaluate(t) : t * t * (3 - 2 * t)));
                if (t >= 1) progress = 1;
                bounce = layout.ReduceHandMotion ? 0 : amplitude * HandReflowSchedule.Settle(elapsed, slide, settle);
                nav.SetPresentedHandOffset(Mathf.Lerp(rootStart, rootEnd, progress));
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i] != null) { tiles[i].localPosition = Vector3.Lerp(starts[i], ends[i], progress) + Vector3.up * bounce; tiles[i].localScale = Vector3.Lerp(startScales[i], layout.HandTileScale, progress); }
                if (elapsed >= slide + settle)
                {
                    for (int i = 0; i < tiles.Length; i++) if (tiles[i] != null) { tiles[i].localPosition = ends[i]; tiles[i].localScale = layout.HandTileScale; }
                    bounce = 0; Moving = false; nav.HandMotionOwned = false;
                }
            }
            else if (schedule.Ready(Time.unscaledTime, Mathf.Max(0, layout.HandResizeQuietSeconds))) Begin();
        }

        private void Begin()
        {
            schedule.Clear();
            var current = hand();
            if (current.Count == 0) { nav.ConfigureHand(0); nav.HandMotionOwned = false; return; }
            tiles = current.ToArray(); starts = new Vector3[tiles.Length]; ends = new Vector3[tiles.Length]; startScales = new Vector3[tiles.Length];
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
            nav.Hand.Anchor(anchor, anchorX);
            rootEnd = nav.Hand.Offset;
            bool safe = !ProjectedOverlap(current);
            for (int i = 0; i < tiles.Length; i++)
            {
                ends[i] = targets[i] + (i == selectedIndex() ? Vector3.up * layout.SelectionRaise : Vector3.zero);
                if (tiles[i].parent != nav.HandRoot || Quaternion.Angle(tiles[i].localRotation, LayoutManager.HandRotation) > 0.1f) safe = false;
                if (i > 0 && starts[i].x <= starts[i - 1].x) safe = false;
            }
            bool unchanged = Mathf.Abs(rootStart - rootEnd) < 1e-6f;
            for (int i = 0; i < tiles.Length; i++) unchanged &= Vector3.Distance(starts[i], ends[i]) < 1e-6f && Vector3.Distance(startScales[i], layout.HandTileScale) < 1e-4f;
            if (unchanged) { nav.HandMotionOwned = false; return; }
            if (!safe)
            {
                // Already-invalid/in-flight starting poses cannot claim a separated slide.
                RecoverySnaps++;
                nav.HandMotionOwned = false;
                nav.SetPresentedHandOffset(rootEnd);
                for (int i = 0; i < tiles.Length; i++) layout.SnapToHand(tiles[i], ends[i]);
                return;
            }
            started = Time.unscaledTime; progress = 0; bounce = 0;
            // The padded hand band reserves selection lift. Skip decorative motion if its clearance is used.
            float clearance = float.PositiveInfinity;
            for (int i = 0; i < tiles.Length; i++)
                clearance = Mathf.Min(clearance, layout.HandLocalY + layout.SelectionRaise + layout.TileSize.y * .04f - Mathf.Max(starts[i].y, ends[i].y));
            amplitude = layout.ReduceHandMotion ? 0 : Mathf.Max(0, Mathf.Min(layout.TileSize.y * 0.04f, 3f * Mathf.Max(1, nav.UiScale) / nav.PixelsPerWorld, clearance));
            Moving = true; nav.HandMotionOwned = true;
        }

        private void OnDisable() { if (nav != null) Cancel(false); }
        private void OnDestroy()
        {
            if (nav == null) return;
            nav.HandGeometryChanged -= GeometryChanged; nav.HandPanning -= Pan;
        }
    }
}
