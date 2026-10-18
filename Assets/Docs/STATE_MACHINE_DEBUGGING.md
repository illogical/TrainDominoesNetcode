# Local player-state refactor and debugging

Updated: 2026-10-17. This is a state infrastructure change, separately authorized from STATE-001's pending rules/app-policy review. It preserves the current legacy flow, including group play. It does not implement the Puremco rules-engine plan.

## Adding and entering a state

Keep constructor injection and put subscriptions in `EnterState`, with matching removals in `LeaveState`. A new state needs no field, registration, reflection, or constructor entry in `GameStateContext`:

```csharp
ctx.SwitchState(c => new PlayerMadeMoveState(c));
```

The generic factory is invoked on first use of that state type and cached per context. Switching to the current type still leaves and re-enters it. Constructors must not subscribe or perform entry work. `Name` defaults to the concrete type's name, fixing the former Pregame mislabel and removing a repeated override.

For the existing round/game restart paths, preserve fresh construction explicitly:

```csharp
ctx.SwitchState(c => new GameStartedState(c), reuse: false);
```

A fresh entry does not replace the cached instance. Transition reasons default to the calling method name; pass `reason:` when a more specific description is useful. Context disposal leaves the active state once and clears cached references. `GameSession.OnDestroy` disposes it before Netcode base cleanup.

## Logging while testing

Select the scene's `GameSession` component and change **State Trace Level** before or during Play Mode:

| Setting | Output |
| --- | --- |
| Off | No new state trace records or state Console output; gameplay still runs |
| Transitions (default) | Previous/next state, triggering method, and lifecycle failures |
| Verbose | Transitions plus entry/exit and instrumented handler start/end, including elapsed milliseconds |

Filter Unity Console by `PlayerState`. Session/entity ID, local client ID, and host/server/client role distinguish log producers; each record also includes a per-context sequence number, UTC timestamp, and frame. Clicking a log selects its `GameSession`. Existing unrelated game logs remain independent of this switch.

Instrument additional state handlers with a scope:

```csharp
private void InputManager_DrawButtonClicked(object sender, EventArgs e)
{
    using var trace = TraceExecution();
    // Existing handler work.
}
```

`GameSession.PlayerStateContext.CurrentState` exposes the current local class. `Diagnostics.History` returns a snapshot of the last 128 enabled trace records; older records are evicted. Verbose execution properties identify the innermost instrumented synchronous operation and restore the outer operation after nested transitions. Once the callback returns, no operation is executing. `End` means the scope finished or unwound; it does not prove success. Lifecycle exceptions add a Fault record and are rethrown; ordinary handler exceptions still use Unity's exception reporting.

Sending an RPC finishes the local handler without proving server acceptance. These traces do not track remote execution, outstanding commands, arbitrary source lines, animation completion, or authoritative legality. Use debugger breakpoints for exact executing lines. `GameStateContext.Update` remains unforwarded by `GameSession`; no previously dormant per-frame logic was activated.

## Diagram and further modularity

The [source-derived current flow](PlayerStateFlow/current.md) shows today's local transition wiring, including inherited exits, guards, self-transitions, and fresh restart entries. Its expanded tables include event reactions and source locations. Regenerate it after state changes; its source hash identifies the inputs. The existing diagram in `PLAYER_STATE_REVIEW.md` describes the proposed future rules-integrated flow, not today's execution.

Use the repository-local [player-state-workflow skill](../../.agents/skills/player-state-workflow/SKILL.md) for planning or changing states. It generates before/proposed-after diagrams without editing production C#, then compares actual source snapshots after implementation. Its Roslyn extractor and proposal tooling run outside Unity, with build caches in the system temporary directory. The static map does not establish runtime event targeting or rule correctness. TASKS.md still tracks the optional live Unity viewer and observed-transition graph at the lowest priority; no Unity UI was added.

Unity's installed Visual Scripting package supports [State Graphs](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.9/manual/vs-state-graphs-intro.html). This C# state machine is not a Visual Scripting graph; using those authoring/debugging tools would require integration or migration. A future observer window can instead consume the diagnostics already exposed. For timing work, [ProfilerMarker](https://docs.unity.com/en-us/engine/6000.6/script-reference/unity/profiling/profilermarker) provides custom Profiler samples; this refactor does not add profiler instrumentation.

Further behavior refactoring remains under STATE-001/ENG-006: derive input permissions from authoritative views, add one pending-action state for accepted/rejected commands, and keep double/draw obligations in engine data. Revisit a small input/request adapter to reduce direct manager access after those contracts settle. Preserve balanced shared and concrete subscriptions when changing `EndTurnStateBase`.

## Focused verification

Run with .NET 10, without Unity or NuGet dependencies:

```sh
dotnet run --project Tests/StateMachine/StateMachine.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
```

The harness compiles the actual state sources using C# 9, with narrow test doubles for Unity, GameSession, and managers. It is not the planned rules-core/NUnit harness and does not validate real RPC routing, Inspector behavior, rendering, services, or multiplayer correctness. Package feeds are disabled for this package-free check project; build outputs remain outside Assets and are ignored.

Checks cover startup/nested transitions and correct names; cache isolation/fresh entries/self re-entry; input subscriptions through placement/undo/waiting/restarts; retained group behavior; bounded ordered history/timestamps; nested execution scopes; handler tracing without transitions; lifecycle fault evidence; Off mode; disposal; and invalid-factory rejection without leaving the active state.

Before-implementation evidence: the harness failed compilation against the old abstract `Name` requirement. After implementation: all nine grouped checks passed. Full gameplay-script C# compilation is additionally checked against the installed Unity 6000.6.6f1 references using a temporary copy of Unity's compiler response file, with outputs redirected outside the project. This is compiler evidence, not a Unity import, Netcode postprocessor, player build, or Play Mode test. Existing warnings remain in legacy RPCs, serialization, and unrelated scripts.

Manual checks still required: use a local host and client to exercise join, draw, placement/undo, waiting, next round, and restart; inspect transitions separately per peer; switch all logging levels in the Inspector; destroy/reload the session and confirm no old handlers fire. Saved development settings remain `IgnoreDominoComparisons: 1`, `StartAtRound: 2`, `DominoStartCount: 12`; do not treat successful moves under matching bypass as rule validation. No live Lobby/Relay calls were made.

Code review: skipped (ce-code-review unavailable). That skill is not in the session catalog; a manual diff scan and the focused checks are the available review evidence.
