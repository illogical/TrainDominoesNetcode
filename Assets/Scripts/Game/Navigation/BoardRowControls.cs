using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TMPro;
using TrainDominoesRulesEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Game.Navigation
{
    /// <summary>
    /// Row-anchored train controls (VIEW-001/U4). Each row gets a train-choice button and a jump-to-end button that
    /// follow the row as the board pans and zooms. While a tile is selected, rows with a legal placement for it are
    /// tinted and get a "Play" marker at their end. Highlights are derived from the current view every frame, so they
    /// cannot go stale. Choosing a row only raises the same train intent as before; the server still decides legality.
    /// </summary>
    public sealed class BoardRowControls : MonoBehaviour
    {
        public static readonly Color Legal = new Color(0.36f, 0.78f, 0.44f), Unavailable = new Color(0.62f, 0.62f, 0.62f), Pulse = new Color(1f, 0.74f, 0.25f);
        private const float TrainWidth = 132f, JumpWidth = 44f, Gap = 4f, Inset = 12f, MarkerWidth = 64f;

        private sealed class RowUi
        {
            public TrainIdDto Train;
            public Button Choose, Jump, Marker;
            public bool Highlighted;
        }

        private readonly List<RowUi> rows = new List<RowUi>();
        private BoardNavigator nav;
        private LayoutManager layout;
        private RectTransform panel;
        private Canvas canvas;
        private Func<string, Action, Button> makeButton;
        private Action<TrainIdDto> chooseTrain;
        private RectTransform track, thumb;
        private PlayerViewDto view;
        private bool playing, choosable;
        private int? selected;

        public IReadOnlyList<Button> TrainButtons => rows.Select(r => r.Choose).ToList();
        public IReadOnlyList<Button> JumpButtons => rows.Select(r => r.Jump).ToList();
        public IReadOnlyList<Button> Markers => rows.Select(r => r.Marker).ToList();
        public bool IsHighlighted(int slot) => slot < rows.Count && rows[slot].Highlighted;

        public void Initialize(BoardNavigator navigator, LayoutManager layoutManager, RectTransform owner, Func<string, Action, Button> factory, Action<TrainIdDto> onChoose)
        {
            nav = navigator; layout = layoutManager; panel = owner; makeButton = factory; chooseTrain = onChoose;
            canvas = owner.GetComponentInParent<Canvas>();
            nav.Applied += Position;
            track = NewImage("Board scroll track", new Color(1, 1, 1, 0.12f)).rectTransform;
            var thumbImage = NewImage("Board scroll thumb", new Color(1, 1, 1, 0.55f));
            thumb = thumbImage.rectTransform;
            thumbImage.raycastTarget = true;
            thumb.gameObject.AddComponent<ScrollThumb>().Dragged = DragThumb;
        }

        private void OnDestroy() { if (nav != null) nav.Applied -= Position; Clear(); }

        /// <summary>Rebuilds the controls for a roster: one row per player train plus the shared Mexican Train.</summary>
        public void Build(PlayerViewDto roster)
        {
            Clear();
            if (roster == null) return;
            for (int i = 0; i <= roster.Players.Length; i++)
            {
                var train = i == roster.Players.Length ? new TrainIdDto { IsShared = true } : new TrainIdDto { Owner = roster.Players[i] };
                int slot = i;
                var row = new RowUi { Train = train };
                row.Choose = makeButton(Title(roster, train), () => chooseTrain(train));
                row.Choose.name = "Train " + slot;
                row.Jump = makeButton(">|", () => nav.JumpToEnd(slot));
                row.Jump.name = "Jump to end " + slot;
                row.Marker = makeButton("Play", () => chooseTrain(train));
                row.Marker.name = "Play marker " + slot;
                rows.Add(row);
            }
        }

        private static string Title(PlayerViewDto view, TrainIdDto train) =>
            train.IsShared ? "Mexican Train" : train.Owner == view.Player ? "Your train" : PlayerText.Name(train.Owner) + " train";

        private void Clear()
        {
            foreach (var row in rows)
                foreach (var button in new[] { row.Choose, row.Jump, row.Marker })
                    if (button != null) Destroy(button.gameObject);
            rows.Clear();
        }

        /// <summary>Current data for highlights. Interactable follows the board's availability, never animation timing.</summary>
        public void Refresh(PlayerViewDto current, bool boardEnabled, int? selectedTile)
        {
            view = current;
            playing = current != null && current.Phase == MatchPhase.Playing && !current.IsSuspended;
            choosable = boardEnabled && selectedTile.HasValue;
            selected = selectedTile;
            Position();
        }

        private bool IsLegal(TrainIdDto train) =>
            selected.HasValue && view?.LegalPlacements != null &&
            view.LegalPlacements.Any(p => p.DominoId == selected.Value && p.Train.IsShared == train.IsShared && (train.IsShared || p.Train.Owner == train.Owner));

        private int Tiles(TrainIdDto train)
        {
            var match = view?.Trains?.FirstOrDefault(t => t.Id.IsShared == train.IsShared && (train.IsShared || t.Id.Owner == train.Owner));
            return match?.Placements?.Length ?? 0;
        }

        /// <summary>Null for an overlay canvas; the canvas camera when the canvas renders in camera space.</summary>
        private Camera UiCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        private void Position()
        {
            if (panel == null || nav == null) return;
            float scale = canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            nav.UiScale = scale;
            var rect = nav.BoardRect;
            float spacing = nav.State.RowSpacing * nav.BoardRoot.localScale.x * nav.PixelsPerWorld / scale;
            float height = Mathf.Clamp(spacing - 3f, 20f, 34f);
            for (int slot = 0; slot < rows.Count; slot++)
            {
                var row = rows[slot];
                bool exists = slot < nav.ActiveRows;
                float y = exists ? nav.WorldToScreen(nav.Row(slot).position).y : -1;
                bool onScreen = exists && playing && y >= rect.yMin - 1 && y <= rect.yMax + 1;
                bool legal = choosable && IsLegal(row.Train);
                row.Highlighted = onScreen && legal;
                Place(row.Choose, onScreen, new Vector2(Inset * scale, y), TrainWidth, height);
                Place(row.Jump, onScreen, new Vector2((Inset + TrainWidth + Gap) * scale, y), JumpWidth, height);
                row.Choose.interactable = choosable;
                row.Choose.image.color = !selected.HasValue ? Color.white : legal ? Legal : Unavailable;
                bool unseen = exists && nav.State.HasUnseenTiles(slot);
                row.Jump.image.color = unseen ? Color.Lerp(Color.white, Pulse, Mathf.PingPong(Time.unscaledTime * 2f, 1f)) : Color.white;
                // The marker sits at the row's end, clamped inside the board when the end is scrolled away.
                if (legal && onScreen)
                {
                    var end = nav.WorldToScreen(nav.Row(slot).TransformPoint(layout.LabelLocalPosition(Tiles(row.Train))));
                    float x = Mathf.Clamp(end.x, rect.xMin, rect.xMax - MarkerWidth * scale);
                    Place(row.Marker, true, new Vector2(x, y), MarkerWidth, height);
                    row.Marker.image.color = Legal;
                    row.Marker.interactable = choosable;
                }
                else row.Marker.gameObject.SetActive(false);
            }
            PositionIndicator(rect, scale);
        }

        private void Place(Button button, bool visible, Vector2 screenLeftCenter, float width, float height)
        {
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (!visible) return;
            var rt = (RectTransform)button.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screenLeftCenter, UiCamera, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = local;
            rt.sizeDelta = new Vector2(width, height);
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) text.fontSize = Mathf.Clamp(height * 0.46f, 11f, 16f);
        }

        // Vertical scroll indicator, shown only when the zoomed rows are taller than the board (D3).
        private void PositionIndicator(Rect rect, float scale)
        {
            var state = nav.State;
            bool show = playing && state.OverflowsVertically;
            track.gameObject.SetActive(show); thumb.gameObject.SetActive(show);
            if (!show) return;
            float x = rect.xMax + 4 * scale, width = 8f;
            SetRect(track, new Vector2(x, rect.center.y), width, rect.height / scale);
            float ratio = Mathf.Clamp01(state.VisibleHeight / (state.ContentTop - state.ContentBottom));
            float thumbHeight = Mathf.Max(24f, rect.height / scale * ratio);
            float travel = rect.height - thumbHeight * scale;
            SetRect(thumb, new Vector2(x, rect.yMax - thumbHeight * scale / 2 - state.VerticalFraction * travel), width, thumbHeight);
        }

        private void SetRect(RectTransform rt, Vector2 screenCenter, float width, float height)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screenCenter, UiCamera, out var local);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
            rt.sizeDelta = new Vector2(width, height);
        }

        private void DragThumb(float pixels)
        {
            var state = nav.State;
            float ratio = Mathf.Clamp01(state.VisibleHeight / (state.ContentTop - state.ContentBottom));
            float travel = nav.BoardRect.height * (1 - ratio);
            if (travel <= 1) return;
            state.ScrollBoardTo(state.VerticalFraction - pixels / travel);
            nav.SnapVisible();
        }

        private Image NewImage(string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(panel, false);
            image.color = color;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        private sealed class ScrollThumb : MonoBehaviour, IDragHandler, IBeginDragHandler
        {
            public Action<float> Dragged;
            public void OnBeginDrag(PointerEventData eventData) { }
            public void OnDrag(PointerEventData eventData) { Dragged?.Invoke(eventData.delta.y); }
        }
    }
}
