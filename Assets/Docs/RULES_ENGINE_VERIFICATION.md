# Standalone rules engine verification

Date: 2026-10-17. Scope: accepted standalone-first engine plan, U1–U5 plus U7 manual announcements. U6 Unity gameplay integration is deferred. The staged anticipated-state document was preserved and updated with the agreed policy additions; no existing gameplay/state source, scene, prefab, asset art, or package manifest was changed.

## Delivered boundary

Runtime: `Assets/Scripts/RulesEngine`, namespace/assembly `TrainDominoesRulesEngine`, .NET Standard 2.1/C# 9. External library and NUnit project link these actual sources. The runtime has no package, Unity, Netcode, rendering, input, animation, persistence, or transport dependencies. [Developer guide and runnable commands](../../RulesEngine/README.md).

The default is Immediate placement, with match-fixed optional Draft/undo. Manual announcements use opponent challenges and a match-fixed DrawOneTile/None penalty, defaulting to DrawOneTile. Readiness, joint winners, suspension, new-match resets, and R-017 double/draw interpretations were reviewed individually with the user before implementation. These are application choices, not claims that the publisher defines every software boundary.

## Checks actually performed

| Check | Observed result / limit |
| --- | --- |
| NuGet restore and standalone build | Passed using SDK 10.0.402; NUnit 4.4.0, adapter 5.2.0, Test SDK 18.10.1 pinned. Sandboxed compiler processes stalled; the same commands completed with sandbox escalation |
| External NUnit suite | 48 tests passed, none skipped; actual domain/command/query/lifecycle sources, without Unity mocks |
| Installed Unity compiler | `python3 RulesEngine/verify_unity_compile.py` passed using Unity 6000.6.6f1's Roslyn compiler, C# 9, warnings as errors, only .NET Standard 2.1 references; output in temporary storage |
| Actual Unity import smoke | Unity 6000.6.6f1 batch import in a temporary minimal project passed; consumer code created a match, readied seats, dealt a round, and reconstructed a position token. Log marker: `RULES_ENGINE_IMPORT_OK: TrainDominoesRulesEngine; refs=netstandard`. Process exited zero |
| Existing state-machine checks | Passed all nine grouped checks; these characterize the unchanged legacy interaction flow, not Puremco correctness |
| Source state map | Strict before/after map comparison passed: eight concrete states, 21 transitions, zero added/removed transitions or event reactions, identical source hash, no extractor warnings |

The temporary Unity project's manifest had no package dependencies and no network-game scene. It proves the new assembly imports and a real Unity consumer can call its public API; it does not prove compatibility with the main scene's managers/RPCs. Unity logged shutdown messages including `Curl error 42: Callback aborted`; the smoke success marker and process exit were observed, with no C# compilation errors. Original smoke log: `/tmp/train-rules-unity-import.log` (ephemeral).

## Behavioral evidence

- `DomainTests`: all 91 legacy-compatible IDs, every 2–8-player deal count, personal plus exactly one shared train, deck membership validation and invalid setup.
- `TurnRulesTests`: exposed endpoint and orientation, ordinary/double access, extra play elsewhere, conditional second double, final-tile priority, separate follow-up draw attempt, marker ownership/removal, actor/tile/train rejection.
- `DraftAndViewTests`: provisional final-double commitment, reverse all three placements of a two-double sequence, sealing on draw, no draw renewal or tile return, private marker effects, old-snapshot stability and read-only collections.
- `AnnouncementTests`: manual notification between turns, repeat/unnecessary feedback, one private penalty draw, stale and duplicate challenge rejection, expiry on accepted action, rejected-action preservation, self-challenge rejection, empty pile, no-penalty setting, private draft suppression.
- `LifecycleTests`: readiness idempotence, stale identity/phase rejection, suspension, fresh roster/reset, blank zero, score once, blocked result without a winner, next-round reset and joint final winners.
- `ReplayTests`: deterministic complete thirteen-round matches at 2/3/8 players in both modes, each replayed twice from explicit shuffled decks. Every accepted transition checks tile conservation and prior-snapshot preservation; final scores/starter/engine schedule are asserted.
- `ViewAndInvariantTests`: all 91 tile IDs against every train across 30 sampled positions in each mode, checking placement query/validation agreement, action queries, immutable rejection, conserved accepted states, standard-library dependencies and exact deck consumption order.
- `RegressionTests`: confirmed and corrected premature blocked-round termination before later passes unlock trains; confirmed and corrected non-public token construction for external adapters.

The initial domain/turn tests failed compilation because the command facade did not yet exist, then passed after implementation. Both review regressions were separately observed failing with assertions before their production fixes. Other cases were added as focused coverage alongside implementation; this is not a claim that every test had an observed red stage. Test fixtures may construct conserved synthetic mid-round snapshots through the test assembly's internal access. Complete-match replay uses only public transitions and no rule mocks. Legacy ID parity is source-derived from `DominoTracker.CreateDominoSet`; the legacy comparison defect is not a target-rule expectation.

## Review and remaining verification

A focused independent static review examined legality, undo, views, announcements and lifecycle; its two concrete findings were fixed and covered by regressions. A manual simplification/diff scan complements that review.

Code review: skipped (ce-code-review unavailable). That skill is absent from the session catalog; the focused static review is not presented as a receipt from it.

Not performed: main-scene Unity Play Mode, host/client RPC or DTO checks, live Lobby/Relay, settings GUI, announcement/challenge buttons, suspension/reconnect UI, serialization replacement, or eight-player visual layout verification. No runtime-characterization task or Unity integration task is closed by these core tests.

## Final validation

The final standalone solution build passed with warnings treated as errors (zero warnings/errors). All 48 rules tests and nine legacy state checks passed. Strict map comparison confirmed no state changes. Source/metadata and documentation-link checks passed; no main-game scene or package edits were made. Core test results do not establish multiplayer convergence or privacy across a serializer/RPC boundary; those remain ENG-006 and NET-001.
