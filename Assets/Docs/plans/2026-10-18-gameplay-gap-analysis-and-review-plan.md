---
title: Gameplay gap analysis and multiplayer review before final art
type: roadmap
date: 2026-10-18
origin: TASKS.md and user review discussion on 2026-10-18
---

# Gameplay gap analysis and multiplayer review before final art

## Goal and decisions

Finish and verify synchronous Mexican Train gameplay before ART-001. Make the current branch reviewable through a scenario script, paired player screenshots, short normal-speed clips, and a guided mode, so the user need not manually construct every situation. This is a high-level roadmap, not an implementation specification for every follow-up. Saving this document does not implement or verify its proposed features.

The selected authority remains the [Puremco rulebook](../TRAIN_DOMINOES_RULEBOOK.md), including reviewed R-015–R-018 policies: 2–8 fixed seats, one personal train per player plus one shared Mexican Train, sequential play, thirteen rounds, Immediate/DrawOneTile defaults, optional match-fixed Draft/None, manual announcements/challenges, readiness sets, joint winners and suspension. Do not reopen these choices or introduce states for individual rule conditions.

User decisions: provide both a review gallery and guided interactive mode; review acting and waiting perspectives; prioritize game logic before art; assess simultaneous visibility of multiple trains as well as reachability through zoom/scroll; use smooth animation with modestly varied starts and arrivals and no visible overlap during travel. Waiting-player product exploration and asynchronous play belong to later goals.

## Current states and gap analysis

Sources: [current state review](../PLAYER_STATE_REVIEW.md), [anticipated inventory](../ANTICIPATED_PLAYER_STATES.md), [integration guide](../GAMEPLAY_INTEGRATION.md), [integration verification](../GAMEPLAY_INTEGRATION_VERIFICATION.md), [core verification](../RULES_ENGINE_VERIFICATION.md), and actual state/core/adapter/presentation sources. Some prose in those documents is historical or stale; the current source and dated verification sections take precedence for implementation claims.

| Implemented local state | Responsibility now | Expected engine conditions, not extra states |
| --- | --- | --- |
| Pregame | Host creates roster; participants Ready | Fixed seats and readiness set |
| RoundInitializing | Await authoritative setup and local deal presentation | Engine, hands and trains supplied by core |
| PlayerActing | Local selection and command forwarding | Legal placement, draw/pass, double requirements, Draft Undo/End Turn |
| PlayerAwaitingTurn | Observe public play; allow announcement/challenge | Another seat is active; private hands/drafts stay hidden |
| PlayerActionPending | Await correlated outcome; retry uncertain command | One pending request, exact identity, current view token |
| RoundOver | Present result and next-round readiness | Empty-hand or blocked result; score once |
| GameOver | Present all joint winners; host creates fresh match | Thirteen results and fresh-match reset |
| PlayerSessionSuspended | Block ordinary actions; sync/reconnect, host resume or fresh match | Missing seats and authoritative suspension |

`PlayerStateCoordinator` prioritizes availability/suspension, pending delivery, phase/setup and actor. Round/game results and obsolete pending requests are reconciled from views. State subscriptions are balanced on entry/exit. Selection, markers, obligations, scoring, announcement windows and commitment remain data owned by the appropriate core/client layer.

The strict source map was regenerated to temporary storage during this review with zero extractor warnings. No topology change is proposed; before and proposed-after state inventories are identical. The [checked-in generated map](../PlayerStateFlow/current.md) remains the durable wiring reference; its graph alone is not runtime proof.

| Finding | Classification and evidence | Follow-up |
| --- | --- | --- |
| Complete ordinary rounds/matches | Implemented with recorded evidence: core replays, adapter checks and real CLI thirteen-round matches. No missing ordinary rule feature or local state identified in this static review | Preserve completed RULES/ENG/STATE/NET tasks; extend targeted coverage rather than rebuild |
| Normal match creation | Verification gap: `IntegrationRuntime` replaces the authority with a deterministic coordinator, including in live-service runs. The normal initial host creation path needs separate proof | GAME-001 |
| Failed round setup | Recovery gap: `TryStartRound` records `LastLifecycleError`; `GameSession` logs it. Players can remain awaiting setup without a clear recovery control | GAME-002 |
| Arrival after roster freeze | Admission/UX gap: quick-join does not filter or lock a started lobby; an unseated arrival receives feedback but no match projection. Connection/capacity effects need characterization | GAME-003 |
| Visual proof | Existing full-match scripts accelerate after initial play. Board screenshot probes use synthetic views and their captures omit UI text. No current short-video/gallery/guided workflow | QA-001/002 |
| Layout and animation acceptance | VIEW-002/003 remain open. Endpoint reachability and final transforms do not establish readable simultaneous train context or appealing motion | QA-003 and existing VIEW tasks |
| Development settings | Scene still serializes IgnoreDominoComparisons=1, StartAtRound=2, DominoStartCount=12; current authority does not consume them | GAME-001 verifies standard rules with scene unchanged; do not change settings to pass |
| Documentation drift | Rulebook still says Unity integration is deferred; overview mentions frozen transport identities rather than reclaimed seats; old harness limits are superseded by later reports | DOC-005 |

This review inspected source and existing reports; it did not rerun behavioral suites or establish new human-visible/live-service results.

## Ordered work and completion criteria

1. **DOC-005 — Reconcile current documentation.** Link this roadmap from TASKS.md; correct current summaries in the rulebook, overview, state matrix/inventory and harness README. Preserve historical baselines and their dates. Point current verification claims to the latest evidence rather than rewriting old reports as fresh results.
2. **GAME-001 — Prove normal startup and lifecycle.** Add a separate fixture that uses the production host match-creation path without replacing its authority. Verify connection before roster freeze, unauthorized creation rejection, duplicate readiness, deal sizes, random initial starter within roster, next-round starter rotation, scoring/redeal, and fresh-match reset. Cover host-as-player and multiple peers. Keep deterministic-deck scenarios separate; an ordinary full match must also progress without the injected authority.
3. **GAME-002 — Recover failed round initialization.** Keep the existing RoundInitializing responsibility. Show a setup failure to participants and offer the host a retry of the same awaiting-deck setup. Retry must preserve match identity, readiness and scored results, validate a fresh supplied deck, and start the round once. Repeated/stale retry, persistent failure, suspension and later-round failure must not partially deal or score again. Define the minimum lifecycle request/feedback contract in its focused implementation plan; retain server validation.
4. **GAME-003 — Handle arrivals after roster freeze.** Preserve fixed seats. Stop advertising a frozen match to new quick-join players; give already-connected/unseated arrivals an explicit waiting-for-new-match outcome. Do not imply spectator support. Verify that a legitimate seat reclaim still works while new admission is closed, including a near-capacity roster, and that a fresh match can seat the intended connected participants. Characterize service behavior locally/mocked first; any new live run requires explicit authorization.
5. **VIEW-005 / MAINT-004 — Remove known review obstacles.** Follow the existing [overlay/prefab plan](2026-10-18-fix-view-005-maint-004-overlays-and-domino-prefab-plan.md). Avoid silently hiding overlays in acceptance runs. These narrow cleanup tasks support reliable review without starting final art integration.
6. **QA-001 — Build scenario runner and expectation matrix.** Extend the isolated harness under `Tests/UnityIntegration`; retain fast regression mode and add visible, normal-speed automatic and guided modes. Reuse production state/client/authority/presenter code and existing console assertions. Pause at checkpoints; allow inspection or manual takeover, then restart the deterministic scenario before resuming scripted assertions. Bound automated waits; intentional human pauses must not count as network/test timeouts. No gameplay code should depend on the runner.
7. **QA-002 — Capture a review gallery.** First prove that actual-frame screenshots include UI text and that recorded animation advances at normal speed. Capture paired per-seat PNGs and short MP4 clips, with optional side-by-side comparison. Use installed ffmpeg for encoding; no Recorder or Multiplayer Play Mode package is required. Link media and expectations from a Markdown index. Store large outputs outside tracked Assets, in a user-selected run directory or a printed temporary default. Record build/source identity, scenario, seed/fixture, mode, resolution, seat and checkpoint. Keep original per-peer captures. A missing capture or unreadable label is a failed capture check, not a reviewed scenario.
8. **VIEW-002 / VIEW-003, then QA-003 — Review, fix and accept.** Use the gallery to identify layout/motion defects and guided mode for interactive checks. Fix gameplay blockers and essential usability defects with placeholder art. Record user-reviewed, automated-pass, failed and unreviewed outcomes separately. Only then switch to ART-001; repeat relevant visual checks with the final assets.

Existing MAINT-001/002 remain optional. SETTINGS-001 stays after art; fixtures can select the already-supported match-fixed options. No core rule, state topology, package upgrade, transport encoding change or asset import is authorized by this documentation work.

## Scenario matrix: what each player should see

Each scenario must name its starting position, actions, rule-linked expectations, checkpoints, expected actor/non-actor controls, and disclosure limits. Define expected behavior from the agreed rules, not solely from current implementation output. Cover these families; do not claim exhaustive coverage of every game position.

| Scenario family | Acting player | Waiting players | Required verification |
| --- | --- | --- | --- |
| Setup/readiness | Own readiness, deal and first-turn controls | Own private deal; correct active-player indication | Normal creation plus deterministic setup; repeated readiness cannot advance twice |
| Access/matching/markers | Select tile and explicit legal train; correct orientation | Public accepted placement and marker changes | Own, marked, empty and shared trains; wrong endpoint/foreign tile rejected |
| Draw/pass | Draw only when unable; playable drawn tile required; pass when legal | Public hand count/marker change, no drawn identity | Empty pile, failed draw, allowance and immutable rejection |
| Doubles | Extra play elsewhere, inherited cover, conditional second double and held third tile | Public obligations and next actor | R-017 separate follow-up draw; final double ends round; no third double |
| Draft/undo/sealing | Private preview and LIFO undo; irreversible draw sealing; commit | Last public board/counts until sealing/commit; no private draft leak | Undo after draw cannot restore pile or renew allowance; no replayed actor motion at seal |
| Announcements/challenges | Manual feedback and penalty only for the affected owner | Public announcement/infraction/count, no private penalty face | Acting and Waiting requests; self/repeated/competing/expired challenge; both penalty settings and empty pile |
| Endings/results | Correct empty-hand/blocked result, scores, Ready or fresh match | Same public result and all joint winners | Score once, next engine, thirteen rounds, fresh identity/readiness/scores |
| Rejection/delivery faults | Clear pending/retry/rejection and usable recovery | Accepted public state only; no replayed action | Duplicate/stale commands/views, gaps, delayed outcomes, late presentation callbacks |
| Disconnect/reclaim | Last usable view, missing-seat feedback, Retry and restored seat | Suspended actions, host resume once everyone returns | In-flight request outcomes, secret rotation and fresh-match abandonment; existing NET coverage retained |
| Navigation/layout | Readable hand, legal target and multiple train context | Understandable public action without losing inspected position | Zoom, horizontal/vertical scroll, fit/end jump, follow policy, true window resizing and real input |
| Motion/interruption | Selection, draw, placement, flip, undo, deal and redeal | Public accepted movement; no foreign hidden hand/preview | Normal speed; concurrent motion; cancellation, suspension/resync and navigation during travel |

Use two peers for baseline acting/waiting comparisons, three for competing challenges, and four/eight for board density. Include host and remote actors. Test core setup counts for every 2–8-player count. Use existing representative resolutions 1280×720, 1920×1080 and 2532×1170; label offscreen targets separately from real-window tests. Add a deliberate vertical-overflow fixture; earlier view fixtures did not establish it. Require readable own and shared train context together, and let review establish whether the wider train overview is sufficient. Retain real trackpad/touch review as explicitly pending until performed.

Rare-rule fixtures and synthetic presentation stress tests must be labeled as such. At least the baseline peer comparison must run through actual localhost transport; a synthetic recipient view is not multiplayer/privacy evidence. A review gallery may show multiple private views to the developer, but each game's rendered observer view and packet assertions must retain recipient privacy.

## Animation acceptance: varied timing, smooth travel, clear separation

VIEW-003 includes choreography, not only endpoint correctness. The current deal already applies an index-based start delay; other batches run motions concurrently. This is not evidence that all in-flight paths remain separated.

- Give groups modestly staggered starts and varied arrivals rather than a synchronized departure/landing. Use bounded, seedable variation so a failed run is reproducible; tune the actual ranges from short normal-speed clips.
- Use smooth acceleration/deceleration and continuous position, rotation and scale. Avoid snapping or abrupt speed changes in ordinary travel; explicit recovery snaps remain separately identified.
- Coordinate paths, delays and durations so moving dominoes do not visibly intersect or obscure each other, or pass through resting dominoes along their routes. Include rotated tile footprints, not only center-point spacing. Timing randomness alone cannot guarantee this; sequence or route conflicting moves.
- Keep separation while the user pans/zooms, while hand tiles rearrange, and during queued placements, draw, undo and redeal. Vary motion without stretching total batches into the existing presentation watchdog.
- Preserve authoritative timing: animations never decide rules, advance a turn, extend challenge windows or delay receipt of accepted state. Local board input may still wait for presentation; Announce/Challenge retain their supported behavior during motion.
- Add timing assertions and sampled projected tile-footprint checks for concurrent motion. Ignore zero-area edge contact and explicitly identified hidden entry/exit regions; do not mask visible collisions. Sampled checks flag likely overlap but cannot prove absence between frames. Review normal-speed clips for feel and frame-step flagged intervals for separation.

Exact easing and timing values are implementation-time tuning within these agreed requirements. Do not mark visual appeal accepted solely because motion completed or collision samples passed.

## Verification and art gate

For follow-up implementation, start with focused failing regressions for concrete defects. Run the relevant core, state/client, adapter and board checks, then actual Unity localhost scenarios. Use the commands in the [core guide](../../../RulesEngine/README.md), [state debugging guide](../STATE_MACHINE_DEBUGGING.md) and [Unity harness README](../../../Tests/UnityIntegration/README.md). Check Unity CLI availability/account/license before builds; lack of CLI account access does not establish Editor license failure. Preserve the pinned editor/packages and saved scene settings.

ART-001 starts when GAME-001–003 and QA-001–003 have their required evidence, VIEW-005/MAINT-004 are resolved for the review build, and essential VIEW-002/003 acceptance has user review. No known gameplay blocker, unreadable required action/result or visible motion collision may be silently waived. Keep nonblocking cosmetic findings and untested device/network cases individually tracked. User review may request changes; a passing script is not approval of visual quality. Completed tasks stay completed unless new evidence identifies a regression.

Record scope precisely: static mapping, console/simulated delivery, synthetic presentation probes, actual localhost peers, human visual review and live-service checks are different evidence. Current work saves this roadmap and tasks only; no new behavioral test result follows from it.

## Manual multi-instance testing

Use Unity 6000.6.6f1 Play Mode alongside a normal development build, or launch multiple normal build instances on macOS:

```sh
open -n "/path/to/TrainDominoesNetcode.app" --args --ugs-profile review-a --mute-audio
open -n "/path/to/TrainDominoesNetcode.app" --args --ugs-profile review-b --mute-audio
```

Use distinct service profiles per build and the Editor's separate default identity. Press Start in each for the existing Lobby/Relay path; once everyone is connected, the host presses Ready to create the roster, then each player readies. This uses online services; local automated review defaults to direct transport. The host must remain running. Do not use the temporary Integration.app for manual play: it injects fixtures and automatically plays. The default runner also launches batchmode/nographics and accelerates matches; visible guided review does not exist yet.

Multiplayer Play Mode is an optional future convenience, not a dependency; its package is not listed in this checkout. Direct Host/Client developer controls are inactive in the saved scene and require direct transport configuration; merely enabling those buttons does not switch the saved Relay transport. Do not use dedicated Server: current match creation requires a seated host.

## Later goals

- **UX-001 — Waiting-player experience.** Brainstorm public turn progress, obligations, opponent actions, last-move context and private planning without revealing hands/drafts or allowing unauthorized out-of-turn actions; preserve supported Announce/Challenge commands. Current target remains a synchronous game with all participants engaged. Produce requirements before adding interaction states or new UI.
- **ASYNC-001 — Asynchronous matches and turn notifications.** Explore players closing the app between turns, receiving an opt-in push, reopening the correct match, taking a turn and leaving. The present in-memory host/Relay and seat-secret lifecycle is insufficient: define durable authoritative hosting/storage, stable identity, recovery, notification delivery and deep links, match-fixed rules, timeout/abandonment policy, security/privacy and costs. Specify duplicate/stale notification handling and authority resynchronization on relaunch. This goal is discovery only, with no provider or architecture selected; it is not a pre-art blocker.
