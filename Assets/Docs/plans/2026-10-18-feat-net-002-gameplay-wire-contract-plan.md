---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implemented
product_contract_source: ce-plan-bootstrap
execution: code
title: Define and test the gameplay wire contract (NET-002) and outline Priority 4
type: feat
date: 2026-10-18
origin: Assets/Docs/plans/2026-10-18-refactor-pre-priority-4-network-readiness-plan.md
---

# Define and test the gameplay wire contract (NET-002) and outline Priority 4

**Authored context:** Saved on 2026-10-18 after static reading of `SessionProtocol.cs`, `GameSession.cs`, `ClientSession.cs`, `AuthoritativeMatchCoordinator.cs`, the installed Netcode `FastBufferWriter`, the Unity CLI harness and the scene's transport settings. Revised the same day after review. This document specifies planned work; it does not assert implementation or verification. Task IDs (NET-001…NET-006, MAINT-001/002) are unchanged; U identifiers below are local to this plan.

**Coordination:** The [pre-Priority-4 refactor](2026-10-18-refactor-pre-priority-4-network-readiness-plan.md) (seat registry, `RecipientProjection`, `LoopbackRelay`) is committed as `d7051d9`. Before starting, confirm the other session has no uncommitted edits to `SessionProtocol.cs`, `GameSession.cs`, `ClientSession.cs` or `AuthoritativeMatchCoordinator.cs`.

**Scope:** NET-002 is planned in implementation depth because it gates the rest of Priority 4. NET-001/005/006/003/004 are an ordered outline; each gets its own plan later.

## Implementation outcome (2026-10-18)

Implemented; evidence is in the [verification report](../GAMEPLAY_INTEGRATION_VERIFICATION.md#net-002-wire-contract-2026-10-18), and the lessons are in the [netcode learning guide](../NETCODE_LEARNING_GUIDE.md). Implementation differed from this plan in these places:

- **The F3 premise was wrong.** UnityTransport's `MaxPayloadSize` limits only unreliable sends; reliable RPCs are streamed. Real eight-player worst-case packets are 22,356 wire bytes and already worked. By decision, U4 became a 64 KB adapter guard (2.93× headroom) with compaction deferred; UTF-8 encoding moved to MAINT-003.
- **The installed transport version is different.** Unity Transport resolves to the editor's built-in 6.6.0, not 2.6.0.
- **Presence flags are wire-only.** `WireCodec` sets them on encode and restores `null` on decode, so `ClientSession`, the coordinator, fingerprinting and existing checks needed no change. The CLI harness now copies and sends raw commands through the codec.
- **U6 removed more dead code.** `JsonContainer` and `Models/DTO` were equally unreferenced and were removed with `NetworkSerializer`.
- **New follow-up.** MAINT-003 records the deprecated `RequireOwnership` RPCs and Unity find APIs found during the library review.

## Decisions (agreed 2026-10-18)

- Keep `JsonUtility` and the JSON DTOs; make absent values explicit rather than adopting a new codec or package.
- Whether the worst-case payload fits is decided by measurement (U1) before any compaction, splitting or scene change.
- Packages are not upgraded incidentally. A library currency review documents what would need to change (U0).
- A learning guide for a developer new to netcode is a deliverable, started in U0 and extended by each unit.

## Findings this plan acts on

- F1. **Null nested classes.** `JsonUtility` cannot represent null nested `[Serializable]` classes (*confirm in U1*); null arrays come back as empty arrays. Affected: `ProjectionPacket.View`/`Outcome`, `PlayerViewDto.RequiredTrain`, `CommandRequest.Expected`/`Train`, `CommandOutcome.Position`. Concrete consequence: the unseated reply (`AuthoritativeMatchCoordinator.Unseated`) has no view; after decoding, `ClientSession.Receive` sees `next != null`, and when it has no view yet it installs a blank `PlayerViewDto` with a null match ID (`ClientSession.cs:99-117`).
- F2. **Strings are two bytes per character on the wire.** RPC `string` parameters go through `FastBufferWriter`, whose default is UTF-16 (`GetWriteSize`: `s.Length * sizeof(char)` unless `oneByteChars`). On-wire size is about twice the JSON length plus headers, so the 6144-byte budget is about 3 000 JSON characters.
- F3. **Inconsistent caps.** `GameSession.MaximumCommandCharacters = 16384` characters (about 32 KB on the wire) can never be reached through a 6144-byte transport, so the cap does not express the real limit. No outbound size check exists; an oversized projection fails inside Netcode rather than at the adapter.
- F4. **Unguarded client decode.** `TryRead` guards inbound commands, but `ProjectionClientRpc` calls `JsonUtility.FromJson` without a guard or validation.
- F5. **Tests skip the production codec.** Console checks use `System.Text.Json` or in-memory objects.
- F6. **Dead serializer.** `Assets/Scripts/Models/NetworkSerializer.cs` (BinaryFormatter) has no code references. `Models/JsonContainer.cs` and `Models/DTO` need the same reference check.
- F7. **No wire version.** `ProfileVersion` identifies the rules profile, not the payload shape. Mismatched builds (plausible over Relay) would fail silently.

## Requirements

- R1. Absent values survive encode/decode and are never confused with default instances; arrays are never null on either side.
- R2. Malformed, truncated, oversized, wrong-typed or wrong-version input is rejected without throwing, on both server and client.
- R3. One budget constant, expressed in on-wire bytes, drives inbound caps, outbound checks and the regression threshold. The worst case fits with recorded headroom.
- R4. No rule, routing or semantic change. Request fingerprinting keeps its deduplication behavior.
- R5. The production codec is exercised by a real Unity check; console checks decode the same Unity-produced fixtures.
- R6. No scene, prefab, package or GUID change unless separately approved.

## Implementation units

### U0. Library review and learning guide skeleton (read-only for code)

- Compare installed versions (Netcode for GameObjects 2.13.3, Unity Transport 2.6.0 per `packages-lock.json`, Lobby 1.1.0, Relay 1.0.5, Unity 6000.6.6f1) with current releases and changelogs. Limit the review to RPC string/byte serialization, `MaxPayloadSize` and fragmentation, RPC attributes (`ServerRpc`/`ClientRpc` versus the newer `Rpc` attribute), and `JsonUtility`. Record findings that affect U2–U4 before designing them. Upgrade nothing without your approval of a specific recommendation.
- Create `Assets/Docs/NETCODE_LEARNING_GUIDE.md` (documentation only), with:
  - **Primer:** server authority, RPC boundaries, idempotent request IDs, recipient-filtered projection, wire budgets, reconnect identity.
  - **Mistakes catalog:** what happened, severity, how it was detected, the fix, and a "do this next time" rule. Seed it with F1–F7 above and F1–F10 from the refactor plan, framed as learnable patterns.
  - **Library changes:** the U0 review.
  - **Change log:** before/after with the reason; filled in by each later unit.

### U1. Measure and confirm behavior (decides U4)

Follow the isolated temporary-project pattern in `Tests/UnityIntegration/README.md`. Keep harness code out of the working project's Assets. Use an Editor `-executeMethod` rather than a player build; no transport is needed to measure.

- Build a worst-case state through the real core: eight players, late round, thirteen results, many placements, full legal-placement lists, event-heavy transition. Project it with `RecipientProjection`.
- Record JSON characters and computed on-wire bytes (UTF-16 string and UTF-8 bytes) for view-only, event-heavy, synchronization and largest-command packets.
- Confirm in Unity how null nested classes, null arrays, unknown fields, missing fields and out-of-range enums round-trip.
- Emit golden JSON fixtures (present and absent variants) into `Tests/GameplayIntegration/Fixtures/` for U5.

Output: a size table with headroom against 6144 and the confirmed decode behavior, recorded in the verification doc and the learning guide.

### U2. Explicit presence and versioning

**Files:** `SessionProtocol.cs`; readers in `ClientSession.cs`, `AuthoritativeMatchCoordinator.ToCommand`/`Fingerprint`, `RecipientProjection`.

- Add presence flags (`HasView`, `HasOutcome`, `HasExpected`, `HasTrain`, `HasRequiredTrain`) set only by builders. Readers check flags instead of null.
- Arrays are always non-null; empty means none.
- Add `WireVersion` to `ProjectionPacket` and `CommandRequest`; a mismatch is rejected with feedback.
- Keep existing field names and order. There is no backward-compatibility requirement because host and clients share a build; the version field makes that explicit.
- `ClientSession.Receive` must ignore a packet without a view for view purposes while still settling its outcome.
- Fingerprinting uses presence flags. A test proves that identical retries still replay and altered payloads still reject.

### U3. Single wire codec with one budget

**Files:** new `Assets/Scripts/Game/Integration/WireCodec.cs` (+ `.meta`); `GameSession.cs`.

- `Encode` and `TryDecode<T>` own: the on-wire byte budget constant (replacing `MaximumCommandCharacters`), try/catch for `ArgumentException`, version and presence validation, and bounded array lengths.
- Outbound: the server checks encoded size before every send. Over budget logs an error and falls through to the U4 strategy; it never silently drops.
- Client decode failure: feedback plus a synchronization request, with a bounded retry count so a persistently bad payload cannot loop.
- The core JSON calls are abstracted behind a delegate so the codec has no hard Netcode dependency; the Unity build supplies `JsonUtility`, and the console projects can link the validation logic.

### U4. Size budget (branches on U1)

If headroom is adequate, record it and set the regression threshold. Otherwise apply these steps in order, stopping when the budget fits:

1. Send UTF-8 bytes instead of a UTF-16 `string` RPC parameter (a `byte[]`, or a small `INetworkSerializable` wrapper). This roughly halves wire size with no DTO change.
2. Compact derivable data (redundant legal placements or counts, repeated train IDs).
3. Chunk synchronization packets with an explicit header (match ID, revision, index, count); the client discards incomplete or superseded sets.

`MaxPayloadSize` in the scene stays unchanged unless you separately approve that edit.

### U5. Tests

- **Unity batch check through the production codec:** every DTO with present and absent values; malformed, truncated, oversized, wrong-type and wrong-version inputs; worst-case size assertion against the budget constant; outbound over-budget path.
- **Console check in `Tests/GameplayIntegration`:** decodes the U1 golden fixtures and runs `WireCodec` validation through `LoopbackRelay`, including the unseated reply, which must settle without installing a view.
- **Unchanged counts:** existing counts in every current check project stay the same; new checks are additive.

### U6. Dead code removal

Repo-wide reference sweep (code, scenes, prefabs, `.meta` GUIDs), then remove `NetworkSerializer.cs` and its `.meta`, plus `JsonContainer.cs`/`Models/DTO` if equally unreferenced, using Unity-aware deletion.

### U7. Docs and task hygiene

- Finish the learning guide's change log for U1–U6.
- Record evidence in `GAMEPLAY_INTEGRATION_VERIFICATION.md`.
- Check NET-002 in `TASKS.md` only when its done criteria are met.
- Refresh `Tests/UnityIntegration/production-source.sha256` and re-run the player matches; otherwise state that the recorded hash no longer matches.

## Outline of remaining Priority 4 work (separate plans)

Each later plan appends its findings and changes to the learning guide.

1. **NET-001 umbrella matrix:** invalid/out-of-turn, stale draw/end-turn, invalid IDs, unauthorized undo, empty boneyard, shared-train capacity, rejected-command recovery, multi-player and host-as-player. Consider MAINT-002 (NUnit consolidation) first.
2. **NET-005:** privacy and announcement fixtures through serialized recipient payloads; both penalty modes; empty-pile penalty; competing/stale/self/expired challenges. Pairs with VIEW-004.
3. **NET-006:** delay/drop/duplicate/reorder fixtures on `LoopbackRelay` with the settled codec (and chunking if U4 adds it); revision gaps across undo; round end and suspension while presentation is queued. MAINT-001 when needed.
4. **NET-003:** review Relay/Lobby/authentication, local transport and shutdown against installed packages, informed by U0. Record local and live-service evidence separately.
5. **NET-004:** reconnect via `SeatRegistry` rebinding with reauthentication, snapshot restore, pending-request resolution and impersonation rejection.

## Verification contract

- From `RulesEngine/`, run the core restore/build/test, plus the `Tests/GameplayIntegration`, `Tests/StateMachine` and `Tests/ClientInteraction` commands from [integration verification](../GAMEPLAY_INTEGRATION_VERIFICATION.md). Existing counts stay unchanged; new checks are additive.
- `python3 RulesEngine/verify_unity_compile.py`; strict state-map comparison (no topology change expected).
- Unity batch wire check and compile of changed sources where licensing permits; record the specific blocker if not.
- Re-run the `Tests/UnityIntegration` player matches, or record the stale hash.
- Keep testing silent: batchmode `-nographics`; apply the [testing audio mute plan](2026-10-18-disable-sound-during-testing-plan.md) before any Editor Play Mode check.
- Report static inspection, console checks, Unity CLI and live-service evidence separately. No scene/package/GUID change; `git diff --check` passes.

## Risks

- `JsonUtility` decode behavior is unconfirmed until U1.
- Chunking, if required, adds ordering and reassembly rules that NET-006 must fault-test.
- Switching the RPC parameter type changes generated RPC code. The player harness must be re-run to confirm it.
- Batch checks rely on the free Personal license staying activated (Unity Hub/CLI sign-in); if a run fails for license reasons, stop and report rather than work around it.

## Definition of done

- U0–U7 are implemented.
- Round-trip, malformed-input, version and size checks pass through the production codec.
- One on-wire budget governs inbound and outbound limits, with worst-case headroom recorded.
- The unseated reply no longer installs a view.
- The dead serializer is removed.
- The learning guide and library review are written, and prior checks are unchanged.
- NET-002 is checked only with evidence. No other Priority 4 task is closed by this slice.
