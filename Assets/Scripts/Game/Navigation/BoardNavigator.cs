using System.Collections.Generic;
using Assets.Scripts.Helpers;
using UnityEngine;

namespace Assets.Scripts.Game.Navigation
{
    /// <summary>
    /// Applies <see cref="BoardNavigationState"/> to board roots created at runtime (VIEW-001). The camera never moves,
    /// so the hand, overlay UI and hand selection are unaffected by board navigation. Local presentation state only.
    /// </summary>
    /// <remarks>
    /// Hierarchy: Board (zoom scale and vertical pan) → Train row i (horizontal offset) → track tiles and the row label.
    /// The engine sits directly under Board. Hand tiles sit under Hand, whose x offset scrolls an overfull hand.
    /// </remarks>
    public sealed class BoardNavigator : MonoBehaviour
    {
        // Reference pixels at the 1080-pixel canvas reference height, scaled by UiScale.
        public float LeftGutter = 200f, RightGutter = 212f, TopGutter = 126f, HandGap = 8f;
        /// <summary>Minimum on-screen height of a track tile's face at 160 dpi, which sets the readable minimum zoom.</summary>
        public float ReadablePixels = 28f;
        public float Smoothing = 16f;

        public BoardNavigationState State { get; } = new BoardNavigationState();
        public HandStrip Hand { get; } = new HandStrip();
        public Transform BoardRoot { get; private set; }
        public Transform HandRoot { get; private set; }
        /// <summary>The board viewport in screen pixels: between the row controls, the action column, the status bar and the hand.</summary>
        public Rect BoardRect { get; private set; }
        public float HandTop { get; private set; }
        public float HandBaseline { get; private set; }
        public float HandViewWidth { get; private set; }
        public float HandTileWidth { get; private set; }
        public float HandCenter { get; private set; }
        public bool HandUsable { get; private set; }
        public event System.Action HandGeometryChanged;
        public event System.Action HandPanning;
        public bool HandMotionOwned { get; set; }
        public bool HandGestureActive { get; set; }
        public float PresentedHandOffset => visibleHand;
        public float PixelsPerWorld { get; private set; } = 1f;
        /// <summary>Canvas scale factor. Zero means height / 1080, matching the scene's canvas scaler.</summary>
        public float UiScale { get; set; }
        public int ActiveRows { get; private set; }
        public Camera Camera => cam;
        /// <summary>Raised after the roots move, so row-anchored controls can follow in the same frame.</summary>
        public event System.Action Applied;

        private readonly List<Transform> rows = new List<Transform>();
        private readonly List<float> visibleOffsets = new List<float>();
        private float visibleZoom = 1f, visiblePanY, visibleHand;
        private LayoutManager layout;
        private Camera cam;
        private Vector3 viewOrigin;
        private int handCount;
        private Vector2Int lastPixels;
        private float lastUi, lastDpi;
        private Rect lastSafe, lastCameraRect;
        private Matrix4x4 lastProjection;

        public void Initialize(LayoutManager owner, Camera camera)
        {
            layout = owner;
            cam = camera;
            BoardRoot = new GameObject("Board").transform;
            HandRoot = new GameObject("Hand").transform;
            UpdateViewport(true);
            State.Reset();
            SnapVisible();
        }

        public Transform Row(int slot) => rows[slot];

        /// <summary>Creates the train rows for this many trains. Rows are never destroyed, so labels parented to them survive.</summary>
        public void SetRowCount(int count)
        {
            while (rows.Count < count)
            {
                var row = new GameObject("Train row " + rows.Count).transform;
                row.SetParent(BoardRoot, false);
                rows.Add(row);
                visibleOffsets.Add(0);
            }
            for (int i = 0; i < rows.Count; i++) if (rows[i].gameObject.activeSelf != i < count) rows[i].gameObject.SetActive(i < count);
            if (count == ActiveRows && State.RowCount == count) return;
            ActiveRows = count;
            var centers = new float[count];
            for (int i = 0; i < count; i++) centers[i] = layout.RowLocalY(i, count);
            State.SetRows(centers, layout.RowSpacing);
            Apply();
        }

        /// <summary>Called when a row's tile count changes; a row that was following its end eases to the new end.</summary>
        public void SetRowTiles(int slot, int tiles)
        {
            if (slot < ActiveRows) State.OnRowGrew(slot, layout.RowContentLength(tiles));
        }

        /// <summary>New round or match: every row back to its end and the board zoomed to fit (D4).</summary>
        public void ResetNavigation()
        {
            State.Reset();
            Hand.Reset();
            SnapVisible();
        }

        public void ConfigureHand(int count)
        {
            handCount = count;
            HandTileWidth = MeasureHandWidth(count);
            Hand.Configure(count, HandViewWidth, HandTileWidth, layout.HandSideMargin,
                HandTileWidth * (1 + Mathf.Max(0, layout.HandMinimumGap)), HandTileWidth * (1 + Mathf.Max(layout.HandMinimumGap, layout.HandMaximumGap)), HandCenter);
        }

        public bool IsSettled
        {
            get
            {
                if (State.IsMoving || !Near(visibleZoom, State.Zoom) || !Near(visiblePanY, State.PanY) || !Near(visibleHand, Hand.Offset)) return false;
                for (int i = 0; i < ActiveRows; i++) if (!Near(visibleOffsets[i], State.Offset(i))) return false;
                return true;
            }
        }

        public void SnapVisible()
        {
            visibleZoom = State.Zoom; visiblePanY = State.PanY; if (!HandMotionOwned) visibleHand = Hand.Offset;
            for (int i = 0; i < ActiveRows; i++) visibleOffsets[i] = State.Offset(i);
            Apply();
        }

        private void LateUpdate()
        {
            UpdateViewport(false);
            float dt = Time.unscaledDeltaTime;
            State.Tick(dt);
            float k = 1f - Mathf.Exp(-Smoothing * dt);
            visibleZoom = Approach(visibleZoom, State.Zoom, k);
            visiblePanY = Approach(visiblePanY, State.PanY, k);
            if (!HandMotionOwned) visibleHand = Approach(visibleHand, Hand.Offset, k);
            for (int i = 0; i < ActiveRows; i++) visibleOffsets[i] = Approach(visibleOffsets[i], State.Offset(i), k);
            Apply();
        }

        private void Apply()
        {
            if (BoardRoot == null) return;
            BoardRoot.position = new Vector3(viewOrigin.x, viewOrigin.y - visibleZoom * visiblePanY, 0);
            BoardRoot.localScale = Vector3.one * visibleZoom;
            for (int i = 0; i < ActiveRows; i++) rows[i].localPosition = new Vector3(visibleOffsets[i], State.RowCenter(i), 0);
            HandRoot.position = new Vector3(visibleHand, 0, 0);
            Applied?.Invoke();
        }

        /// <summary>Recomputes the viewport when the window, canvas scale or dpi changes.</summary>
        public void UpdateViewport(bool force)
        {
            if (cam == null || layout == null) return;
            var pixels = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            float ui = UiScale > 0 ? UiScale : pixels.y / 1080f;
            var safe = cam.targetTexture != null ? cam.pixelRect : Screen.safeArea;
            if (!force && pixels == lastPixels && Mathf.Approximately(ui, lastUi) && Mathf.Approximately(Screen.dpi, lastDpi)
                && safe == lastSafe && cam.pixelRect == lastCameraRect && cam.projectionMatrix == lastProjection) return;
            lastSafe = safe; lastCameraRect = cam.pixelRect; lastProjection = cam.projectionMatrix;
            lastPixels = pixels; lastUi = ui; lastDpi = Screen.dpi;
            if (pixels.x <= 0 || pixels.y <= 0 || safe.width <= 0 || safe.height <= 0)
            {
                HandUsable = false;
                HandGeometryChanged?.Invoke();
                return;
            }
            var size = layout.TileSize;
            // Use the body face nearest the camera so perspective depth cannot clip the lower values.
            var usable = Rect.MinMaxRect(Mathf.Max(cam.pixelRect.xMin, safe.xMin), Mathf.Max(cam.pixelRect.yMin, safe.yMin),
                Mathf.Min(cam.pixelRect.xMax, safe.xMax), Mathf.Min(cam.pixelRect.yMax, safe.yMax));
            Vector3 handLow = ScreenToPlane(usable.min, layout.HandBodyCenter.z - size.z / 2), handHigh = ScreenToPlane(usable.max, layout.HandBodyCenter.z - size.z / 2);
            HandViewWidth = Mathf.Max(0, handHigh.x - handLow.x);
            HandCenter = (handLow.x + handHigh.x) / 2;
            HandBaseline = handLow.y - layout.HandBodyCenter.y + size.y / 2 + layout.HandBottomPadding;
            HandTileWidth = MeasureHandWidth(handCount);
            HandUsable = usable.width > 0 && usable.height > 0 && HandViewWidth >= HandTileWidth + layout.HandSideMargin
                && handHigh.y - handLow.y >= size.y + layout.HandBottomPadding + layout.SelectionRaise;
            HandTop = cam.WorldToScreenPoint(new Vector3(0, HandBaseline + layout.HandBodyCenter.y + size.y / 2 + layout.SelectionRaise + size.y * .04f + .001f, layout.HandBodyCenter.z - size.z / 2)).y;
            var rect = Rect.MinMaxRect(LeftGutter * ui, Mathf.Max(0, HandTop + HandGap * ui), pixels.x - RightGutter * ui, pixels.y - TopGutter * ui);
            if (rect.width < pixels.x * 0.25f || rect.height < pixels.y * 0.2f) rect = Rect.MinMaxRect(pixels.x * 0.05f, pixels.y * 0.3f, pixels.x * 0.95f, pixels.y * 0.9f);
            BoardRect = rect;
            Vector3 low = ScreenToPlane(rect.min), high = ScreenToPlane(rect.max);
            viewOrigin = new Vector3(low.x, (low.y + high.y) / 2, 0);
            PixelsPerWorld = rect.height / Mathf.Max(1e-6f, high.y - low.y);
            State.SetViewport(high.x - low.x, high.y - low.y);
            float readable = GestureMetrics.Physical(ReadablePixels, Screen.dpi) / Mathf.Max(1e-6f, size.x * PixelsPerWorld);
            State.SetZoomLimits(readable, Mathf.Max(1.5f, readable * 4f), 1f);
            State.EndTolerance = size.y * 0.05f;
            State.TrailingSpace = layout.RowTrailingSpace;
            if (force) ConfigureHand(handCount);
            HandGeometryChanged?.Invoke();
            Apply();
        }

        // Measure at the content edges, not the empty viewport edges: a fitting hand must not
        // acquire wider gaps merely because the window gets wider. Overflow uses visible edges.
        private float MeasureHandWidth(int count)
        {
            var size = layout.TileSize;
            float width = size.x;
            for (int iteration = 0; iteration < 8; iteration++)
            {
                float half = Mathf.Min(HandViewWidth / 2, Mathf.Max(0, count - 1) * width
                    * (1 + Mathf.Max(0, layout.HandMinimumGap, layout.HandMaximumGap)) / 2);
                for (int sample = -1; sample <= 1; sample++)
                {
                    var center = new Vector3(HandCenter + sample * half, HandBaseline, 0) + layout.HandBodyCenter;
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var world = center + Vector3.Scale(size / 2, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        float x = ScreenToPlane(cam.WorldToScreenPoint(world)).x;
                        min = Mathf.Min(min, x); max = Mathf.Max(max, x);
                    }
                    width = Mathf.Max(width, max - min);
                }
            }
            return width;
        }

        // Screen ↔ board plane (z = 0). The camera is perspective, so this is a ray-plane intersection.
        public Vector3 ScreenToPlane(Vector2 screen, float z = 0)
        {
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            float t = Mathf.Abs(ray.direction.z) < 1e-6f ? 0 : (z - ray.origin.z) / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        public Vector2 WorldToScreen(Vector3 world) => cam.WorldToScreenPoint(world);
        public bool InBoard(Vector2 screen) => BoardRect.Contains(screen);
        public bool InHand(Vector2 screen) => screen.y <= HandTop && screen.y >= 0;
        public float PixelsToView(float pixels) => pixels / PixelsPerWorld;

        public void ScreenToView(Vector2 screen, out float vx, out float vy)
        {
            var world = ScreenToPlane(screen);
            vx = world.x - viewOrigin.x;
            vy = world.y - viewOrigin.y;
        }

        /// <summary>The row under a screen point, using what is drawn now rather than where smoothing is heading.</summary>
        public int RowAtScreen(Vector2 screen)
        {
            if (!InBoard(screen)) return -1;
            ScreenToView(screen, out _, out float vy);
            float boardY = vy / visibleZoom + visiblePanY;
            for (int i = 0; i < ActiveRows; i++)
                if (Mathf.Abs(boardY - State.RowCenter(i)) <= State.RowSpacing / 2) return i;
            return -1;
        }

        public void ZoomAtScreen(float factor, Vector2 screen, int row)
        {
            ScreenToView(screen, out float vx, out float vy);
            State.ZoomAt(factor, vx, vy, row);
        }

        public void Fit() => State.Fit();
        public void JumpToEnd(int row) { if (row >= 0 && row < ActiveRows) State.JumpToEnd(row); }
        public void ToggleEndOrFit(int row) { if (row >= 0 && row < ActiveRows) State.ToggleEndOrFit(row); }

        /// <summary>Scrolls and pans so a track tile is fully on screen.</summary>
        public void RevealTile(int row, int index)
        {
            if (row < 0 || row >= ActiveRows) return;
            State.RevealX(row, layout.TrackLocalX(index), layout.TileSize.y);
        }

        public void PanHand(float pixels) { HandPanning?.Invoke(); Hand.Pan(PixelsToView(pixels)); }
        public void SetPresentedHandOffset(float offset) { visibleHand = offset; Apply(); }
        public void EnsureHandVisible(int index) { Hand.EnsureVisible(index); }

        private static float Approach(float current, float target, float k)
        {
            float next = Mathf.Lerp(current, target, k);
            // At very high frame rates the step can fall below float precision; snap rather than stall short of the target.
            return Mathf.Abs(next - target) < 1e-5f || next == current ? target : next;
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-5f;
    }
}
