# Hand zoom and rendered-body centering

**Later iteration:** the user requested staggered zoom/movement timing. [Hand wave verification](HAND_WAVE_VERIFICATION.md) records the current per-tile wave and safety checks. The synchronized-phase evidence below remains the preceding iteration.

2026-10-18. User-requested follow-up to the [centered hand/reflow implementation](HAND_REFLOW_VERIFICATION.md). The user verified the preceding iteration, then requested automatic hand zoom-out and correction of unequal visible margins. This new direction supersedes the earlier plan's fixed-size overflow policy; the broader device/visual acceptance gates remain separate.

## Result and policy

The supplied screenshot reports 16 local tiles but shows only 15. Its overflow presentation does not establish that all 16 were centered: overflow initially used a left-edge/scrolling layout. The new policy first fits the whole hand using a uniform hand-only zoom, then uses scrolling if further shrinking would be unreadable.

- Normal size is 100%; hands are never enlarged beyond it. Crowding reduces scale only enough to fit at the minimum non-overlapping pitch.
- Minimum zoom defaults to **60%**, raised when necessary to keep the projected body at least **48 reference-DPI pixels** high. Unreadable fits fall back to scrolling at this floor.
- Each outer margin reserves **32 reference UI pixels**, scaled by the existing canvas-height convention, or the larger existing world-space padding. Small hands retain capped gaps and gain outer whitespace on wider windows.
- Fitting rows center their combined projected renderer-body bounds, correcting mesh-pivot offsets. This measures actual bodies, not just the first/last transform centers or shadows.
- `LayoutManager` applies target hand scale on initial deal, snaps, rearrangement, draws and undo returns. Draw/reveal remains full size before returning into the hand; track placement returns to the existing track scale. The hand root and the board zoom remain independent.
- Reflow interpolates all hand scales and positions through the same monotonic phase. Retargeting captures actual visible scales; authoritative presentation, selection and gestures keep their existing priority. Current rule decisions, states and network contracts are unchanged.

Settings are editable on `LayoutManager`: `HandMinimumZoom`, `HandReadablePixels`, `HandMinimumSideMargin`, and the existing gap/reflow fields. No scene, prefab, animation asset or package was changed for this follow-up.

## Observed checks

| Check | Result and evidence |
| --- | --- |
| Source-linked layout regression | Observed failing zoom-to-fit assertion before implementation, then **1,307 BoardPresentation checks passed**. New checks cover just-enough zoom, outer margins/centering, no unnecessary shrinking, floor-limited overflow and endpoint reachability. |
| ClientInteraction | Passed correlation, freshness, uncertainty, terminal preemption and presentation-generation checks. |
| Unity build/license | Pinned **6000.6.6f1** isolated build passed; `/tmp/td-hand-zoom-build.log`. CLI beta.13 account lookup remains blocked by the read-only Hub account database; the Editor's separate license is usable. |
| Waiting-player, 16 unchanged tiles | Real client sizes **1356×754 → 1848×595 → 720×1000 → 1356×754**. Passed rendered-body centering within 1px, minimum margins, target scale/positions, full fitting-body visibility and raycasts. **616 rendered samples, 87 moving samples, zero recovery snaps**. [Gallery/clip](/private/tmp/td-hand-zoom-waiting16/index.md). |
| Acting player, selected tile and interruption | Same window sequence; selection preserved with correct raised pose, same recipient revision and no command. Simulated held navigation ownership stops reflow and guards release. **642 rendered samples, 87 moving samples, zero recovery snaps**. [Gallery/clip](/private/tmp/td-hand-zoom-selected16/index.md). This ownership flag fixture is not a real touch/trackpad gesture. |
| 60 tiles, reduced motion | Same sizes, **60% floor** with scrolling, both endpoints fully visible and raycastable. **553 rendered samples, 30 moving samples, one recorded invalid-start recovery** when the wider viewport reveals formerly clipped geometry. [Gallery/clip](/private/tmp/td-hand-zoom-60/index.md). |
| Existing view regressions | **2- and 8-player probes passed at 1280×720**: pointer/raycast, navigation, placement/undo/flip, queue cancellation and reset. These use offscreen cameras in actual players; they are distinct from the real-window captures above. Logs: `/tmp/td-view-1280x720-{2,8}p.log`. |
| Production localhost | Host and client completed production startup, **all thirteen rounds**, scoring and fresh-match reset. [Host](/private/tmp/td-hand-zoom-localhost/observed-normal-host.log), [client](/private/tmp/td-hand-zoom-localhost/observed-normal-client.log). Normal match fixture accelerates after the first placement; use gallery clips for 1× timing evidence. |
| Review and preservation | Independent read-only agent review found no serious scale-path, centering or motion-ownership defect. Manual diff review and `git diff --check` passed. Code review: skipped (ce-code-review unavailable). Existing user log/DS_Store changes were preserved. |

At the screenshot's approximate client size **1356×754**, zoom is **92.4804%** and rendered body margins measure **22.3993px left / 22.3992px right** (configured minimum 22.3407px). All 16 tiles are visible. At **1848×595**, zoom returns to 100% and margins are **318.0376px on both sides**. At 720×1000, the readable floor takes priority and scrolling remains available.

[Corrected waiting-hand screenshot](/private/tmp/td-hand-zoom-waiting16/0-1356x754.png) · [Wider window](/private/tmp/td-hand-zoom-waiting16/1-1848x595.png)

## Reproduce and source identity

Use the isolated-project procedure in [README](README.md), including the current `HandReflowRuntime.cs` and current production sources, then:

```sh
python3 Tests/UnityIntegration/run_hand_reflow.py \
  --app /tmp/td-hand-zoom-build/Integration.app/Contents/MacOS/TrainDominoesNetcode \
  --waiting --sizes 1356x754,1848x595,720x1000,1356x754 \
  --output /tmp/new-hand-zoom-review
```

For acting/selected interaction omit `--waiting` and add `--exercise-interruption`. For readable overflow use `--count 60 --reduced-motion` with a new output directory. The runner rejects nonempty output directories and records command, executable hash, exact sizes, target/actual scale, body bounds, per-frame samples and a timestamped normal-speed MP4.

All **158 production source/metadata files** plus the runtime harness were byte-compared with the isolated build project. [Final source manifest](hand-zoom-source.sha256). The built `Contents/Resources/Data/Managed/Assembly-CSharp.dll` SHA-256 is `7190a4ab38e1f1bdb5b019593ecca2a4ab614dd2208d476a69d31e2a9136da96`. Media/logs remain outside tracked Assets; `/tmp` evidence is reproducible but may be cleaned by the OS.

These are local presentation and localhost results. No new live-service run, real iPad/touch/trackpad, native titlebar-drag pause characterization, or exhaustive resize-during-every-gameplay-event run was performed. The new zoom/centering captures await user visual acceptance. The previous iteration's user verification does not automatically approve this new policy or close VIEW-003/QA-003/final-art gates.
