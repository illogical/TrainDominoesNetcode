# Local player-state refactor and debugging

Updated: 2026-10-18. The eight selected interaction states and rules adapter are implemented; runtime verification remains in progress. See [gameplay integration](GAMEPLAY_INTEGRATION.md) for ownership/data flow, [verification](RULES_ENGINE_VERIFICATION.md) for outcomes, and [STATE-002](STATE_002_RUNTIME_BASELINE.md) for the dated legacy runtime characterization.

## Where to start debugging

Read `GameSession` for transport/lifecycle, `AuthoritativeMatchCoordinator` for serialized core mutation, `ClientSession` for recipient freshness/pending/selection, `PlayerStateCoordinator.Reconcile` for mode selection, and `GameplayManager`/`GameplayPresenter` for visual progress. The core is the only rule authority. A state handler ending or a mesh arriving does not mean a command was accepted.

Trace a request by its ID. `ClientSession.Submit` installs `Pending` and raises `RequestChanged` before sending, including on a synchronous host. `GameSession` marks it uncertain after five seconds without confirmation; Retry resends the same request. `ClientSession.Receive` settles a matching outcome even if its included view is old, while refusing to roll the current view backward. Terminal phase replacement or suspension retires obsolete requests. A matching accepted Ready need not change the revision.

`GameplayManager` drains visual batches separately. `ClientSession.PresentationGeneration` invalidates old completion callbacks after replacement, synchronization or suspension. The default ten-second per-batch watchdog cancels motion and snaps to the latest visible view. The queue also snaps if it reaches 32 pending batches. Board actions are masked until presentation catches up; Announce/Challenge can use the command slot during motion.

## Adding and entering a state

Keep constructor injection and balanced `EnterState`/`LeaveState` subscriptions. Constructors must not subscribe or perform entry work. New state types need no per-state registration field:

```csharp
ctx.SwitchState(c => new PlayerActingState(c));
```

The factory is invoked on first use and cached per context. An explicit switch to the current type still leaves and re-enters it. The coordinator avoids that switch for ordinary same-mode updates. Fresh round setup uses:

```csharp
ctx.SwitchState(c => new RoundInitializingState(c), reuse: false);
```

A fresh entry does not replace the cached instance. Transition reasons default to the calling method; use `reason:` for a more specific description. Context disposal leaves the active state once and clears cached references. `PlayerStateCoordinator.Dispose` removes shared client subscriptions before context disposal.

`ObservingStateBase` balances announcement/challenge handlers shared by Acting, Waiting and RoundInitializing. Pregame/RoundOver own Ready; Pending owns Retry; Suspended owns Resume/New Match/Synchronize. States do not subscribe to legacy winner/turn events or perform manager resets. Rule requirements belong in views, not new state subclasses.

## Logging and inspection

`GameSession.StateTracing` exposes Off, Transitions (default), and Verbose. It can be set by CLI harness code; Inspector access is optional. Diagnostics contain the last 128 enabled trace records, current local class, nested handler scopes, timestamps and lifecycle faults. `GameSession.PlayerStateContext` exposes that context. `TraceExecution()` scopes instrument concrete handlers; their completion proves only local synchronous execution.

Command logs include local client, request ID or settled status, and uncertainty. Inspect `ClientSession.View.Token`, `Pending`, `IsUncertain`, `Feedback`, `IsPresentationBusy`, `IsRoundPresented`, `PresentationGeneration` and `SelectedTile` alongside state diagnostics. Do not log private hand contents in shared reports. Animation progress does not belong in transition traces, and state tracing does not prove real RPC sender identity or authoritative legality.

`GameStateContext.Update` is not forwarded by GameSession; interaction is event-driven. Do not add state per-frame behavior without wiring and checking that lifecycle explicitly.

## Source-derived diagrams

Use the repository-local [player-state-workflow skill](../../.agents/skills/player-state-workflow/SKILL.md). The [generated flow](PlayerStateFlow/current.md) includes coordinator routes, guards and fresh entries. Regenerate after state changes and compare actual versus proposed maps. Source hashes establish inputs, not runtime correctness. The historical diagram in PLAYER_STATE_REVIEW is explicitly a before snapshot. Optional live graph/debugger windows remain deferred.

## Focused verification without opening the Unity GUI

Run from `RulesEngine` to enforce its SDK pin:

```sh
dotnet run --project ../Tests/StateMachine/StateMachine.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
dotnet run --project ../Tests/GameplayIntegration/GameplayIntegration.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
dotnet run --project ../Tests/ClientInteraction/ClientInteraction.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
```

The first links actual state/coordinator sources with narrow adapter doubles and checks lifecycle, routing, subscriptions and diagnostics. The second links actual boundary sources and core for authentication, token validation, privacy, duplicate handling and lifecycle. The third links the actual client session for correlation, monotonic freshness, uncertainty and generation behavior. These console checks do not exercise Netcode generated RPCs, scene references, real transforms, buttons or live services. Use [RulesEngine README](../../RulesEngine/README.md) for core tests and compile checks.

The CLI Unity harness documentation is `Tests/UnityIntegration/README.md`; use its isolated build/run workflow for actual Netcode host/client and scene checks. Batch-mode Unity performs import, code generation and player compilation without operating the GUI. Keep working scene development settings unchanged; `IgnoreDominoComparisons`, `StartAtRound` and `DominoStartCount` are legacy settings and no longer override core legality/dealing. The replacement profile must be verified from recipient/core outputs.

The [legacy baseline](STATE_002_RUNTIME_BASELINE.md) found a nested ServerRpc sender bug that prevented group submission completion. Its subsequent round/result scenarios remained blocked; do not carry forward implied passes. Record new CLI outcomes separately. GUI visual quality, human mouse interaction and live Lobby/Relay are unverified unless an explicit later report supplies evidence.
