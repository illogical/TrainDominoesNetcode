# TrainDominoesNetcode: current project state

Inspection date: 2026-10-17. Source baseline: commit `852f223`.

The original baseline below is a static inspection of source, serialized scene settings, package manifests, and Git history. No Unity play session, build, automated gameplay tests, or live multiplayer services were run. Descriptions below explain code paths, not verified runtime correctness or an agreed rules specification.

Start with [TASKS.md](../../TASKS.md) for priorities and [AGENTS.md](../../AGENTS.md) for coding guidance.

**Design update after the initial inspection:** the user selected [Puremco rules](TRAIN_DOMINOES_RULEBOOK.md), superseding earlier recollections/preferences. The [state review](PLAYER_STATE_REVIEW.md) and [engine plan](RULES_ENGINE_IMPLEMENTATION_PLAN.md) now define the target: 2–8 players, one shared train plus personal trains, sequential turns from the beginning, and Puremco double follow-ups. Existing group-turn behavior described below is legacy implementation to refactor, not the selected rule. No gameplay was changed by this update.

State-infrastructure update (2026-10-17): constructor-injected lazy factories now replace per-state context fields; bounded diagnostics and Off/Transitions/Verbose logging are implemented. The source-linked state checks pass, and gameplay scripts compile against installed Unity references. No Unity Play Mode/host-client verification was performed. See [state debugging and verification](STATE_MACHINE_DEBUGGING.md). This does not close STATE-001 or implement its proposed behavior.

## Purpose and development context

The project is a multiplayer train-dominoes game using Unity and Netcode for GameObjects. It generates a double-twelve set (91 dominoes), places an engine, lets players build opening tracks during a shared group turn, and then advances through individual turns. It contains selection, matching/orientation, provisional moves, undo, station synchronization, round scores, and end-of-round/game UI.

At the initial inspection the rules variant was unsettled. The Puremco baseline is now selected; existing behavior remains the implementation baseline and must not silently become the specification. Review the first state matrix and the rulebook's explicitly identified software conventions before behavior changes.

The user is separately preparing four colored domino sets for four players. Integrating them is a late priority after rules and player-state work. This presentation requirement does not establish a four-player gameplay limit.

### Repository baseline

| Item | Saved configuration / observation |
| --- | --- |
| Unity editor | `6000.6.6f1`, from `ProjectSettings/ProjectVersion.txt` |
| Netcode for GameObjects | `2.13.3` |
| Input System | `1.20.0` |
| Lobby / Relay packages | `1.1.0` / `1.0.5` |
| Unity Test Framework | `1.8.0`; installed package alone does not establish project tests |
| Build scene | `Assets/Scenes/GameScene.unity` |
| Saved development settings | `IgnoreDominoComparisons: 1`, `StartAtRound: 2`, `DominoStartCount: 12` |
| Project tests / assembly boundaries | Focused package-free `Tests/StateMachine` checks link actual state sources with adapter stubs; no project-owned `.asmdef` or rules-core harness yet |

Package versions come from [manifest.json](../../Packages/manifest.json). The scene settings bypass matching checks in `GameplayManager.ServerCompareDominoToEngine` and `ServerCompareDominoToTrackDomino`. A successful move under these settings is not evidence of legal matching. `StartAtRound` feeds both engine indexing and round initialization; their exact alignment needs characterization rather than assuming the label proves the expected engine.

### Where work appears to have stopped

Git history supports the user's recollection of state work, but shows subsequent presentation work too:

| Commit | Date | Evidence |
| --- | --- | --- |
| `de075a4` | 2023-07-31 | Split out `PlayerMadeMoveState` |
| `0437813` | 2023-07-31 | Introduced shared `EndTurnStateBase` |
| `e280abd`, `402a1c7` | 2023-08-02 / 2023-08-06 | Turn reversal and reversal WIP |
| `ff8c993` | 2023-08-09 | Group-turn track domino removal |
| `fb88498`, `2101c94`, `b2d81e6` | 2023-08-23–27 | Track labels, client fixes, and label timing |
| `a1516a2`, `680e2de` | 2023-08-29–30 | Scroll input and track-scrolling preparation |
| `10c8452`, `852f223` | 2026-10-17 | Relay DTLS endpoint initialization and Unity 6 upgrade |

This indicates an unfinished turn/state refactor followed by presentation work, then a recent environment/networking refresh. Commit descriptions are context, not proof that the corresponding behavior works today.

## Architecture and authority

| Area | Current responsibility | Boundary to preserve or improve |
| --- | --- | --- |
| [GameSession](../Scripts/GameSession.cs) | `NetworkBehaviour`; RPC entry points for draw, selection, placement, undo, turn completion, readiness, and scoring synchronization | Intended server authority, but validation is incomplete and rule logic is embedded in transport handlers |
| [GameplayManager](../Scripts/Game/GameplayManager.cs) | Creates managers, bridges gameplay events, calls comparisons, and coordinates meshes, sound, layout, and UI | Mixes rule-related operations and presentation |
| [DominoTracker](../Scripts/Game/DominoTracker.cs) | Domino catalog, boneyard, hands, selection, engine, main station, per-player provisional stations, matching, undo data, and pip totals | Principal extraction candidate; currently uses Unity randomness and mutable shared data |
| [TurnManager](../Scripts/Game/TurnManager.cs) / [TurnStatus](../Scripts/Models/TurnStatus.cs) | Group/individual phase, player order, turn flags, last played domino, winner IDs | Turn flags are not a complete rules model; no draw-used flag exists |
| [RoundManager](../Scripts/Game/RoundManager.cs) / models | Round records, cumulative totals, lowest-score winners; station and track data | Current round limit is 12; selected Puremco target is 13 engines, twelve through blank |
| [Player states](../Scripts/Game/States) | Local subscriptions, available input, RPC requests, and UI transitions | These are local interaction states, not a complete authoritative world-state machine |
| Presentation | `InputManager`, `MeshManager`, `LayoutManager`, UI scripts, helpers, and ScriptableObject animation/sound settings | Keep visuals and animation timing outside the future rules core |
| Networking setup | `MyNetworkManager`, `DominoPlayer`, `NetworkManagerUI`, `SimpleMatchmaking`, `RelayTester` | Several entry paths exist; saved scene enables `SimpleMatchmaking` and disables the `RelayTester` component |

The main station represents committed tracks. Per-player turn stations hold provisional arrangements; RPCs update the acting player's presentation, then turn completion merges and broadcasts station data. During opening group play, multiple players build provisional tracks before the merge. Hands are mutated while moves are provisional, so extraction must characterize both commit and undo behavior.

`NetworkSerializer<T>` uses `BinaryFormatter` for several DTO payloads. `JsonContainer` separately serializes score dictionaries as JSON through Netcode. Serializer replacement belongs in a separate task with payload compatibility tests, not an incidental rules refactor.

## Player-state inventory

“Cached on first use” below means reached through a typed factory and retained by `GameStateContext`; no per-state registration list is required. Expected conditions describe what callers appear to assume; the states do not independently enforce all of them.

All three turn states inherit these `EndTurnStateBase` transitions: `AwaitTurn` or `PlayerTurnEnded` → `PlayerAwaitingTurnState`; `PlayerHasWonRound` → `RoundOverState`; `PlayerHasWonGame` → `GameOverState`. Each removes its subscriptions on exit. These shared transitions also allow an awaiting player to receive round/game completion.

| Concrete state | Entry behavior and available actions | Events / exits | Expected conditions and gaps |
| --- | --- | --- | --- |
| `PregameState` — cached on first use | Subscribes to `GameSession.OnPlayerJoined`; does not explicitly set gameplay button availability | Join event calls `PlayerJoinedServerRpc`, then switches to `GameStartedState` | Assumes network spawn arrives after subscription. No all-player readiness gate; join RPC is effectively a stub. Name now comes from the concrete type, correctly reporting Pregame |
| `GameStartedState` — cached on first use, declared in `GameStartState.cs` | Requests engine placement via `PlaceEngineServerRpc`, immediately enters `PlayerTurnStartedState` | Immediate transition; does not wait for placement acknowledgment | Assumes session/managers/engine are ready. Initial dealing remains manual through Draw |
| `PlayerTurnStartedState` — cached on first use | Enables Draw, disables End Turn; subscribes to domino clicks, Draw, End Turn, `PlayerAddedDomino`, and `PlayerTurnStarted` | Draw sends RPC and toggles buttons immediately; `PlayerAddedDomino` → `PlayerMadeMoveState` only outside group play; `PlayerTurnStarted` re-enters itself; shared exits above | Used both for opening group play and the active individual player. No distinct selected/drawn/pending phase. Group placements remain here. Button state is not authoritative validation |
| `PlayerMadeMoveState` — cached on first use | Disables Draw, enables End Turn; forwards domino clicks to server; subscribes to undo and End Turn | `PlayerReversedMove` → `PlayerTurnStartedState`; End Turn requests RPC and disables its button; shared exits above | Assumes an individual-turn provisional move. Server identifies the undoable domino. Undo re-entry enables Draw again without a preserved draw-used flag |
| `PlayerAwaitingTurnState` — cached on first use | Disables Draw and End Turn; listens for turn start; no domino-click handler of its own | `PlayerTurnStarted` → `PlayerTurnStartedState`; shared exits above | Can mean waiting for others to finish group play or waiting for the next individual turn. Correct event targeting depends on RPC routing |
| `RoundOverState` — cached on first use | Reads winner/scores and requests round-over UI; listens for Ready and all-ready events | Ready sends `PlayerReadyForNextRoundServerRpc`; all-ready resets local tracker/turn data and meshes, then constructs a new `GameStartedState` | Assumes scores/winner synchronized. Returns without showing UI if winner missing. No turn-base subscriptions or input reset of its own; relies on round UI and server coordination |
| `GameOverState` — cached on first use | Reads winner/scores, enables restart-ready button, shows game-over UI; subscribes to New Game | New Game constructs a new `GameStartedState` | Shared restart/reset/readiness protocol remains unfinished. Restart-ready emits `ReadyButtonClicked` through input, but this state only subscribes to New Game. No draw/end-button reset of its own |
| `GameJoinedState` — unregistered placeholder | Empty entry/exit implementations, with a comment proposing button changes | No event subscriptions or transitions | Not constructed by the context; no active flow role found |

### Shared infrastructure

- `GameStateBase` derives `Name` from the concrete type and defines entry/exit, an optional update hook, and a handler-trace scope.
- `GameStateContext` enters Pregame, creates further state types on demand, and switches by leaving the old state before entering the new one. Repeated typed factories reuse a per-context instance; same-state transitions still leave/re-enter. Round/game restarts explicitly request a fresh uncached started-state instance with `reuse: false`. Disposal removes active subscriptions; GameSession disposes the context on destruction. Diagnostics retain the last 128 enabled records with optional handler/lifecycle scopes.
- `EndTurnStateBase` centralizes waiting and winner-event handling; it is abstract, not a playable phase.
- `GameStateContext.Update()` exists, but no forwarding call was found in `GameSession`. The current concrete states are event-driven; verify update wiring before relying on future per-frame state logic.

### Opening group-turn flow

1. `GameSession.Start` constructs the context; `OnNetworkSpawn` raises the join event. Pregame handles that event and advances to Started. Startup ordering still needs runtime verification.
2. Started requests engine display and immediately enters Turn Started. Draw deals `DevMode.DominoStartCount` dominoes when the player's hand is empty; otherwise the same RPC draws one domino. It does not use an explicit initial-deal phase.
3. Players select a hand domino, then the engine or a track. Server handlers check some matching/ownership conditions and mutate provisional stations and hands. `PlayerAddedDomino` deliberately leaves the client in Turn Started during group play, allowing continued construction.
4. With no selected domino, clicking a provisional track's end can request group-turn undo. Normal-turn undo instead identifies the recorded played domino. Validation and ownership of undo need review before treating these paths as safe for arbitrary requests.
5. End Turn adds the player to the readiness list and `TurnManager`. Until all registered players finish, there is no immediate per-player waiting transition in this group completion handler; disabling End Turn does not fully lock other interactions.
6. When all are ready, the server merges provisional tracks, broadcasts station updates, completes the group phase, and checks for an empty-hand winner. Otherwise it starts the first individual player and sends others to Awaiting Turn.

On initial registration, turn order follows group-turn completion order because `AddPlayer` runs there. `ResetForNextRound` retains the player-order list, so later rounds do not simply reconstruct that order from scratch.

### Ordinary-turn and round flow

1. A targeted start event enters Turn Started. Selection can toggle or replace the selected domino; clicking the engine may start a track, and clicking a track may append to it.
2. A successful placement raises `PlayerAddedDomino` and enters Made Move. The server restricts normal turns using current-player and made-move checks. Clicking the recorded domino can undo; the local state returns to Turn Started.
3. Drawing toggles local buttons but stays in Turn Started. The server does not yet track whether drawing has already occurred this turn. Passing and draw eligibility therefore need explicit rules and server validation.
4. End Turn checks the current player, commits station changes, checks for an empty hand, and otherwise broadcasts updates, ends the old player's turn, increments turn order, and starts the next player. Pending/rejected RPCs have no dedicated state or general recovery flow.
5. A round ends when a hand-empty condition is detected. Scores sum remaining pips. RoundManager accumulates scores; the lowest cumulative total wins the game, but the RPC currently chooses the first returned winner when totals tie.
6. At non-final round completion, all-ready triggers server reset/new-engine/new-round work and client reset/re-entry. Host and client share parts of this path; reset sequencing, round counts, and scoring must be tested for duplication or divergence. New Game currently re-enters Started without an equivalent complete shared-game reset protocol.

## Gaps and candidate phases for review

This initial assessment is retained as inspection context, not an approved list of classes. The later [Puremco state mapping](PLAYER_STATE_REVIEW.md) supersedes these open design questions; in particular it removes group-opening construction from the target profile and puts turn obligations in engine data.

| Candidate phase / concern | Existing coverage | Decision needed before implementation |
| --- | --- | --- |
| Pregame readiness and dealing | Pregame placeholder plus manual Draw | Roster limits, start gate, deal counts, joins/leaves, startup ordering |
| Opening group play and submission | Shared Turn Started plus provisional stations and readiness list | Legal opening construction, undo scope, final submission lock, merge conflicts and station capacity |
| Selecting / drawing / passing | Selection data and buttons in Turn Started | Legal draw conditions, number of draws, drawn-piece play, empty boneyard, pass/blocked-round rules |
| Doubles and follow-up obligations | Doubles exist as domino values; no dedicated obligation handling found | Which double rules apply and whether obligations span players or turns |
| Provisional move / commit pending | Made Move, mutable hand/station, RPCs | Undo boundaries, command rejection, repeated clicks, atomic commits, animation versus rule completion |
| Train accessibility | Track owner and `HasTrain`; ownership checks | When markers open/close a train, public/shared tracks, first-track restrictions |
| Round/game completion and restart | Round Over and Game Over UI paths | Ties, engine/round schedule, readiness deduplication, full reset, disconnect/restart coordination |

Other concrete review targets: unguarded empty-boneyard indexing; invalid IDs/null selections or tracks; eight-track capacity during concurrent group submissions; stale commands and duplicate readiness; reset behavior retaining some player/winner fields. These are static risk findings, not reproduced failures.

## Recommended rules separation

Keep the rules in source and let Unity build a dedicated assembly through an `.asmdef`, rather than maintaining a manually copied DLL. Use the same pure C# source files from a standalone test project so most rules tests can run without launching Unity. This is an architecture recommendation; no assembly or test project has been added.

The core should own domino values/IDs, hands and boneyard, legal placement and orientation, train accessibility, turn obligations, undo/commit rules, round progression, and scoring. It should accept commands and expose outcomes/updated game data without referencing Unity, Netcode, transforms, UI, or animations. Randomness should be supplied through a controllable shuffle/draw boundary so tests can reproduce deals.

`GameSession` should remain the authoritative network adapter: establish the acting player, ask the rules core to validate/apply a command, and synchronize accepted results or rejection feedback. Local player states should request actions and reflect results; buttons must not be the sole enforcement of rules.

The [engine plan](RULES_ENGINE_IMPLEMENTATION_PLAN.md) now selects .NET Standard 2.1, supported C# 9 constructs, external .NET 10/NUnit tests, and shared source integration based on inspected compatibility settings. Validate both Unity and external compilation during scaffolding, and characterize relevant behavior before replacement. Preserve existing IDs and compatible payloads; explicitly plan needed view/result additions. No public APIs or types were changed by this documentation work.

## Next session

Start with **STATE-001** in [TASKS.md](../../TASKS.md). Review the saved Puremco rulebook/matrix and application-policy proposals before adding states. Characterize current behavior separately from desired behavior; do not lock unfinished prototype behavior into rule tests merely because it exists today.

### Inspection sources

- [State implementations](../Scripts/Game/States), [GameSession](../Scripts/GameSession.cs), [GameplayManager](../Scripts/Game/GameplayManager.cs).
- [DominoTracker](../Scripts/Game/DominoTracker.cs), [TurnManager](../Scripts/Game/TurnManager.cs), [RoundManager](../Scripts/Game/RoundManager.cs), [models](../Scripts/Models).
- [InputManager](../Scripts/Game/InputManager.cs), [MeshManager](../Scripts/Game/MeshManager.cs), [LayoutManager](../Scripts/Game/LayoutManager.cs), and UI scripts.
- [Scene](../Scenes/GameScene.unity), [build settings](../../ProjectSettings/EditorBuildSettings.asset), [editor version](../../ProjectSettings/ProjectVersion.txt), [packages](../../Packages/manifest.json).
- Local Git history and tracked-file inventory. Recheck these findings after behavior changes; this document is a dated baseline.
