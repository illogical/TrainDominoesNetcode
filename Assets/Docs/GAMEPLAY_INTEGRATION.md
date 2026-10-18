# Gameplay integration developer guide

Updated: 2026-10-18. This describes implemented source, with CLI runtime verification recorded separately. The [rulebook](TRAIN_DOMINOES_RULEBOOK.md) owns behavior; the [rules-engine plan](RULES_ENGINE_IMPLEMENTATION_PLAN.md) owns the portable assembly boundary. This is the live integration of its U6 / ENG-006, not a second rule implementation.

## Follow the ownership chain

| Owner | Read here for | Must not own |
| --- | --- | --- |
| `GameSession` | Netcode spawn/disconnect lifecycle, authenticated sender metadata, JSON DTO transport, host-only match creation/resume | Tile legality, score calculations or animation completion |
| `AuthoritativeMatchCoordinator` | Server-only `MatchState`, serialized `Core.Apply`, per-seat request outcome cache, trusted `StartRound`/suspension, frozen `Participants` | Unity objects or client rendering |
| `SeatRegistry` | Frozen seats and the transport ID currently bound to each; rule state and caches key on seats, not transport IDs | Authentication or reconnect policy (NET-004) |
| `RecipientProjection` | The only conversion from host snapshot to client payload: recipient view plus audience-filtered events | Transport or command validation |
| `ClientSession` | Latest recipient view, one pending command, freshness, uncertainty, local selected tile, presentation generation/input mask | Shared hands or authoritative mutations |
| `PlayerStateCoordinator.Reconcile` | One routing policy from availability, pending request, authoritative phase and presented setup | Double/draw/scoring decisions |
| Concrete states / `ObservingStateBase` | Balanced local input handlers for the selected interaction responsibility | Manager resets or winner/turn-event interpretation |
| `InputManager` | Real action, train and challenge Buttons plus local tile raycasts; displayed permissions/status | Enforcement of rules |
| `GameplayManager` / `GameplayPresenter` | Ordered visual batches, terminal snap, cancellation, watchdog, view-to-mesh projection | Turn progression or readiness |
| Rules core | Puremco decisions, hands, trains, requirements, scores and lifecycle | Unity, Netcode, transport serialization, animation |

The former `DominoTracker`, `TurnManager`, `RoundManager` and `TurnStatus` rule owners are removed. Existing mesh/layout/sound helpers remain presentation services. `DevMode` scene references are preserved but cannot bypass core legality, choose a later engine or change core deal counts.

The state runner remains flat and factory-cached. The eight concrete modes are Pregame, RoundInitializing, PlayerAwaitingTurn, PlayerActing, PlayerActionPending, RoundOver, GameOver and PlayerSessionSuspended. `RoundInitializingState.cs` replaces the old Started responsibility; Acting replaces TurnStarted/MadeMove. No separate selecting, double-cover, draw-used or Draft-review class is required. Use the [state review](PLAYER_STATE_REVIEW.md) and [generated current map](PlayerStateFlow/current.md).

## Follow a placement

1. `InputManager.Update` raycasts a domino and emits `DominoClicked`. `PlayerActingState.TileClicked` calls `ClientSession.ClickTile`. Selecting a hand tile changes `SelectedTile` locally; `GameplayManager.SelectionChanged` asks `GameplayPresenter.Select` to animate it. No selection RPC exists.
2. A generated train Button emits `TrainChosen`; `PlayerActingState.TrainChosen` calls `ClientSession.ChooseTrain`. A legal local preview submits `CommandKind.Place` with an explicit domino ID and `TrainIdDto`. The server still revalidates it.
3. `ClientSession.Submit` captures the current token, creates a request ID, assigns `Pending`, and raises `RequestChanged` **before** calling the transport delegate. `PlayerStateCoordinator` enters Pending before an immediate host response can arrive.
4. `GameSession.SubmitCommandServerRpc` parses the bounded JSON request and supplies `rpc.Receive.SenderClientId` to `AuthoritativeMatchCoordinator.Submit`. The coordinator maps that authenticated identity to a seat through `SeatRegistry` and reconstructs the original submitted token. Under its lock, `Core.Apply` accepts or rejects the command.
5. Accepted results replace only the host snapshot. `RecipientProjection.Build` obtains each seat's `Core.GetPlayerView` and filters event audiences before DTO serialization. Only the sender receives its correlated outcome. `ProjectionClientRpc` calls `ClientSession.Receive`.
6. The client settles matching pending delivery independently of installing a newer view. `PresentationRequested` queues the captured before/after views. `GameplayPresenter.Present` compares visible placement identities and animates newly visible placements through `LayoutManager.PlaceOnTrackRoutine`; sealing a private Draft does not replay its actor's already-visible motion.
7. `PlayerStateCoordinator.Reconcile` chooses Acting, Waiting or a result from the latest view. `BoardEnabled` stays false until the final queued batch completes. `CompletePresentation` checks the generation; it never sends End Turn or changes core state.

Empty train destinations are explicit Buttons, so clicking a nonexistent endpoint is unnecessary. Train slots use fixed seat order plus the shared train, supporting nine logical trains at eight seats. Generated controls clone the serialized Draw Button as the style template, preserve existing scene references and replace its listeners. GUI visual quality remains unverified.

## Follow a draw or rejection

**Draw:** `PlayerActingState.DrawClicked` submits `CommandKind.Draw` through the same path. The core determines eligibility and the drawn tile. Recipient views reveal that tile only to its owner; opponents see updated public counts. The presenter compares old/new local hands, runs `AddNewDominoForPlayerRoutine` for a new private tile and rearranges the remaining hand. A return from a visible Draft uses `ReturnDominoToPlayerRoutine`. The next permitted draw/play/pass comes from the new core view, never from an entry-time button toggle.

**Rejection:** The coordinator returns the error and current recipient view without changing rules. `ClientSession.Receive` clears a matching request even if the supplied revision is unchanged or older than an already-received view. It retains the freshest view and displays the rule error. The coordinator routes from that view, rather than restoring the state that originally sent the command.

A five-second delivery wait marks uncertainty; it does not prove rejection. Retry sends the same pending object and ID. The host caches outcomes by participant and request ID within the match; identical replay returns the original outcome, while changed payload under the same identity is invalid. Synchronization resubmits the retained pending identity if present and then fetches the current projection. It can snap presentation without replaying missing history. A revision gap also invalidates incremental presentation, including when the same tile was moved between trains in the missing history. No animation rollback or new request ID is manufactured for an uncertain delivery.

## Follow a round transition

`RoundOverState.ReadyClicked` calls `ClientSession.Ready`, which submits a revision-stamped Ready when allowed. A no-op acceptance still settles its request. The results UI remains driven by the authoritative phase, including blocked rounds with no ending player.

When the final readiness command moves the core to AwaitingDeck, `AuthoritativeMatchCoordinator.TryStartRound` supplies a shuffled, validated deck using `Core.StartRound`. It publishes Ready and StartRound revisions independently and in order. Deck failure records `LastLifecycleError` rather than constructing a partial local deal. The resulting view includes the engine and dealt local hand; `RoundInitializingState` remains active until setup presentation finishes. Then the coordinator chooses Acting for the active seat and Waiting for other seats.

Final-match views render `Winners` together. A host New Match request calls `CreateMatchServerRpc(true)`, which requires a finished or suspended match and freezes a fresh 2–8-person roster. It creates a new authority/cache/match identity and fresh readiness. It does not remove seats or scores from the previous active match in place.

## Animation, suspension and privacy boundaries

`GameplayManager` captures immutable-use view references in FIFO batches and waits for aggregate layout routines. Concurrent independent transforms may complete within a batch; later batches wait. Selection operations are cancelled before conflicting authoritative placement motion. A batch taking over the serialized presentation budget (default ten seconds), or a queue reaching 32 entries, cancels and snaps to the latest recipient view so board input can recover.

Round/match replacement, synchronization and terminal results advance `PresentationGeneration`. Results/suspension cancel queued motion and snap; they need not wait for cosmetic completion. Old generation callbacks cannot release the current mask. `GameplayPresenter.Cancel` cancels selection and layout-owned routines; disposal removes client subscriptions and generated controls. No animation callback invokes a rules command.

Announcement/challenge handlers are shared by Acting, Waiting and RoundInitializing. They use the single command slot and bypass the board-animation mask, while ordinary placement/draw/pass/undo/end remains blocked. Eligibility and once-only effects stay in the core; a harmless unnecessary announcement can return feedback.

`GameSession.Disconnected` suspends authority when a frozen participant disappears. A local unavailable session cancels presentation and routes to Suspended. Resume is host-only and requires every original transport identity connected; synchronized usable views restore availability. Otherwise the host may start a fresh match with remaining connected seats. Detailed reconnect transport, seat reauthentication and recovery UX are deferred; merely reconnecting a transport is not a completed recovery implementation.

The server never serializes raw `MatchState`. Views include only the recipient's hand and permitted private Draft plus public board/counts/results. `RecipientProjection.EventsFor` assigns event source indexes before filtering audiences. JSON is the new gameplay DTO boundary; migration of unrelated legacy serialization is a separate concern. Never broadcast one player's projection to all clients.

## Verification and remaining limits

See [state debugging](STATE_MACHINE_DEBUGGING.md) for source-linked console commands and `Tests/UnityIntegration/README.md` for the isolated Unity CLI build/local-player harness. Use [RulesEngine README](../../RulesEngine/README.md) for core tests. `Tests/GameplayIntegration/LoopbackRelay.cs` connects real `ClientSession` instances to a real coordinator through an encode/decode step and can hold, drop, duplicate or reorder deliveries; use it for NET-001/NET-006 fixtures. It currently uses System.Text.Json, not production `JsonUtility` (see NET-002). Console stubs cannot validate Netcode generated RPCs, actual transforms, serialized scene references or button dispatch.

The [STATE-002 baseline](STATE_002_RUNTIME_BASELINE.md) records a real pre-integration CLI build and local host/client characterization. Its nested-RPC group-readiness failure blocked individual-turn/result/redeal checks; it is not a passing test of this replacement. Record current run outcomes in the [verification report](RULES_ENGINE_VERIFICATION.md), including precise scenario and settings.

The user requested CLI-only Unity work. Headless builds/local players can establish compilation, transport and instrumented animation invariants without opening the editor GUI. Human-visible motion/layout quality, human input usability and live Lobby/Relay services remain unverified. Settings GUI, final art, persistent recovery and detailed reconnect UX remain deferred.
