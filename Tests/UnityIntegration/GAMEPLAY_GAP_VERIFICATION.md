# Gameplay gap implementation and verification

Date: 2026-10-18. Implements the actionable portions of the [gameplay-gap roadmap](../../Assets/Docs/plans/2026-10-18-gameplay-gap-analysis-and-review-plan.md). This is a partial roadmap delivery: the art gate remains closed. No new live-service run or human visual acceptance is claimed.

## Delivered behavior

- **DOC-005:** reconciled current rulebook, overview, state matrix/inventory and harness summaries while preserving dated history.
- **GAME-001:** separate `--normal` fixture uses the real host Ready → CreateMatchServerRpc path, original authority and original shuffled supplier. Two actual localhost peers complete thirteen rounds and reset. Checks cover host-only creation, connection before roster freeze, duplicate readiness, correct deal, random initial actor within roster, starter rotation, score/redeal and fresh identity/empty hands/readiness/scores. Eight-player `run_eight.py --normal` separately covers production creation, nine trains/nine-tile hands, action, suspension and seven-seat replacement. The eight-player fixture is not a full thirteen-round run.
- **GAME-002:** setup failure reaches participants, and the host can Retry in the existing RoundInitializing state. The existing position-stamped Ready command represents retry only in AwaitingDeck. The coordinator checks host, suspension, exact position and cached request identity; it accepts no deck from clients. Supplier exceptions and invalid decks preserve readiness, match identity and scores. A corrected supplier starts once. The retry outcome accompanies its phase-changing projection; observers clear obsolete setup failure text. See the [focused contract](../../Assets/Docs/plans/2026-10-18-round-setup-recovery-contract.md).
- **GAME-003, partial:** production creation requests Lobby `IsLocked=true`; failed updates log and retry on heartbeat. Relay remains available for seat reclaim. Already-connected unseated arrivals receive waiting-for-fresh-match feedback and no projection. Console checks cover eight frozen seats, unauthorized arrival, reclaim and fresh seating. The new Lobby update and near-capacity real-transport admission/reclaim behavior remain unverified; the SDK call was inspected locally, not mocked or exercised live. No spectator feature or full GAME-003 completion is claimed.
- **VIEW-005 / MAINT-004:** Start hides on any network start and stays hidden through reconnect. Quantum Console starts closed in every build (user-selected D1), retaining its backquote shortcut. Editor-authored cleanup removes both unused network components and the default registration from the domino prefab, and removes the parenting workaround. DominoPlayer and its explicit network list remain byte-identical.
- **QA-001/002, partial:** automatic/guided normal-speed two-peer review with paired actual-frame PNGs, original per-seat MP4s, optional side-by-side, OCR checks, motion samples, build/source metadata and a linked gallery. Guided checkpoints do not consume script/network deadlines. Manual takeover requires a fresh deterministic restart before assertions resume. The rule-linked expectation matrix explicitly identifies missing scenario-family fixtures.
- **VIEW-003, partial:** seedable initial-deal start/duration variation stays inside the original batch deadline. Missing curves use smoothstep. Numerical tests sample separated parallel deal columns. This does not solve or establish general rotated placement/draw/undo/resting-tile separation.

No core rule, state inventory, package version, transport encoding, final art or saved development bypass setting changed.

## Automated evidence

The isolated Editor license works; Unity CLI account access itself failed because its account database is read-only in the sandbox. Builds use the pinned Editor 6000.6.6f1 directly with local IPC permission. No Pipeline package was installed and the working Editor was not driven.

| Check | Observed outcome and boundary |
| --- | --- |
| NUnit core | 48 passed; initial sandbox run could not bind the test-runner socket, rerun with local socket permission passed |
| Gameplay integration console | All suites pass, including new initial/later-round setup failure, invalid deck, host-only retry, duplicate/stale/suspended retry, score preservation, phase-change acknowledgment, observer feedback and 2–8 setup counts; frozen eight-seat admission/reclaim checks are coordinator-only |
| State console | 18 grouped checks pass; Retry entry/exit preserves observing handlers; actual state/client sources with adapter stubs |
| Client / board console | Client correlation checks and 98 board-presentation checks pass |
| Motion console | 1,331,855 numerical timing/motion checks pass; actual AnimationHelper linked with Unity stubs; synthetic orthographic footprints, not actual projected rotated meshes |
| Mapper | 11 tests pass; strict before/preview/after comparison has zero warnings, unchanged states/routes and a balanced Retry handler addition; checked-in map regenerated |
| Core Unity compiler | C# 9 / .NET Standard 2.1 check passes |
| Isolated Unity build | Final build exit 0; working source, metadata, affected scene/assets and runtime harness byte-compared against the temporary project (165 files) |
| Production normal lifecycle | Two real localhost peers complete thirteen rounds and fresh reset; accelerated after initial setup, separate from visual evidence |
| Setup failure localhost | Production authority retained; supplier wrapper fails twice then uses the original shuffled supplier. Both peers observe failure, host-only/persistent/duplicate/stale retry checks pass, recovery and thirteen-round completion/reset pass |
| Eight-player startup | Both production-entry/random-deck and existing injected-authority capacity fixtures pass; action, suspension and seven-seat replacement, not full eight-seat matches |
| Visible gallery | 19 paired checkpoints (38 PNGs), two original clips plus comparison; eight ordinary accepted actions; OCR and production moving-transform/1× elapsed-time checks pass; human acceptance UNREVIEWED |
| Guided smoke | Initial readiness checkpoint remained paused beyond the normal script timeout; takeover paused both scripts, restart created a fresh directory/fixture, then explicit stop exited 2 as designed. No manual gameplay/device acceptance |
| Full regression matrix | All final-build modes and all nine view combinations pass; zero NGO parenting errors; details below |

Console receipts: [observed-gameplay-gap-console.txt](observed-gameplay-gap-console.txt). Editor prefab/console receipts: [observed-presentation-assets.txt](observed-presentation-assets.txt). Source identity: [production-source-gameplay-gap.sha256](production-source-gameplay-gap.sha256). The full executable/source comparison manifest and new localhost logs are in `/tmp/td-gameplay-gap-evidence/`; original checked-in log edits were preserved.

## Review media and reproduction

Gallery: [/tmp/td-gameplay-review-verified/index.md](/tmp/td-gameplay-review-verified/index.md). It includes the original actor/observer captures, source/build identity, actual dimensions, seat, fixture, mode and checkpoint. Its manifest must be read with the matching `/tmp/td-gameplay-gap-evidence/build-identity.json`; current-workspace digest alone is not a build provenance guarantee. Large media stays outside tracked Assets in the printed temporary run directory. Copy it elsewhere if long-term retention is needed.

```sh
python3 Tests/UnityIntegration/run_players.py --normal --output-dir /tmp/my-run
python3 Tests/UnityIntegration/run_players.py --setup-failure --output-dir /tmp/my-run
python3 Tests/UnityIntegration/run_eight.py --normal --output-dir /tmp/my-run/eight
python3 Tests/UnityIntegration/run_review.py --actions 8 --side-by-side
python3 Tests/UnityIntegration/run_review.py --guided --actions 8 --output /tmp/my-review
```

The review fixture deliberately injects authority/deck before Ready and is labeled accordingly. It proves actual localhost/render/capture plumbing, not production startup or exhaustive privacy. Production startup is the separate normal fixture. [README](README.md) documents temporary-project copying/building; [expectations](REVIEW_EXPECTATIONS.md) names each required family and outstanding checks.

## Scope, serialization and residual work

The Unity Editor reserialized the scene and domino prefab while applying the authorized changes. Most scene diff is wrapping/default-field migration. NGO also serialized its already-effective SpawnTimeout clamp `1 → 10` and scene-object `SceneMigrationSynchronization 1 → 0`; these were not hand-edited. Existing GUIDs/references were retained, and IgnoreDominoComparisons=1, StartAtRound=2, DominoStartCount=12 remain unchanged. Core rules ignore these development fields. Quantum Console's third-party prefab/source was not edited.

The setup regression was observed failing before implementation (host retry did not call the supplier). Focused Editor checks failed on baseline networked tiles before cleanup and passed afterward. A normal-fixture stale-command assertion initially mistook a concurrent remote move for mutation; the fixture was corrected without weakening the coordinator's existing duplicate/stale tests. ReviewRuntime's missing namespace import was corrected before the successful builds.

**Still required:** mocked/new live admission locking and near-capacity real-transport reclaim matrix (no live service authorization requested here); visible rare-rule, Draft/sealing, three-player competing challenge, ending/result and fault/recovery scenarios; four/eight-player density and deliberate vertical overflow captures; actual pointer/window/trackpad/touch review; general moving/resting rotated-footprint collision diagnostics and choreography; readable simultaneous own/shared train context; user review of essential layout and motion. Existing row-label/row-control clipping candidates remain open. QA-001/002 are useful infrastructure plus baseline coverage, not complete family coverage. QA-003, VIEW-002/003 and ART-001 remain open; no visual or collision defect is waived.

Code review: skipped (ce-code-review unavailable). The skill is absent from this session's catalog and its local entrypoint was unavailable. Simplification used a manual pass because ce-simplify-code was also unavailable. Manual diff/source/privacy/subscription/scope inspection was performed; it is not an independent review receipt. Changes are left uncommitted to preserve the user's staged plan/task work and unrelated log edits.

## Final execution receipts

All final-build runs passed: `--normal`, `--setup-failure`, Immediate, Draft, presentation-only, challenge DrawOneTile, challenge None, reconnect, injected-authority eight-player and production-entry eight-player. All nine view combinations passed (1280×720 / 1920×1080 / 2532×1170 × 2 / 3 / 8 players). Direct startup and reconnect logs assert Start remains hidden; view logs assert the console starts closed without deactivation. Final raw two-peer/eight-player logs contain zero NGO `re-parent` / `not listening, start` errors, with the prefab workaround removed.

[Durable localhost receipts](observed-gameplay-gap-localhost.txt) retain selected assertions and each PASS ALL. Full filtered logs and build/source identity remain in `/tmp/td-gameplay-gap-evidence`. The final gallery has 38 readable paired checkpoint PNGs, per-seat MP4s and side-by-side comparison. Moving-frame pairs: host 34, client 46; elapsed scaled/realtime ratios 0.9987 / 1.0000. The gallery executable hash matches the independently byte-compared build manifest. These checks do not measure footprint collisions or constitute user acceptance.

Final strict state map: same eight nodes and transitions; one balanced Retry reaction added; zero extractor warnings. Documentation relative-link checks and `git diff --check` pass. NUnit core, final five source-linked console suites, mapper tests, core Unity compile and Editor asset checks passed as reported above. No package, core-rule or transport-schema edits. No commit or publication performed.
