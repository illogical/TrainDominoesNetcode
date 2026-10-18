---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implementation-ready
product_contract_source: ce-plan-bootstrap
execution: code
title: Refactor the integration boundary for Priority 4 network validation
type: refactor
date: 2026-10-18
origin: Assets/Docs/plans/2026-10-17-refactor-rules-gameplay-integration-plan.md
---

# Refactor the integration boundary for Priority 4 network validation

**Authored context:** Saved on 2026-10-18 after a static review of the implemented [gameplay integration plan](2026-10-17-refactor-rules-gameplay-integration-plan.md) and its [handoff](../handoffs/2026-10-17-rules-engine-gameplay-integration.md). This document specifies planned work; it does not assert implementation or verification completion. Existing task IDs (NET-001…NET-006) are unchanged; R/U identifiers below are local to this plan.

## Context

The integration matches its ownership table: `AuthoritativeMatchCoordinator` owns the only `MatchState`, `GameSession` authenticates one JSON RPC boundary, `ClientSession` holds the recipient view and one pending request, `PlayerStateCoordinator.Reconcile` is the single routing policy, and `GameplayManager`/`GameplayPresenter` sequence view-diff presentation. The legacy rule owners are removed.

Several properties of that boundary would make Priority 4 fixtures expensive to write now and to rewrite later. This plan makes a small behavior-neutral refactor first, then re-scopes NET-002 so the wire contract is settled before the network fixtures depend on it.

## Review findings

Findings come from static reading on 2026-10-18; items marked *verify* are risks, not observed failures.

**Affect Priority 4**

- F1. **The production wire format is not what the console checks exercise.** `GameSession` uses `JsonUtility`; `Tests/GameplayIntegration` uses `System.Text.Json`, and the other checks pass objects in memory. `JsonUtility` does not represent null nested `[Serializable]` classes (*verify*): `RequiredTrain`, `ProjectionPacket.Outcome`, `CommandRequest.Train` and the null `View` returned to an unseated sender arrive as default instances. `ClientSession.Receive` would install that blank view when it has none.
- F2. **Payload size.** `GameScene.unity` sets UnityTransport `m_MaxPayloadSize: 6144`. A verbose JSON `PlayerViewDto` late in an eight-player round (about ninety placements plus thirteen rounds of eight scores and legal placements) can exceed it (*verify*). The eight-player CLI run covered only a few actions.
- F3. **NET-002's premise is outdated.** `Assets/Scripts/Models/NetworkSerializer.cs` (BinaryFormatter) has no remaining references.
- F4. **Seats are keyed by transport ID.** The coordinator maps `ulong` directly to `PlayerId` and caches outcomes by `ulong`. NET-004 requires a reconnecting participant with a new transport ID to keep its seat and cached retry outcomes.
- F5. **No delivery-fault seam.** NET-006 needs delayed, dropped, duplicated and reordered packets between real `ClientSession` and coordinator sources without Unity.

**Cleanups**

- F6. Presentation events are transported but `GameplayPresenter.Present` ignores them, so announcements, challenges and penalty draws have no feedback (needed by NET-005/VIEW-002).
- F7. `AuthoritativeMatchCoordinator`, `PlayerViewDto.From`, `GameSession` and `GameplayManager` are written as very long single lines, against the integration plan's goal that developers can follow an action. Recipient filtering, the core privacy rule, has no name.
- F8. `ClientSession` combines view store, request tracking, selection, presentation mask, feedback text and lifecycle requests; the integration plan intended a separate pending-request tracker.
- F9. Three console-check projects each have ad-hoc `Check` helpers and their own project files.
- F10. Minor: `ClientSession.Ready()` doubles as the host's Start Match when no view exists; rejection feedback shows raw `RuleError` names.

## Requirements

- R1. Behavior-neutral: no rule, routing, payload-shape, scene, prefab, package or GUID change in U1–U3. Existing checks keep passing unchanged except for test-only additions.
- R2. Seat identity is resolved through one registry that can later rebind a participant to a new transport ID without changing seats or cached outcomes.
- R3. Recipient projection and event filtering are one named, directly testable unit.
- R4. A test-side relay can deliver, hold, drop, duplicate and reorder packets between actual `ClientSession` and coordinator sources.
- R5. Integration sources are readable at normal line lengths, matching `ClientSession` and the state classes.

## Implementation units

### U1. Seat registry (F4; R1–R2)

**Files:** new `Assets/Scripts/Game/Integration/SeatRegistry.cs` (+ new `.meta`); `AuthoritativeMatchCoordinator.cs`; `GameSession.cs`.

Freeze sorted transport IDs into seats exactly as today. Expose seat lookup by transport, transport lookup by seat, and the frozen seat/transport list. Key the outcome cache by `PlayerId`. `GameSession` derives participants from the coordinator rather than its own copy. Do not add rebinding yet; NET-004 owns that behavior and its authentication.

### U2. Recipient projection and readability (F7; R1, R3, R5)

**Files:** new `Assets/Scripts/Game/Integration/RecipientProjection.cs` (+ `.meta`); `AuthoritativeMatchCoordinator.cs`; `SessionProtocol.cs`; `GameSession.cs`; `GameplayManager.cs`.

Move view projection and audience filtering out of `AuthoritativeMatchCoordinator.Packet`. Reformat the listed files with no semantic change. Keep DTO field names and order so JSON payloads are byte-compatible.

### U3. Loopback delivery relay (F5; R4)

**Files:** `Tests/GameplayIntegration/LoopbackRelay.cs`, `Tests/GameplayIntegration/RelayChecks.cs`, `Program.cs`.

Connect several `ClientSession` instances to one coordinator through an encode/decode step (System.Text.Json until NET-002 selects a portable codec). Support immediate delivery plus held queues with release, drop, duplicate and reorder. Add checks that a normal Ready→deal→draw flow converges and that a duplicated command delivery has one effect. Production sources do not change in this unit.

### U4. Documentation and task hygiene

**Files:** `TASKS.md`, `Assets/Docs/GAMEPLAY_INTEGRATION.md`, the handoff front matter, the 2026-10-17 integration plan.

Re-scope NET-002 per F1–F3. Make NET-001 the umbrella that links NET-005/006. Move the fresh-session prompt to Priority 4. Mark the handoff historical and record that the integration plan's PlayMode test files were replaced by `Tests/UnityIntegration`. Record F6, F8–F10 as follow-ups rather than silently expanding tasks.

## Deferred to Priority 4 (not in this slice)

1. **NET-002 (re-scoped), first:** explicit null-safe, size-bounded wire contract; round-trip and malformed-input tests through the production serializer; worst-case eight-player payload measurement; delete `NetworkSerializer.cs` and its `.meta` after a reference check. Swap U3's codec to the selected one.
2. Then NET-001 umbrella → NET-005 → NET-006 → NET-003 → NET-004.
3. F6 with NET-005; F8 when NET-006 needs it; F9 at the start of NET-001 if the matrix grows; F10 with VIEW-002.

## Verification contract

- From `RulesEngine/`, run the core restore/build/test and the `Tests/GameplayIntegration`, `Tests/StateMachine` and `Tests/ClientInteraction` commands in [integration verification](../GAMEPLAY_INTEGRATION_VERIFICATION.md). Existing counts must be unchanged; new relay checks are additive.
- Run `python3 RulesEngine/verify_unity_compile.py` and the strict state map with before/after comparison; no topology change is expected.
- Real Unity compilation of the changed production sources (Unity CLI build or batch import) where licensing permits. Record if blocked.
- Re-running the `Tests/UnityIntegration` player matches is desirable because production sources change; if not run, state that their recorded source hash no longer matches.
- Testing should be silent. CLI `-batchmode -nographics` player runs produce no audio. Before any Editor Play Mode check, apply the [testing audio mute](2026-10-18-disable-sound-during-testing-plan.md) (not yet implemented as of this plan).
- Report static inspection, console checks and Unity/CLI evidence separately. No scene/package/GUID changes; `git diff --check` passes.

## Definition of done

U1–U4 are implemented; all prior checks pass with unchanged results; relay checks pass; payload JSON field names are unchanged; documentation and TASKS.md reflect the re-scoped Priority 4 order. No Priority 4 task is closed by this slice.
