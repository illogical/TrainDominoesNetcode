# Networking entry paths

**Reviewed:** 2026-10-18 for NET-003, against Unity 6000.6.6f1, Netcode for GameObjects 2.13.3, built-in Unity Transport 6.6.0 and Multiplayer Services 2.3.3 (which replaced the deprecated standalone Lobby 1.1.0 and Relay 1.0.5 packages, with the user's approval). Gameplay traffic is the same on every path: `GameSession` RPCs carrying `WireCodec` payloads ([wire contract](GAMEPLAY_INTEGRATION.md#wire-contract-net-002)).

## Supported paths

### 1. Online: Lobby + Relay (`SimpleMatchmaking`), the scene default

This is the path the game ships with. The `SimpleMatchmaking` component on the `NetworkManager` object is enabled, and the transport's saved protocol is Relay.

1. The player presses **Start**. The button disables itself so a second click cannot start a second attempt.
2. Unity Services initializes and signs in anonymously. A `--ugs-profile <name>` launch argument gives an instance its own anonymous identity, which is needed when two copies run on one machine.
3. The game quick-joins an open lobby. If none exists (`NoOpenLobbies`), it creates one for 8 players with a Relay allocation for 7 connections, and stores the join code in the lobby data.
4. The transport is configured with `allocation.ToRelayServerData("dtls")` (encrypted Relay), then the game calls `StartHost` or `StartClient`.
5. The host sends a lobby heartbeat every 15 seconds and deletes the lobby when it shuts down; a client leaves it.
6. Any failure is logged and the Start button is enabled again for a retry.

**Setup:**
- The project must be linked to a Unity Cloud project. `ProjectSettings` has `cloudProjectId`, project `MultiplayerTrainDominoes`.
- Lobby and Relay must be enabled for that project in the Unity Cloud dashboard. Both have a free tier.
- An internet connection is required.

**Host-only control:** only the lobby creator (the host) can start a match; that rule is enforced in `GameSession.CreateMatchServerRpc`.

### 2. Direct connection (UnityTransport address and port)

`UnityTransport.SetConnectionData(address, port)` switches the transport to direct mode, followed by `StartHost` or `StartClient`. The CLI harness uses this on `127.0.0.1`. It needs no services and works for LAN play. The scene's saved direct address is `127.0.0.1:7777`.

## Developer tools and legacy paths

| Path | State in the scene | Disposition |
| --- | --- | --- |
| `NetworkManagerUI` (Host / Client / Server buttons) | GameObject inactive | **Developer tool** for direct-connection testing. Host and Client work. **Server is unsupported:** a dedicated server is not a player, and `GameSession` requires the host to be a seated player to create matches. |
| `RelayTester` (Quantum Console `CreateRelay` / `JoinRelay`) | Component disabled | **Developer tool** for manual Relay tests with a join code. It uses DTLS, like `SimpleMatchmaking`. |
| `MyNetworkManager.Players` / `LocalPlayer` and `DominoPlayer` | Active (the NetworkManager subclass) | **Legacy and unused:** gameplay identity comes from `SeatRegistry`. They are kept because removing the NetworkManager subclass would mean editing the scene. Do not build new features on them. |
| ParrelSync profile hook (commented out) | Removed | Replaced by `--ugs-profile`. |

## Failure and shutdown behavior

| Event | Behavior |
| --- | --- |
| A seated client disconnects | The host suspends the match and records the seat as missing. Every peer sees "waiting for Player N to reconnect". The disconnected player sees "Disconnected · press Retry". Retry reconnects (Relay rejoin through `SimpleMatchmaking`, otherwise the saved address). The new connection then presents its seat secret (`ReclaimSeatServerRpc`), and only an accepted claim receives a projection. Once every seat is back, the host's Resume is enabled. The host can also start a fresh match with the players present. |
| The host disconnects or quits | Clients receive their own disconnect callback; `ClientSession` becomes unavailable and presentation is cancelled. Host migration is out of scope. The host's lobby is deleted on shutdown on a best-effort basis; a lobby without heartbeats also lapses on the service side. |
| Matchmaking or sign-in fails | The error is logged and the Start button becomes available again. A Relay join failure after joining a lobby leaves that lobby before reporting. |
| A payload cannot be decoded or encoded | See the [wire contract](GAMEPLAY_INTEGRATION.md#wire-contract-net-002): the failure is reported, the client resynchronizes (bounded), and nothing is silently dropped. |
| A Relay allocation expires during a long idle period | **Not handled:** the transport disconnects, which then behaves like a disconnect. |

## Changes made in NET-003 / MAINT-003

- **Packages:** Lobby 1.1.0 and Relay 1.0.5 were replaced by Multiplayer Services 2.3.3. `Lobbies.Instance` became `LobbyService.Instance`.
- **`SimpleMatchmaking`:**
  - DTLS Relay through `ToRelayServerData`, replacing unencrypted UDP `SetHostRelayData`.
  - An 8-player lobby and 7-connection allocation, where it was 100.
  - Awaited and logged service calls: the heartbeat and lobby leave were previously fire-and-forget.
  - Only "no open lobbies" falls through to creating a lobby; previously every error did.
  - Protection against a second click, a retry path, and `--ugs-profile`.
- **`GameSession`:**
  - The four obsolete `[ServerRpc(RequireOwnership = false)]` RPCs became `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]` with `RpcParams`.
  - The `[ClientRpc]` + `ClientRpcParams` targeting became `[Rpc(SendTo.SpecifiedInParams)]` with `RpcTarget.Single(id, RpcTargetUse.Temp)`.
  - The sender identity is still `rpc.Receive.SenderClientId`.
- **API cleanup:** obsolete Unity find APIs were replaced in `SimpleMatchmaking` and the CLI harness.

## Verification

| Kind | Evidence |
| --- | --- |
| Compile | A Unity 6000.6.6f1 player build has no compile errors and no obsolete-API warnings from these sources. |
| Direct path, real transport (localhost) | All CLI scenarios pass on the migrated RPCs: Immediate, Draft, eight-player, presentation-only, `--challenge` and `--challenge --penalty-none`. That includes raw duplicate and stale RPCs and disconnect suspension. See the [verification report](GAMEPLAY_INTEGRATION_VERIFICATION.md). |
| Reconnect (direct path, real transport) | `run_players.py --reconnect` passes. The client drops mid-move, reconnects through Retry with a new transport ID, keeps its seat and rotated secret, and a forged claim is refused. The host sees the missing seat, then resumes, and the match completes. |
| Online path (Lobby + Relay) | **Not yet verified live.** `run_players.py --live` runs a full match through `SimpleMatchmaking` with two separate anonymous profiles. It needs network access and the user's permission because it uses the linked Unity Cloud project's free tier. |
