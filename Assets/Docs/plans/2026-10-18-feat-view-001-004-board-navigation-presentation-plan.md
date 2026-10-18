---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implementation-ready
product_contract_source: ce-plan-bootstrap
execution: code
title: Board navigation, responsive layout, animation polish and event feedback (VIEW-001–004)
type: feat
date: 2026-10-18
origin: TASKS.md (Follow-up: board navigation and presentation usability)
---

# Board navigation, responsive layout, animation polish and event feedback (VIEW-001–004)

**Authored context:** Saved on 2026-10-18 after a static review of the presentation and input sources, and after the user chose the control mappings recorded below. This document describes planned work. It does not claim anything is implemented or verified. Task IDs VIEW-001…VIEW-004 are unchanged; the F/R/U identifiers below are local to this plan. A separate session is working on Priority 4 (NET-002 → NET-001…) at the same time, so this plan is ordered to avoid the files that work touches. See [Coordination with Priority 4](#coordination-with-priority-4).

## Context

The authoritative gameplay integration is implemented, and real host/client CLI matches pass (see [integration verification](../GAMEPLAY_INTEGRATION_VERIFICATION.md)). Those checks deliberately skipped human-visible usability. On screen today:

- **Trains overflow and cannot be navigated.** Each train is a horizontal world-space row; rows are stacked vertically (up to nine for eight players plus the shared Mexican Train). One perspective camera shows the board at a fixed framing, so long trains run off-screen. Nothing clips, scrolls or zooms.
- **Layout is not reviewed at different window sizes or player counts.** The hand compresses without limit, and train-choice buttons are a fixed column of overlay buttons.
- **Animation quality during normal-speed play is unreviewed**, especially when actions are interrupted.
- **Announcements, challenges and penalty draws produce no visible feedback**, and rejections show raw `RuleError` names.

The goal is board navigation that feels natural on **both desktop (mouse/trackpad) and touch screens**, plus the layout, animation and feedback review that depends on it.

## User decisions (2026-10-18)

| ID | Decision |
|----|----------|
| D1 | **Desktop:** the vertical mouse wheel **zooms**, anchored at the cursor. **Click-drag** on a row scrolls it horizontally. **Shift+wheel**, or a horizontal wheel delta (a sideways trackpad swipe), also scrolls the hovered row. Double-click a row, or press its **jump-to-end** button, to return to the playable end. |
| D2 | **Touch:** a **one-finger drag** along a row scrolls it, with inertia after a fling. A **tap** below the drag threshold selects, as today. A **two-finger pinch** zooms around the pinch midpoint. A **double-tap** on a row toggles between its playable end and fit-to-screen. |
| D3 | **Zoom is board-wide.** One zoom level applies to every train, so rows stay aligned. When zoomed rows no longer fit vertically, the board scrolls vertically: two-finger drag on touch, and on desktop a vertical-dominant drag in the row gutter or a vertical scroll indicator (plain wheel stays zoom). The hand, action buttons and HUD never move. |
| D4 | **Follow only at the end.** A row that was showing its playable end keeps following new placements. A row the player has scrolled away from keeps its position, and its jump-to-end button pulses. All navigation resets on a new round or match. |

**Why the wheel zooms:** drag already gives a natural way to scroll a row, so the wheel's single axis is better used for the one thing drag cannot do. Wheel-to-zoom at the cursor is also the convention in maps and digital tabletop games. Trackpads send two-finger swipes as wheel input, so D1 routes horizontal deltas to scrolling, and only vertical deltas zoom.

## Review findings

These come from static reading on 2026-10-18. Items marked *verify* are risks, not observed failures.

- **F1. Positions and animations are absolute world space.** `LayoutManager.GetTrackPosition`, `GetEnginePosition` and the track row/X helpers (`Assets/Scripts/Game/LayoutManager.cs` ~88–165) compute world positions from `Camera.main` at call time. `GameplayPresenter` (~86–147) feeds those to `AnimationHelper`, which lerps `transform.position` and rotation in world space (`Assets/Scripts/Helpers/AnimationHelper.cs` ~17, 32, 63). Moving the camera or a parent mid-animation would mis-place tiles.
- **F2. Selection fires on press.** `InputManager` calls the legacy `Input.GetMouseButtonDown` and `Physics.Raycast` from `MainCamera.ScreenPointToRay`, skipping hits when `EventSystem.IsPointerOverGameObject()` is true (`Assets/Scripts/Game/InputManager.cs` ~124–128). Unless this changes, starting a drag would also select a tile.
- **F3. There is unused scroll plumbing.** `Assets/Input/Controls.inputactions` binds the `Scroll` action to `<Mouse>/scroll` and `<Touchscreen>/primaryTouch/delta`. `ControlsManager` exposes it, and `InputManager.ScrollOverDomino` has no subscriber. `activeInputHandler` is Both, with Input System 1.20.0. No tap, pinch or multi-touch handling exists. Whether touch taps currently reach `Input.GetMouseButtonDown` through mouse emulation is *verify*.
- **F4. The hand compresses without a floor.** `PositionHelper.GetLayoutAcrossScreen` spreads the hand across the full screen width with no minimum spacing (`Assets/Scripts/Helpers/PositionHelper.cs` ~174–193).
- **F5. Train choice is a fixed button column, and legal targets have no board highlight.** Train buttons are created at fixed top-left overlay offsets (`InputManager.cs` ~79). They do not track their rows, so with pan/zoom they will drift away from them. Legality exists only as `LegalPlacements` data.
- **F6. Camera and canvas.** The scene has a single perspective camera (FOV 25, local z −0.6, identity rotation), and tiles lie on the z = 0 plane. The canvas is Screen Space Overlay with ScaleWithScreenSize at 1920×1080, matching height. `LayoutManager` caches `Camera.main` in `Awake`.
- **F7. Round reset already has one detection point.** `GameplayPresenter.Present` treats a change of MatchId, RoundIndex or EngineId as a new round (~110–119) and calls `MeshManager.ResetDominoMeshes()`.
- **F8. Events are ignored and errors are raw.** `ClientSession` forwards `PresentationEventDto`s (`SourceIndex`, `Kind`, `Player`, `DominoId`) through `PresentationRequested`, but `GameplayPresenter.Present` ignores them. Rejection feedback is `Outcome.Error.ToString()` (`ClientSession.cs` ~107).
- **F9. Probes never raycast.** The `Tests/UnityIntegration` presentation probes check positions numerically against `LayoutManager` (`IntegrationRuntime.cs` ~166–228). VIEW-001 explicitly needs real pointer/raycast evidence.

## Requirements

- **R1:** On every supported window size, for 2–8 players and up to nine trains, every tile and every playable endpoint can be reached and selected (VIEW-001).
- **R2:** The navigation in D1–D4 works with mouse, trackpad and touch. Drag gestures never select; taps and clicks still do (VIEW-001).
- **R3:** Navigation is **local presentation state only**. It never affects commands, authority, legality or challenge timing (AGENTS.md: the server stays authoritative).
- **R4:** Placement, flip, undo, queued and cancelled animations land in the right place regardless of pan or zoom, including when the player navigates mid-animation (VIEW-001, VIEW-003).
- **R5:** Tiles stay readable at minimum zoom. Legal targets are highlighted accurately and stay attached to their rows. The hand and turn controls stay reachable (VIEW-001, VIEW-002).
- **R6:** No required action or result is clipped or hidden at supported sizes and player counts. Blocked-round and joint-winner results have deliberate fixtures (VIEW-002).
- **R7:** Each event a recipient is meant to see gives understandable feedback exactly once, including after resynchronization. A private penalty tile is revealed only to its owner. Rule errors use player-facing text (VIEW-004).
- **R8:** Preserve GUIDs, `.meta` files, serialized references and scene wiring. Do not upgrade packages. Do not add state-machine states; navigation is not a player state.

## Implementation units

The suggested order is U1 → U2 → U3 → U4 → U6 → U5 → U7. U1–U4 and U6 touch only presentation and input files.

### U1 — Pure navigation model (R1, R2, R3, D3, D4)

Add a Unity-free `BoardNavigationState` (plain C#, no `UnityEngine` types) holding:

- **Zoom:** a board scale clamped to `[fitAll, readableMax]`, where `fitAll` may not go below a readable minimum (R5). Zoom is anchored: given a pointer position in board space, it keeps that point under the pointer.
- **Vertical offset:** clamped so the stacked rows cannot be dragged off-screen.
- **Per row:** horizontal offset, content length, and a `FollowingEnd` flag.

Operations: `PanRow`, `PanBoard`, `ZoomAt`, `Fit`, `JumpToEnd(row)`, `OnRowGrew(row, newLength)` (follows only when `FollowingEnd` is true, per D4), `SetViewport`, and `Reset`. Scrolling a row away from its end clears `FollowingEnd`; returning within a small tolerance of the end sets it again.

Put it under `Assets/Scripts/Game/Navigation/` so console tests can link it as source, the same way `Tests/StateMachine` links production files.

### U2 — Board content roots and local-space layout (R4, F1, F6)

- Create a `BoardRoot` transform, which applies zoom scale and vertical offset. Give it one `TrainRowRoot` child per train slot, which applies that row's horizontal offset. The engine and row labels go under `BoardRoot`. **The camera does not move**, so hand positions, overlay UI and `ScreenPointToRay` hand selection are unchanged.
- Add local-space variants of the track and engine layout queries to `LayoutManager` (for example `GetTrackLocalPosition(slot, index)`). The existing world-space methods become wrappers that convert through the row root, so the current `IntegrationRuntime` probes keep compiling and passing.
- Track and engine animations: when a move starts, reparent the tile to the destination root using `SetParent(root, worldPositionStays: true)`, then lerp `localPosition`/`localRotation` towards the local target. Hand-bound moves (undo, redeal) reparent back to the hand container and use the existing world-space targets. Because targets are local, panning or zooming mid-flight carries the tile along with the row (R4).
- `GameplayPresenter.Cancel`/`Snap` snap to local targets. The round reset at F7 also calls `BoardNavigator.Reset()`.
- Add a thin `BoardNavigator` MonoBehaviour. It owns a `BoardNavigationState`, applies it to the roots with short smoothing, keeps the viewport updated on resolution changes, and works out row content length from the tile count and the measured tile size (`PositionHelper` already measures renderer bounds). **Add the roots in code at runtime** so `GameScene.unity` does not need editing. If scene wiring turns out to be needed, add it through the Unity Editor and keep existing references.

### U3 — Pointer and touch gestures (R2, F2, F3, D1, D2)

Add a `BoardGestureController` using the Input System (`Mouse.current`, `EnhancedTouch.Touch`), which is already installed. Rows are hit-tested with `Plane(z = 0)` ray intersections, mapped to a row band in board space, so a drag can start in the gaps between tiles.

| Input | Desktop | Touch |
|-------|---------|-------|
| Select tile | Click: press and release within the threshold | Tap within the threshold |
| Scroll a row | Drag horizontally on the row; Shift+wheel; horizontal wheel/trackpad delta | One-finger horizontal drag, with fling inertia |
| Scroll the board vertically (only when it overflows) | Vertical-dominant drag in the gutter, or the scroll indicator | Two-finger drag; a vertical-dominant one-finger drag |
| Zoom | Vertical wheel, anchored at the cursor (per-notch step, smoothed) | Pinch, anchored at the midpoint |
| Return or overview | Double-click a row → jump to end; per-row button | Double-tap a row → toggle end/fit |
| Fit everything | Keyboard `F` / HUD fit button | HUD fit button |

- **Threshold:** set in physical units: about 8 px mouse and about 14 px touch at 160 dpi, scaled by `Screen.dpi` with a fallback when it reports 0. Once a gesture becomes a drag, it cannot select.
- **Selection moves to release.** `InputManager` raises `DominoClicked` on pointer-up when the gesture stays under the threshold, and keeps the `IsPointerOverGameObject` gate (passing the touch ID on touch). The board raycast that resolves a tile ID stays the single selection path, and works for both mouse and touch.
- **Unused scroll plumbing:** retire `ScrollOverDomino` and the `Scroll` action subscription, or point the action at the gesture controller. Do not delete or rename the input-actions asset or its GUID. Removing a binding is acceptable if nothing else references it.
- **Pointer over UI:** gestures starting on overlay UI (buttons, panels, hand-scroll strip) are ignored by the board.
- **No pinch conflict:** while two touches are active, single-touch drag and tap are suppressed until all fingers lift.

### U4 — Readability, target highlights and row-anchored controls (R5, F4, F5)

- **Minimum readable zoom:** derive it from the on-screen pixel height of a tile's pip face (for example, at least about 28 px tall at the physical-dpi reference). `Fit` respects it; if all rows cannot fit legibly, `Fit` fits the height and leaves rows horizontally scrollable.
- **Row-anchored train controls:** replace the fixed column at `InputManager` ~79 with one control per row, positioned each frame from the projection of that row's root (clamped to the screen edge). Each control contains the train choice and the jump-to-end button. When a tile is selected, highlight the rows in `LegalPlacements` (control tint plus an end-of-row marker under the row root). Highlights are derived each frame from current data, so they cannot go stale.
- **Optional, if it reads well in review:** a tap on a legal row's end marker chooses that train, the same as the button. It is a second input path to the same `TrainChosen` event, never a separate legality check.
- **Hand:** keep a minimum tile spacing. Beyond it, the hand becomes a horizontally scrollable strip using the same drag/fling handling as rows, with the selected tile kept visible. Its scroll offset is local and resets on a new round.

### U5 — Responsive layout and result review (VIEW-002, R6)

- **Matrix to review:** player counts 2, 3 and 8. Window sizes 1280×720, 1920×1080, 2560×1440, and a phone/tablet aspect (for example 2532×1170 landscape; portrait only if the user wants it supported). Elements: hand, row controls, player and shared-train labels, markers, double obligations, selected tile, action buttons, pending/rejection feedback, results.
- **Fixtures:** add synthetic-view fixtures in `Tests/UnityIntegration` for a **blocked round** and **joint winners** (two or more tied lowest totals, all shown as winners, with no tiebreaker per RULES-005).
- Fix each defect found, or track it as its own small linked task in TASKS.md.

### U6 — Animation polish and interruption (VIEW-003, R4)

- **Review at normal speed:** draw, placement, flip, undo, scoring and redeal.
- **Interruption cases:** rapid accepted actions, skipped or cancelled sequences (`GameplayPresenter.Cancel`), suspension, and resync, each repeated **while the player is panning, zooming or flinging**.
- **Rules to keep:** input availability and challenge windows follow authoritative data, never animation duration. A cancelled sequence snaps to the latest authoritative view under the row roots. Highlights follow the current selection and legality, never a cached pre-animation state.
- **Interaction with following (D4):** a following row eases to its new end after the placement animation starts, never before, so the tile is not hidden mid-flight.

### U7 — Announcement, challenge and penalty feedback (VIEW-004, R7, F8)

- `GameplayPresenter` (or a small `EventFeedbackPresenter` it owns) consumes the `PresentationEventDto[]` already passed with each presentation.
- **Exactly once:** keep a presented-once set keyed by `(MatchId, RoundIndex, SourceIndex)`. Clear it on a new match. A resynchronization snapshot replays nothing that was already shown, and shows nothing for events older than the snapshot that the client never received; the latest view stands on its own.
- **Feedback per kind:** short toast or banner text ("Ana announced one tile", "Ben challenged Ana — penalty draw", "No penalty: the pile is empty"). A penalty tile animates face-up only for the owner (the projection already filters `DominoId` per recipient); opponents see a face-down draw.
- **Errors:** add a `RuleError` → player-facing text table, with a fallback for unknown values, replacing `Outcome.Error.ToString()`.
- **See also:** [Coordination with Priority 4](#coordination-with-priority-4).

## Coordination with Priority 4

- **Files to avoid while the parallel session works:** `SessionProtocol.cs`, `WireCodec.cs`, `GameSession.cs`, `ClientSession.cs`, `RecipientProjection.cs`, and the `Tests/GameplayIntegration` relay. Those are owned by NET-002/NET-001.
- **U1–U4 and U6** only touch `LayoutManager`, `PositionHelper`, `AnimationHelper`, `GameplayPresenter` (layout and cancel paths), `InputManager`, `MeshManager` if needed, the new navigation and gesture files, and test fixtures.
- **U7** reads `PresentationEventDto` and changes rejection text at `ClientSession.cs` ~107. Schedule it **after NET-002 lands**, consuming the DTO shape NET-002 settles. The error-text table can live in a new presentation file, and `ClientSession` would only call it.
- If NET-005 needs visible feedback for its fixtures first, U7 can come forward after NET-002, as long as NET-001 has not started editing `ClientSession`.

## Deferred / out of scope

- Final four-colour domino art (ART-001). Recheck U4/U5 readability after it lands.
- Persisting the zoom level between sessions, keyboard-only navigation beyond `F`/fit, and accessibility settings such as reduced motion. Log these as follow-ups if wanted.
- Portrait-orientation layout, unless the user asks for it during U5.
- Real touch-hardware certification. CLI synthetic touch is evidence of the logic, not of feel on a device.

## Verification contract

Label evidence by type, following AGENTS.md: **console/unit**, **Unity CLI localhost**, **human-visible GUI review**, and **real touch device**. Keep testing silent per the [testing audio mute plan](2026-10-18-disable-sound-during-testing-plan.md).

1. **Console checks for `BoardNavigationState`:** clamping at both zoom bounds; zoom anchoring keeps the point under the pointer; `Fit` respects the readable minimum; following when `FollowingEnd` is true and holding position when false; the `FollowingEnd` tolerance; viewport resize; `Reset`. Link the source the way `Tests/StateMachine` does (or under MAINT-002's NUnit project if that lands first).
2. **Unity CLI probes in `Tests/UnityIntegration`:**
   - **Fixtures:** a 40+ tile train, nine rows, at 1280×720, 1920×1080 and a phone aspect.
   - **Reachability:** for every tile and endpoint, scripted navigation (`JumpToEnd`, `PanRow`, `ZoomAt`) brings it on-screen. An **actual raycast through the production selection path** then returns the expected tile ID (R1, F9).
   - **Synthetic input:** Input System `Mouse`/`Touchscreen` state events queued in the player drive drag, wheel, Shift+wheel, pinch and double-tap. Assert that a drag does not select, a tap does, and pinch/wheel anchoring holds.
   - **Animations:** placement, flip and undo, plus a pan **during** an in-flight animation and a cancel/replace, all land at the correct local targets (R4).
   - **Follow policy:** a following row tracks a new placement; a manually inspected row keeps its position, and its jump-to-end control reports the pending state (D4). A round change resets navigation.
3. **Regression:** the existing `run_players.py`/`run_eight.py` matches, the presentation probes, `Tests/StateMachine` and `Tests/ClientInteraction` still pass.
4. **Human-visible review (VIEW-002/003):** go through the U5 matrix and U6 interruption cases in a running build or Play Mode, record findings with screenshots, and fix or track each one.
5. **Touch device:** test on at least one real touchscreen (a tablet or touch laptop). Until that is done, report it as untested.
6. **U7:** fixtures for each event kind delivered once, a resync after an event (not repeated), owner-only penalty reveal, empty-pile penalty text, and mapped error text. Coordinate with NET-005's real-transport assertions.

## Definition of done

- **VIEW-001:** long-train fixtures prove every tile and playable endpoint is reachable and selectable by real raycast, at the supported sizes, for 2–8 players and nine trains. Placement, flip, undo and queued animations stay correct after and during navigation. D1–D4 are implemented for mouse, trackpad and touch.
- **VIEW-002:** a human-visible host/client review over the U5 matrix confirms nothing required is clipped or hidden. Blocked-round and joint-winner fixtures exist. Defects are fixed or tracked individually.
- **VIEW-003:** visual review and focused interruption checks show no stranded tiles, stale highlights, overlapping transitions or blocked controls, including while navigating.
- **VIEW-004:** each recipient-relevant event gives feedback exactly once, including after resync. Penalty tiles are revealed only to their owner. Rule errors show player-facing text.
- **Records:** evidence is recorded beside each task in TASKS.md, separated by type. `PROJECT_STATE.md` is updated to describe the navigation layer. The player-state matrix is unchanged, because no states are added.
