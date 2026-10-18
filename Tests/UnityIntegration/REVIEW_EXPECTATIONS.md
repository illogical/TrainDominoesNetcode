# Gameplay review expectations and evidence matrix

Dated 2026-10-18. Authority: [Puremco rulebook](../../Assets/Docs/TRAIN_DOMINOES_RULEBOOK.md), reviewed R-015–R-018; [roadmap](../../Assets/Docs/plans/2026-10-18-gameplay-gap-analysis-and-review-plan.md). Expected behavior below comes from the selected rules/policies. A successful script is not human visual acceptance.

## Runnable baseline

Build the isolated temporary project with `IntegrationRuntime.cs` **and** `ReviewRuntime.cs` in its Assets. Copy the current scene/domino prefab/network list into the isolated project along with scripts; do not install the runner into the working project. Then:

```sh
python3 Tests/UnityIntegration/run_review.py --actions 8
python3 Tests/UnityIntegration/run_review.py --guided --actions 8 --output /tmp/my-gameplay-review
python3 Tests/UnityIntegration/run_players.py --normal
```

`run_review.py` opens two real windows on localhost port 17901, retains paired actual-frame PNGs and per-seat MP4s, logs and checkpoint metadata, writes a Markdown gallery, checks actual rasterized text using macOS Vision OCR, and prints its temporary output directory. It never starts Lobby/Relay. It requires ffmpeg and Swift. Each checkpoint records actual width/height rather than assuming the requested resolution was honored. Production UI is not hidden. The frame proof stamp is added in the upper left beneath the status and may overlap UI; this is a harness annotation, not an accepted product layout.

Modes: automatic checkpoints proceed when both peers have captured; guided checkpoints wait indefinitely for Enter. No test/network deadline runs during that human pause. `t` grants manual takeover with both scripts paused; gameplay UI remains active. The modified manual game cannot resume scripted assertions. Restart creates a fresh deterministic run and fresh evidence directory. Scripted network/presentation waits remain bounded. Recording omits intentional checkpoint dwell time, preserves actual elapsed frame timestamps between checkpoints, and keeps Time.timeScale=1. A slow renderer lowers frame density rather than accelerating playback.

Baseline fixture: actual two-peer transport, Immediate/DrawOneTile, initial actor host/seat 0, ordered round-0 `IntegrationRuntime.Deck`, no random seed. The fixture replaces authority before readiness and is explicitly **not** normal startup evidence. Ordinary production match creation/random decks use the separate `run_players.py --normal` fixture, whose later full-match playback is accelerated and must not be used for motion acceptance.

| Checkpoint | Starting position / actions | Acting-player expectation | Waiting-player expectation | Automated evidence / limits |
| --- | --- | --- | --- | --- |
| readiness | Two frozen seats, no hands; each peer Ready after pause | Ready only for unready local seat; no board input | Same independent readiness; no private tiles | Real projected phase/seat, UI text snapshot, paired PNG; human checks controls |
| deal | Both Ready; round 0 double-twelve engine | 16 own tiles, personal/shared target context, actor controls | 16 different own private tiles; ordinary placement disabled | Hand size, three trains, production presentation settled; count is not face-readability acceptance |
| before-action-N | Both peers synchronized at prior checkpoint; actor selects first permitted tile if one exists | Selection only local; legal target remains explicit; draw/pass when unable | No other hand/selection disclosed; public active actor | Client API selection; actor board-enabled assertion at submission; no pointer input claim |
| after-action-N | One permitted placement, draw or pass accepted; animation settled | Own count matches hand; updated public board/obligations | Only accepted public move/count/marker; no foreign provisional placements | Revision advances through actual transport, no pending request, real presentation completion, 1x time scale |
| finished | Requested action count complete | Usable current controls | Correct public state and private hand | Script PASS separate from OCR PASS and human review; no full-match result claim |

The frame proof stamp must not obscure required hand values; inspect its placement at each resolution.

The baseline does not force a particular action family at any numbered step; inspect JSON/clip to see what actually occurred. It does not establish exhaustive privacy, exact target orientation, marker rules or double-rule correctness. Existing console/transport suites cover additional behavior separately.

## Required family coverage backlog

All rows below require named deterministic starting positions and paired media before claiming review coverage. Existing behavioral tests do not automatically supply those media. Status here is **unreviewed / not yet scripted in visible runner** except the named baseline subset.

| Family | Rule-linked expected actor behavior | Expected non-actor/disclosure limits | Required follow-up |
| --- | --- | --- | --- |
| Setup/readiness | Fixed 2–8 seats; duplicate Ready cannot deal twice; normal random initial starter and rotation | Own private hand, public actor/readiness | Baseline two-seat deal only; normal production fixture separate; every 2–8 setup count, 4/8 density media pending |
| Access/matching/markers | Own train, marked foreign train, one shared Mexican Train; matching endpoint/orientation; reject wrong endpoint/foreign tile without mutation | Accepted public train/marker changes only | Explicit own/marked/empty/shared and rejection fixtures pending |
| Draw/pass | Draw only when unable; required playable drawn tile; pass only when permitted | Public count/marker, no drawn face | Empty pile, failed draw, allowance and immutable rejection media pending |
| Doubles | Extra play elsewhere, inherited cover, conditional second double and held third tile; R-017 separate follow-up draw; final double ends round; no third double | Public obligation and actor update | Rare-rule fixtures pending; do not infer from first-legal-action baseline |
| Draft/undo/sealing | Private preview/LIFO Undo, irreversible draw seals, End Turn commits; no restored pile/renewed allowance | Last public board/count until commit; no private draft leakage | Draft transport regression exists separately; paired preview/seal/no-replay-motion clips pending |
| Announcements/challenges | Manual announce/challenge in Acting/Waiting; DrawOneTile default/None option; self/repeated/competing/expired invalid | Public notice/count, private penalty face only to owner | Existing challenge regression separate; three-seat competing challenge and both penalty/empty-pile media pending |
| Endings/results | Empty-hand/blocked scoring once, next engine, thirteen results, fresh identity/readiness/scores | Same public result, all joint winners | Normal lifecycle script separate; blocked/joint-winner visual fixtures pending |
| Delivery faults | Clear pending/uncertain/retry/rejection; repeated/stale safe; no replayed accepted move | Accepted public state only | Existing regression separate; delay/gap/stale view/late callback gallery pending |
| Disconnect/reclaim | Last usable view; missing seats; Retry reclaims secret/seat; host resumes only after all return | Suspended ordinary commands, no new private disclosure | Existing reconnect transport regression separate; paired clips including in-flight outcomes/fresh abandonment pending |
| Navigation/layout | Readable hand, legal target, own/shared context together, multi-train overview | Public action understandable without losing inspected position | 1280×720, 1920×1080, 2532×1170 actual windows; four/eight seats; deliberate vertical overflow; fit/end/follow/pan/zoom/resize and real pointer verification pending |
| Motion/interruption | Smooth position/rotation/scale; modest varied starts/arrivals; separated rotated footprints throughout travel | Accepted public motion, hidden hands/previews absent | Baseline deal and ordinary moves only; draw/undo/redeal/flip, concurrent navigation, suspension/resync/cancel clips, timing/footprint assertions pending |

## Human review record

For each finding record scenario, checkpoint, seat, media path/time interval, actual resolution, input device, expected behavior and status: `automated-pass`, `failed`, `user-reviewed` or `unreviewed`. Only the user can supply visual acceptance. Record readable own/shared context together, wider train overview, normal-speed motion feel, visible overlap and essential controls/results independently. Real trackpad/touch, live services, final art, exhaustive packet privacy and all family-specific edge fixtures remain pending until explicitly exercised.

Per-frame motion samples require actual production domino transforms to change while presentation is busy and compare elapsed unpaused scaled/realtime clocks. They do not filter camera-visible bounds or test rotated footprint intersections; normal-speed visual review and overlap checks remain required.

Missing files or unreadable proof/production labels fail capture checks. Source identity includes the current workspace digest and executable digest; the current-workspace digest alone does not prove that executable was built from those sources. Preserve original per-peer PNG/MP4s when generating comparisons. Large media stays outside tracked Assets.
