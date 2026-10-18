# Gameplay integration verification

Date: 2026-10-18. Implements the [integration plan](plans/2026-10-17-refactor-rules-gameplay-integration-plan.md), expanding existing U6/ENG-006. The [developer guide](GAMEPLAY_INTEGRATION.md) describes the resulting ownership and action flow. The earlier [standalone verification](RULES_ENGINE_VERIFICATION.md) remains evidence for the portable core, not a substitute for this report.

## Implemented boundary

`GameSession` now authenticates one command RPC boundary and sends recipient-only JSON projections. `AuthoritativeMatchCoordinator` owns the sole live rules snapshot and outcome cache. The legacy group turn and mutable DominoTracker/TurnManager/RoundManager/TurnStatus authorities were removed. State transitions consume projections through `PlayerStateCoordinator`; selection remains local and permission buttons remain advisory.

The eight concrete modes are Pregame, RoundInitializing, Acting, AwaitingTurn, ActionPending, RoundOver, GameOver and SessionSuspended. The existing factory/cache/explicit-self-reentry/disposal semantics remain. A same-mode refresh does not re-enter. The source mapper now follows coordinator lifecycle and routing rather than silently omitting its transition calls.

Existing animation assets and serialized references remain. Presentation batches are ordered independently from command acknowledgment; aggregate motion controls only local board input. Announce/Challenge bypass that mask. Generation guards, queue-overflow reconciliation, terminal cancellation and a ten-second default safety budget prevent old motion from controlling a new round or match. New controls use actual Unity Buttons cloned from the saved style template.

## Observed automated checks

| Check | Outcome and limit |
| --- | --- |
| Pinned standalone restore/build | Passed; build with warnings as errors, zero warnings/errors. NuGet audit lookup disabled during restore because that service was unavailable; package versions unchanged |
| Standalone NUnit core suite | 48 passed, zero failed/skipped; actual shared core sources |
| `Tests/GameplayIntegration` | 26 boundary assertions passed: authenticated mapping, submitted-token checks, request deduplication, no-op Ready, ordered setup, private draw/events, Draft/sealing and eight-seat train capacity |
| `Tests/StateMachine` | 17 grouped checks passed: actual states/coordinator/client sources with adapter stubs; includes host immediate replies, recovery, setup/observing announcements, generation and disposal behavior |
| `Tests/ClientInteraction` | Passed correlation, monotonic views, uncertainty, retired matches, suspension and transport-restart reset checks |
| State mapper | 11 behavioral tests passed; strict source map and before/actual comparison succeeded with zero extractor warnings |
| Installed Unity compiler | Core compiled under C# 9/.NET Standard references; gameplay compilation passed against installed project references. Legacy serializer, deprecated RPC API and unrelated existing warnings remain |
| Metadata/scope | State rename GUIDs preserved, no missing C# sidecars or duplicate asset GUIDs, no scene/prefab/package/project-setting changes, `git diff --check` passed |

Console commands, run from `RulesEngine` to enforce the SDK pin:

```sh
dotnet restore TrainDominoesRulesEngine.slnx --ignore-failed-sources -p:NuGetAudit=false
dotnet build TrainDominoesRulesEngine.slnx --no-restore --disable-build-servers -p:UseSharedCompilation=false -warnaserror
dotnet test TrainDominoesRulesEngine.Tests/TrainDominoesRulesEngine.Tests.csproj --no-build --no-restore --disable-build-servers
dotnet run --no-restore --project ../Tests/GameplayIntegration/GameplayIntegration.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
dotnet run --no-restore --project ../Tests/StateMachine/StateMachine.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
dotnet run --no-restore --project ../Tests/ClientInteraction/ClientInteraction.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
```

Mapper tests and `RulesEngine/verify_unity_compile.py` run from repository root using their documented commands. Initial client tests observed a missing-production-source compilation failure; mapper regressions were observed failing before their fixes. Other coverage was added alongside implementation. This is not a claim that every test was observed red first.

## Unity CLI and actual local players

The [legacy baseline](STATE_002_RUNTIME_BASELINE.md) was captured before replacement with the actual scene, Netcode RPCs and two localhost players. It exposed sender identity loss in nested group-turn RPCs and repeated draw acceptance. Later legacy paths were explicitly blocked; they were never forced into a passing result.

Replacement verification uses a temporary project and Unity 6000.6.6f1 CLI player builds. The working project's saved scene settings remain unchanged. Actual core legality governs the replacement even though the saved development settings retain bypass/round/hand values. Temporary instrumentation disables live matchmaking and injects reproducible valid decks; it does not replace commands, rules, transport serialization, presenters or state classes with stubs.

The final temporary build source was independently byte-compared against all 146 working `Assets/Scripts` files, including metadata. Durable logs contain success receipts from all eight capacity-test peers, both Immediate peers, both Draft peers and both final presentation-regression peers.

See the [CLI run report](../../Tests/UnityIntegration/VERIFICATION.md) and [reproduction instructions](../../Tests/UnityIntegration/README.md) for completed per-mode assertions, filtered player logs and the exact production-source hash. Initial setup and a selected placement run at normal animation speed; later lifecycle verification is accelerated. Synthetic presentation stress probes are explicitly separate from authoritative gameplay/transport proof.

## Review and corrections

Code review: skipped (ce-code-review unavailable). Its cataloged definition was unavailable in this environment; the independent static review and manual diff scan are recorded separately, not presented as a receipt from that skill. `ce-simplify-code` was also unavailable; a manual simplification pass removed replaced authorities and consolidated routing and presentation ownership.

Independent static review found four concrete issues, all corrected:

- A late deselection could animate the placed tile while track movement owned it. Selection bookkeeping now clears before placement presentation begins.
- Queue overflow could discard earlier visual history while continuing an incremental diff. Overflow now snaps to the latest projection.
- Suspension within the same core phase could retain an old pending request. Suspension now retires it.
- A rejected stale/retired synchronization could restore availability. Only a current usable projection restores it.

A follow-up independent static review confirmed these fixes and found no additional concrete P1/P2 issue.

A later revision-gap regression was observed failing before a focused fix: missing revisions now invalidate incremental presentation and snap to the latest projection. Actual CLI players verified that a previously seen tile moved to its new train. Full lifecycle runs and this later focused build have distinct recorded source hashes; complete matches were not repeated after that gap-only change.

Client/state regressions cover correlation and lifecycle fixes. Actual CLI transform and synthetic presentation probes cover placement/overflow/cancellation independently from perceived motion quality.

## Pre-Priority-4 refactor re-verification (2026-10-18)

The behavior-neutral [refactor](plans/2026-10-18-refactor-pre-priority-4-network-readiness-plan.md) added `SeatRegistry` and `RecipientProjection`, reformatted the coordinator, `PlayerViewDto.From`, `GameSession` and `GameplayManager`, and added a test-side `LoopbackRelay`. DTO field declarations are unchanged (diffed against HEAD), so JSON payload shape is unchanged. The CLI harness no longer injects the removed `GameSession.matchParticipants` field; participants come from the coordinator.

| Check | Outcome |
| --- | --- |
| Standalone NUnit core suite | 48 passed (core sources unchanged) |
| `Tests/GameplayIntegration` | 26 boundary assertions passed, unchanged; 12 new relay assertions passed: setup convergence, duplicated command delivery applies once, reordered projections converge, dropped projection recovers by synchronization |
| `Tests/StateMachine`, `Tests/ClientInteraction` | 17 grouped checks and client checks passed, unchanged |
| Strict state map/compare | State-source SHA-256 unchanged (`c039bb9f…`); no added or removed states or transitions |
| Unity 6000.6.6f1 CLI player build | Succeeded; only the existing deprecated `RequireOwnership` RPC warnings |
| Actual CLI players | Immediate and Draft two-player matches, eight-player deal/suspension/seven-seat replacement, and the presentation probe all passed; filtered logs identical to the earlier runs |

The isolated build's `Assets/Scripts` was compared against the working tree with `diff -rq` (no differences). Its hash is in `Tests/UnityIntegration/production-source-refactor.sha256`. That hash uses a stated method that does not reproduce the earlier `.sha256` files, so compare it only with itself. Relay delivery faults are simulated and use System.Text.Json, not production `JsonUtility`. They are not live-transport evidence.

## Remaining limits

Human-visible layout/motion quality, pointer/raycast usability, live Lobby/Relay, packet-loss recovery, seat reauthentication/reconnect, valid announcement-penalty delivery over real RPCs, deliberate joint-winner fixtures, and exhaustive malformed/unauthorized transport cases are not established by these smoke runs. Core and console assertions cover more cases but do not promote them to live transport evidence. Keep NET-001 and networking follow-up tasks open until their remaining criteria are met.

Settings GUI, final art, serializer migration, persistent recovery and detailed reconnect UX remain deferred. No gameplay decision depends on animation completion. No task is considered complete merely because its source compiles or its graph is present.
