# STATE-002 legacy runtime baseline

Observed: 2026-10-18. Source: `0eec4e56dd974d84a02d2b33f7aa198484f180e8`, captured before integration edits. This is a legacy characterization, not the selected Puremco specification or a passing integration test.

## Environment and method

Built an isolated copy of Assets, Packages, and ProjectSettings with Unity `6000.6.6f1` into a macOS development player. Ran two actual players with GameScene, GameSession, spawned Netcode participants, and UnityTransport over `127.0.0.1:17897`. SimpleMatchmaking was disabled by temporary instrumentation, with no live Lobby/Relay/Authentication calls. The working scene and gameplay sources were unchanged.

Saved scene development settings were preserved and confirmed at runtime on both peers: `IgnoreDominoComparisons=True`, `StartAtRound=2`, `DominoStartCount=12`. Therefore placement acceptance does not establish legal pip matching or Puremco compliance.

The external harness invokes the existing RPC entrypoints, leaving transport serialization, sender metadata, manager mutations, local state events, meshes, and coroutines real. This was a headless run: it does not validate mouse/button dispatch, visible animation quality, or final transform coordinates. Fixed waits are not proof of aggregate animation completion.

Reproduction sources and filtered logs: [Tests/LegacyBaseline](../../Tests/LegacyBaseline/README.md). Full ephemeral logs remain `/tmp/td-legacy-build-elevated.log`, `/tmp/td-baseline-host.log`, and `/tmp/td-baseline-client.log`.

## Observations

| Scenario | Observed result | Interpretation |
| --- | --- | --- |
| Build | Unity batch build exited 0; player packaged successfully | Actual Unity/player compilation passes at captured baseline |
| Host/client startup | Both `start=True`, `connected=True`, two peers; both transition Pregame → Started → TurnStarted | Startup and local scene-object spawn work in this ordering |
| Engine/start state | Engine placement RPC dispatched by Started; both entered TurnStarted | No rendered-frame or exact engine-transform assertion |
| Initial deal | Host hand changed 0 → 12 after DrawDominoes RPC | Initial draw is explicit input, not automatic on startup |
| Repeated draw | Two immediate host draw submissions changed hand 12 → 14 | Server accepts repeated draw mutations; UI availability is not validation |
| Remote group submission | Client submits first; host logs `Player 0 is ready` | Remote identity is lost by the nested RPC path |
| Host group submission | Host then logs `Player 0 is already ready`; both remain TurnStarted/group phase | Blocking defect; group cannot complete in the tested normal path |
| Host selection/placement while still in group | Hand 14 → 13 after selection then engine RPC | Real group placement path reached under matching bypass |
| Host undo while still in group | Selecting placed tile restores hand 13 → 14 | Real group undo path reached |
| Subsequent draw/end attempt | Hand 14 → 15; End logs duplicate-ready again; group remains true | These are not verified individual-turn operations |
| Animation routines | Existing draw routines log `Intro animation complete` after repeated draws | Coroutines execute; no proof of sequencing, cancellation, or visual correctness |

The remote client's local DominoTracker reported hand zero after its draw. That collection is not the server's authoritative client hand in this legacy design; do not interpret the log as a failed delivery assertion.

## Blocking defect and integration implication

The observed host stack for duplicate readiness includes:

```text
GameSession.EndGroupTurnServerRpc
GameSession.__rpc_handler_3609967299
Unity.Netcode.NetworkBehaviour.__endSendServerRpc
GameSession.EndGroupTurnServerRpc
GameSession.EndTurnServerRpc
GameSession.__rpc_handler_77808528
```

`EndTurnServerRpc` invokes another `[ServerRpc]` instead of a plain server helper. The second dispatch reconstructs sender metadata from the server/host invocation, yielding client 0 for the remote player's group submission. Both real players then occupy one readiness identity. This interpretation combines the observed stack/ordering with source inspection; it is not a simulated stub result.

The planned replacement removes this legacy group flow and nested rule RPCs. Preserve authenticated sender identity at the new single mutation boundary and cover remote sender handling with a real transport check. This defect is evidence for that replacement, not a reason to preserve group rules or force the old readiness list to advance during verification.

## Untested and blocked scenarios

Normal individual-turn placement, individual undo/draw/end, round scoring, next-round readiness/redeal, and match completion were blocked by the group-submission defect. No private state was patched and no group transition forced to manufacture a pass. True button interaction, visible animation review, disconnect/resume, eight players, and live services were not tested.

The initial sandboxed build attempt failed because Unity Package Manager could not bind its local Unix socket (`listen EPERM`). After escalation, the same isolated project built and both local UDP peers connected; this environmental issue is distinct from the gameplay defect.

STATE-002 now has a reproducible partial baseline and a concrete blocking result. It must not be marked as fully passing; replacement verification must explicitly cover the downstream scenarios above.
