# Centered hand and resize verification

**Follow-up:** the user subsequently requested zoom-out before scrolling and rendered-body centering. Current behavior and new evidence are in [hand zoom verification](HAND_ZOOM_VERIFICATION.md); the fixed-size overflow discussion below records the preceding iteration.

Dated 2026-10-18. Focused coverage for the [centered-hand plan](../../Assets/Docs/plans/2026-10-18-1625-fix-centered-hand-spacing-and-resize-reflow-plan.md). This file records the implementation, observed checks, reproducible harness and remaining review limits. No human acceptance is inferred from automated receipts.

## Actual-window harness

`HandReflowRuntime.cs` belongs only in the isolated temporary Integration player, alongside the existing runtime harnesses. Do not copy it into tracked production Assets. Use the existing temporary-project build procedure in [README](README.md), adding this runtime source before the build. `IntegrationRuntime.Boot` excludes `--integration-hand-reflow`, so the ordinary match runner does not start alongside this fixture. Reuse the pinned Editor/packages and muted-player startup policy. The runner launches one visible player without batchmode or a network connection:

```sh
python3 Tests/UnityIntegration/run_hand_reflow.py \
  --app /tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode \
  --sizes 1280x720,1920x1080,1280x720
```

Use `--count 60` for overflow/end-point reachability, `--count 1`/`2`/`5` for small hands, and `--reduced-motion` for the short nonbouncing production preference. Any `--sizes` sequence can include compact portrait fixtures.

The direct player flags are `--integration-hand-reflow --mute-audio --hand-reflow-dir <new-output-directory> --hand-reflow-sizes 1280x720,1920x1080,1280x720`. The runner also supplies `-screen-fullscreen 0`, initial width/height and an isolated log path. It refuses a nonempty evidence directory. `--no-clip` retains PNG frames/timestamps when ffmpeg is unavailable.

The runtime injects one synthetic actor recipient view through `ClientSession.Receive`, with a configurable hand (default 16; range 1–74) and revision 1. It lets the production initial deal and selection animation complete at `Time.timeScale=1`, selects a middle tile, then holds that exact view/hand/revision while changing actual window resolution. No resize callback injects another recipient update or full presenter Snap. `Screen.width/height` **and** camera pixel dimensions must equal each requested size; a platform clamp produces a timeout, rather than an offscreen fallback or a false pass. ScreenCapture records actual game frames.

Each phase records a settled PNG and JSON metadata, every-frame JSONL measurements, conservative projected renderer-bound corners for all hand tiles, selected ID, baseline, hand scroll offset, safe area, board rect, client dimensions, revision, actual reflow-moving flag/recovery-snap count and raycast result. Adjacent projected body intersections are clipped to the visible window and must remain separated at every sampled rendered frame; final transforms must equal the current layout plus selected lift. Every visible center must raycast to its displayed ID. Overflow cases then inspect the first and last tile through production scrolling and assert complete body visibility/raycasts. The last inspected anchor remains in place before the next resize, even when the selected ID is now offscreen. A fitting hand must show the entire body within the screen and below the hand boundary. Selection and empty pending-command slot must survive; the same DTO and revision remain installed.

Animation PNG frames are sampled at up to 20 Hz and retained with realtime timestamps. ffmpeg creates a variable-frame-rate normal-speed clip. Different window sizes are padded to a constant canvas without scaling original UI pixels. The clip is review evidence, not an automated aesthetic verdict. Projected world AABBs are conservative diagnostics, not a continuous-time collision proof. No claim is made about intervals between rendered frames.

## Status and scope

- Harness Python command/help parsing: checked during implementation. Runtime compilation and actual-window results are recorded by the implementing agent's final receipts/gallery.
- Source-linked policy/scheduler/motion checks: separate from this runtime; use their actual command outputs.
- Actual desktop resizing in this harness: programmatic `Screen.SetResolution` on a visible native game window. Manual OS titlebar dragging/render-loop pauses require separate review.
- Synthetic recipient presentation: no authoritative match, host/client, reconnect or live-service verification.
- Human visual acceptance, actual iPad orientation/safe area, real touch, physical trackpad gestures and native drag behavior: **UNREVIEWED** until explicitly exercised.
- Draw/placement/undo/suspension/resynchronization and gesture interruption need separate scenario receipts; the basic unchanged-view fixture does not claim those cases.

U4 is not fully closed solely by a passing narrow/wide/narrow run. Review paired PNGs and clips, then record the user's findings. This focused hand result does not close VIEW-003, QA-003, final-art or the wider UI modernization plan.

## Implementation

Fitting hands use capped center pitch and centered content in `HandStrip`; overflow retains fixed readable bodies and bounded scrolling. `BoardNavigator` measures conservative projected renderer-body width, including perspective side faces, and uses safe-area geometry and the body-center offset for bottom clearance. All hand entry paths use the same `LayoutManager` policy.

`HandReflowController` owns a latest-only 160 ms quiet timer, 320 ms shared monotonic slide, and at most 80 ms of shared vertical settling. Fitting footprint measurement uses the content edges, so extra empty viewport width does not increase pitch. Settings and a hand-specific editable `AnimationCurve` live on `LayoutManager`; no shared animation asset or scene is changed. This uses a serialized dedicated curve rather than introducing a separate `AnimationDefinition` asset. Reduced motion uses an 80 ms non-bouncing slide. Selection, current IDs, and manual inspection survive; actual selection coroutine completion and gameplay availability gate ownership. New authority, cancellation, disposal and hand gestures preempt cosmetic motion. Input gates only hand raycasts while moving; board navigation and command availability retain their existing owners.

A newly widened viewport may reveal already-intersecting side faces that were clipped in the old window. That case immediately installs a separated current layout and increments `RecoverySnaps`; it is a recovery snap, not a claim of collision-free animation from invalid starting poses. Ordinary valid resizes stay debounced. No-op layouts skip the animation and input mask.

## Engineering checks

- The changed capped-gap assertion failed against the old spread implementation, then passed after correction.
- From `RulesEngine`: `dotnet run --project ../Tests/BoardPresentation/BoardPresentation.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false`: **1,300 checks passed**, covering capped/centered geometry, counts 0/1/2/5/16/60, invalid/narrow widths, asymmetric centers, scrolling/anchor reachability, quiet timer, cancellation and shared settle bounds. Numerical checks are not renderer/device proof.
- `Tests/Motion/Motion.csproj`: **1,331,855 existing timing/motion checks passed**; pre-existing unused-variable warning in AnimationHelper remains. This synthetic suite does not prove resize renderer safety.
- `Tests/ClientInteraction/ClientInteraction.Checks.csproj`: passed.
- `Tests/GameplayIntegration/GameplayIntegration.Checks.csproj`: passed coordinator, request/privacy, setup/recovery and delivery checks; no live service.
- `Tests/StateMachine/StateMachine.Checks.csproj`: **18 checks passed**; state source/transition wiring was not edited.
- `unity version`, `unity doctor`, `unity auth list`, `unity skill show` inspected. CLI beta.13 is installed; account access fails on a read-only Hub database. The pinned Editor independently resolved a usable license and built the isolated player. Initial sandboxed import failed on the Package Manager socket; an authorized build outside that sandbox succeeded.
- Manual engineering review and a bounded independent agent review found and corrected stale selection identity, availability/disposal gating, selection coroutine ownership, no-op input stalls, root snapping during resize, and held-gesture takeover. Code review: skipped (ce-code-review unavailable). The dedicated skill definition is absent; the manual and agent diff review are recorded separately from that unavailable workflow.
- `git diff --check`: passed. No rules, player-state classes, scene, existing animation assets or package files were edited by this work. Existing user package/log/config changes are preserved.

## Failed experiments retained

The first visible-window run (`/tmp/td-hand-reflow-first`) caught insufficient selected-body clearance; the body-center/hand-band correction fixed it. `/tmp/td-hand-reflow-second` passed runtime assertions but exposed a final asynchronous screenshot missing at process exit; captures now write PNGs synchronously from the rendered framebuffer before quitting. `/tmp/td-hand-reflow-shapes` exposed projected side-face intersection on portrait-to-wide expansion; projected-width spacing and the documented invalid-start recovery address it. `/tmp/td-hand-reflow-projected16` passed the corrected layout assertions but exposed competing asynchronous screenshot requests; synchronous end-of-frame PNG capture fixed that evidence bug. These earlier runs are not final pass evidence.

## Observed runtime results

The final pinned-Editor build completed successfully (`/tmp/td-hand-gesture-build.log`). All **158 production script/metadata files** and the runtime harness were byte-compared with the isolated project; [source manifest](hand-reflow-source.sha256) records the final working sources. Each gallery has executable hash, exact launch arguments, real client dimensions, per-frame samples and timestamped clip frames. Media remains outside tracked Assets. These `/tmp` artifacts can be removed by the OS; the harness reproduces them.

| Run | Observed result | Evidence |
| --- | --- | --- |
| 16 tiles, 1425×590 → 1848×595 → 720×1000 → 1425×590 | Passed exact targets, visible raycasts, selected ID, unchanged revision and no pending command. 643 rendered-frame samples, 48 moving samples, one explicitly recorded invalid-start recovery on portrait → wide. | [Gallery and normal-speed clip](/private/tmp/td-hand-verified-16/index.md) |
| Same 16 tiles plus held-gesture interruption | Passed on final guard build: reflow stops during simulated navigation ownership, remains stopped and nonselectable for 0.306 seconds, and release is guarded. 661 rendered-frame samples, 33 moving samples and one recovery. This uses the navigator ownership flag, not queued physical input. | [Gallery/clip](/private/tmp/td-hand-guard-16/index.md), [receipts](/private/tmp/td-hand-guard-16/receipt.log) |
| 60 tiles, reduced motion, 1280×720 → 1848×595 → 720×1000 → 1280×720 | Passed on final guard build. Fixed readable bodies, both endpoints fully visible and selectable through production scrolling/raycast, selection retained across all sizes. | [Gallery/clip](/private/tmp/td-hand-guard-60-reduced/index.md) |
| 5 tiles through wide and portrait windows | Passed fitting-body visibility, selection/raycast and exact current targets. All 674 samples are already at the correct centered geometry, so no unnecessary resize motion is started. | [Gallery/clip](/private/tmp/td-hand-verified-5/index.md) |
| Existing view probes, 1280×720, 2 and 8 players | Passed navigation/input, placement/undo/flip, queued/cancelled motion and round-reset probes after the capped-footprint change. Eight-player probe passed again after cancellation cleanup. These remain offscreen fixtures, distinct from actual-window captures. | `/tmp/observed-view.log`; full logs `/tmp/td-view-1280x720-{2,8}p.log` |
| Production localhost host/client | Both peers passed production startup, thirteen rounds, scoring and fresh-match reset. Run after capped-footprint/cancellation cleanup and before the last held-gesture-only guard; that guard was checked separately above. Match runner accelerates after the first ordinary placement; it is not normal-speed visual acceptance. | [Host](/private/tmp/td-hand-final-localhost/observed-normal-host.log), [client](/private/tmp/td-hand-final-localhost/observed-normal-client.log) |

For the two screenshot proportions, the 16-tile content center span is **0.512201 world units at both sizes**, with identical pitch **0.0341467**. Extra window width becomes outer whitespace. All fitting bodies, including bottom values and selected lift, remain visible. The captured values/IDs are a synthetic fixture, not the user's original hand.

Final focused interruption command:

```sh
python3 Tests/UnityIntegration/run_hand_reflow.py \
  --app /tmp/td-hand-gesture-build/Integration.app/Contents/MacOS/TrainDominoesNetcode \
  --sizes 1425x590,1848x595,720x1000,1425x590 \
  --exercise-interruption --output /tmp/new-hand-review
```

For the overflow preference case, use `--count 60 --reduced-motion --sizes 1280x720,1848x595,720x1000,1280x720` with a new empty output directory. The normal-speed clip is timestamped; it does not imply aesthetic acceptance.

Remaining: user visual acceptance and bounce tuning; actual iPad/touch/trackpad and native titlebar-drag render-loop behavior; safe-area hardware cases; resize concurrently with each draw/undo/accepted-placement/suspension/resync path. Existing view regressions cover those gameplay motion families separately, not every resize interruption combination. No before-fix native-window recording was captured in this session: the supplied screenshots and the observed failing capped-gap regression establish the initial defect. The source-linked units are implemented, while full U3/U4 visual/device acceptance stays open. VIEW-003, QA-003 and final-art gates remain open.
