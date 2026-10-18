---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implementation-ready
product_contract_source: ce-plan-bootstrap
execution: code
title: Define and test the gameplay wire contract (NET-002) and outline Priority 4
type: feat
date: 2026-10-18
origin: Assets/Docs/plans/2026-10-18-refactor-pre-priority-4-network-readiness-plan.md
---

# Define and test the gameplay wire contract (NET-002) and outline Priority 4

**Authored context:** Saved on 2026-10-18 after static reading of `SessionProtocol.cs`, `GameSession.cs`, `AuthoritativeMatchCoordinator.cs`, the Unity CLI harness layout and the scene's transport settings. This document specifies planned work; it does not assert implementation or verification. Task IDs (NET-001…NET-006, MAINT-001/002) are unchanged; U identifiers below are local to this plan.

**Coordination:** Another session owns the [pre-Priority-4 refactor plan](2026-10-18-refactor-pre-priority-4-network-readiness-plan.md) (seat registry, `RecipientProjection`, `LoopbackRelay`). This plan edits some of the same files (`SessionProtocol.cs`, `GameSession.cs`, `AuthoritativeMatchCoordinator.cs`). Implementation starts only after that work is merged or handed off.

**Scope:** NET-002 is planned in implementation depth because it gates the rest of Priority 4. NET-001/005/006/003/004 appear as an ordered outline; each gets its own plan later.

## Decisions (agreed 2026-10-18)

- Keep `JsonUtility` and the JSON DTOs; make absent values explicit rather than adopting a new codec or package.
- Whether the worst-case payload fits is decided by measurement (U1) before any compaction, splitting or scene change.
- Packages are not upgraded incidentally. A library currency review documents what would need to change (U6).
- A learning guide is a deliverable (U7), for a developer new to netcode.

## Findings this plan acts on

- F1. `JsonUtility` cannot represent null nested `[Serializable]` classes (*verify in U1*). Affected fields: `PlayerViewDto.RequiredTrain`, `ProjectionPacket.View`/`Outcome`, `CommandRequest.Train`/`Expected`, `CommandOutcome.Position`. A reply to an unseated sender carries no view, and `ClientSession.Receive` could install a blank one.
- F2. The console checks use `System.Text.Json` or in-memory objects, so the production codec is not exercised there.
- F3. `GameScene.unity` sets UnityTransport `m_MaxPayloadSize: 6144`. A verbose late-round eight-player view may exceed it (*verify*).
- F4. `Assets/Scripts/Models/NetworkSerializer.cs` (BinaryFormatter) has no code references; `Models/JsonContainer.cs` and `Models/DTO` need a reference check.
- F5. Decode paths are inconsistent: `TryRead` guards command input, but `ProjectionClientRpc` calls `JsonUtility.FromJson` unguarded.

## Requirements

- R1. Absent values survive encode/decode and are never confused with default instances.
- R2. Malformed, truncated, oversized or wrong-typed input is rejected without throwing on either side.
- R3. The worst-case payload fits the transport limit with recorded headroom, and a regression threshold exists.
- R4. No rule, routing or semantic change; DTO changes are additive.
- R5. The production codec is exercised by a real Unity check; console checks assert shape compatibility.
- R6. No scene, prefab, package or GUID change unless separately approved.

## Implementation units

### U1. Measure and confirm behavior (decides U4)

**Files:** new Unity batch method beside `Tests/UnityIntegration/IntegrationBuild.cs` pattern (path chosen at implementation; production-source-linked like the existing harness).

Build a worst-case state through the real core (eight players, late round, thirteen results, many placements, full legal-placement lists), project with `RecipientProjection`/`PlayerViewDto.From`, and serialize with `JsonUtility`. Record byte sizes for view-only, event-heavy and synchronization packets. Also confirm, in Unity, what a null nested class and missing array become after a round trip. Output: a recorded size table with headroom against 6144 and the confirmed null behavior.

### U2. Explicit presence contract

**Files:** `SessionProtocol.cs`; readers in `ClientSession.cs` and `AuthoritativeMatchCoordinator.ToCommand`.

Add presence flags (`HasView`, `HasOutcome`, `HasExpected`, `HasTrain`, `HasRequiredTrain`) or an empty-array convention, set by the builders and checked by readers instead of null checks. Existing field names/order stay; flags are additive so older payloads default to "absent". An unseated reply must never install a blank view. Update fingerprinting to use presence consistently.

### U3. Single wire codec helper

**Files:** new `Assets/Scripts/Game/Integration/WireCodec.cs` (+ `.meta`); `GameSession.cs`.

Replace the inline `JsonUtility.ToJson`/`FromJson` calls with `Encode` and `TryDecode<T>`: length cap, try/catch for `ArgumentException`, presence validation, bounded array sizes. A decode failure on the client becomes feedback and a synchronization request, not an exception. The codec has no Netcode dependency so the console projects can link it.

### U4. Size budget (branches on U1)

If headroom is adequate, record it and add a regression threshold to the check. Otherwise, in order: compact payloads (drop derivable data such as redundant legal placements or counts, avoid empty arrays), then split synchronization into chunks with an explicit chunk header. `MaxPayloadSize` in the scene is unchanged unless you separately approve that edit.

### U5. Tests

- Unity batchmode round-trip check through the production codec: every DTO with present and absent values, malformed/truncated/oversized/wrong-type strings, worst-case size assertion.
- Console shape-compat check in `Tests/GameplayIntegration` using the same DTO sources and `LoopbackRelay`: presence rules and unseated-reply handling.
- Existing counts in all current check projects remain unchanged.

### U6. Cleanup and library currency review

- Repo-wide reference sweep (code, scenes, prefabs, `.meta` GUIDs), then remove `NetworkSerializer.cs` and its `.meta` with Unity-aware means.
- Compare installed versions in `Packages/manifest.json` (Netcode for GameObjects 2.13.3, Unity Transport, Relay/Lobby/Authentication, Unity 6000.6.6f1) with current releases and changelogs, limited to changes touching RPC serialization, `JsonUtility`, `MaxPayloadSize`/fragmentation and RPC attributes. Record the required code changes per library change in the learning guide. Upgrade only on your approval of a specific recommendation.

### U7. Netcode learning guide

**File:** `Assets/Docs/NETCODE_LEARNING_GUIDE.md` (documentation only; written as work lands).

Contents:
- Before/after for each change, with the reason.
- A mistakes catalog: what happened, severity, how it was detected, how it was fixed, and a "do this next time" rule. Seed entries: unused BinaryFormatter serializer; null nested classes under `JsonUtility`; transport ID used as player identity; verbose JSON versus a 6144-byte limit; trusting button state instead of server validation; no delivery-fault testing; presentation events transported but unused; unguarded client decode. Framed as learnable patterns.
- A short primer: server authority, RPC boundaries, idempotent request IDs, recipient-filtered projection, reconnect identity.
- The library-changes section from U6.
- Links to code and tests.
Each later Priority 4 task appends its findings.

### U8. Docs and task hygiene

Record evidence in `GAMEPLAY_INTEGRATION_VERIFICATION.md`. Check NET-002 in `TASKS.md` only when its done criteria are met. Refresh `Tests/UnityIntegration/production-source.sha256` and re-run the player matches, or state that the recorded hash no longer matches.

## Outline of remaining Priority 4 work (separate plans)

1. **NET-001 umbrella matrix:** invalid/out-of-turn, stale draw/end-turn, invalid IDs, unauthorized undo, empty boneyard, shared-train capacity, rejected-command recovery, multi-player and host-as-player. Consider MAINT-002 (NUnit consolidation) first.
2. **NET-005:** privacy and announcement fixtures through serialized recipient payloads; both penalty modes; empty-pile penalty; competing/stale/self/expired challenges. Pairs with VIEW-004.
3. **NET-006:** delay/drop/duplicate/reorder fixtures on `LoopbackRelay` using the settled codec; revision gaps across undo; round end and suspension while presentation is queued. MAINT-001 when needed.
4. **NET-003:** review Relay/Lobby/authentication, local transport and shutdown paths against installed packages; local and live-service evidence recorded separately.
5. **NET-004:** reconnect via `SeatRegistry` rebinding with reauthentication, snapshot restore, pending-request resolution, impersonation rejection.

## Verification contract

- From `RulesEngine/`: core restore/build/test, plus the `Tests/GameplayIntegration`, `Tests/StateMachine` and `Tests/ClientInteraction` commands from [integration verification](../GAMEPLAY_INTEGRATION_VERIFICATION.md). Existing counts unchanged; new checks additive.
- `python3 RulesEngine/verify_unity_compile.py`; strict state-map comparison (no topology change expected).
- Unity batchmode wire check and compile of changed sources where licensing permits; record the specific blocker if not.
- Re-run `Tests/UnityIntegration` player matches (or record the stale hash).
- Keep testing silent: batchmode `-nographics`; apply the [testing audio mute plan](2026-10-18-disable-sound-during-testing-plan.md) before any Editor Play Mode check.
- Report static inspection, console checks, Unity CLI and live-service evidence separately. No scene/package/GUID change; `git diff --check` passes.

## Risks

- JsonUtility null behavior is unconfirmed until U1.
- Splitting synchronization, if required, adds a message type and ordering rules.
- Batchmode needs a usable Unity license.
- File conflicts with the concurrent refactor session until it lands.

## Definition of done

U1–U8 implemented; round-trip, malformed-input and size checks pass through the production codec; worst-case headroom recorded; dead serializer removed; learning guide and library review written; prior checks unchanged; NET-002 checked only with evidence. No other Priority 4 task is closed by this slice.
