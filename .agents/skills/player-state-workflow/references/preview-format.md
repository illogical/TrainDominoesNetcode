# Planning overlays and comparisons

Use this when proposing a state change without editing production C#. Generate/read the current map first, and copy exact state/event identifiers from `current.json`. Run the preview against the same source snapshot; if source changes, regenerate the proposal's before-map.

## Overlay shape

The JSON fields below are optional except `title`. Unknown keys fail rather than silently omitting edits:

```json
{
  "title": "Illustrative request-processing state; not an approved gameplay change",
  "impacts": [
    "Decide which inputs are disabled while this illustrative request is pending.",
    "Specify the actual acceptance/rejection events before implementing.",
    "Inherit the four EndTurnStateBase exits by calling its entry/exit lifecycle."
  ],
  "add_states": [
    {
      "name": "ExampleProcessingState",
      "base": "EndTurnStateBase",
      "inheritFrom": "EndTurnStateBase"
    }
  ],
  "remove_states": [],
  "remove_transitions": [],
  "add_transitions": [
    {
      "source": "PlayerTurnStartedState",
      "event": "ctx.GameplayManager.InputManager.DrawButtonClicked",
      "target": "ExampleProcessingState"
    },
    {
      "source": "ExampleProcessingState",
      "event": "IllustrativeAcceptedEvent",
      "target": "PlayerTurnStartedState",
      "guard": "accepted and additional interaction required"
    }
  ]
}
```

This example demonstrates the format, not an agreed design or existing acknowledgment API. The overlay adds graph edges only; it does not implement button changes, command suppression, server validation, subscriptions, or network messages. Record those responsibilities and tests in `impacts` and the accompanying explanation.

- `add_states`: `name`, optional `base` (default `GameStateBase`), optional `inheritFrom`. Names must be C#-style identifiers. New state nodes are concrete.
- `inheritFrom`: explicitly asserts that the new state's lifecycle will invoke the named base lifecycle. It must equal `base`. Copies known shared transition/event-reaction templates whose defining owner is that base, expanding shared exits for review. Verify them against the actual base; this does not infer overrides, transitive contracts, or new base behavior. Omitting it for another base produces a review warning, rather than silently assuming inherited subscriptions.
- `remove_states`: exact names. Explicitly remove or redirect every incident transition as well; dangling endpoints fail. Node removal also removes its event-reaction records.
- `remove_transitions`: exact selectors with `source`, `event`, and `target`. Add `guard`, `reuse`, `handler`, or `owner` to disambiguate. A selector must match exactly one edge. Read these values from the snapshot.
- `add_transitions`: `source`, `event`, `target`; optional `guard`, `reuse` (boolean, default true), `handler`, `owner`. Destination/source states must exist. Use `source: "[*]"` only for initialization. Changing a transition means removing its old edge and adding its replacement. A new event label is a proposed contract, not evidence that the event exists.
- `impacts`: design notes that remain visibly unverified. Include allowed-action changes and verification needs; the map itself does not decide them.

Outputs are `before.md/json`, `after.md/json`, and `preview.md`. The combined preview includes both diagrams and the added/removed transition tables. Proposed snapshots retain the before-source hash and are labeled `proposed`; they must never replace the checked-in source map.

## Compare actual changes

Capture `map` output before and after implementation, then use `compare`. Transition comparisons include source, event, target, guard, reuse, handler, and defining owner; source-file line movement alone is not a behavior change. Base/abstract changes and event-reaction/action changes are also reported. This is a static diff, not proof of equivalent runtime behavior.

For a committed baseline:

```sh
python3 .agents/skills/player-state-workflow/scripts/state_map.py map --repo . --ref HEAD --output /tmp/player-state-head --strict
```

`--ref` uses read-only Git commands and includes only committed files. The default reads actual working files, including relevant uncommitted state changes. Neither mode modifies sources. Do not discard user edits to obtain a baseline.

## Coverage limits

The analyzer resolves state classes within the states directory by simple name and follows named subscriptions directly in effective `EnterState`/`LeaveState`, inherited lifecycle calls, local helper invocations, and supported control flow. Source guards are preserved textually, never evaluated. Graph edges describe possible call-site paths; callbacks may be stale, conditionally subscribed, or targeted incorrectly at runtime.

The coverage warnings identify recognized gaps such as dynamic factories, inline callbacks, overloads, unbalanced event removal, conditional compilation, async methods, unsupported control flow, and unmatched transition call sites. The analyzer is deliberately not a whole-program semantic/control-flow analyzer. Changes to startup, external callers, event emission, virtual dispatch, dependency injection, or complex/deferred execution require manual review even when no warnings appear. Partial classes, new syntax patterns, and indirect event wrappers may require extending the extractor.

Mermaid arrows from an inheritance group summarize identical known shared edges for every member of that group. The full transition table retains the individual concrete sources. An isolated node means no static route from the recognized initialization path, not proof that nothing external can instantiate it.

## Lifetime coordinator routing

`PlayerStateCoordinator` is recognized by name as an orchestration entrypoint, not a state node. Its `Start()` body and named event handlers subscribed directly in `Start()` are followed through local helpers. Matching removals must exist in `Dispose()`. Public `Reconcile()` is also inventoried with an explicit caller-wiring-unverified label because external invocations are outside the source boundary. Simple if/else and early-return guards remain visible on each route.

Coordinator routes expand across every concrete state as possible shared routing; the enclosing Mermaid group summarizes identical routes while the table retains each source. This deliberately does not infer current-state reachability or evaluate guards. Keep state selection in direct `ctx.SwitchState(c => new State(c), ...)` calls so the extractor can retain factory and reuse semantics. Coordinator code may suppress same-mode switches; static self-arrows alone do not establish runtime re-entry.

Every `SwitchState` call in all classes within the source boundary is checked for a recognized path. Unrecognized router classes or deferred/unsupported call sites warn rather than silently omitting their transitions. Files outside `Assets/Scripts/Game/States` and external callers/event targeting still need manual review.
