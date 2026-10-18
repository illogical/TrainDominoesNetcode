using System;
using System.Collections.Generic;

namespace Assets.Scripts.Game.Navigation
{
    /// <summary>
    /// Local board navigation (VIEW-001). Plain C# so console checks can link it. It never affects commands,
    /// legality or authority: it only decides which part of the board the local player is looking at.
    /// </summary>
    /// <remarks>
    /// Units. "Board units" are local units under the board root: one unit is one world unit at zoom 1.
    /// "View units" are world units measured on the board plane from the board viewport: <c>vx</c> from its
    /// left edge, <c>vy</c> from its vertical centre (up is positive).
    /// A board point (x, y) in row i appears at view position
    /// <c>vx = Zoom * (x + Offset(i))</c>, <c>vy = Zoom * (y - PanY)</c>.
    /// Row offsets are zero or negative: a negative offset scrolls the row's later tiles into view.
    /// </remarks>
    public sealed class BoardNavigationState
    {
        private sealed class Row
        {
            public float Center, Length, Offset, Velocity;
            public bool FollowingEnd = true, HasUnseenTiles;
        }

        private readonly List<Row> rows = new List<Row>();
        private float zoomBeforeFit;
        private float boardVelocity;

        public float Zoom { get; private set; } = 1f;
        public float PanY { get; private set; }
        public float MinZoom { get; private set; } = 0.25f;
        public float MaxZoom { get; private set; } = 3f;
        /// <summary>The zoom that today's fixed layout used. Fit never zooms in past it.</summary>
        public float DefaultZoom { get; private set; } = 1f;
        public float ViewWidth { get; private set; } = 1f;
        public float ViewHeight { get; private set; } = 1f;
        public float RowSpacing { get; private set; } = 1f;
        /// <summary>How close to its end (in board units) a row counts as showing its playable end.</summary>
        public float EndTolerance { get; set; } = 0.001f;
        private float trailingSpace;
        /// <summary>
        /// Space kept after a row's last tile (for its label) when showing the end. Capped at a share of the visible
        /// width, so at high zoom the last tile itself always stays on screen.
        /// </summary>
        public float TrailingSpace { get => trailingSpace; set { trailingSpace = Math.Max(0, value); ClampAll(); } }
        private float Trailing => Math.Min(trailingSpace, VisibleWidth * 0.35f);
        /// <summary>Exponential decay rate for fling inertia, per second.</summary>
        public float FlingDecay { get; set; } = 4f;
        public int RowCount => rows.Count;

        public void SetZoomLimits(float minZoom, float maxZoom, float defaultZoom = 1f)
        {
            if (minZoom <= 0 || maxZoom <= 0) throw new ArgumentOutOfRangeException(nameof(minZoom));
            MinZoom = Math.Min(minZoom, maxZoom);
            MaxZoom = maxZoom;
            DefaultZoom = Clamp(defaultZoom, MinZoom, MaxZoom);
            Zoom = Clamp(Zoom, MinZoom, MaxZoom);
            ClampAll();
        }

        /// <summary>The visible board region, in world units on the board plane.</summary>
        public void SetViewport(float width, float height)
        {
            ViewWidth = Math.Max(1e-6f, width);
            ViewHeight = Math.Max(1e-6f, height);
            ClampAll();
        }

        /// <summary>Row centres in board units (any order) and the distance between neighbouring rows.</summary>
        public void SetRows(IReadOnlyList<float> centers, float spacing)
        {
            RowSpacing = Math.Max(1e-6f, spacing);
            while (rows.Count > centers.Count) rows.RemoveAt(rows.Count - 1);
            while (rows.Count < centers.Count) rows.Add(new Row());
            for (int i = 0; i < centers.Count; i++) rows[i].Center = centers[i];
            ClampAll();
        }

        public float VisibleWidth => ViewWidth / Zoom;
        public float VisibleHeight => ViewHeight / Zoom;
        public float ContentBottom { get { float min = 0; for (int i = 0; i < rows.Count; i++) min = i == 0 ? rows[i].Center : Math.Min(min, rows[i].Center); return min - RowSpacing / 2; } }
        public float ContentTop { get { float max = 0; for (int i = 0; i < rows.Count; i++) max = i == 0 ? rows[i].Center : Math.Max(max, rows[i].Center); return max + RowSpacing / 2; } }
        /// <summary>True when the stacked rows are taller than the viewport at this zoom, so the board scrolls vertically.</summary>
        public bool OverflowsVertically => rows.Count > 0 && ContentTop - ContentBottom > VisibleHeight + EndTolerance;

        public float Offset(int row) => rows[row].Offset;
        public float Length(int row) => rows[row].Length;
        public float RowCenter(int row) => rows[row].Center;
        public bool FollowingEnd(int row) => rows[row].FollowingEnd;
        /// <summary>New tiles arrived while the player was looking elsewhere in this row (its jump control pulses).</summary>
        public bool HasUnseenTiles(int row) => rows[row].HasUnseenTiles;
        public float EndOffset(int row) => Math.Min(0f, VisibleWidth - rows[row].Length - Trailing);
        public bool AtEnd(int row) => rows[row].Offset <= EndOffset(row) + EndTolerance;
        public bool IsMoving { get { if (Math.Abs(boardVelocity) > 0) return true; foreach (var r in rows) if (Math.Abs(r.Velocity) > 0) return true; return false; } }

        /// <summary>Content length of a row in board units: from the board's left edge to the right edge of its last tile.</summary>
        public void OnRowGrew(int row, float length)
        {
            var r = rows[row];
            bool grew = length > r.Length + EndTolerance;
            r.Length = Math.Max(0f, length);
            if (r.FollowingEnd) r.Offset = EndOffset(row);
            else if (grew) r.HasUnseenTiles = true;
            ClampRow(row);
        }

        public void PanRow(int row, float viewDelta)
        {
            var r = rows[row];
            r.Offset += viewDelta / Zoom;
            ClampRow(row);
            r.FollowingEnd = AtEnd(row);
            if (r.FollowingEnd) r.HasUnseenTiles = false;
        }

        /// <summary>Moves the board up by <paramref name="viewDelta"/> view units (content follows a dragging finger).</summary>
        public void PanBoard(float viewDelta)
        {
            PanY -= viewDelta / Zoom;
            ClampPan();
        }

        /// <summary>Vertical scroll position as a fraction: 0 shows the top of the stacked rows, 1 the bottom.</summary>
        public float VerticalFraction
        {
            get
            {
                float range = ContentTop - ContentBottom - VisibleHeight;
                return range <= 0 ? 0 : Clamp((ContentTop - VisibleHeight / 2 - PanY) / range, 0, 1);
            }
        }

        public void ScrollBoardTo(float fraction)
        {
            float range = ContentTop - ContentBottom - VisibleHeight;
            if (range <= 0) return;
            PanY = ContentTop - VisibleHeight / 2 - Clamp(fraction, 0, 1) * range;
            ClampPan();
        }

        /// <summary>
        /// Zooms by <paramref name="factor"/> keeping the board point under (vx, vy) fixed. The anchored row, and any row
        /// not following its end, keep that point under the pointer. Other rows that were following their end stay at it.
        /// </summary>
        public void ZoomAt(float factor, float vx, float vy, int anchorRow = -1)
        {
            if (factor <= 0 || float.IsNaN(factor)) return;
            float next = Clamp(Zoom * factor, MinZoom, MaxZoom);
            if (Math.Abs(next - Zoom) < 1e-7f) return;
            float boardY = vy / Zoom + PanY;
            var anchored = new bool[rows.Count];
            var boardX = new float[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                anchored[i] = i == anchorRow || !rows[i].FollowingEnd;
                boardX[i] = vx / Zoom - rows[i].Offset;
            }
            Zoom = next;
            PanY = boardY - vy / Zoom;
            // Follow flags are recomputed from the new offsets; re-snapping following rows first would undo the anchor.
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Offset = anchored[i] ? vx / Zoom - boardX[i] : EndOffset(i);
                ClampRow(i);
                rows[i].FollowingEnd = AtEnd(i);
                if (rows[i].FollowingEnd) rows[i].HasUnseenTiles = false;
            }
            ClampPan();
        }

        /// <summary>The zoom at which every row fits, never below the readable minimum or above the default.</summary>
        public float FitZoom
        {
            get
            {
                float longest = 0;
                foreach (var r in rows) longest = Math.Max(longest, r.Length);
                float height = rows.Count == 0 ? 0 : ContentTop - ContentBottom;
                float fit = DefaultZoom;
                if (longest > 0) fit = Math.Min(fit, ViewWidth / (longest + trailingSpace));
                if (height > 0) fit = Math.Min(fit, ViewHeight / height);
                return Clamp(fit, MinZoom, DefaultZoom);
            }
        }

        public bool IsAtFit => Math.Abs(Zoom - FitZoom) < 1e-4f;

        /// <summary>Overview: everything fits if it can be read; otherwise height first and rows stay scrollable.</summary>
        public void Fit()
        {
            StopInertia();
            if (!IsAtFit) zoomBeforeFit = Zoom;
            Zoom = FitZoom;
            PanY = (ContentTop + ContentBottom) / 2;
            for (int i = 0; i < rows.Count; i++) { rows[i].FollowingEnd = true; rows[i].HasUnseenTiles = false; rows[i].Offset = EndOffset(i); }
            ClampAll();
        }

        public void JumpToEnd(int row)
        {
            var r = rows[row];
            r.Velocity = 0;
            r.Offset = EndOffset(row);
            r.FollowingEnd = true;
            r.HasUnseenTiles = false;
            RevealRow(row);
        }

        /// <summary>Touch double-tap: go to the row's playable end; from there, toggle between the overview and the previous zoom.</summary>
        public void ToggleEndOrFit(int row)
        {
            if (!rows[row].FollowingEnd) { JumpToEnd(row); return; }
            if (IsAtFit && zoomBeforeFit > 0 && Math.Abs(zoomBeforeFit - Zoom) > 1e-4f)
            {
                Zoom = Clamp(zoomBeforeFit, MinZoom, MaxZoom);
                zoomBeforeFit = 0;
                ClampAll();
                JumpToEnd(row);
                return;
            }
            Fit();
        }

        /// <summary>Scrolls the board vertically, if needed, so the whole row is visible.</summary>
        public void RevealRow(int row)
        {
            float half = VisibleHeight / 2, top = rows[row].Center + RowSpacing / 2, bottom = rows[row].Center - RowSpacing / 2;
            if (top > PanY + half) PanY = top - half;
            if (bottom < PanY - half) PanY = bottom + half;
            ClampPan();
        }

        /// <summary>Scrolls a row so a board x position is visible (centred if it was off-screen), and reveals the row.</summary>
        public void RevealX(int row, float boardX, float margin = 0)
        {
            var r = rows[row];
            float left = -r.Offset, right = left + VisibleWidth;
            if (boardX - margin < left || boardX + margin > right) r.Offset = VisibleWidth / 2 - boardX;
            ClampRow(row);
            r.FollowingEnd = AtEnd(row);
            if (r.FollowingEnd) r.HasUnseenTiles = false;
            RevealRow(row);
        }

        /// <summary>The row whose band contains view height <paramref name="vy"/>, or -1.</summary>
        public int RowAt(float vy)
        {
            float boardY = vy / Zoom + PanY;
            for (int i = 0; i < rows.Count; i++)
                if (Math.Abs(boardY - rows[i].Center) <= RowSpacing / 2) return i;
            return -1;
        }

        public void ViewToBoard(int row, float vx, float vy, out float x, out float y)
        {
            x = vx / Zoom - (row >= 0 ? rows[row].Offset : 0);
            y = vy / Zoom + PanY;
        }

        public void BoardToView(int row, float x, float y, out float vx, out float vy)
        {
            vx = Zoom * (x + (row >= 0 ? rows[row].Offset : 0));
            vy = Zoom * (y - PanY);
        }

        public bool IsVisible(int row, float boardX, float boardY)
        {
            BoardToView(row, boardX, boardY, out float vx, out float vy);
            return vx >= -EndTolerance && vx <= ViewWidth + EndTolerance && Math.Abs(vy) <= ViewHeight / 2 + EndTolerance;
        }

        public void FlingRow(int row, float viewVelocity) { rows[row].Velocity = viewVelocity; }
        public void FlingBoard(float viewVelocity) { boardVelocity = viewVelocity; }

        public void StopInertia()
        {
            boardVelocity = 0;
            foreach (var r in rows) r.Velocity = 0;
        }

        /// <summary>Advances fling inertia. Stops at a scroll limit and below a small speed.</summary>
        public void Tick(float seconds)
        {
            if (seconds <= 0) return;
            float decay = (float)Math.Exp(-FlingDecay * seconds), stop = ViewWidth * 0.01f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Velocity == 0) continue;
                float before = r.Offset;
                PanRow(i, r.Velocity * seconds);
                r.Velocity = Math.Abs(r.Offset - before) < 1e-7f || Math.Abs(r.Velocity * decay) < stop ? 0 : r.Velocity * decay;
            }
            if (boardVelocity != 0)
            {
                float before = PanY;
                PanBoard(boardVelocity * seconds);
                boardVelocity = Math.Abs(PanY - before) < 1e-7f || Math.Abs(boardVelocity * decay) < stop ? 0 : boardVelocity * decay;
            }
        }

        /// <summary>New round or match: empty rows, everything following its end, zoomed to fit.</summary>
        public void Reset()
        {
            StopInertia();
            foreach (var r in rows) { r.Length = 0; r.Offset = 0; r.FollowingEnd = true; r.HasUnseenTiles = false; }
            zoomBeforeFit = 0;
            Zoom = FitZoom;
            PanY = (ContentTop + ContentBottom) / 2;
            ClampAll();
        }

        private void ClampAll()
        {
            Zoom = Clamp(Zoom, MinZoom, MaxZoom);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].FollowingEnd) rows[i].Offset = EndOffset(i);
                ClampRow(i);
            }
            ClampPan();
        }

        private void ClampRow(int row) { rows[row].Offset = Clamp(rows[row].Offset, EndOffset(row), 0f); }

        private void ClampPan()
        {
            if (rows.Count == 0) { PanY = 0; return; }
            float top = ContentTop, bottom = ContentBottom, half = VisibleHeight / 2;
            PanY = top - bottom <= VisibleHeight ? (top + bottom) / 2 : Clamp(PanY, bottom + half, top - half);
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
