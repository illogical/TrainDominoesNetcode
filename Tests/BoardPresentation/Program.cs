using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game.Navigation;
using TrainDominoesRulesEngine;

// VIEW-001 navigation model, gestures and hand strip; VIEW-002/004 player-facing text and exactly-once notices.
// Links the production sources; no Unity types. Unity rendering, raycasts and input devices are checked separately
// by the Tests/UnityIntegration view probes.
static class Program
{
    static int passed;
    static void Check(bool value, string reason) { if (!value) throw new Exception("FAIL: " + reason); passed++; }
    static bool Near(float a, float b, float eps = 1e-4f) => Math.Abs(a - b) <= eps;

    // A board like the scene's: rows one tile-width apart, tiles 2:1, viewport about 0.3 × 0.17 world units.
    const float W = 0.0125f, L = 0.025f;
    static float RowLength(int tiles) => 2 * W + L * Math.Max(1, tiles) - L / 2;
    static BoardNavigationState Board(int rows = 9, float viewW = 0.30f, float viewH = 0.17f, float minZoom = 0.5f)
    {
        var s = new BoardNavigationState { EndTolerance = L * 0.05f, TrailingSpace = 3 * L };
        s.SetViewport(viewW, viewH);
        s.SetZoomLimits(minZoom, 3f, 1f);
        s.SetRows(Enumerable.Range(0, rows).Select(i => i * W - (rows - 1) * W / 2).ToArray(), W);
        s.Reset();
        for (int i = 0; i < rows; i++) s.OnRowGrew(i, RowLength(0));
        return s;
    }

    static void Main()
    {
        Navigation();
        Gestures();
        Hand();
        HandZoom();
        Reflow();
        Feedback();
        Console.WriteLine($"PASS: {passed} board presentation checks (navigation model, gestures, hand strip, feedback text). No Unity rendering or input devices.");
    }

    static void Navigation()
    {
        // Zoom clamps at both bounds.
        var s = Board();
        s.ZoomAt(100f, 0.1f, 0); Check(Near(s.Zoom, s.MaxZoom), "zoom clamps at the maximum");
        s.ZoomAt(1e-4f, 0.1f, 0); Check(Near(s.Zoom, s.MinZoom), "zoom clamps at the readable minimum");
        s.ZoomAt(-1f, 0, 0); s.ZoomAt(float.NaN, 0, 0); Check(Near(s.Zoom, s.MinZoom), "invalid zoom factors are ignored");

        // Anchoring: the board point under the pointer stays under it, in the anchored row and vertically.
        s = Board();
        s.OnRowGrew(4, RowLength(40));
        s.PanRow(4, 0.05f); // away from the end so the row is anchored rather than following
        s.ViewToBoard(4, 0.12f, 0.004f, out float bx, out float by);
        s.ZoomAt(1.7f, 0.12f, 0.004f, 4);
        s.BoardToView(4, bx, by, out float vx, out float vy);
        Check(Near(vx, 0.12f) && Near(vy, 0.004f), "zoom-in keeps the anchored point under the pointer");
        s.ZoomAt(0.95f, 0.12f, 0.004f, 4);
        s.BoardToView(4, bx, by, out vx, out vy);
        Check(Near(vx, 0.12f, 2e-4f) && Near(vy, 0.004f, 2e-4f), "zoom-out keeps the anchored point under the pointer");
        // Zooming in over a row that is following its end anchors that row too (and it stops following).
        s = Board(); s.OnRowGrew(4, RowLength(40));
        Check(s.FollowingEnd(4), "long row starts at its end");
        s.ViewToBoard(4, 0.15f, 0.004f, out bx, out by);
        s.ZoomAt(1.5f, 0.15f, 0.004f, 4);
        s.BoardToView(4, bx, by, out vx, out vy);
        Check(Near(vx, 0.15f) && !s.FollowingEnd(4), "zooming over a following row keeps the point under the pointer");
        // Near the top edge the vertical clamp wins: rows never leave a gap above the board.
        s = Board(); s.ZoomAt(1.7f, 0.12f, 0.08f);
        Check(Near(s.PanY, s.ContentTop - s.VisibleHeight / 2), "zooming at the edge clamps instead of exposing empty space");

        // A following row that is not under the pointer stays at its end through zoom.
        s = Board();
        s.OnRowGrew(2, RowLength(30));
        s.ZoomAt(2f, 0.05f, 0, 7);
        Check(s.FollowingEnd(2) && s.AtEnd(2), "following rows stay at their end when zooming elsewhere");

        // Fit respects the readable minimum: everything fits if legible, otherwise height first and rows scroll.
        s = Board();
        for (int i = 0; i < 9; i++) s.OnRowGrew(i, RowLength(3));
        s.Fit();
        Check(s.Zoom <= s.DefaultZoom && s.Zoom >= s.MinZoom, "fit stays between the readable minimum and the default");
        Check(!s.OverflowsVertically, "a short board fits vertically at fit zoom");
        s.OnRowGrew(0, RowLength(60)); s.Fit();
        Check(Near(s.Zoom, s.MinZoom) && s.EndOffset(0) < 0, "a very long row cannot shrink below the readable minimum; it stays scrollable");
        Check(s.FollowingEnd(0) && s.AtEnd(0), "fit shows every row's playable end");
        var tall = Board(rows: 9, viewH: 0.05f);
        tall.Fit();
        Check(Near(tall.Zoom, tall.MinZoom) && tall.OverflowsVertically, "nine rows in a short window overflow at the minimum and scroll vertically");

        // Follow only at the end (D4).
        s = Board();
        s.OnRowGrew(1, RowLength(20));
        Check(s.FollowingEnd(1) && s.AtEnd(1), "a following row tracks new placements");
        s.PanRow(1, 0.1f);
        Check(!s.FollowingEnd(1), "scrolling away clears following");
        float held = s.Offset(1);
        s.OnRowGrew(1, RowLength(21));
        Check(Near(s.Offset(1), held) && s.HasUnseenTiles(1), "an inspected row keeps its position and reports unseen tiles");
        s.JumpToEnd(1);
        Check(s.AtEnd(1) && s.FollowingEnd(1) && !s.HasUnseenTiles(1), "jump to end returns and clears the pending state");
        s.PanRow(1, 0.05f);
        s.PanRow(1, -0.05f + L * 0.04f * s.Zoom);
        Check(s.FollowingEnd(1), "returning within the tolerance of the end resumes following");
        s.OnRowGrew(1, RowLength(10));
        Check(s.AtEnd(1), "a shrinking (undone) row stays at its end");

        // Viewport resize keeps following rows at their end and clamps the rest.
        s = Board();
        s.OnRowGrew(3, RowLength(30)); s.OnRowGrew(5, RowLength(30)); s.PanRow(5, 0.4f);
        s.SetViewport(0.15f, 0.17f);
        Check(s.AtEnd(3), "resize keeps a following row at its end");
        Check(s.Offset(5) <= 0 && s.Offset(5) >= s.EndOffset(5), "resize clamps an inspected row");

        // Reset (new round or match).
        s.Reset();
        Check(Enumerable.Range(0, 9).All(i => s.FollowingEnd(i) && s.Offset(i) == 0 && !s.HasUnseenTiles(i)), "reset returns every row to following");
        Check(Near(s.Zoom, s.FitZoom), "reset zooms to fit");

        // Vertical scroll and row hit-testing.
        s = Board();
        s.ZoomAt(3f, 0.1f, 0);
        Check(s.OverflowsVertically, "zooming in makes nine rows overflow");
        s.PanBoard(10f);
        Check(Near(s.PanY, s.ContentBottom + s.VisibleHeight / 2), "vertical pan clamps at the bottom row");
        s.PanBoard(-10f);
        Check(Near(s.PanY, s.ContentTop - s.VisibleHeight / 2), "vertical pan clamps at the top row");
        s.RevealRow(0);
        s.BoardToView(-1, 0, s.RowCenter(0), out _, out vy);
        Check(Math.Abs(vy) <= s.ViewHeight / 2 && s.RowAt(vy) == 0, "reveal brings a row on screen and hit-testing finds it");
        s.ScrollBoardTo(0.5f);
        Check(Near(s.VerticalFraction, 0.5f), "the vertical indicator position round-trips");
        s.ScrollBoardTo(0f); Check(Near(s.PanY, s.ContentTop - s.VisibleHeight / 2), "indicator top shows the top row");

        // Double-tap toggling (D2).
        s = Board();
        s.OnRowGrew(6, RowLength(40));
        s.ZoomAt(2f, 0.1f, 0, 6); s.PanRow(6, 0.2f);
        s.ToggleEndOrFit(6); Check(s.AtEnd(6) && Near(s.Zoom, 2f), "double-tap away from the end jumps to it");
        s.ToggleEndOrFit(6); Check(s.IsAtFit, "double-tap at the end shows the overview");
        s.ToggleEndOrFit(6); Check(Near(s.Zoom, 2f) && s.AtEnd(6), "double-tap in the overview returns to the end at the previous zoom");

        // Fling inertia decays and stops at the scroll limit.
        s = Board(); s.OnRowGrew(0, RowLength(60)); s.PanRow(0, 0.6f);
        s.FlingRow(0, 5f); // travels about velocity / decay = 1.25 units, more than the 0.65 to the start
        for (int i = 0; i < 300 && s.IsMoving; i++) s.Tick(1 / 60f);
        Check(!s.IsMoving && Near(s.Offset(0), 0), "a fling towards the start stops at the limit");
        s.JumpToEnd(0); s.PanRow(0, 0.05f); float before = s.Offset(0); s.FlingRow(0, 0.03f);
        for (int i = 0; i < 600 && s.IsMoving; i++) s.Tick(1 / 60f);
        Check(!s.IsMoving && s.Offset(0) > before, "a small fling decays to rest");

        // At high zoom the label space is capped, so jumping to the end always shows the last tile.
        s = Board(viewW: 0.06f); s.OnRowGrew(0, RowLength(30)); s.ZoomAt(100f, 0, 0); s.JumpToEnd(0);
        Check(s.IsVisible(0, RowLength(30) - L / 2, s.RowCenter(0)), "the last tile stays visible at maximum zoom");

        // Model-level reachability: every tile of nine 40-tile rows can be brought on screen at the default zoom.
        s = Board(minZoom: 0.9f);
        for (int r = 0; r < 9; r++) s.OnRowGrew(r, RowLength(40));
        s.ZoomAt(1.4f, 0.1f, 0);
        bool all = true;
        for (int r = 0; r < 9; r++)
            for (int t = 0; t < 40; t++)
            {
                float x = 2 * W + t * L;
                s.RevealX(r, x, L);
                all &= s.IsVisible(r, x, s.RowCenter(r));
            }
        Check(all, "every tile in nine 40-tile rows is reachable by scripted navigation");
    }

    static void Gestures()
    {
        var g = new PointerGesture(8);
        g.Begin(new Vector2(100, 100), 0);
        Check(g.Move(new Vector2(104, 103), 0.02f) == Vector2.Zero && !g.IsDrag, "movement under the threshold is not a drag");
        Check(g.End(new Vector2(105, 103), 0.04f), "a press released under the threshold is a tap");
        g.Begin(new Vector2(100, 100), 0);
        var d = g.Move(new Vector2(112, 101), 0.02f);
        Check(g.IsDrag && g.Axis == DragAxis.Horizontal && Near(d.X, 12) && Near(d.Y, 1), "crossing the threshold starts a horizontal drag without a jump");
        g.Move(new Vector2(104, 100), 0.04f);
        Check(!g.End(new Vector2(101, 100), 0.05f), "a drag never becomes a tap, even if it returns to the start");
        g.Begin(new Vector2(0, 0), 0); g.Move(new Vector2(2, 20), 0.02f);
        Check(g.Axis == DragAxis.Vertical, "vertical-dominant drags lock to the vertical axis");
        g.Begin(new Vector2(0, 0), 0); g.Move(new Vector2(30, 0), 0.016f); g.Move(new Vector2(60, 0), 0.032f); g.End(new Vector2(90, 0), 0.048f);
        Check(g.Velocity.X > 1000, "a quick release keeps fling velocity");
        g.Begin(new Vector2(0, 0), 0); g.Move(new Vector2(30, 0), 0.016f); g.End(new Vector2(30, 0), 0.5f);
        Check(g.Velocity == Vector2.Zero, "stopping before lifting does not fling");
        g.Begin(new Vector2(0, 0), 0); g.Cancel();
        Check(!g.End(Vector2.Zero, 0.01f), "a cancelled (pinch-interrupted) press never taps");

        var taps = new TapSequence(0.35f, 40);
        Check(!taps.Tap(new Vector2(10, 10), 0, 2) && taps.Tap(new Vector2(14, 12), 0.2f, 2), "two quick taps on one row are a double tap");
        Check(!taps.Tap(new Vector2(14, 12), 0.3f, 2), "a third tap starts a new sequence");
        Check(!taps.Tap(new Vector2(14, 12), 0.4f, 3), "a tap on another row is not a double tap");
        Check(!taps.Tap(new Vector2(14, 12), 1.0f, 3), "a slow second tap is not a double tap");

        Check(Near(GestureMetrics.DragThreshold(0, false), 8) && Near(GestureMetrics.DragThreshold(0, true), 14), "thresholds fall back when dpi is unknown");
        Check(Near(GestureMetrics.DragThreshold(320, true), 28) && Near(GestureMetrics.DragThreshold(96, false), 8), "thresholds scale up with dpi but never below the reference");
        Check(Near(GestureMetrics.WheelZoomFactor(1) * GestureMetrics.WheelZoomFactor(-1), 1), "wheel zoom in and out are symmetric");
        Check(Near(GestureMetrics.WheelNotches(120, false), 1) && Near(GestureMetrics.WheelNotches(1, true), 1), "wheel notches normalise both delta ranges");
        Check(Near(GestureMetrics.PinchFactor(100, 150), 1.5f) && Near(GestureMetrics.PinchFactor(0, 150), 1), "pinch factor follows finger separation");
    }

    static void Hand()
    {
        var h = new HandStrip();
        const float view = 0.47f, tile = 0.0125f, margin = 0.01f;
        // Fitting content stays together; extra viewport width becomes outer whitespace.
        h.Configure(15, view, tile, margin, tile * 1.08f);
        float spacing = tile * 1.16f, offset = 14 * spacing / 2;
        Check(!h.Scrolls && Enumerable.Range(0, 15).All(i => Near(h.X(i), i * spacing - offset)), "a normal hand is centered with a capped gap");
        h.Configure(1, view, tile, margin, tile * 1.08f);
        Check(h.X(0) == 0, "a single tile is centred");
        // Beyond it, tiles keep the minimum spacing and the hand scrolls.
        h.Configure(60, view, tile, margin, tile * 1.08f);
        Check(h.Scrolls && Near(h.X(1) - h.X(0), tile * 1.08f), "an overfull hand keeps the minimum spacing");
        Check(h.X(0) - tile / 2 >= -view / 2 + margin / 2 - 1e-6f, "an overfull hand starts at the left edge");
        h.EnsureVisible(59);
        Check(h.X(59) + tile / 2 <= view / 2 - margin / 2 + 1e-5f, "the selected last tile is scrolled into view");
        h.Pan(-10);
        Check(Near(h.Offset, h.MinOffset), "hand scrolling clamps at the end");
        Check(Near(h.LocalX(3) + h.Offset, h.X(3)), "local positions exclude the scroll offset");
        h.Reset(); Check(h.Offset == 0, "the hand offset resets on a new round");
        h.Configure(5, view, tile, margin, tile * 1.08f); h.Pan(-1);
        Check(h.Offset == 0, "a hand that fits cannot scroll");
    }

    static void HandZoom()
    {
        var h = new HandStrip();
        h.Configure(16, .47f, .03f, .024f, .0324f, .0348f, .07f, .6f);
        Check(h.Scale < 1 && h.Scale >= .6f && !h.Scrolls, "crowded hand zooms just enough to fit instead of clipping an end");
        Check(Near((h.X(0) + h.X(15)) / 2, .07f), "zoomed hand stays centered within its usable region");
        Check(h.X(0)-h.TileWidth/2 >= .07f-.47f/2+.012f-1e-5f && h.X(15)+h.TileWidth/2 <= .07f+.47f/2-.012f+1e-5f, "zoom preserves minimum outer margins");
        h.Configure(60, .47f, .03f, .024f, .0324f, .0348f, 0, .6f);
        Check(Near(h.Scale,.6f) && h.Scrolls, "unreadable fit falls back to scrolling at zoom floor");
        h.EnsureVisible(59); Check(h.X(59)+h.TileWidth/2<=.47f/2-.012f+1e-5f, "zoomed overflow still exposes complete last tile");
        h.Configure(5, 2, .03f, .024f, .0324f, .0348f, 0, .6f);
        Check(h.Scale==1 && !h.Scrolls, "small hands retain original readable size");
        float content=h.ContentWidth;
        h.Configure(5, 4, .03f, .024f, .0324f, .0348f, 0, .6f);
        Check(Near(h.ContentWidth,content), "extra width remains outside a fitting hand");
    }

    static void Reflow()
    {
        var timer = new HandReflowSchedule();
        timer.Dirty(1); Check(!timer.Ready(1.159f, .16f), "resize waits for quiet threshold");
        Check(timer.Ready(1.161f, .16f), "resize becomes ready after quiet threshold");
        timer.Dirty(1.15f); Check(!timer.Ready(1.2f, .16f), "resize burst coalesces to latest geometry");
        timer.Clear(); Check(!timer.Ready(100, .16f), "cancelled resize never completes late");
        var h = new HandStrip();
        foreach (int count in new[] { 0, 1, 2, 5, 16, 60 })
        foreach (float width in new[] { 0, .03f, .23f, .47f, 5f })
        {
            h.Configure(count, width, W, .01f, W * 1.08f, W * 1.16f, .1f);
            Check(float.IsFinite(h.ContentWidth), "finite layout including zero width");
            if (count > 1)
            {
                Check(h.Spacing >= W * 1.08f - 1e-6f && h.Spacing <= W * 1.16f + 1e-6f, "bounded nonoverlapping pitch");
                if (!h.Scrolls) Check(Near((h.X(0) + h.X(count - 1)) / 2, .1f), "fitting content centers on asymmetric region");
                if (h.Usable) for (int i = 0; i < count; i++)
                {
                    h.EnsureVisible(i);
                    Check(h.X(i) - W / 2 >= .1f - width / 2 + .005f - 1e-5f && h.X(i) + W / 2 <= .1f + width / 2 - .005f + 1e-5f, "every hand tile reachable");
                }
            }
        }
        h.Configure(60, .23f, W, .01f, W * 1.08f); h.EnsureVisible(30);
        float anchor = h.X(30);
        h.Configure(60, .3f, W, .01f, W * 1.08f); h.Anchor(30, anchor);
        Check(Near(h.X(30), anchor), "overflow resize retains inspected anchor");
        h.Configure(5, .47f, W, .01f, W * 1.08f);
        Check(h.Offset == 0, "overflow to fitting clears offset");
        h.Configure(-1, float.NaN, -1, -1, -1); Check(h.Count == 0 && float.IsFinite(h.ContentWidth), "invalid settings sanitized");
        for (int frame = 0; frame <= 400; frame++)
        {
            float t = frame / 400f;
            // Shared monotonic interpolation between separated starts and ends preserves body separation.
            float pitch = W * 1.08f * (1 - t) + W * 1.16f * t;
            Check(pitch >= W, "stable hand bodies stay separated throughout slide");
            float lift = HandReflowSchedule.Settle(t * .4f, .32f, .08f);
            Check(lift >= 0 && lift <= 1, "shared settle is bounded");
        }
        Check(HandReflowSchedule.Settle(0, .32f, .08f) == 0 && HandReflowSchedule.Settle(.4f, .32f, .08f) == 0, "settle has exact endpoints");
    }

    static PlayerViewDto View(int player, long revision, string match = "m", int round = 0, AnnouncementPenalty penalty = AnnouncementPenalty.DrawOneTile, int boneyard = 10, int targetCount = 1)
        => new PlayerViewDto
        {
            Token = new TokenDto { MatchId = match, RoundIndex = round, Revision = revision },
            Settings = new SettingsDto { MissedAnnouncementPenalty = penalty },
            Player = player, Players = new[] { 0, 1, 2 }, Phase = MatchPhase.Playing, BoneyardCount = boneyard,
            HandCounts = new[] { new PlayerCountDto { Player = 0, Count = 5 }, new PlayerCountDto { Player = 1, Count = targetCount }, new PlayerCountDto { Player = 2, Count = 4 } },
            Hand = Array.Empty<int>(), Trains = Array.Empty<TrainViewDto>(), Allowed = new AllowedActionsDto { ChallengeTargets = Array.Empty<int>() },
            LegalPlacements = Array.Empty<LegalPlacementDto>(),
        };
    static PresentationEventDto E(int source, EventKind kind, int player, int domino = -1) => new PresentationEventDto { SourceIndex = source, Kind = kind, Player = player, DominoId = domino };

    static void Feedback()
    {
        foreach (RuleError error in Enum.GetValues(typeof(RuleError)))
        {
            string text = PlayerText.Rejection(error);
            Check(error == RuleError.None ? text == "" : text.Length > 0 && !text.Contains(error.ToString()), "player-facing text for " + error);
        }
        Check(PlayerText.Rejection((RuleError)999) == "That action was not accepted.", "unknown errors fall back to generic text");
        Check(PlayerText.Winners(new[] { 0, 2 }) == "Joint winners: Player 1 & Player 3", "joint winners are all named");
        Check(PlayerText.Winners(new[] { 1 }) == "Player 2 wins", "a single winner");
        Check(PlayerText.RoundEnding(-1).StartsWith("Blocked round") && PlayerText.RoundEnding(0) == "Player 1 went out", "round endings");

        // Exactly once per (match, revision, source index), for the announcer and an observer.
        var observer = new EventFeedback();
        var announce = new[] { E(0, EventKind.OneTileAnnounced, 1) };
        Check(observer.Consume(View(0, 4), View(0, 5), announce).Single() == "Player 2 announced one tile left.", "observers see an announcement");
        Check(observer.Consume(View(0, 4), View(0, 5), announce).Count == 0, "a duplicated delivery is not shown again");
        Check(observer.Consume(View(0, 5), View(0, 5), Array.Empty<PresentationEventDto>()).Count == 0, "a resynchronization replays nothing");
        Check(new EventFeedback().Consume(null, View(1, 5), announce).Single() == "You announced your last tile.", "the announcer sees their own notice");
        Check(observer.Consume(View(0, 5), View(0, 6), new[] { E(0, EventKind.TileDrawn, 0, 12), E(1, EventKind.TileDrawn, 0, 13) }).Count == 0, "ordinary draws are shown by the board, not as notices");

        // A challenge with a penalty: the target sees the tile; opponents only see that a tile was drawn.
        var target = new EventFeedback();
        var penaltyEvents = new[] { E(0, EventKind.MissedAnnouncement, 1), E(1, EventKind.PenaltyTileDrawn, 1, 7) };
        string own = target.Consume(View(1, 6), View(1, 7, targetCount: 2), penaltyEvents).Single();
        Check(own.Contains("penalty tile") && own.Contains(PlayerText.Tile(7)), "the target sees their private penalty tile: " + own);
        string seen = observer.Consume(View(0, 6), View(0, 7, targetCount: 2), new[] { E(0, EventKind.MissedAnnouncement, 1) }).Single();
        Check(seen == "Player 2 was challenged for not announcing one tile and drew a penalty tile." && !seen.Contains("["), "opponents see a face-down penalty: " + seen);
        // A second challenge later in the same round: source index 0 again, a new revision, so it is shown.
        string again = observer.Consume(View(0, 9, boneyard: 0, targetCount: 2), View(0, 10, boneyard: 0, targetCount: 2), new[] { E(0, EventKind.MissedAnnouncement, 1) }).Single();
        Check(again.EndsWith("No penalty: the boneyard is empty."), "an empty-pile penalty is explained to observers: " + again);
        Check(new EventFeedback().Consume(View(1, 9, boneyard: 0), View(1, 10, boneyard: 0), new[] { E(0, EventKind.MissedAnnouncement, 1) }).Single().EndsWith("No penalty: the boneyard is empty."), "an empty-pile penalty is explained to the target");
        Check(new EventFeedback().Consume(View(0, 9, penalty: AnnouncementPenalty.None), View(0, 10, penalty: AnnouncementPenalty.None), new[] { E(0, EventKind.MissedAnnouncement, 1) }).Single().EndsWith("This match has no penalty."), "the None penalty is explained");
        // A new match clears the shown set, so identical keys in it are new events.
        Check(observer.Consume(null, View(0, 5, match: "m2"), announce).Count == 1, "a new match starts a fresh exactly-once set");

        // Rejections settle with player-facing text and keep the error identity for checks.
        CommandRequest sent = null;
        var client = new ClientSession(r => sent = r);
        var start = View(0, 1); start.ActivePlayer = 0; start.Allowed.CanDraw = true;
        client.Receive(new ProjectionPacket { View = start }); client.CompletePresentation(client.PresentationGeneration);
        Check(client.Submit(CommandKind.Draw), "draw submitted");
        client.Receive(new ProjectionPacket { View = start, Outcome = new CommandOutcome { RequestId = sent.RequestId, Accepted = false, Error = RuleError.DrawNotAllowed } });
        Check(client.LastRejection == RuleError.DrawNotAllowed && client.Feedback == "You cannot draw right now.", "rejections show player-facing text");
    }
}
