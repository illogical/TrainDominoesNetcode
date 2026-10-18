using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game.Navigation;
using TrainDominoesRulesEngine;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// VIEW-001–004 probes in one actual player (no network). Install only in the isolated temporary project.
// Synthetic recipient views drive the production presenter, layout, navigator, row controls and state machine.
// Pointer evidence uses queued Input System device events and the production raycast. Presentation probes, not
// full matches. They are logic evidence, not human-visible review or a real touch device.
public sealed class ViewRuntime : MonoBehaviour
{
    GameSession session; ClientSession Client => session.Client; InputManager input; LayoutManager layout; BoardNavigator nav; MeshManager meshes; Camera cam; GameplayManager gameplay;
    Mouse mouse; Keyboard keyboard; Touchscreen screen;
    int players, width, height; string tag; long revision; string match = "view-probe"; int round; bool screenshots; string shots;
    PlayerViewDto view; bool offscreen;
    int W => cam.pixelWidth; int H => cam.pixelHeight;
    int Shared => players;
    const int Me = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (Environment.GetCommandLineArgs().Contains("--integration-view")) new GameObject("ViewRuntime").AddComponent<ViewRuntime>(); }
    static string Arg(string name, string fallback) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback; }
    void Mark(string s) => Debug.Log("VIEW " + tag + " " + s);
    void Assert(bool condition, string name) { if (!condition) throw new Exception("ASSERT " + name); Mark("PASS " + name); }
    IEnumerator Wait(Func<bool> condition, string name, float limit = 20)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition()) { if (Time.realtimeSinceStartup - start > limit) throw new Exception("TIMEOUT " + name + " feedback=" + Client?.Feedback + " nav: " + NavDiag()); yield return null; }
    }
    IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    IEnumerator Start()
    {
        players = int.Parse(Arg("--view-players", "2"));
        var size = Arg("--view-size", "1280x720").Split('x');
        width = int.Parse(size[0]); height = int.Parse(size[1]);
        screenshots = Environment.GetCommandLineArgs().Contains("--view-screenshots");
        shots = Arg("--view-shots", "/tmp/td-view-shots");
        tag = width + "x" + height + "-" + players + "p";
        foreach (var m in FindObjectsByType<SimpleMatchmaking>()) m.enabled = false;
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(2);
        session = FindAnyObjectByType<GameSession>(); input = FindAnyObjectByType<InputManager>(); layout = FindAnyObjectByType<LayoutManager>();
        meshes = FindAnyObjectByType<MeshManager>(); gameplay = FindAnyObjectByType<GameplayManager>(); nav = layout.Navigator; cam = input.MainCamera;
        var sound = FindAnyObjectByType<Assets.Scripts.Game.SoundManager>(); Assert(sound != null && sound.IsMuted && AudioListener.volume == 0f, "audio-muted-for-testing");
        var canvas = input.GetComponentInChildren<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (Screen.width != width || Screen.height != height)
        {
            // Batchmode players keep a 640×480 screen. Render the real camera into an offscreen target of the requested
            // size and put the canvases in that camera's space, so UI, raycasts and screenshots share one pixel space.
            cam.targetTexture = new RenderTexture(width, height, 24);
            foreach (var c in FindObjectsByType<Canvas>().Where(c => c.isRootCanvas))
            { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = Mathf.Min(0.3f, Mathf.Max(cam.nearClipPlane * 2, 0.05f)); }
            offscreen = true;
            yield return Frames(3);
        }
        Mark($"SCREEN screen={Screen.width}x{Screen.height} camera={cam.pixelWidth}x{cam.pixelHeight} offscreen={offscreen} canvasScale={canvas?.scaleFactor} dpi={Screen.dpi} graphics={SystemInfo.graphicsDeviceType}");
        Assert(cam.pixelWidth == width && cam.pixelHeight == height && Mathf.Abs(canvas.scaleFactor - height / 1080f) < 1e-3f, "view-size-applied");
        // A started Relay match hides the start menu (SimpleMatchmaking). These probes start no network, so hide it the
        // same way. The direct/LAN path does not hide it (finding VIEW-005).
        var menu = FindAnyObjectByType<MainMenuUI>(); if (menu != null) { menu.gameObject.SetActive(false); Mark("main-menu-hidden-as-in-a-started-match"); }
        // The Quantum Console opens at startup in every build; close it as a developer would (finding VIEW-005).
        foreach (var console in FindObjectsByType<MonoBehaviour>().Where(b => b.GetType().Name == "QuantumConsole"))
        { console.GetType().GetMethod("Deactivate")?.Invoke(console, null); Mark("developer-console-closed"); }
        mouse = InputSystem.AddDevice<Mouse>("ProbeMouse"); keyboard = InputSystem.AddDevice<Keyboard>("ProbeKeyboard"); screen = InputSystem.AddDevice<Touchscreen>("ProbeTouch");
        mouse.MakeCurrent(); keyboard.MakeCurrent();
        if (screenshots) Directory.CreateDirectory(shots);

        yield return Present(Fixture(1), true);
        var first = Tile(Train(0).Placements[0].DominoId);
        Mark($"HIERARCHY rows={nav.ActiveRows} board.children={nav.BoardRoot.childCount} row0.children={nav.Row(0).childCount} hand.children={nav.HandRoot.childCount} firstTrack.parent={first.parent?.name} engine.parent={Tile(view.EngineId).parent?.name} hand0.parent={Tile(view.Hand[0]).parent?.name} board.scene={nav.BoardRoot.gameObject.scene.name} tile.scene={first.gameObject.scene.name}");
        yield return Shot("board");
        yield return LayoutChecks();
        yield return Reachability();
        yield return MouseChecks();
        yield return TouchChecks();
        yield return AnimationChecks();
        yield return FollowChecks();
        yield return NoticeChecks();
        yield return ResultFixtures();
        Mark("PASS ALL"); yield return new WaitForSecondsRealtime(0.5f); Application.Quit(0);
    }

    // ---- Fixtures ----

    /// <summary>One 40-tile shared train, the rest spread over the personal trains, and an eight-tile hand.</summary>
    PlayerViewDto Fixture(long rev, int roundIndex = 0)
    {
        int engine = DominoCatalog.EngineForRound(roundIndex);
        var pool = DominoCatalog.All.Select(d => d.Id).Where(id => id != engine).ToList();
        int others = Math.Min(13, (90 - 40 - 8 - 3) / players); // leave three tiles for the flip and penalty probes
        var trains = new List<TrainViewDto>();
        for (int slot = 0; slot <= players; slot++)
        {
            int count = slot == players ? 40 : others;
            var ids = pool.Take(count).ToArray(); pool.RemoveRange(0, count);
            trains.Add(new TrainViewDto { Id = slot == players ? new TrainIdDto { IsShared = true } : new TrainIdDto { Owner = slot }, Placements = ids.Select(Place).ToArray() });
        }
        var hand = pool.Take(8).ToArray(); pool.RemoveRange(0, 8);
        var v = new PlayerViewDto
        {
            ProfileVersion = "view-probe", Token = new TokenDto { MatchId = match, RoundIndex = roundIndex, TurnNumber = 1, Revision = rev },
            Settings = new SettingsDto { Commitment = CommitmentMode.Immediate, MissedAnnouncementPenalty = AnnouncementPenalty.DrawOneTile },
            Phase = MatchPhase.Playing, Player = Me, ActivePlayer = Me, EngineId = engine, BoneyardCount = pool.Count,
            Players = Enumerable.Range(0, players).ToArray(), ReadyPlayers = new int[0], Hand = hand, AnnouncedPlayers = new int[0],
            HandCounts = Enumerable.Range(0, players).Select(p => new PlayerCountDto { Player = p, Count = p == Me ? hand.Length : 5 }).ToArray(),
            Totals = Enumerable.Range(0, players).Select(p => new PlayerCountDto { Player = p }).ToArray(),
            Trains = trains.ToArray(), OpenDoubles = new TrainIdDto[0], RoundResults = new RoundResultDto[0], Winners = new int[0],
            Allowed = new AllowedActionsDto { CanDraw = true, ChallengeTargets = new int[0] },
        };
        Legal(v);
        return v;
    }
    static PlacementDto Place(int id) { var d = DominoCatalog.Get(id); return new PlacementDto { DominoId = id, Inward = d.Low, Outward = d.High, PlayedBy = 0 }; }
    /// <summary>The first hand tile may go on my train or the shared train. Probe data only; legality is the server's.</summary>
    static void Legal(PlayerViewDto v)
    {
        v.LegalPlacements = v.Hand.Length == 0 ? new LegalPlacementDto[0] : new[]
        {
            new LegalPlacementDto { DominoId = v.Hand[0], Train = new TrainIdDto { Owner = Me } },
            new LegalPlacementDto { DominoId = v.Hand[0], Train = new TrainIdDto { IsShared = true } },
        };
    }
    static PlayerViewDto Copy(PlayerViewDto v) => JsonUtility.FromJson<PlayerViewDto>(JsonUtility.ToJson(v));
    PlayerViewDto Next() { var v = Copy(view); v.Token.Revision = ++revision; return v; }

    IEnumerator Present(PlayerViewDto v, bool sync = false, PresentationEventDto[] events = null, CommandOutcome outcome = null)
    {
        revision = v.Token.Revision; view = v;
        Client.Receive(new ProjectionPacket { View = v, Events = events ?? new PresentationEventDto[0], Outcome = outcome }, sync);
        yield return Wait(() => !Client.IsPresentationBusy, "presented-" + v.Token.Revision);
        yield return Wait(() => nav.IsSettled, "navigation-settled");
        yield return null;
    }
    string NavDiag()
    {
        var root = nav.BoardRoot;
        return $"zoom={nav.State.Zoom:F5} drawn={root.localScale.x:F5} panY={nav.State.PanY:F5} moving={nav.State.IsMoving} rect={nav.BoardRect} dt={Time.unscaledDeltaTime:F4} ui={nav.UiScale:F4} offsets={string.Join(",", Enumerable.Range(0, nav.ActiveRows).Select(i => nav.State.Offset(i).ToString("F4") + "/" + nav.Row(i).localPosition.x.ToString("F4")))}";
    }

    IEnumerator Shot(string name)
    {
        if (!screenshots) yield break;
        yield return new WaitForEndOfFrame();
        string path = Path.Combine(shots, tag + "-" + name + ".png");
        if (offscreen)
        {
            var target = cam.targetTexture; cam.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
        }
        else ScreenCapture.CaptureScreenshot(path);
        yield return Frames(2);
        Mark("SCREENSHOT " + tag + "-" + name + ".png");
    }

    // ---- Geometry helpers ----

    Transform Tile(int id) => meshes.GetDominoMeshById(id).transform;
    Vector2 ScreenOf(int id) => cam.WorldToScreenPoint(Tile(id).position);
    int Slot(TrainIdDto id) => GameplayPresenter.TrainSlot(view, id);
    TrainViewDto Train(int slot) => view.Trains.Single(t => Slot(t.Id) == slot);
    Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4]; rt.GetWorldCorners(c);
        var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
        var eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(eye, c[0]), b = RectTransformUtility.WorldToScreenPoint(eye, c[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }
    /// <summary>The shared-row tile drawn nearest the board's centre (a stable zoom anchor).</summary>
    int CentreTile() => Train(Shared).Placements.Select(x => x.DominoId).OrderBy(id => Mathf.Abs(ScreenOf(id).x - nav.BoardRect.center.x)).First();
    /// <summary>
    /// Anchor error in pixels. Vertically, rows that do not fill the board are centred, and zooming in from there clamps
    /// so no empty space shows past the outer rows. So y counts only when the board overflowed before and after.
    /// </summary>
    float AnchorError(Vector2 now, Vector2 then, bool overflowedBefore) => overflowedBefore && nav.State.OverflowsVertically ? Vector2.Distance(now, then) : Mathf.Abs(now.x - then.x);
    void ZoomForScroll() { nav.State.ZoomAt(100f, 0, 0); nav.SnapVisible(); }
    float RowPixels => nav.State.RowSpacing * nav.BoardRoot.localScale.x * nav.PixelsPerWorld;

    /// <summary>The tile is drawn inside the board viewport, not under overlay UI, and the production raycast selects it.</summary>
    bool Selectable(int id, out string why)
    {
        var p = ScreenOf(id);
        why = "";
        if (!nav.BoardRect.Contains(p)) { why = "outside board " + p; return false; }
        if (input.Gestures.OverUi(p))
        {
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = p }, hits);
            why = "under UI " + p + " " + string.Join(",", hits.Select(h => h.gameObject.name + "<" + h.gameObject.transform.parent?.name));
            return false;
        }
        int? hit = input.TileAt(p);
        if (hit != id)
        {
            string Describe(int t) { var x = Tile(t); return $"{t}:parent={x.parent?.name} local={x.localPosition.ToString("F4")} world={x.position.ToString("F4")} scale={x.lossyScale.x:F4} collider={x.GetComponent<Collider>()?.bounds.ToString("F4")}"; }
            why = "raycast hit " + hit + " at " + p + " | expected " + Describe(id) + (hit.HasValue ? " | hit " + Describe(hit.Value) : "") + $" | board={nav.BoardRoot.position.ToString("F4")} zoom={nav.BoardRoot.localScale.x:F3}";
            return false;
        }
        return true;
    }

    // ---- VIEW-002: nothing required clipped or overlapping at this size and player count ----

    IEnumerator LayoutChecks()
    {
        var full = new Rect(0, 0, W, H);
        var panel = GameObject.Find("Match controls")?.GetComponent<RectTransform>();
        Assert(panel != null, "match-controls-exist");
        var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b => b.gameObject.activeInHierarchy).ToList();
        foreach (var b in buttons) { var r = ScreenRect((RectTransform)b.transform); if (!(r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= full.xMax + 0.5f && r.yMax <= full.yMax + 0.5f)) throw new Exception("ASSERT button-on-screen " + b.name + " " + r); }
        Mark("PASS every-visible-control-on-screen count=" + buttons.Count);
        var rows = input.RowControls.TrainButtons.Concat(input.RowControls.JumpButtons).Where(b => b.gameObject.activeInHierarchy).Select(b => (b.name, r: ScreenRect((RectTransform)b.transform))).ToList();
        var actions = buttons.Where(b => !b.name.StartsWith("Train ") && !b.name.StartsWith("Jump ") && !b.name.StartsWith("Play marker")).Select(b => (b.name, r: ScreenRect((RectTransform)b.transform))).ToList();
        foreach (var a in actions) foreach (var r in rows) if (a.r.Overlaps(r.r)) throw new Exception("ASSERT action-row-control-overlap " + a.name + " " + r.name);
        foreach (var a in actions) if (a.r.Overlaps(nav.BoardRect)) throw new Exception("ASSERT action-overlaps-board " + a.name);
        for (int i = 0; i < actions.Count; i++) for (int j = i + 1; j < actions.Count; j++) if (actions[i].r.Overlaps(actions[j].r)) throw new Exception("ASSERT actions-overlap " + actions[i].name + " " + actions[j].name);
        Assert(rows.All(r => r.r.yMin >= nav.HandTop - 2), "row-controls-clear-of-hand");
        Mark("PASS controls-do-not-overlap actions=" + actions.Count + " rowControls=" + rows.Count);
        var status = panel.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.name == "Match status");
        var sr = ScreenRect(status.rectTransform);
        Assert(sr.xMin >= 0 && sr.yMax <= full.yMax + 0.5f && !sr.Overlaps(nav.BoardRect), "status-on-screen-and-clear-of-board");
        Assert(view.Hand.All(id => { var p = ScreenOf(id); return p.x > 0 && p.x < W && p.y >= 0 && p.y <= nav.HandTop && input.TileAt(p) == id; }), "every-hand-tile-on-screen-and-selectable");
        Assert(nav.BoardRect.width > W * 0.4f && nav.BoardRect.height > H * 0.25f, "board-viewport-usable " + nav.BoardRect);
        // Readability at the minimum zoom: a track tile's face is at least the readable size.
        nav.State.ZoomAt(1e-3f, 0, 0); nav.SnapVisible();
        float min = GestureMetrics.Physical(nav.ReadablePixels, Screen.dpi);
        Assert(RowPixels >= min - 0.5f, $"min-zoom-readable rowPixels={RowPixels:F1} min={min:F1}");
        nav.Fit(); nav.SnapVisible(); yield return null;
        Assert(nav.State.IsAtFit, "fit-after-min");
        Mark($"LAYOUT board={nav.BoardRect} handTop={nav.HandTop:F0} zoom={nav.State.Zoom:F2} min={nav.State.MinZoom:F2} overflowsVertically={nav.State.OverflowsVertically}");
    }

    // ---- VIEW-001: every tile and playable endpoint reachable and selectable by a real raycast ----

    IEnumerator Reachability()
    {
        foreach (var zoom in new[] { "fit", "max" })
        {
            if (zoom == "fit") nav.Fit(); else nav.State.ZoomAt(100f, 0, 0);
            nav.SnapVisible();
            int checkedTiles = 0;
            for (int slot = 0; slot <= players; slot++)
            {
                var train = Train(slot);
                for (int i = 0; i < train.Placements.Length; i++)
                {
                    nav.RevealTile(slot, i); nav.SnapVisible();
                    if (!Selectable(train.Placements[i].DominoId, out var why)) throw new Exception($"ASSERT tile-reachable zoom={zoom} slot={slot} index={i} {why}");
                    checkedTiles++;
                }
                // The playable endpoint: jump-to-end shows the last tile, and the row's controls are on screen.
                nav.JumpToEnd(slot); nav.SnapVisible();
                if (train.Placements.Length > 0 && !Selectable(train.Placements.Last().DominoId, out var endWhy)) throw new Exception($"ASSERT endpoint-reachable zoom={zoom} slot={slot} {endWhy}");
                yield return null;
                var control = input.RowControls.TrainButtons[slot];
                if (!control.gameObject.activeInHierarchy) throw new Exception($"ASSERT row-control-visible zoom={zoom} slot={slot}");
            }
            Mark($"PASS reachability zoom={zoom} ({nav.State.Zoom:F2}) tiles={checkedTiles} endpoints={players + 1}");
        }
        nav.Fit(); nav.SnapVisible(); yield return null;
    }

    // ---- VIEW-001: desktop gestures through queued Input System events ----

    IEnumerator MouseAt(Vector2 p, bool down, Vector2 scroll = default)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = p, buttons = (ushort)(down ? 1 : 0), scroll = scroll });
        yield return null;
    }
    IEnumerator Click(Vector2 p) { yield return MouseAt(p, false); yield return MouseAt(p, true); yield return MouseAt(p, false); yield return null; }
    IEnumerator Drag(Vector2 from, Vector2 to, int steps = 8)
    {
        yield return MouseAt(from, false); yield return MouseAt(from, true);
        for (int i = 1; i <= steps; i++) yield return MouseAt(Vector2.Lerp(from, to, i / (float)steps), true);
        yield return MouseAt(to, false); yield return null;
    }
    IEnumerator Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; }

    IEnumerator MouseChecks()
    {
        yield return Wait(() => Client.BoardEnabled, "board-enabled");
        int tile = view.Hand[0];
        yield return Click(ScreenOf(tile));
        Assert(Client.SelectedTile == tile, "mouse-click-selects-hand-tile");
        yield return Wait(() => nav.IsSettled, "settle");
        Assert(input.RowControls.IsHighlighted(Me) && input.RowControls.IsHighlighted(Shared), "legal-rows-highlighted");
        Assert(Enumerable.Range(0, players).Where(s => s != Me).All(s => !input.RowControls.IsHighlighted(s)), "illegal-rows-not-highlighted");
        Assert(input.RowControls.Markers[Me].gameObject.activeInHierarchy && input.RowControls.Markers[Shared].gameObject.activeInHierarchy, "legal-end-markers-shown");

        // Wheel zooms at the cursor; the board point under it stays put.
        nav.Fit(); nav.JumpToEnd(Shared); nav.SnapVisible(); yield return null;
        var anchor = ScreenOf(CentreTile());
        var local = nav.Row(Shared).InverseTransformPoint(nav.ScreenToPlane(anchor));
        float zoom = nav.State.Zoom; bool overflowed = nav.State.OverflowsVertically;
        yield return MouseAt(anchor, false, new Vector2(0, 1)); yield return MouseAt(anchor, false);
        yield return Wait(() => nav.IsSettled, "wheel-settle");
        var after = nav.WorldToScreen(nav.Row(Shared).TransformPoint(local));
        Assert(nav.State.Zoom > zoom * 1.05f, "wheel-zooms-in");
        Assert(AnchorError(after, anchor, overflowed) < 2f, $"wheel-zoom-anchored-at-cursor error={AnchorError(after, anchor, overflowed):F2}px");
        yield return MouseAt(anchor, false, new Vector2(0, -1)); yield return MouseAt(anchor, false);
        yield return Wait(() => nav.IsSettled, "wheel-out-settle");
        Assert(Mathf.Abs(nav.State.Zoom - zoom) < 1e-3f, "wheel-zoom-out-restores");

        // A drag that starts on a track tile scrolls its row and never selects or chooses.
        ZoomForScroll(); nav.JumpToEnd(Shared); nav.SnapVisible(); yield return null;
        var start = ScreenOf(Train(Shared).Placements[Train(Shared).Placements.Length - 3].DominoId);
        float before = nav.State.Offset(Shared);
        yield return Drag(start, start + new Vector2(160, 0));
        Assert(nav.State.Offset(Shared) > before + 1e-5f && !nav.State.FollowingEnd(Shared), "mouse-drag-scrolls-row");
        Assert(Client.SelectedTile == tile && Client.Pending == null, "drag-never-selects-or-chooses");


        // Shift+wheel and a sideways trackpad delta scroll the hovered row without zooming.
        var rowPoint = ScreenOf(Train(Shared).Placements[Train(Shared).Placements.Length - 2].DominoId);
        zoom = nav.State.Zoom; before = nav.State.Offset(Shared);
        yield return Keys(Key.LeftShift);
        yield return MouseAt(rowPoint, false, new Vector2(0, 2)); yield return MouseAt(rowPoint, false);
        yield return Keys();
        Assert(Mathf.Abs(nav.State.Offset(Shared) - before) > 1e-5f && Mathf.Approximately(nav.State.Zoom, zoom), "shift-wheel-scrolls-row");
        before = nav.State.Offset(Shared);
        yield return MouseAt(rowPoint, false, new Vector2(-2, 0.2f)); yield return MouseAt(rowPoint, false);
        Assert(Mathf.Abs(nav.State.Offset(Shared) - before) > 1e-5f && Mathf.Approximately(nav.State.Zoom, zoom), "horizontal-wheel-scrolls-row");

        // Double-click a scrolled row: back to its playable end. F fits the board.
        nav.State.PanRow(Shared, 0.05f); nav.SnapVisible(); yield return null;
        Assert(!nav.State.AtEnd(Shared), "row-scrolled-away");
        var rowTap = ScreenOf(CentreTile());
        yield return Click(rowTap); yield return Frames(2); yield return Click(rowTap);
        Assert(nav.State.AtEnd(Shared) && nav.State.FollowingEnd(Shared), "double-click-jumps-to-end");
        nav.State.ZoomAt(2f, 0.05f, 0); nav.SnapVisible();
        yield return Keys(Key.F); yield return Keys();
        Assert(nav.State.IsAtFit, "f-key-fits");
        yield return Wait(() => nav.IsSettled, "fit-settle");

        // Clicking the end tile of a legal row chooses that train through the same production path.
        Assert(Client.SelectedTile == tile, "selection-kept-through-navigation");
        nav.JumpToEnd(Me); nav.SnapVisible(); yield return null;
        yield return Click(ScreenOf(Train(Me).Placements.Last().DominoId));
        Assert(Client.Pending != null && Client.Pending.Kind == CommandKind.Place && Client.Pending.DominoId == tile && !Client.Pending.Train.IsShared && Client.Pending.Train.Owner == Me, "click-on-legal-row-end-sends-place");
        // Settle it with a rejection: the status shows player-facing text, not an enum name.
        var reject = Next();
        yield return Present(reject, false, null, new CommandOutcome { RequestId = Client.Pending.RequestId, Accepted = false, Error = RuleError.PlacementNotLegal });
        Assert(Client.Feedback == PlayerText.Rejection(RuleError.PlacementNotLegal) && input.StatusText.Contains(Client.Feedback) && !input.StatusText.Contains("PlacementNotLegal"), "rejection-shows-player-text");
        yield return Shot("mouse");
        yield return Click(ScreenOf(tile)); // deselect
        Assert(Client.SelectedTile == null, "second-click-deselects");
    }

    // ---- VIEW-001: touch gestures ----

    IEnumerator Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 p)
    {
        InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = p });
        yield return null;
    }
    IEnumerator Tap(Vector2 p) { yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Began, p); yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, p); yield return Frames(2); }

    IEnumerator TouchChecks()
    {
        nav.Fit(); nav.SnapVisible(); yield return null;
        int tile = view.Hand[1];
        yield return Tap(ScreenOf(tile));
        Assert(Client.SelectedTile == tile, "touch-tap-selects-hand-tile");

        // One-finger drag scrolls the row; it never selects.
        ZoomForScroll(); nav.JumpToEnd(Shared); nav.SnapVisible(); yield return null;
        var p = ScreenOf(Train(Shared).Placements[Train(Shared).Placements.Length - 3].DominoId);
        float before = nav.State.Offset(Shared);
        yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Began, p);
        for (int i = 1; i <= 6; i++) yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Moved, p + new Vector2(i * 30, 0));
        yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, p + new Vector2(180, 0)); yield return null;
        Assert(nav.State.Offset(Shared) > before + 1e-5f && Client.SelectedTile == tile && Client.Pending == null, "touch-drag-scrolls-without-selecting");
        Assert(nav.State.IsMoving, "touch-fling-has-inertia");
        yield return Wait(() => !nav.State.IsMoving, "fling-stops", 10);

        // Double-tap: back to the end, then the overview.
        yield return Tap(p); yield return Tap(p);
        Assert(nav.State.AtEnd(Shared), "double-tap-jumps-to-end");

        // Pinch zooms around the midpoint; no tap fires while two fingers are down.
        nav.Fit(); nav.JumpToEnd(Shared); nav.SnapVisible(); yield return null;
        var mid = ScreenOf(CentreTile());
        var local = nav.Row(Shared).InverseTransformPoint(nav.ScreenToPlane(mid));
        float zoom = nav.State.Zoom; bool overflowed = nav.State.OverflowsVertically;
        yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Began, mid - new Vector2(40, 0));
        yield return Touch(2, UnityEngine.InputSystem.TouchPhase.Began, mid + new Vector2(40, 0));
        for (int i = 1; i <= 6; i++)
        {
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = mid - new Vector2(40 + i * 8, 0) });
            yield return Touch(2, UnityEngine.InputSystem.TouchPhase.Moved, mid + new Vector2(40 + i * 8, 0));
        }
        yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, mid - new Vector2(88, 0));
        yield return Touch(2, UnityEngine.InputSystem.TouchPhase.Ended, mid + new Vector2(88, 0)); yield return Frames(2);
        var after = nav.WorldToScreen(nav.Row(Shared).TransformPoint(local));
        Assert(nav.State.Zoom > zoom * 1.3f, "pinch-zooms-in");
        Assert(AnchorError(after, mid, overflowed) < 3f, $"pinch-anchored-at-midpoint error={AnchorError(after, mid, overflowed):F2}px");
        Assert(Client.SelectedTile == tile && Client.Pending == null, "pinch-never-selects");
        yield return Tap(ScreenOf(tile));
        Assert(Client.SelectedTile == null, "touch-tap-deselects");
        nav.Fit(); nav.SnapVisible(); yield return null;
    }

    // ---- VIEW-001/003: animations land on local targets while navigating ----

    IEnumerator NavigateDuring(Func<bool> busy)
    {
        int moves = 0;
        while (busy())
        {
            nav.State.PanRow(Me, (moves % 2 == 0 ? 1 : -1) * 0.02f);
            nav.State.ZoomAt(moves % 2 == 0 ? 1.15f : 0.9f, 0.05f, 0.01f, Me);
            nav.State.PanBoard(0.003f);
            nav.SnapVisible(); moves++;
            yield return new WaitForSecondsRealtime(0.05f);
        }
        Mark("navigated-during-animation steps=" + moves);
    }

    void AssertOnTrack(int id, string name)
    {
        var train = view.Trains.First(t => t.Placements.Any(x => x.DominoId == id));
        int slot = Slot(train.Id), index = Array.FindIndex(train.Placements, x => x.DominoId == id);
        var t = Tile(id);
        Assert(t.parent == nav.Row(slot) && Vector3.Distance(t.localPosition, layout.TrackLocalPosition(index)) < 1e-5f
            && Quaternion.Angle(t.localRotation, LayoutManager.TrackRotation) < 0.01f && Vector3.Distance(t.localScale, Tile(view.Hand.Length > 0 ? view.Hand[0] : id).localScale) < 1e-5f
            && Vector3.Distance(t.position, layout.GetTrackPosition(index, slot, players + 1)) < 1e-5f, name);
    }

    void AssertInHand(int id, string name)
    {
        var t = Tile(id); int index = Array.IndexOf(view.Hand, id);
        var local = layout.GetHandLocalPositions(view.Hand.Length)[index];
        Assert(t.parent == nav.HandRoot && Vector3.Distance(t.localPosition, local) < 1e-5f && Quaternion.Angle(t.localRotation, LayoutManager.HandRotation) < 0.01f && Mathf.Abs(t.lossyScale.x - Tile(view.Hand.First(h => h != id)).lossyScale.x) < 1e-6f, name);
    }

    IEnumerator AnimationChecks()
    {
        nav.Fit(); nav.SnapVisible();
        // Placement from the hand onto my train, while panning and zooming.
        var place = Next(); int tile = place.Hand[0];
        place.Hand = place.Hand.Skip(1).ToArray(); place.HandCounts.First(c => c.Player == Me).Count--;
        var mine = place.Trains.First(t => !t.Id.IsShared && t.Id.Owner == Me);
        mine.Placements = mine.Placements.Concat(new[] { Place(tile) }).ToArray(); Legal(place);
        view = place; Client.Receive(new ProjectionPacket { View = place });
        yield return NavigateDuring(() => Client.IsPresentationBusy);
        yield return Wait(() => nav.IsSettled, "settle");
        AssertOnTrack(tile, "placement-lands-on-local-target-while-navigating");
        // Undo: the tile goes back to the hand.
        var undo = Next(); undo.Hand = undo.Hand.Concat(new[] { tile }).ToArray(); undo.HandCounts.First(c => c.Player == Me).Count++;
        var undoTrain = undo.Trains.First(t => !t.Id.IsShared && t.Id.Owner == Me); undoTrain.Placements = undoTrain.Placements.Take(undoTrain.Placements.Length - 1).ToArray(); Legal(undo);
        view = undo; Client.Receive(new ProjectionPacket { View = undo });
        yield return NavigateDuring(() => Client.IsPresentationBusy);
        AssertInHand(tile, "undo-returns-to-hand-with-hand-scale-while-navigating");
        // Flip: an opponent's tile placed with its high end inward appears flipped on the shared train.
        var flip = Next(); int fresh = DominoCatalog.All.Select(d => d.Id).First(id => id != flip.EngineId && !flip.Hand.Contains(id) && !flip.Trains.Any(t => t.Placements.Any(x => x.DominoId == id)) && DominoCatalog.Get(id).Low != DominoCatalog.Get(id).High);
        var d = DominoCatalog.Get(fresh); var shared = flip.Trains.First(t => t.Id.IsShared);
        shared.Placements = shared.Placements.Concat(new[] { new PlacementDto { DominoId = fresh, Inward = d.High, Outward = d.Low, PlayedBy = 1 } }).ToArray();
        view = flip; Client.Receive(new ProjectionPacket { View = flip });
        yield return NavigateDuring(() => Client.IsPresentationBusy);
        AssertOnTrack(fresh, "flipped-opponent-placement-lands-while-navigating");
        Assert(meshes.GetDominoMeshById(fresh).GetComponent<DominoEntityUI>().DominoInfo.Flipped, "flip-orientation-from-placement");
        // Queued placements (rapid accepted actions) while flinging, then a cancel/replace mid-flight.
        var queue = new List<int>();
        for (int k = 0; k < 3; k++)
        {
            var q = Next(); int id = q.Hand[0]; queue.Add(id);
            q.Hand = q.Hand.Skip(1).ToArray(); q.HandCounts.First(c => c.Player == Me).Count--;
            var row = q.Trains.First(t => !t.Id.IsShared && t.Id.Owner == Me); row.Placements = row.Placements.Concat(new[] { Place(id) }).ToArray(); Legal(q);
            view = q; Client.Receive(new ProjectionPacket { View = q });
        }
        nav.State.FlingRow(Me, 0.4f);
        yield return NavigateDuring(() => Client.IsPresentationBusy);
        foreach (var id in queue) AssertOnTrack(id, "queued-placement-lands-" + id);
        var moving = Next(); int flying = moving.Hand[0];
        moving.Hand = moving.Hand.Skip(1).ToArray(); moving.HandCounts.First(c => c.Player == Me).Count--;
        var target = moving.Trains.First(t => !t.Id.IsShared && t.Id.Owner == Me); target.Placements = target.Placements.Concat(new[] { Place(flying) }).ToArray(); Legal(moving);
        view = moving; Client.Receive(new ProjectionPacket { View = moving });
        yield return new WaitForSecondsRealtime(0.15f);
        nav.State.PanRow(Me, 0.03f); nav.SnapVisible();
        // A revision gap replaces the in-flight placement: the tile is now on the shared train instead.
        var replace = Copy(moving); replace.Token.Revision = revision += 2;
        replace.Trains.First(t => !t.Id.IsShared && t.Id.Owner == Me).Placements = target.Placements.Take(target.Placements.Length - 1).ToArray();
        var sharedRow = replace.Trains.First(t => t.Id.IsShared); sharedRow.Placements = sharedRow.Placements.Concat(new[] { Place(flying) }).ToArray();
        view = replace; Client.Receive(new ProjectionPacket { View = replace });
        yield return Wait(() => !Client.IsPresentationBusy, "replacement");
        yield return Wait(() => nav.IsSettled, "settle");
        AssertOnTrack(flying, "cancelled-flight-reconciles-to-latest-view");
        Assert(input.RowControls.Markers.All(m => !m.gameObject.activeInHierarchy) || Client.SelectedTile.HasValue, "no-stale-highlight-after-cancel");
        yield return Shot("animations");
    }

    // ---- D4: follow only at the end; reset on a new round ----

    IEnumerator FollowChecks()
    {
        ZoomForScroll(); nav.JumpToEnd(Me); nav.JumpToEnd(Shared); nav.State.PanRow(Shared, 0.05f); nav.SnapVisible();
        float held = nav.State.Offset(Shared);
        var grow = Next();
        foreach (var slot in new[] { Me, Shared })
        {
            int id = grow.Hand[0]; grow.Hand = grow.Hand.Skip(1).ToArray(); grow.HandCounts.First(c => c.Player == Me).Count--;
            var t = grow.Trains.First(x => Slot(x.Id) == slot); t.Placements = t.Placements.Concat(new[] { Place(id) }).ToArray();
        }
        Legal(grow);
        yield return Present(grow);
        Assert(nav.State.AtEnd(Me) && nav.State.FollowingEnd(Me), "following-row-tracks-new-placement");
        Assert(Mathf.Abs(nav.State.Offset(Shared) - held) < 1e-6f && nav.State.HasUnseenTiles(Shared), "inspected-row-keeps-position-and-reports-unseen");
        bool pulsed = false;
        for (float t = 0; t < 0.8f && !pulsed; t += Time.unscaledDeltaTime) { pulsed |= input.RowControls.JumpButtons[Shared].image.color != Color.white; yield return null; }
        Assert(pulsed, "jump-to-end-control-pulses");
        input.RowControls.JumpButtons[Shared].onClick.Invoke();
        Assert(nav.State.AtEnd(Shared) && !nav.State.HasUnseenTiles(Shared), "jump-to-end-button-returns");
        nav.State.PanRow(Shared, 0.05f); nav.State.ZoomAt(2f, 0.05f, 0);
        var next = Fixture(++revision, 1); next.Trains = next.Trains.Select(t => { t.Placements = t.Placements.Take(2).ToArray(); return t; }).ToArray(); Legal(next);
        yield return Present(next);
        Assert(Enumerable.Range(0, players + 1).All(s => nav.State.FollowingEnd(s) && !nav.State.HasUnseenTiles(s)) && nav.State.IsAtFit, "new-round-resets-navigation");
    }

    // ---- VIEW-004: notices exactly once; private penalty only for its owner ----

    IEnumerator NoticeChecks()
    {
        int count = input.NoticeHistory.Count;
        var announce = Next(); var events = new[] { new PresentationEventDto { SourceIndex = 0, Kind = EventKind.OneTileAnnounced, Player = 1 } };
        yield return Present(announce, false, events);
        Assert(input.NoticeHistory.Count == count + 1 && input.NoticeHistory.Last() == "Player 2 announced one tile left.", "announcement-notice");
        Client.Receive(new ProjectionPacket { View = announce, Events = events }); yield return Frames(3);
        Client.Receive(new ProjectionPacket { View = announce }, true); yield return Wait(() => !Client.IsPresentationBusy, "resync");
        Assert(input.NoticeHistory.Count == count + 1, "duplicate-and-resync-do-not-repeat");
        // My missed announcement: I see my penalty tile face up in my hand.
        var penalty = Next(); int drawn = DominoCatalog.All.Select(d => d.Id).First(id => id != penalty.EngineId && !penalty.Hand.Contains(id) && !penalty.Trains.Any(t => t.Placements.Any(x => x.DominoId == id)));
        penalty.Hand = penalty.Hand.Concat(new[] { drawn }).ToArray(); penalty.HandCounts.First(c => c.Player == Me).Count++; penalty.BoneyardCount--;
        yield return Present(penalty, false, new[] { new PresentationEventDto { SourceIndex = 0, Kind = EventKind.MissedAnnouncement, Player = Me }, new PresentationEventDto { SourceIndex = 1, Kind = EventKind.PenaltyTileDrawn, Player = Me, DominoId = drawn } });
        Assert(input.NoticeHistory.Last().Contains(PlayerText.Tile(drawn)), "owner-sees-penalty-tile: " + input.NoticeHistory.Last());
        AssertInHand(drawn, "penalty-tile-face-up-in-owner-hand");
        // An opponent's penalty: only the public count changes, and the notice names no tile.
        var other = Next(); other.HandCounts.First(c => c.Player == 1).Count++; other.BoneyardCount--;
        yield return Present(other, false, new[] { new PresentationEventDto { SourceIndex = 0, Kind = EventKind.MissedAnnouncement, Player = 1 } });
        Assert(input.NoticeHistory.Last() == "Player 2 was challenged for not announcing one tile and drew a penalty tile.", "observer-sees-face-down-penalty");
        Assert(input.NoticeHistory.Count == count + 3, "each-event-shown-once");
        yield return Shot("notice");
    }

    // ---- VIEW-002: blocked round and joint winners ----

    IEnumerator ResultFixtures()
    {
        T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        var roundUi = Field<RoundOverUI>(gameplay, "roundOverUI"); var gameUi = Field<GameOverUI>(gameplay, "gameOverUI");
        var scores = Enumerable.Range(0, players).Select(p => new PlayerCountDto { Player = p, Count = 10 + p }).ToArray();
        var blocked = Next(); blocked.Phase = MatchPhase.RoundOver; blocked.ActivePlayer = -1; blocked.LegalPlacements = new LegalPlacementDto[0];
        blocked.RoundResults = new[] { new RoundResultDto { RoundIndex = blocked.Token.RoundIndex, Reason = RoundEndReason.Blocked, EndingPlayer = -1, Scores = scores } };
        blocked.Totals = scores; blocked.Allowed = new AllowedActionsDto { CanReady = true, ChallengeTargets = new int[0] };
        yield return Present(blocked);
        Assert(roundUi.gameObject.activeInHierarchy && Field<TextMeshProUGUI>(roundUi, "winnerNameText").text == PlayerText.RoundEnding(-1), "blocked-round-result-shown");
        Assert(roundUi.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "Player 1"), "score-rows-name-players");
        yield return Shot("blocked-round");
        var joint = Next(); joint.Phase = MatchPhase.MatchOver; joint.Winners = new[] { 0, 1 };
        joint.Totals = Enumerable.Range(0, players).Select(p => new PlayerCountDto { Player = p, Count = p < 2 ? 40 : 60 + p }).ToArray();
        joint.Allowed = new AllowedActionsDto { ChallengeTargets = new int[0] };
        yield return Present(joint);
        Assert(gameUi.gameObject.activeInHierarchy && Field<TextMeshProUGUI>(gameUi, "winnerNameText").text == "Joint winners: Player 1 & Player 2", "joint-winners-all-shown");
        var newMatch = GameObject.Find("Match controls").GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(b => b.name == "New Match");
        Assert(newMatch != null && newMatch.gameObject.activeInHierarchy, "new-match-action-visible-on-results");
        yield return Shot("joint-winners");
    }
}
