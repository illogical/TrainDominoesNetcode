# Netcode learning guide

This guide is for a developer building their first networked Unity game. It explains the networking ideas this project relies on and records the mistakes found in its netcode, with how each was fixed and a rule to apply next time. It also notes what would change if the libraries were upgraded.

It is a living document. Each Priority 4 task (NET-001…NET-006) adds its lessons here. Mistakes are recorded as patterns to learn from, not blame; nearly every first multiplayer project makes most of them.

**Started:** 2026-10-18, during [NET-002](plans/2026-10-18-feat-net-002-gameplay-wire-contract-plan.md). Related: [integration guide](GAMEPLAY_INTEGRATION.md), [verification report](GAMEPLAY_INTEGRATION_VERIFICATION.md).

## Contents

1. [Primer: the ideas that matter](#primer-the-ideas-that-matter)
2. [Mistakes catalog](#mistakes-catalog)
3. [Change log](#change-log)
4. [Libraries: versions and upgrade notes](#libraries-versions-and-upgrade-notes)
5. [Checklist for new netcode](#checklist-for-new-netcode)

## Primer: the ideas that matter

### 1. One authority decides

Every client runs its own copy of the game, and those copies drift apart unless one place decides what is true. Here the host owns the only real `MatchState`, inside `AuthoritativeMatchCoordinator`. Clients send *requests* ("I'd like to draw") and receive *results*. A client never changes game state itself.

Disabling a button helps the player, but it is not a rule check. A modified client, a double-click or a delayed packet can still send a request, so the server must validate every request as if the UI did not exist.

### 2. An RPC is a trust boundary

A `ServerRpc` is a function a remote computer can call with any arguments it likes. Treat its parameters like form input on a website:

- **Who is calling?** Use the transport-provided sender (`rpc.Receive.SenderClientId`), never an ID inside the payload.
- **Is the payload well-formed?** Parse defensively, bound its size, and never let a parse error throw out of the RPC.
- **Is the action legal now?** Ask the rules engine, not the client.

### 3. Requests need identities (idempotency)

Networks repeat and lose messages, and a player may click Retry. Each command carries a `RequestId`. The host caches the outcome per seat, so a retried request gets the *same* answer without applying twice. Reusing an ID for a different command is rejected. This is what makes "send again if unsure" safe.

### 4. Positions prevent stale actions

Each command also carries the `PositionToken` (match, round, turn, revision) the client was looking at. If the game has moved on, the host rejects it as `StalePosition` instead of applying it to a board the player never saw.

### 5. Each player sees only their own projection

The host never sends the full game state. `RecipientProjection.Build` creates one view per seat: that player's own hand plus public information. Events are filtered by audience too. Hiding information only in the UI does not protect it, because anything sent can be read from memory or the network.

### 6. Reliable and unreliable delivery

Netcode for GameObjects (NGO) RPCs default to **reliable, ordered** delivery: messages arrive once, in order, or the connection drops. Unreliable delivery suits rapidly replaced data like positions. Turn-based game commands want reliable delivery, and this project uses it.

### 7. The wire format is a contract

Whatever you send must decode identically on the other side, in every build that might talk to it. That means:

- **Explicit absence.** Decide how "no value" is represented and test it.
- **A version number.** A mismatched build should fail clearly, not misread data.
- **Size awareness, measured in bytes on the wire.** That is not the same as the length of your string.
- **Tests that use the real serializer.**

### 8. Transport ID is not player identity

The transport gives each connection a number. If the player reconnects, they get a new number. Game seats should be a separate identity, mapped to the current connection (`SeatRegistry`), so a reconnect can be rebound to the same seat (NET-004).

## Mistakes catalog

Severity: **High** means it can corrupt or leak game state or crash a peer. **Medium** means it causes wrong behavior in plausible situations. **Low** means maintainability or a latent risk.

### M1. BinaryFormatter left in the project (High, fixed)

- **What happened:** `Models/NetworkSerializer.cs` serialized payloads with `BinaryFormatter`. It was no longer referenced, but it stayed in the tree with two other legacy serialization files.
- **Why it matters:** `BinaryFormatter` can run arbitrary code when deserializing untrusted bytes. Microsoft has deprecated and disabled it in modern .NET. Any future use on network input would be a remote-code-execution hole.
- **How it was detected:** static review before Priority 4, then a repo-wide name and GUID sweep.
- **Fix:** deleted `NetworkSerializer`, `JsonContainer` and `Models/DTO` with their `.meta` files after confirming no code, scene, prefab or asset referenced them.
- **Next time:** never deserialize network input with a type-embedding serializer. Use plain data formats (JSON, explicit binary writers) into known types. Delete dead serialization code promptly so nobody revives it.

### M2. JsonUtility turns "nothing" into "something" (High, fixed)

- **What happened:** `JsonUtility` cannot represent a null nested class. The Unity batch check observed that a null `View` decodes as a blank `PlayerViewDto`, and a null `Train` decodes as a train with `Owner=-1`. A player who is not seated gets a reply with no view. After decoding, the client saw a blank view and installed it, with a null match ID.
- **Why it matters:** clients act on fake data, and checks like `Train != null` silently stop meaning anything.
- **How it was detected:** reasoning from Unity's serialization rules, then confirmed by `WireContractBatch` (recorded in `Tests/UnityIntegration/observed-wire-contract.txt`).
- **Fix:** `WireCodec` writes explicit flags (`HasView`, `HasOutcome`, `HasRequiredTrain`, `HasExpected`, `HasTrain`) when encoding. On decode it restores `null` when a flag is false. In memory, `null` keeps meaning absent.
- **Next time:** know your serializer's null, default and enum behavior before designing DTOs. Test absence explicitly.

### M3. Tests used a different serializer than the game (Medium, fixed)

- **What happened:** console checks used `System.Text.Json`, which handles nulls correctly, while the game used `JsonUtility`. The tests passed for the wrong reason and could not catch M2.
- **Fix:** both paths now go through `WireCodec`. The Unity batch check asserts the real `JsonUtility` behavior and writes golden fixtures, and the console suite decodes those fixtures.
- **Next time:** a test of a boundary must use the production encoder, or real output captured from it.

### M4. Size limits in the wrong units, and no outbound check (Medium, fixed)

- **What happened:** the inbound cap was 16,384 *characters*. NGO writes a `string` RPC argument as UTF-16, two bytes per character (`FastBufferWriter.GetWriteSize`). Nothing checked the size of outgoing projections at all.
- **Fix:** `WireCodec.WireBytes` computes on-wire bytes. Commands are capped at 4 KB and projections at 64 KB. Oversized outbound packets are logged, and the recipient gets feedback instead of a silent drop.
- **Next time:** express limits in the unit the transport uses, and check both directions.

### M5. Assuming what a library setting means (Medium, fixed in the docs)

- **What happened:** the planning documents, including this task's first draft, assumed UnityTransport's `MaxPayloadSize` (6144) capped our RPCs. Reading the installed transport showed that cap applies only to *unreliable* sends. Reliable sends are streamed across packets and bounded only by the send queue. Real packets reach 22 KB and work fine.
- **How it was detected:** a measurement contradicted the assumption. Packets three times the "limit" had passed thirteen-round CLI matches. The installed source (`UnityTransport.Send`, `SelectSendPipeline`) explained why.
- **Next time:** when behavior depends on a library setting, read the installed version's source or docs and confirm with a measurement. Setting names and tooltips can mislead.

### M6. Client decode could throw (Medium, fixed)

- **What happened:** the server guarded command parsing, but the client's `ProjectionClientRpc` called `JsonUtility.FromJson` directly. A malformed or mismatched payload would throw inside the RPC.
- **Fix:** guarded decode. On failure the client shows feedback and requests resynchronization, at most three times, so a persistently bad payload cannot loop.
- **Next time:** guard *both* directions. Any automatic retry needs a bound.

### M7. No wire version (Medium, fixed)

- **What happened:** a host and client running different builds could misread each other without noticing. `ProfileVersion` describes the rules, not the payload shape.
- **Fix:** `WireVersion` on every packet and command; a mismatch is rejected with a clear message.
- **Next time:** version the wire format from the first networked build.

### M8. Transport ID used as the seat (Medium, fixed)

- **What happened:** the coordinator keyed players and cached outcomes by transport `ulong`. A reconnecting player would look like a stranger.
- **Fix:** `SeatRegistry` (pre-Priority-4 refactor) separates seats from transport IDs, and the outcome cache is keyed by seat. NET-004 added authenticated rebinding with host-issued seat secrets, so a player who reconnects with a new transport ID gets the same seat, hand and history back.
- **Next time:** model "who is playing" separately from "which connection" from the start.

### M9. Trusting the UI and nesting RPCs (High, fixed by the integration)

- **What happened** (STATE-002 legacy baseline): two quick host draws changed a hand from 12 to 14 tiles, because the server accepted repeated draws and relied on button state. Separately, `EndTurnServerRpc` called another `[ServerRpc]`. The inner call saw the host as the sender, so a remote player's action was credited to the host.
- **Fix:** the integration replaced these paths with one authenticated mutation boundary that validates every command in the core and deduplicates requests.
- **Next time:** server code that needs shared logic calls a plain method, never another RPC. Validate on the server even when the button "can't" be pressed.

### M10. No way to test bad networks (Medium, partly fixed)

- **What happened:** nothing could simulate delayed, dropped, duplicated or reordered packets without real networking.
- **Fix so far:** `Tests/GameplayIntegration/LoopbackRelay.cs` connects real clients to a real coordinator, now through the production codec, with hold, drop, duplicate and reorder controls. NET-006 builds the full matrix.
- **Next time:** build a fault-injecting loopback early; most netcode bugs only appear under reordering or loss.

### M11. Events sent but never shown (Low, open: VIEW-004)

Announcements, challenges and penalty draws travel to clients, but `GameplayPresenter.Present` ignores them. Players get no feedback for those actions. **Next time:** when you add a message type, add its consumer and a test in the same change.

### M12. Deprecated RPC attributes (Low, fixed: MAINT-003)

The four `[ServerRpc(RequireOwnership = false)]` RPCs compiled with obsolete warnings under NGO 2.13.3. They are now `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]`, and client targeting uses `[Rpc(SendTo.SpecifiedInParams)]` with `RpcTarget.Single(id, RpcTargetUse.Temp)`. **Next time:** treat obsolete warnings as a to-do list; fix them while the old and new forms both still work, so the change is small and testable.

### M13. Fire-and-forget async service calls (Low, fixed: NET-003)

`SimpleMatchmaking` called `Lobbies.Instance.SendHeartbeatPingAsync(lobbyId)` without `await`, so any failure was silently lost; the lobby leave on shutdown had the same problem. Both now await inside methods that catch and log. The `async void` button handler also catches everything, because an exception escaping `async void` cannot be caught anywhere else. **Next time:** never discard a `Task` from a network service call. When a fire-and-forget is unavoidable (`_ = Heartbeat()`), make the method itself catch and log.

### M14. Online matchmaking defaults that did not match the game (Medium, fixed: NET-003)

- **What happened:**
  - The lobby and Relay allocation allowed 100 players for a 2–8 player game.
  - Relay used unencrypted UDP (`SetHostRelayData` with `RelayServer`), although `RelayTester` already showed the DTLS setup.
  - Every quick-join error, including a real outage, fell through to creating a new lobby.
  - Two copies on one machine signed in as the same anonymous player, so they could not be tested together.
- **Fix:**
  - `MaxPlayers = 8` (the allocation counts the 7 non-host connections).
  - `allocation.ToRelayServerData("dtls")`.
  - Only `LobbyExceptionReason.NoOpenLobbies` falls through to creating a lobby.
  - `--ugs-profile <name>` gives each instance its own identity.
- **Next time:** take limits from the game's rules, choose the encrypted transport option by default, and handle the one expected exception narrowly instead of catching everything.

### Design lessons (from the pre-Priority-4 review)

- **Readable boundaries:** the coordinator and DTO code were written as very long single lines. Network code is where you most need to follow an action step by step, so keep it plainly formatted.
- **One class, one job:** `ClientSession` mixes view storage, request tracking, selection and feedback (MAINT-001). Separating request tracking makes retry and uncertainty logic testable alone.

## Change log

### NET-002 — wire contract (2026-10-18)

**Before**, `GameSession` serialized directly:

```csharp
SubmitCommandServerRpc(JsonUtility.ToJson(request));
// ...
Client.Receive(JsonUtility.FromJson<ProjectionPacket>(json), synchronization);  // could throw; nulls become blanks
```

**After**, every payload goes through one codec:

```csharp
public static readonly WireCodec Wire = new WireCodec(JsonUtility.ToJson, JsonUtility.FromJson);

if (!Wire.TryEncode(request, out var json, out var error)) { Debug.LogError(...); Client.MarkUncertain(); return; }
SubmitCommandServerRpc(json);
// ...
if (!Wire.TryDecodeProjection(json, out var packet, out var error)) { /* feedback + bounded resync */ return; }
Client.Receive(packet, synchronization);
```

**Why the codec takes the JSON functions as parameters:** `JsonUtility` exists only inside Unity, but the console tests must run the same validation. Passing `JsonUtility.ToJson` in Unity and `System.Text.Json` in tests keeps one implementation of the rules (presence, version, bounds) with no Unity dependency.

**Why presence flags live only on the wire:** the rest of the code already treats `null` as absent. The codec translates at the boundary, so the coordinator, `ClientSession` and the rules core did not change. Keeping translation at the edge limits how far a format quirk spreads.

**Measured:** the largest of more than 500,000 eight-player packets was 22,356 bytes on the wire, about a third of the 64 KB guard. The largest command was 566 bytes.

### NET-001, NET-005, NET-006 — validation (2026-10-18)

No gameplay code changed. The work was all about *proving* the netcode, and the lessons are about testing networked games.

**T1. Test as the attacker, not the UI.** The NET-001 checks send commands straight to the host (`LoopbackRelay.SubmitRaw`), skipping `ClientSession`'s own guards, because a modified client can do the same. For every rejected command, the test compares every seat's encoded projection before and after: a rejection must change nothing anywhere.

**T2. Check invariants on every message, not just examples.** Instead of asserting a few hand-picked views, a hook inspects every payload a client receives:
- no other player's provisional tile;
- no other player's private event;
- no tile ID that another player currently holds;
- own hand size equal to the public count;
- every one of the 90 tiles accounted for.

One hook covers thousands of situations no one would think to write by hand.

**T3. Make sure a test can fail.** Passing on the first try is suspicious. Two deliberate bugs were introduced (removing the event-audience filter, and accepting older revisions); both made the suites fail, then the code was restored. Without that, a test could silently check nothing.

**T4. Separate "wrong" from "different by design."** Three early failures were mistakes in the tests:
- **A rejection reply carries the sender's current view.** The client catches up from the rejection itself, which is good self-healing, so the test was wrong to expect the old view to remain.
- **A private draft changes only the owner's view of its own train marker**, so "public board" comparisons must ignore markers while a draft is open, as they already ignored draft hand counts.
- **When updates are dropped, every peer that missed them must resynchronize**, not just the one being tested.

Before changing a test to pass, decide which side is wrong and write down why.

**T5. Real concurrency produces legitimate races.** Over real transport, an "expired challenge" probe could be overtaken by the opponent's next move. The host then correctly rejected it as stale, or the round ended first. The harness records those runs as *inconclusive* and retries later; only a definite `ChallengeNotAllowed` counts as proof. A test that treated the race as a pass would hide bugs, and one that treated it as a failure would be flaky.

**T6. Search seeds instead of hand-building states.** Fixtures such as "an open challenge window with an empty boneyard" come from playing seeded matches until the situation occurs. Fixed seeds make this deterministic, and the fixtures keep working if dealing details change.

**The fault harness:** `LoopbackRelay` can hold, drop, duplicate, reorder and release commands and projections independently. `Resynchronize` mirrors `GameSession`'s synchronization (resubmitting the pending request first), and `RelayScenario.Slow` simulates a player whose animations lag. All of this is simulated delivery. It proves the protocol logic, not a real network.

### NET-003, MAINT-003, NET-004 — packages, entry paths and reconnect (2026-10-18)

**Before**, a dropped player was gone for good: the seat was tied to a transport ID that never comes back, so the only option was a fresh match without them.

**After:**

```csharp
// Host, at match start: each seat gets a random 128-bit secret, sent only in that seat's own projection.
packet.SeatSecret = seats.SecretFor(seat);

// Client, on the new connection: present the credential that survived the disconnect.
if (!IsServer && credential != null && Wire.TryEncode(credential, out var claim, out _))
    ReclaimSeatServerRpc(claim, pendingJson);

// Host: verify, rebind seat -> new transport, rotate the secret; only then send any projection.
var result = authority.Reclaim(sender, request, id => !departed.Contains(id) && NetworkManager.ConnectedClients.ContainsKey(id));
```

**Why a secret, not the player's name or seat number?** Anyone can claim to be "Player 2". The secret is something only the real Player 2 was ever sent. It is checked in constant time, so response timing does not leak how many characters matched. It rotates on every reclaim, so a secret seen once cannot be replayed.

**Why "seat still connected" is a rejection:** even with the right secret, a seat whose player is still connected cannot be taken over. A leaked secret then cannot kick out a playing user.

**Why a failed claim gets nothing:** a projection contains the seat's hand. Sending one before the claim is verified would disclose a private hand to an impostor.

**What happens to a request that was in flight when the connection dropped?** It is resubmitted with its original request ID:
- If it had reached the host, the host replays the cached outcome, and it is never applied twice. This works because the outcome cache is keyed by *seat*, not by transport.
- If it never arrived, it reaches the host while the match is suspended and is rejected. It cannot apply later, and the player simply acts again after resuming.

Both outcomes are safe, and the tests pin both down.

**Why no new player state?** The existing suspended state already had Resume, New Match and Retry. Making Retry reconnect when the connection is gone added the feature without touching the state machine, and the strict state-map comparison confirms the topology did not change. Reusing an existing control is usually cheaper and safer than adding a state.

**Developing a package migration safely:** the Editor had the project open, so changing the manifest there would have triggered an immediate re-import mid-change. The migration was made and verified in an isolated copy (compile, every CLI scenario), then applied to the real project in one step.

## Libraries: versions and upgrade notes

Reviewed 2026-10-18 against the installed packages (`Packages/manifest.json`, `packages-lock.json`) and public release notes. **Updated the same day:** with the user's approval, Lobby and Relay were migrated to Multiplayer Services 2.3.3, and the deprecated RPC attributes and find APIs were replaced (MAINT-003).

| Package | Installed | Status | What an upgrade or migration would require |
| --- | --- | --- | --- |
| Unity Editor | 6000.6.6f1 | Pinned by `ProjectVersion.txt` | Unity 6.6 deprecates `FindFirstObjectByType`, `FindObjectsSortMode` and `FindObjectOfType` (warnings in `SimpleMatchmaking` and the CLI harness). Use `FindAnyObjectByType` or the new `FindObjectsByType` overloads (MAINT-003). |
| Netcode for GameObjects | 2.13.3 | Current 2.13 line (2.13.0 announced July 2026) | **Done (MAINT-003)**, with the steps listed here. `[ServerRpc(RequireOwnership = false)]` is obsolete. Replace it with `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]`. Replace the `[ClientRpc]` + `ClientRpcParams` targeting with `[Rpc(SendTo.SpecifiedInParams)]` and `RpcParams`, and read the sender from `RpcParams.Receive.SenderClientId`. These attributes generate different code, so re-run the wire batch check and all four CLI player scenarios afterward. |
| Unity Transport | 6.6.0 (built-in, resolved by the editor) | Built into the editor since Unity 6.5; changes ship in editor release notes | No code change needed. `MaxPayloadSize` limits only unreliable sends (see M5). The reliable pipeline window is 64 packets. If payloads grow a lot, watch the send queue (exceeding it disconnects the peer) rather than `MaxPayloadSize`. |
| Lobby | 1.1.0 → **Multiplayer Services 2.3.3 (migrated)** | Deprecated in Unity 6 in favor of `com.unity.services.multiplayer` | **Done.** Remove `com.unity.services.lobby` and `com.unity.services.relay`, add `com.unity.services.multiplayer`, change `Lobbies.Instance` to `LobbyService.Instance` (`SimpleMatchmaking.cs:63, 93, 118`), and update any asmdef references. Decide during NET-003 whether to migrate or retire `SimpleMatchmaking`. |
| Relay | 1.0.5 → **Multiplayer Services 2.3.3 (migrated)** | Deprecated in favor of the same Multiplayer Services package | **Done.** `ToRelayServerData("dtls")` replaced the manual UDP setup. `RelayService.Instance` calls in `SimpleMatchmaking` and `RelayTester` move with the package. Confirm namespaces and the Relay server-data setup against the Multiplayer Services migration guide during NET-003. |
| Authentication | 3.8.0 (dependency) | Pulled in by the services packages | Comes along with Multiplayer Services; no direct code change expected. |

Sources: [NGO 2.13.0 announcement and changelogs](https://discussions.unity.com/tag/Netcode-for-GameObjects), [Unity Transport changelog](https://docs.unity3d.com/Packages/com.unity.transport@6.5/changelog/CHANGELOG.html), [Multiplayer Services migration path](https://docs.unity.com/en-us/mps-sdk/migration-path), [Multiplayer Services FAQ](https://docs.unity.com/en-us/mps-sdk/faq). Installed behavior was read from `Library/PackageCache/com.unity.netcode.gameobjects@…/Runtime` (`UnityTransport.cs`, `FastBufferWriter.cs`, `RpcAttributes.cs`).

**Payload bandwidth (deferred):** a `string` RPC argument costs two bytes per character. Sending UTF-8 bytes would roughly halve the worst case (about 22 KB to about 11 KB). It is grouped with the RPC attribute migration in MAINT-003 because both change generated RPC code. Any change to the encoding bumps `WireCodec.Version`.

## Checklist for new netcode

- [ ] The server validates sender, payload and legality, whatever the UI allows.
- [ ] Server-side helpers are plain methods, not nested RPCs.
- [ ] Every command has a request ID and the position it was based on; retries are safe.
- [ ] Each recipient gets only its own projection; private data never leaves the host otherwise.
- [ ] Payloads go through `WireCodec`: absence explicit, version checked, size bounded, decode never throws.
- [ ] A DTO shape change bumps `WireCodec.Version`, refreshes the fixtures (Unity batch check), and passes the console suite.
- [ ] New message types have a consumer and a test in the same change.
- [ ] Behavior that depends on a library setting is confirmed against the installed source or with a measurement.
- [ ] Fault cases (duplicate, drop, reorder, delay) are exercised through `LoopbackRelay` before relying on live play.
- [ ] Simulated, localhost CLI and live-service evidence are reported separately.
- [ ] Identity that must survive a reconnect is a seat with a secret, never a transport ID; a failed claim receives no private data.
- [ ] Every `Task` from a service call is awaited or explicitly observed; `async void` is limited to event handlers that catch everything.
