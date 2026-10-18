---
name: player-state-workflow
description: Map, plan, add, change, or remove TrainDominoesNetcode player interaction states. Generate source-derived Mermaid diagrams and before/after change previews, including shared handlers and test impacts. Use for Assets/Scripts/Game/States and its transition wiring, not unrelated Unity components or standalone authoritative rule calculations.
---

# Player-state workflow

Give the developer a current-flow picture and a reviewable explanation of each proposed state change before changing gameplay code. Keep the state implementation as the source of truth; do not maintain a second state-registration list.

## Ground the change

- Follow the repository's `AGENTS.md`. Read relevant portions of `Assets/Docs/PROJECT_STATE.md`, `PLAYER_STATE_REVIEW.md`, the Puremco rulebook's decision register, and `TASKS.md`. Consult `RULES_ENGINE_IMPLEMENTATION_PLAN.md` when changing authority or asynchronous requests. Proposed app policies are not approved merely because Puremco was selected.
- Recheck actual states, their base classes, and affected emitters in `GameSession`, `GameplayManager`, and `InputManager`. This tool inventories state-side subscriptions; it does not verify event targeting, network startup order, or server validation.
- Generate the before-map from the working tree, including relevant uncommitted changes. Preserve the snapshot before edits. Do not substitute the future-policy diagram for the current implementation. Use `--ref` only for an explicitly labeled committed baseline.

Run from the repository root (Python 3 and the .NET 10 SDK are required; no Unity or NuGet package downloads):

```sh
python3 .agents/skills/player-state-workflow/scripts/state_map.py map --repo . --output /tmp/player-state-before --strict
```

Read `current.md`, `current.json`, and warnings. The graph groups matching inherited handlers, while the table expands concrete source states and includes guards, self-transitions, fresh-instance requests, and source locations. The action inventory includes events that send requests without directly switching state. Disconnected states remain visible.

## Show the proposal before implementation

For a proposed addition/change/removal, read [references/preview-format.md](references/preview-format.md). Describe the proposed edge/node edits in a temporary JSON overlay; do not modify production C# merely to obtain a speculative picture.

```sh
python3 .agents/skills/player-state-workflow/scripts/state_map.py preview --repo . --proposal /tmp/player-state-proposal.json --output /tmp/player-state-preview --strict
```

Show **Before** and **Proposed after** Mermaid diagrams in the conversation, plus the small list of changed transitions. Link the artifacts for the expanded tables. For a large graph, focus the conversational view on affected states and common exits, retaining the complete generated map in the artifact. Label any manually simplified view and preserve its guards/shared exits. A no-topology-change refactor can show the current diagram once and state that the proposed graph is identical.

Explain the consequences that matter for the suggested change:

- What each affected state means, which inputs/buttons change, which actions only request work, and which confirmed event actually advances the state.
- Shared-base entry/exit behavior, terminal round/game exits, self re-entry, cached versus fresh instances, and undo/reset/disposal behavior. If inheriting a base, explicitly decide whether its lifecycle handlers should run.
- Server authority, pending acceptance/rejection, repeated/stale callbacks, and privacy where affected; do not invent an acknowledgment or rule contract the project lacks.
- Tests to add/change and any difference between legacy characterization and intended Puremco behavior. Mark unresolved policies as questions, not implemented branches.

The preview is a communication requirement, not a new permission gate. When implementation is already authorized, show it in commentary and proceed within that scope. When the request is exploratory, stop at the proposal; resolve genuinely open behavior choices before implementation. Do not use this skill to authorize rules changes, network/service actions, or unrelated cleanup.

## Implement and verify when authorized

- Preserve constructor-injected factories: `ctx.SwitchState(c => new SomeState(c))`. Constructors have no subscription/entry side effects. Current self-transitions leave/re-enter; current restart paths use `reuse: false`. Preserve those semantics unless the reviewed change explicitly replaces them.
- Keep event subscriptions balanced through shared and concrete entry/exit. Remove all incoming/outgoing uses when deleting a state and check indirect helpers, external callers, serialized references, and existing metadata. Do not rely on syntax extraction to prove a state unused.
- Keep rule decisions out of local interaction classes. Add a class for a distinct interaction responsibility, not every double/draw/selection condition. Reuse the existing `TraceExecution()` scopes; preserve current-state and diagnostic visibility.
- Add focused behavior tests for changed transitions, input/request effects, and cleanup. The current package-free regression checks use real state sources with adapter stubs, not production Netcode. Do not claim that they establish rule correctness or multiplayer convergence.

```sh
dotnet run --project Tests/StateMachine/StateMachine.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
python3 .agents/skills/player-state-workflow/scripts/state_map.py map --repo . --output /tmp/player-state-after --strict
python3 .agents/skills/player-state-workflow/scripts/state_map.py compare --before /tmp/player-state-before/current.json --after /tmp/player-state-after/current.json --output /tmp/player-state-actual-diff --strict
```

Compare the actual result to the proposed changes. Explain deviations and update the overview/state matrix when behavior changes. Refresh the checked-in current-flow artifact after source changes:

```sh
python3 .agents/skills/player-state-workflow/scripts/state_map.py map --repo . --output Assets/Docs/PlayerStateFlow --strict
```

Report actual tests/compilation separately from Unity Play Mode and host/client checks. Do not mark STATE-001 or later gameplay tasks complete based on a graph or source checks. Leave the optional Unity debugger UI at its separately tracked priority.

## Tool boundaries and maintenance

Roslyn parses syntax without loading Unity or executing state classes. Named direct lifecycle subscriptions, local helper calls, simple if/else/early-return guards, and direct factory expressions are supported. Unsupported paths emit warnings; `--strict` returns 2 after writing review artifacts. Treat warnings as incomplete extraction and manually trace those paths or improve the extractor; never suppress them to claim an exhaustive map. Exit 1 means an invalid proposal, input, or command failure.

Sources are limited to `Assets/Scripts/Game/States`. Conditional compilation, lambdas, overloads, complex control flow, and external transitions need particular care. See the generated coverage section and [references/preview-format.md](references/preview-format.md) for proposal limitations. A source hash identifies the exact input; the graph is not a live-state view, permission validator, or state-machine execution engine.

When changing tooling, run its behavioral checks:

```sh
python3 -B -m unittest discover -s .agents/skills/player-state-workflow/scripts -p 'test_*.py' -v
```
