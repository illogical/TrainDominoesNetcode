---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implementation-ready
product_contract_source: ce-plan-bootstrap
execution: code
title: Overlays off the board in play, and network components off the domino prefab (VIEW-005, MAINT-004)
type: fix
date: 2026-10-18
origin: Assets/Docs/plans/2026-10-18-feat-view-001-004-board-navigation-presentation-plan.md
---

# Overlays off the board in play, and network components off the domino prefab (VIEW-005, MAINT-004)

**Authored context:** Written on 2026-10-18, just after VIEW-001 and VIEW-004 were implemented. It describes planned work only; nothing here is implemented. The VIEW-001 view probes found both problems (see [TASKS.md](../../../TASKS.md) and the [VIEW plan's status section](2026-10-18-feat-view-001-004-board-navigation-presentation-plan.md#implementation-status-2026-10-18)). The two tasks are independent and can be done in either order. Each is small.

## Start here

1. Read [AGENTS.md](../../../AGENTS.md), then VIEW-005 and MAINT-004 in [TASKS.md](../../../TASKS.md).
2. Read [networking entry paths](../NETWORKING_ENTRY_PATHS.md) for the Start button flow, and M15 in the [netcode learning guide](../NETCODE_LEARNING_GUIDE.md) for the prefab problem.
3. Read the [CLI harness README](../../../Tests/UnityIntegration/README.md): the match scenarios, the view probes (`run_view.py`), and how to build the isolated project.
4. **Ask the user decision D1 below before changing the Quantum Console.**
5. The user's Unity Editor may have the project open. Prefab and scene edits must be made in the Editor; check before changing assets.

## Facts checked on 2026-10-18

Recheck each fact before relying on it.

| ID | Fact | Where |
| --- | --- | --- |
| F1 | The Start button is `SimpleMatchmaking._startButton`, under `MainMenuUI` near the centre of the screen. It is hidden only when a Relay lobby connects (`if (_connectedLobby != null) _startButton.gameObject.SetActive(false);` in the `finally` of `CreateOrJoinLobby`). Nothing hides it when the network starts another way. | `Assets/Scripts/Game/SimpleMatchmaking.cs` ~30, ~62 |
| F2 | The direct path calls `UnityTransport.SetConnectionData` then `StartHost`/`StartClient`. The `NetworkManagerUI` developer buttons are on an inactive GameObject. The CLI harness uses this path and disables `SimpleMatchmaking` first. | `NETWORKING_ENTRY_PATHS.md` §2; `IntegrationRuntime.cs` |
| F3 | The scene's Quantum Console is a prefab instance of `Assets/Plugins/QFSW/Quantum Console/Source/Prefabs/Quantum Console.prefab`. The prefab sets `_activateOnStartup: 1` and `_supportedState: 0`. `SupportedState` 0 is **`Always`**, so the console opens at startup in every build, release included, not only development builds. The scene instance does not override either field. | prefab lines ~568–569; `SupportedState.cs` |
| F4 | `RelayTester` uses Quantum Console commands (`CreateRelay`/`JoinRelay`); its component is disabled in the scene. No other gameplay script references QFSW. | `Assets/Scripts/Game/RelayTester.cs` |
| F5 | `Assets/Prefabs/Dominoes/domino_original.prefab` has a `NetworkObject` (`AutoObjectParentSync: 1`, `GlobalObjectIdHash: 951099334`) and a `NetworkTransform`. `DominoEntityUI` and `Mover` on it are plain MonoBehaviours. | prefab ~640–700 |
| F6 | `MeshManager.CreateDominoFromInfo` instantiates tiles locally and never spawns them. It now sets `AutoObjectParentSync = false` as a workaround. | `MeshManager.cs` ~86–88 |
| F7 | `domino_original` is listed in `Assets/DefaultNetworkPrefabs.asset` (`IsDefault: 1`, NGO's auto-maintained list), together with `DominoPlayer`. The scene's `NetworkManager` uses `Assets/Prefabs/NetworkPrefabsList.asset`, which lists only `DominoPlayer`. `ForceSamePrefabs: 1`. | assets named |
| F8 | **`DominoPlayer.prefab` (the NetworkManager `PlayerPrefab`) has the same `GlobalObjectIdHash` 951099334 as `domino_original`.** Two different prefabs share a hash, probably because one was copied from the other. Removing the `NetworkObject` from `domino_original` also removes that collision. `DominoPlayer` must keep its hash. | `DominoPlayer.prefab` ~48 |

## Decisions

| ID | Question | Recommended default |
| --- | --- | --- |
| D1 (**ask the user**) | What should the Quantum Console do at startup and in release builds? | Keep it, but **start closed** in all builds by overriding `_activateOnStartup = false` on the **scene instance**. The plugin prefab stays untouched. The backquote key still opens it. Also offer `_supportedState = Development` (no console in release builds) as a separate choice. |
| D2 | When should the Start button hide? | When this peer's network starts by any path (host or client started), not only after a Relay lobby connects. |
| D3 | Should the Start button come back when the network stops? | Only when there is no match to return to: after a failed start, or after leaving with no seat credential. During a suspended or reconnectable match, **Retry** handles reconnection (NET-004). The Start button must not offer a second, competing path. |

## Units

### U1 — Hide the Start control whenever a session starts (VIEW-005 part 1, D2/D3)

- In `SimpleMatchmaking`, subscribe to the `NetworkManager` start events (`OnClientStarted`, and `OnServerStarted` for a host) and hide `_startButton` there. Keep the existing hide after a lobby connects; it becomes redundant but harmless.
- Subscribe in `Awake` (or resolve the `NetworkManager` lazily). Unity calls `Awake` even when the component is disabled, and the CLI harness disables `SimpleMatchmaking` before its `Start`, so the hide must not depend on `Start`. Unsubscribe in `OnDestroy`, balancing the subscriptions.
- D3: on `OnClientStopped`, show the button again only when `GameSession.Instance?.Client.Credential == null` (no seat to reclaim) and no lobby is connected. Otherwise leave it hidden; Retry handles reconnection. Re-enable `interactable`.
- This changes NET-003's file. Keep the change narrow, keep the Relay path's behaviour identical, and record it in `NETWORKING_ENTRY_PATHS.md` (the Start button steps and the failure table).

### U2 — Quantum Console starts closed (VIEW-005 part 2, D1)

- Apply the user's D1 choice as a **scene-instance override** in the Unity Editor. Do not edit the third-party prefab (AGENTS.md). Save the scene and check in the diff that only the expected override lines changed.
- If the user picks `Development`, check that `RelayTester` does not fail without the console in a release build. It is disabled in the scene, but confirm.

### U3 — Remove the network components from the domino prefab (MAINT-004)

Make the change in the Unity Editor, not by editing YAML.

1. Open `domino_original.prefab`. Remove `NetworkTransform` first, then `NetworkObject`. Save.
2. Confirm that NGO removed the prefab from `DefaultNetworkPrefabs.asset` automatically (it maintains that list). If it did not, remove the entry in the Editor. Leave `NetworkPrefabsList.asset` and `DominoPlayer` unchanged; check that `DominoPlayer` still has hash 951099334.
3. Remove the workaround from `MeshManager.CreateDominoFromInfo` (the `TryGetComponent<NetworkObject>` line and its comment).
4. Search `Assets/Scripts` and the harness for `NetworkObject`/`NetworkTransform` uses on tiles (none on 2026-10-18).
5. In the git diff, confirm only the intended prefab components, list entries and `MeshManager` changed. Preserve GUIDs and other serialized references (AGENTS.md).
6. Update M15 in the learning guide from "worked around" to "fixed", and drop the "worked around" wording from `PROJECT_STATE.md` and the VIEW plan's status section if present.

### U4 — Checks

Run these in the isolated project described in the CLI README. Keep testing silent: batchmode plus `--mute-audio`, as the runners already do.

- **View probes:** remove the probe's workarounds in `Tests/UnityIntegration/ViewRuntime.cs`:
  - Keep hiding `MainMenuUI` only with a comment that view mode starts no network. Better, start a host on a spare port before the probes, so U1 hides the button and the probe can assert that instead.
  - Remove the Quantum Console `Deactivate` call and assert the console is not active at startup (`IsActive == false` via reflection, as the probe does now). Expect this only if D1 chooses "start closed".
  - `python3 Tests/UnityIntegration/run_view.py` must still pass for every size and player count, with no UI covering the board.
- **Match scenarios:** in `IntegrationRuntime.cs`, after `network-spawn` on the direct path, assert that the Start button is inactive (find it through `SimpleMatchmaking`'s serialized field by reflection, or by name). Run all seven localhost scenarios: `run_players.py` (plain, `--draft`, `--presentation-only`, `--challenge`, `--challenge --penalty-none`, `--reconnect`) and `run_eight.py`. In the reconnect scenario, also assert the Start button stays hidden after the drop and reclaim (D3).
- **Parenting errors (MAINT-004):** grep every player log for `re-parent` and `not listening, start`. The count must be 0 **without** the `MeshManager` workaround.
- **Console suites:** run from `RulesEngine/`: `dotnet test` and the four `Tests/*` check projects. They should be unaffected.
- **Live path:** `run_players.py --live` uses Lobby and Relay. Run it only with the user's permission. Otherwise report the Relay-path Start-button behaviour as unchanged by reasoning, and untested.

## Definition of done

- **VIEW-005:** on the direct path (CLI localhost) the Start button is hidden once the network starts, and it stays hidden through a reconnect. The Quantum Console follows D1. The view probes pass with no overlay workarounds other than view mode's no-network start menu. `NETWORKING_ENTRY_PATHS.md` is updated.
- **MAINT-004:** the domino prefab has no network components and is not in any network prefab list. `DominoPlayer` keeps its hash. The `MeshManager` workaround is gone. All CLI scenarios and view probes pass with zero NGO parenting errors.
- **Records:** check the boxes in `TASKS.md` with evidence separated by type (console, Unity CLI localhost, live if permitted, human-visible if any). Update the learning guide (M15, plus a change-log line) and `Tests/UnityIntegration/VERIFICATION.md`. Add a new `production-source-*.sha256` for the verified build.

## Out of scope

- The two visual candidates from the VIEW-001 screenshots belong to the VIEW-002 human review: a row scrolled to its end slides its first tiles under the row-control column, and the "Play" marker covers the start of the row label.
- Why UI text is missing from the offscreen probe screenshots.
- Retiring `NetworkManagerUI` or `RelayTester`.
- Any change to `DominoPlayer`, the rules engine, the wire contract or player states.
