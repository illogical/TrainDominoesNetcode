# Actual Unity CLI integration checks

These are temporary-player integration checks, separate from the source-linked .NET suites. Do not copy the runtime harness into the working project's Assets: it replaces the authority with a reproducible deck and automatically plays a match.

The harness runs the actual GameScene, MonoBehaviours, Netcode-generated RPCs, UnityTransport localhost sockets, JsonUtility projections, state coordinator, mesh presenter, and animation coroutines. By default it disables SimpleMatchmaking before Start and uses no Lobby/Relay service. Explicit `--live` modes retain SimpleMatchmaking and use Lobby/Relay, as described below. Both players first complete setup and the host's first selected placement at normal animation speed. Later lifecycle work uses `Time.timeScale=50`, which is explicitly logged. This verifies completion and final transforms, not perceived visual quality.

Current recorded outcomes are in [integration verification](../../Assets/Docs/GAMEPLAY_INTEGRATION_VERIFICATION.md), including the later NET-001/005/006, NET-003/004 and VIEW sections. [VERIFICATION.md](VERIFICATION.md) preserves the initial smoke-run limits and later appended reruns; those initial limits are not a current inventory of all coverage. The [gameplay review roadmap](../../Assets/Docs/plans/2026-10-18-gameplay-gap-analysis-and-review-plan.md) tracks the remaining startup, recovery, admission and human review gaps.

## Reproduce

Use Unity **6000.6.6f1** and an isolated copy of the project's Assets, Packages, and ProjectSettings. The observed run reused `/tmp/td-legacy-baseline` from the earlier baseline; its production scripts were replaced using `rsync --delete`, and both legacy harness scripts were removed. Reusing its Library cache avoids importing the whole project again. Main-project editor locks do not affect this isolated project.

1. Copy current `Assets/Scripts` plus their metadata into temporary `Assets/Scripts`, deleting obsolete sources there.
2. Copy `IntegrationRuntime.cs` into temporary Assets and `IntegrationBuild.cs` into temporary Assets/Editor.
3. Build and run:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath /tmp/td-legacy-baseline -executeMethod IntegrationBuild.Build -logFile /tmp/td-integration-build.log
python3 Tests/UnityIntegration/run_players.py
python3 Tests/UnityIntegration/run_players.py --draft
python3 Tests/UnityIntegration/run_eight.py
python3 Tests/UnityIntegration/run_players.py --presentation-only
python3 Tests/UnityIntegration/run_players.py --challenge
python3 Tests/UnityIntegration/run_players.py --challenge --penalty-none
python3 Tests/UnityIntegration/run_players.py --reconnect
```

`--reconnect` (NET-004): after three actions, the client submits a move and drops its connection before the answer arrives. It reconnects through the Retry action under a new transport ID and must reclaim the same seat with a rotated secret. A forged claim for the other seat is sent over a real RPC and must be refused. The host must report the missing seat, then everyone back, and resume. The match then finishes.

`--live` (NET-003) connects through the real Unity Lobby + Relay path (`SimpleMatchmaking`) with separate anonymous profiles (`--ugs-profile td-host` and `td-client`). `--live --reconnect` adds the drop, Relay rejoin and seat reclaim. **They use network services (free tier): run them only with the user's permission.** Both modes share the `reconnect` log names, so copy `observed-reconnect-*.log` aside before running the localhost `--reconnect` again; the 2026-10-18 live reconnect logs are `observed-live-reconnect-*.log`.

The two `--challenge` modes (NET-005) play complete Immediate matches without announcing. The player who sees an open window challenges, after first ignoring one window to prove the next accepted action closes it. They check self and repeated challenge rejections, penalty application and allowance, and private penalty delivery. Every mode checks privacy invariants on each received view.

The builder writes `/tmp/td-integration-build/Integration.app`; the runner starts two actual player processes over `127.0.0.1:17898`. Do not run the two modes concurrently. The runner clears its own coordination files, applies a bounded deadline, terminates remaining child processes on failure, and writes filtered evidence logs only after both players pass. Unity requires access to its local Package Manager Unix socket and localhost UDP sockets, which may require sandbox escalation.

## Assertions and limits

The assertions cover recipient projection startup, profile-sized hands, three train identities, accepted commands, final selected-placement positions, identical-request replay, stale command rejection without mutation, thirteen-round results/winners, fresh-match score reset, and remote disconnect input suspension. Draft additionally exercises undo and end-turn through the same client APIs and real transport. Automated selection in the match scenarios calls the production ClientSession API; pointer hit testing is exercised by the view probes below, not by these matches. Initial Ready uses the actual generated Button.onClick listener, while tile/train actions use the production ClientSession selection API.

A complete smoke run is not exhaustive adapter coverage. Later recorded runs cover seat reclaim/resume over localhost and authorized Lobby/Relay, challenge penalties/privacy over actual transport, and synthetic presentation recovery/joint-winner fixtures. Delivery faults below the reliable transport, competing challenges with three or more peers and empty-pile penalties remain console-only evidence; human visual quality and real-world network conditions are unverified. Deterministic legal play chooses the first permitted placement, then draw/pass; it does not use development legality bypasses.

The match harness injects a deterministic `AuthoritativeMatchCoordinator`, including in `--live` runs. These runs do not prove the normal initial production host-creation path; GAME-001 tracks that separate fixture. Setup supplier failures currently log `LastLifecycleError` without participant retry controls (GAME-002). Arrival after roster freeze remains GAME-003. Existing automated full-match runs accelerate after initial play; normal-speed gallery and guided review modes are tracked by QA-001/002.

The eight-player harness runs on port 17899. It checks nine trains and nine tiles per hand at all eight recipients, a host action, participant disconnect suspension, disabled input, and a replacement match with the seven remaining seats. Readiness and normal commands may receive safe StalePosition rejections when participants act concurrently; the harness retries from the latest view after acknowledgment.

The later `--challenge` and `--challenge --penalty-none` runs verified both DrawOneTile and None over actual Netcode transport; every match mode checks recipient privacy on received views. Console `PrivacyAnnouncementChecks` separately covers private Draft/undo/draw sealing, competing challenges and empty-pile penalties. Preserve the distinction between console-only cases and actual transport evidence; see the dated NET-005 section of [integration verification](../../Assets/Docs/GAMEPLAY_INTEGRATION_VERIFICATION.md).

## Board navigation and presentation probes (VIEW-001–004)

`ViewRuntime.cs` runs in one player per window size and player count, with no network. Copy it into the temporary project's `Assets` beside `IntegrationRuntime.cs` (which skips view mode), rebuild, then run:

```sh
python3 Tests/UnityIntegration/run_view.py                 # 1280x720, 1920x1080, 2532x1170 × 2, 3, 8 players
python3 Tests/UnityIntegration/run_view.py --screenshots   # also saves PNGs to /tmp/td-view-shots for human review
```

Batchmode players always report a 640×480 screen. The probe therefore renders the real camera into an offscreen target of the requested size and puts the canvases in that camera's space, so overlay UI and raycasts use that pixel space. The recorded screenshots omit UI text; they do not establish readable action/result labels. No window opens. This simulates window sizes; it is not a resized desktop window. `--screenshots` keeps a Metal device (still batchmode). The default uses `-nographics`.

The probe hides the start menu and closes the developer console, as a started match and a developer would (findings under VIEW-005). It feeds synthetic recipient views through `ClientSession.Receive`, so the production presenter, layout, navigator, row controls and state machine all run. Fixtures: one 40-tile shared train, other trains with up to 13 tiles, an eight-tile hand, and nine rows at 8 players.
- **Layout (VIEW-002, automated part):** every visible control is on screen; action buttons, row controls, the board and the hand do not overlap; the hand is selectable; the minimum zoom keeps a tile face at least 28 px tall (scaled by dpi).
- **Reachability (VIEW-001):** at fit and maximum zoom, scripted navigation brings every track tile and every row's playable end on screen. The production raycast (`InputManager.TileAt`) then returns that tile, and no UI covers it.
- **Pointer input:** queued Input System `Mouse`, `Keyboard` and `Touchscreen` events. Click selects; a drag scrolls and never selects or chooses a train. The wheel zooms anchored at the cursor; Shift+wheel and sideways deltas scroll; double-click returns to the end; `F` fits. Clicking a legal row's last tile sends the Place request; its rejection shows player-facing text. For touch: tap, drag with fling inertia, double-tap, and a pinch anchored at its midpoint that never selects.
- **Animations (VIEW-001/003):** placement, undo, a flipped opponent placement, three queued placements and a cancelled mid-flight placement, each while the board pans and zooms. Each tile must end exactly on its local target, under the right root, at hand scale.
- **Follow policy (D4):** a following row tracks a new tile; an inspected row keeps its position and its jump control pulses; a new round resets navigation.
- **Notices (VIEW-004):** an announcement is shown once, including after duplicate delivery and resynchronization. The owner's penalty tile appears face up in their hand and in their notice; an observer's notice names no tile.
- **Results (VIEW-002):** a blocked-round result, joint winners ("Joint winners: Player 1 & Player 2"), score rows named "Player N", and New Match visible.

These are presentation probes with synthetic input. They are not a human-visible review, a real trackpad or touch device, or a full match. The last results are in `observed-view.log`.

## Silent testing

Batchmode players are muted by `SoundManager` (a batchmode macOS player otherwise still plays sound), and the runners also pass `--mute-audio`. Every scenario asserts `audio-muted-for-testing`. To check the mute policy itself, copy `TestingAudioBatch.cs` into the temporary project's `Assets/Editor` and run `-executeMethod TestingAudioBatch.Run`.

## Wire contract batch check (NET-002)

`WireContractBatch.cs` runs in the Editor without a player build. Copy it with `Tests/GameplayIntegration/WorstCaseMatch.cs` into the temporary project's `Assets/Editor`, then run:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /tmp/td-legacy-baseline -executeMethod WireContractBatch.Run -wireOut /tmp/td-wire-contract -logFile /tmp/td-wire-contract.log
```

It exits non-zero on failure. It writes `wire-contract-report.txt`, which records the raw `JsonUtility` behavior, the codec assertions and measured sizes from 20 seeded matches each at 2, 4 and 8 players. It also writes `Fixtures/*.json`. Copy the fixtures to `Tests/GameplayIntegration/Fixtures/` when the payload shape changes; the console suite decodes them. The last report is `observed-wire-contract.txt`.

The focused `--presentation-only` mode performs regular real host/client startup and Ready, then runs a synthetic recipient-view regression: a tile visible on one train at revision 10 is moved to another train at revision 12. It checks generation invalidation and the final mesh position. Its logs and `production-source-gap.sha256` are separate from the earlier complete lifecycle runs.

## Gameplay closure and visible review (2026-10-18)

Copy `ReviewRuntime.cs` beside the other temporary runtime files before building. Copy the current scene, domino prefab and `DefaultNetworkPrefabs.asset` too; copying scripts alone leaves the old console/prefab defects in a cached temporary project. The working project has no runner dependency. New source-linked setup/admission checks live in `Tests/GameplayIntegration`; numerical deal-timing checks live in `Tests/Motion`.

```sh
python3 Tests/UnityIntegration/run_players.py --normal --output-dir /tmp/my-run
python3 Tests/UnityIntegration/run_players.py --setup-failure --output-dir /tmp/my-run
python3 Tests/UnityIntegration/run_eight.py --normal --output-dir /tmp/my-run/eight
python3 Tests/UnityIntegration/run_review.py --actions 8 --side-by-side
python3 Tests/UnityIntegration/run_review.py --guided --actions 8 --output /tmp/my-review
```

`--normal` uses production host creation and the original shuffled supplier. It checks host-only creation, duplicate readiness, standard setup, full thirteen-round progression, starter rotation, scores and fresh reset. `--setup-failure` keeps that production authority but wraps its deck supplier to fail twice; it tests participant feedback, host retry, foreign/stale/duplicate retry rejection and recovery before completing the match. Eight-seat `--normal` uses production creation/random deck for the capacity/startup/suspension/replacement checks. It is not an eight-seat full-match run. `--output-dir` preserves existing evidence logs.

The visible review runner uses two actual localhost peers with a labeled deterministic authority fixture. It records actual game frames, production UI text, moving tile transform samples, paired PNGs, timestamped MP4s and a Markdown gallery. Vision OCR and elapsed/moving-frame checks verify capture plumbing; they do not establish visual quality or absence of collisions. Guided mode pauses both scripts indefinitely at checkpoints; manual takeover requires a fresh deterministic restart before scripted assertions resume. It keeps gameplay UI enabled. Large media goes to an empty requested directory or a printed temporary directory outside tracked Assets. See [expectations and required coverage](REVIEW_EXPECTATIONS.md) and [implementation evidence](GAMEPLAY_GAP_VERIFICATION.md).
