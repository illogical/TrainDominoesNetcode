# Staggered hand zoom and movement

2026-10-18. User-requested follow-up to [hand zoom/centering](HAND_ZOOM_VERIFICATION.md): make the hand zoom and move as a subtle wave, and consider staggered departures and/or arrivals when adding future animations. This supersedes the earlier hand plan's strictly shared-phase choice for ordinary reflow; authority, readability, final centering and separation requirements remain.

## Implementation

`LayoutManager.HandResizeWaveSeconds` defaults to **120 ms total**, capped at **200 ms for the whole hand**. `HandReflowSchedule.WaveDelay` distributes that budget left-to-right over the current tile order. Each tile has its own delayed eased phase for position, zoom and final settling. Both departure and completion times vary; IDs/order and exact final poses stay unchanged. A normal job is approximately **320 ms slide + up to 120 ms wave + 80 ms settle**, after the existing 160 ms quiet timer.

The hand root still has one owner. Tile trajectories are evaluated from captured world poses and scales, then converted under the current scroll root, so the root offset is never applied twice. Cancellation removes each tile's own bounce, preserves its actual current scale/pose, and adopts the cohort's average presented pitch/overall span for direct manipulation. Invisible coordinate rebasing skips the animation/input mask.

Independent phases can compress gaps. Before accepting a wave, the controller samples the proposed projected body corners at **240 Hz**, checking order, visible pair intersections and vertical hand-band clearance. It halves the timing spread when needed, with synchronized motion as the final fallback. Already-invalid starting geometry still uses a recorded recovery snap. This is sampled trajectory validation, not a mathematical continuous-time collision guarantee.

Reduced motion uses **zero wave, zero bounce**, and the existing 80 ms synchronized slide. Gameplay/selection/direct manipulation retain priority; the cosmetic job neither emits a command nor changes authority/challenge timing. No existing scene, prefab, AnimationDefinition, final art or package was changed.

Future design guidance is now recorded in [animation guidelines](../../Assets/Docs/ANIMATION_GUIDELINES.md) and linked from the repository's `AGENTS.md`. This records the user's instruction without retrofitting every unrelated existing animation. It calls for deliberately considering bounded start/arrival variation, ownership, interruption cleanup, reduced motion and projected separation for each future grouped animation.

## Observed verification

| Check | Result and evidence |
| --- | --- |
| Source-linked timing/layout | **1,388 BoardPresentation checks passed.** New wave-delay regression was observed failing with a zero delay before implementation, then passing. Counts 1/2/16/60, first/last timing, bounded whole-hand duration, ordered starts and invalid timing budgets are covered. Existing fit/centering/readability checks remain. |
| Clear width-only wave | Final build, real **1356×754 → 1000×754 → 1356×754** windows; same 16-tile waiting-player view/revision throughout. **445 rendered samples, 79 moving samples**, full **120 ms** wave used, maximum phase spread **0.5361**, zero safety reductions and zero recovery snaps. Zoom changes **92.4804% → 69.7909% → 92.4804%**. Correct final scales, centered body bounds, margins, full fitting visibility and visible raycasts all passed. [Normal-speed clip](/private/tmp/td-hand-wave-final-width/resize.mp4), [gallery/metadata](/private/tmp/td-hand-wave-final-width/index.md). |
| Selected hand and gesture interruption | Real **1356×754 → 1848×595 → 720×1000 → 1356×754**, 16 unchanged actor tiles, with `--require-wave --exercise-interruption`. **645 rendered samples, 110 moving samples**, accepted spreads **120/60/30 ms**, three safety reductions, zero recovery snaps. Rendered per-tile zoom phases genuinely differed, with selected lift and empty pending slot preserved. Simulated held navigation ownership stopped motion and guarded release. [Gallery/clip](/private/tmp/td-hand-wave-16/index.md). This is an ownership-flag fixture, not a physical touch device. |
| Reduced motion / 60-tile overflow | **577 rendered samples, 15 moving samples**, wave and phase spread both **zero**, no wave-safety reductions; both ends remain fully visible and raycastable via scrolling. One existing invalid-start recovery occurred on widening. [Gallery/clip](/private/tmp/td-hand-wave-reduced60/index.md). |
| Existing eight-player view probes | Passed at **1280×720**: navigation, pointer/raycast, placement/undo/flip, queue cancellation and round reset. Log `/tmp/td-view-1280x720-8p.log`. These are offscreen probes in a real player, distinct from the native-window wave captures. |
| ClientInteraction | Passed request correlation/freshness, uncertainty, terminal preemption and presentation-generation checks. |
| Build/environment | Pinned **Unity 6000.6.6f1** player build passed. Final log `/tmp/td-hand-wave-final-build.log`. CLI beta.13 is installed; account lookup still hits the read-only Hub database, while the Editor's independent license works. |
| Review | Manual diff/simplification review and independent read-only agent review found no concrete defect in phase calculation, projected preflight, per-tile cleanup or root rebasing. `git diff --check` passed. Code review: skipped (ce-code-review unavailable). The dedicated skill definition is absent; this does not relabel the independent engineering review as that workflow. |

The selected/reduced-motion/view runs used the candidate build; the final rebuild adds only a NaN guard to the wave-budget setting, and the width-only run rechecked the final binary. All tested timing settings are finite/default. Existing full-match evidence in the zoom report remains previous-iteration evidence; no new full-match or live-service run was performed for this local resize-timing change.

## Reproduce and identity

Follow [the isolated-player setup](README.md), copying current production sources plus `HandReflowRuntime.cs`. The observed final temporary builder wrote `/tmp/td-hand-wave-final-build/Integration.app`; adjust `--app` to the output of your builder.

```sh
python3 Tests/UnityIntegration/run_hand_reflow.py \
  --app /tmp/td-hand-wave-final-build/Integration.app/Contents/MacOS/TrainDominoesNetcode \
  --waiting --require-wave --sizes 1356x754,1000x754,1356x754 \
  --output /tmp/new-hand-wave-review
```

For selection/interruption use `--require-wave --exercise-interruption` with the default four-size sequence. For the readable overflow preference use `--count 60 --reduced-motion`; requiring a wave is intentionally disabled in reduced-motion runs. The harness now records effective wave spread, actual per-tile phase spread and adaptive-reduction count, alongside the existing body bounds/scale/frame samples. It fails `--require-wave` unless a rendered moving job has more than 20 ms of stagger and genuinely different tile phases.

[Final source manifest](hand-wave-source.sha256) covers **158 production source/metadata files plus the runtime harness**, byte-compared against the isolated project. Final `Assembly-CSharp.dll` SHA-256: `d4be6b81fee4dfa0e15584b3d91857f42a3afab9df936f916d9ddd584bb9e69a`. Large media stays outside tracked Assets; `/tmp` evidence can be cleaned by the OS and is reproducible with the runner.

Remaining review: user aesthetic acceptance of this wave and its timing, real touch/trackpad/iPad/native titlebar dragging, and exhaustive resize-during-every-authoritative-event combinations. Preflight/rendered samples do not prove safety between samples or for future rotated/depth-changing choreography. Revalidate final art. VIEW-003, QA-003 and the final-art gate remain open. Existing user working/staged changes were not staged or committed by this task.
