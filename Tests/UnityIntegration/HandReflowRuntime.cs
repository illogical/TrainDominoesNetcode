using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game.Navigation;
using TrainDominoesRulesEngine;
using UnityEngine;

// Temporary-project-only harness. Visible real window, synthetic recipient view, no network.
// Never substitute a RenderTexture when the OS cannot apply the requested client dimensions.
public sealed class HandReflowRuntime : MonoBehaviour
{
    [Serializable] sealed class TileSample { public int id; public Vector3 local, scale; public Rect pixels; public int raycast; }
    [Serializable] sealed class Sample
    {
        public string stage; public float realtime, scaledtime; public int width, height, frame, selected;
        public Rect safeArea, handUsableRect, board; public float handTop, baseline, scroll, handZoom, minimumMarginPixels; public Vector3 handTileScale; public Rect handBounds; public long revision;
        public bool busy, reflowMoving; public int recoverySnaps; public TileSample[] tiles;
    }
    LayoutManager layout; BoardNavigator nav; MeshManager meshes; InputManager input; GameSession session; Camera cam;
    PlayerViewDto fixture; string output, stage = "startup"; bool capture; int frame; float nextCapture;
    StreamWriter samples, frames; int handCount; bool waiting;
    static string Arg(string key, string fallback) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, key); return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (Environment.GetCommandLineArgs().Contains("--integration-hand-reflow")) new GameObject("HandReflowRuntime").AddComponent<HandReflowRuntime>(); }
    void Mark(string s) => Debug.Log("HAND_REFLOW " + s);
    void Check(bool ok, string name) { if (!ok) throw new Exception("ASSERT " + name); Mark("PASS " + name); }
    IEnumerator Wait(Func<bool> condition, string name, float timeout = 20)
    {
        float began = Time.realtimeSinceStartup;
        while (!condition()) { if (Time.realtimeSinceStartup - began > timeout) throw new Exception("TIMEOUT " + name); yield return null; }
    }
    IEnumerator Start()
    {
        handCount = int.Parse(Arg("--hand-reflow-count", "16"));
        waiting = Environment.GetCommandLineArgs().Contains("--hand-reflow-waiting");
        if (handCount < 1 || handCount > 74) throw new Exception("ASSERT hand-count-range-1-74");
        output = Arg("--hand-reflow-dir", "/tmp/td-hand-reflow"); Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "frames"));
        samples = new StreamWriter(Path.Combine(output, "samples.jsonl")) { AutoFlush = true };
        frames = new StreamWriter(Path.Combine(output, "frames.csv")) { AutoFlush = true };
        foreach (var m in FindObjectsByType<SimpleMatchmaking>()) m.enabled = false;
        yield return null;
        layout = FindAnyObjectByType<LayoutManager>(); nav = layout.Navigator; meshes = FindAnyObjectByType<MeshManager>();
        input = FindAnyObjectByType<InputManager>(); session = FindAnyObjectByType<GameSession>(); cam = input.MainCamera;
        layout.ReduceHandMotion = Environment.GetCommandLineArgs().Contains("--reduced-motion");
        Check(cam.targetTexture == null && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "real-window-rendering");
        Check(Mathf.Approximately(Time.timeScale, 1), "normal-time-scale");
        var sound = FindAnyObjectByType<SoundManager>(); Check(sound != null && sound.IsMuted, "muted");
        var menu = FindAnyObjectByType<MainMenuUI>(); if (menu != null) menu.gameObject.SetActive(false);
        var sizes = Arg("--hand-reflow-sizes", "1280x720,1920x1080,1280x720").Split(',');
        yield return Resize(sizes[0]);
        fixture = Fixture(); stage = "deal";
        session.Client.Receive(new ProjectionPacket { View = fixture }); // production initial deal; no forced snap or timescale boost
        yield return Wait(() => !session.Client.IsPresentationBusy, "initial-deal");
        yield return new WaitForSecondsRealtime(.6f);
        int selected = waiting ? -1 : fixture.Hand[fixture.Hand.Length / 2];
        if (!waiting) session.Client.ClickTile(selected);
        yield return new WaitForSecondsRealtime(.6f);
        capture = true;
        StartCoroutine(CaptureFrames());
        for (int index = 0; index < sizes.Length; index++)
        {
            stage = index + "-" + sizes[index];
            if (index > 0) yield return Resize(sizes[index]);
            if (index == 2 && Environment.GetCommandLineArgs().Contains("--hand-reflow-interrupt"))
                yield return HeldGestureInterruption();
            // Keep the same DTO/revision/hand. Resize must trigger presentation without another Receive.
            float began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < 1.5f) {
                // Navigator/controller own LateUpdate. Validate only poses rendered after those writers.
                yield return new WaitForEndOfFrame();
                Validate(false);
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            Validate(true);
            Check((session.Client.SelectedTile ?? -1) == selected && session.Client.Pending == null, stage + "-selection-kept-no-command");
            Check(ReferenceEquals(session.Client.View, fixture) && session.Client.View.Token.Revision == 1, stage + "-unchanged-recipient-view");
            yield return new WaitForEndOfFrame();
            CapturePng(Path.Combine(output, stage + ".png"));
            File.WriteAllText(Path.Combine(output, stage + ".json"), JsonUtility.ToJson(Measure(), true));
            Mark("CHECKPOINT " + stage + " actual=" + Screen.width + "x" + Screen.height);
            yield return new WaitForSecondsRealtime(.6f);
            if (nav.Hand.Scrolls) yield return Endpoints();
        }
        capture = false;
        // CapturePng writes synchronously from the rendered framebuffer. Advance once so the
        // capture coroutine observes capture=false before receipts are disposed.
        yield return null;
        if (frame > 0) yield return Wait(() => FrameWritten(frame - 1), "last-animation-frame-written", 10);
        for (int i = 0; i < sizes.Length; i++) {
            string path = Path.Combine(output, i + "-" + sizes[i] + ".png");
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 24, "checkpoint-written-" + i, 10);
        }
        samples.Dispose(); frames.Dispose(); samples = null; frames = null;
        File.WriteAllText(Path.Combine(output, "complete"), "PASS\n"); Mark("PASS ALL"); Application.Quit(0);
    }
    IEnumerator Resize(string dimensions)
    {
        var size = dimensions.Split('x'); int w = int.Parse(size[0]), h = int.Parse(size[1]);
        Mark("RESIZE requested=" + dimensions + " from=" + Screen.width + "x" + Screen.height + " time=" + Time.realtimeSinceStartup);
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
        yield return Wait(() => Screen.width == w && Screen.height == h && cam.pixelWidth == w && cam.pixelHeight == h, "actual-client-size-" + dimensions, 8);
        Check(cam.targetTexture == null, "no-offscreen-substitution");
    }
    PlayerViewDto Fixture()
    {
        int engine = DominoCatalog.EngineForRound(0);
        int[] hand = DominoCatalog.All.Select(d => d.Id).Where(id => id != engine).Take(handCount).ToArray();
        return new PlayerViewDto {
            ProfileVersion = "hand-reflow-probe", Token = new TokenDto { MatchId = "hand-reflow", RoundIndex = 0, TurnNumber = 1, Revision = 1 },
            Settings = new SettingsDto { Commitment = CommitmentMode.Immediate, MissedAnnouncementPenalty = AnnouncementPenalty.DrawOneTile },
            Phase = MatchPhase.Playing, Player = 0, ActivePlayer = waiting ? 1 : 0, EngineId = engine, BoneyardCount = 90 - handCount - 16,
            Players = new[] { 0, 1 }, ReadyPlayers = new int[0], Hand = hand, AnnouncedPlayers = new int[0],
            HandCounts = new[] { new PlayerCountDto { Player = 0, Count = handCount }, new PlayerCountDto { Player = 1, Count = 16 } },
            Totals = new[] { new PlayerCountDto { Player = 0 }, new PlayerCountDto { Player = 1 } },
            Trains = new[] { new TrainViewDto { Id = new TrainIdDto { Owner = 0 }, Placements = new PlacementDto[0] }, new TrainViewDto { Id = new TrainIdDto { Owner = 1 }, Placements = new PlacementDto[0] }, new TrainViewDto { Id = new TrainIdDto { IsShared = true }, Placements = new PlacementDto[0] } },
            OpenDoubles = new TrainIdDto[0], RoundResults = new RoundResultDto[0], Winners = new int[0],
            Allowed = new AllowedActionsDto { CanDraw = !waiting, ChallengeTargets = new int[0] }, LegalPlacements = new LegalPlacementDto[0]
        };
    }
    IEnumerator HeldGestureInterruption()
    {
        // Simulated navigator ownership flag only: this is not queued pointer input or a real device.
        yield return Wait(() => layout.HandReflow.Moving, "interruptible-reflow-start", 5);
        nav.HandGestureActive = true;
        yield return null;
        yield return new WaitForEndOfFrame();
        Check(!layout.HandReflow.Moving, "simulated-held-gesture-stops-reflow");
        float began = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - began < .3f) {
            yield return null;
            yield return new WaitForEndOfFrame();
            Check(!layout.HandReflow.Moving && !layout.HandReflow.CanSelect, "simulated-held-gesture-suppresses-motion-selection");
        }
        nav.HandGestureActive = false;
        Check(!layout.HandReflow.CanSelect, "simulated-held-gesture-release-guard");
        Mark("INTERRUPTION simulated-nav-ownership-only held-seconds=" + (Time.realtimeSinceStartup - began));
    }
    IEnumerator Endpoints()
    {
        // Inspect both ends through production scrolling. Leave the last inspected anchor in place,
        // preserving the selected ID even when it is now offscreen before the next resize.
        foreach (int index in new[] { 0, fixture.Hand.Length - 1 }) {
            nav.EnsureHandVisible(index); nav.SnapVisible();
            yield return new WaitForEndOfFrame();
            int id = fixture.Hand[index]; var tile = meshes.GetDominoMeshById(id);
            Rect body = Body(tile); var point = cam.WorldToScreenPoint(tile.transform.position);
            Check(body.xMin >= -.75f && body.xMax <= Screen.width + .75f && body.yMin >= -.75f && body.yMax <= nav.HandTop + .75f,
                stage + "-overflow-endpoint-full-body-" + index);
            Check(input.TileAt(point) == id, stage + "-overflow-endpoint-raycast-" + index);
            Mark("ENDPOINT index=" + index + " id=" + id + " scroll=" + nav.Hand.Offset + " body=" + body);
        }
    }
    Rect Body(GameObject tile)
    {
        // Conservative projection of the actual renderer world-bounds corners (not center spacing).
        var bounds = tile.GetComponent<Renderer>().bounds;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity); var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++) { var p = cam.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    Rect CombinedBody()
    {
        var bodies = fixture.Hand.Select(id => Body(meshes.GetDominoMeshById(id))).ToArray();
        return Rect.MinMaxRect(bodies.Min(b => b.xMin), bodies.Min(b => b.yMin), bodies.Max(b => b.xMax), bodies.Max(b => b.yMax));
    }
    Sample Measure() => new Sample {
        stage = stage, realtime = Time.realtimeSinceStartup, scaledtime = Time.time, frame = Time.frameCount,
        width = Screen.width, height = Screen.height, safeArea = Screen.safeArea, handUsableRect = nav.HandUsableRect, board = nav.BoardRect, handTop = nav.HandTop,
        baseline = layout.HandLocalY, scroll = nav.Hand.Offset, handZoom = nav.HandZoom, handTileScale = layout.HandTileScale,
        minimumMarginPixels = nav.HandMinimumMarginPixels, handBounds = CombinedBody(), selected = session.Client.SelectedTile ?? -1,
        revision = fixture.Token.Revision, busy = session.Client.IsPresentationBusy, reflowMoving = layout.HandReflow.Moving, recoverySnaps = layout.HandReflow.RecoverySnaps,
        tiles = fixture.Hand.Select(id => { var t = meshes.GetDominoMeshById(id); var p = cam.WorldToScreenPoint(t.transform.position); return new TileSample { id = id, local = t.transform.localPosition, scale = t.transform.localScale, pixels = Body(t), raycast = input.TileAt(p) ?? -1 }; }).ToArray()
    };
    void Validate(bool settled)
    {
        var s = Measure();
        for (int i = 1; i < s.tiles.Length; i++) {
            Rect a = s.tiles[i - 1].pixels, b = s.tiles[i].pixels;
            // Only the intersection actually drawn inside the window counts. Overflow bodies
            // outside the clip are retained in metadata and checked when scrolled into view.
            float overlapX = Mathf.Min(Screen.width, Mathf.Min(a.xMax, b.xMax)) - Mathf.Max(0, Mathf.Max(a.xMin, b.xMin));
            float overlapY = Mathf.Min(Screen.height, Mathf.Min(a.yMax, b.yMax)) - Mathf.Max(0, Mathf.Max(a.yMin, b.yMin));
            if (overlapX > .75f && overlapY > .75f) throw new Exception("ASSERT projected-body-overlap " + stage + " pair=" + (i - 1) + "/" + i);
        }
        if (!settled) return;
        var targets = layout.GetHandLocalPositions(fixture.Hand.Length);
        for (int i = 0; i < s.tiles.Length; i++) {
            var t = s.tiles[i]; var expected = targets[i]; if (t.id == s.selected) expected.y += layout.SelectionRaise;
            Check(Vector3.Distance(t.local, expected) < .0001f, stage + "-exact-current-target-" + t.id);
            Check(Vector3.Distance(t.scale, s.handTileScale) < .0001f, stage + "-exact-current-scale-" + t.id);
            // Overflow tiles may be outside the viewport; every displayed center must hit its visible tile.
            if (t.pixels.center.x > 0 && t.pixels.center.x < Screen.width) Check(t.raycast == t.id, stage + "-visible-raycast-" + t.id);
            if (!nav.Hand.Scrolls) Check(t.pixels.xMin >= -.75f && t.pixels.xMax <= Screen.width + .75f && t.pixels.yMin >= -.75f && t.pixels.yMax <= nav.HandTop + .75f, stage + "-full-fitting-body-visible-" + t.id);
        }
        if (!nav.Hand.Scrolls) {
            float left = s.handBounds.xMin - s.handUsableRect.xMin, right = s.handUsableRect.xMax - s.handBounds.xMax;
            Check(Mathf.Abs(left - right) <= 1f, stage + "-rendered-bounds-centered-within-one-pixel");
            Check(left >= s.minimumMarginPixels - .75f && right >= s.minimumMarginPixels - .75f, stage + "-minimum-outer-margins");
        }
        Check(s.handZoom > 0 && s.handZoom <= 1, stage + "-valid-hand-zoom");
        Mark("BOUNDS " + JsonUtility.ToJson(s));
    }
    IEnumerator CaptureFrames()
    {
        while (capture) {
            // All transform-owning LateUpdates and rendering have completed. Metadata and raster
            // capture now describe the same presented frame, independent of component update order.
            yield return new WaitForEndOfFrame();
            if (!capture || fixture == null) yield break;
            samples.WriteLine(JsonUtility.ToJson(Measure()));
            if (Time.realtimeSinceStartup >= nextCapture) {
                nextCapture = Time.realtimeSinceStartup + 1f / 20f;
                CapturePng(Path.Combine(output, "frames", frame.ToString("D6") + ".png"));
                frames.WriteLine(frame + "," + Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); frame++;
            }
            yield return null;
        }
    }
    void CapturePng(string path) {
        // CaptureScreenshot has a single deferred request; simultaneous checkpoint/clip requests
        // can replace each other. Synchronous texture capture preserves both from this EOF frame.
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { Destroy(texture); }
    }
    bool FrameWritten(int number) {
        string path = Path.Combine(output, "frames", number.ToString("D6") + ".png");
        return File.Exists(path) && new FileInfo(path).Length > 24;
    }
    void OnEnable() { Application.logMessageReceived += Fatal; }
    void Fatal(string message, string stack, LogType type) {
        if (type != LogType.Exception) return;
        capture = false;
        if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "failed.txt"), message + "\n" + stack);
        samples?.Flush(); frames?.Flush();
        Application.Quit(1);
    }
    void OnDestroy() { Application.logMessageReceived -= Fatal; samples?.Dispose(); frames?.Dispose(); }
}
