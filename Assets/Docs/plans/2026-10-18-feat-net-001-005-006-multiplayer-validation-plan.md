---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implemented
product_contract_source: ce-plan-bootstrap
execution: code
title: Validate commands, privacy and delivery faults (NET-001, NET-005, NET-006)
type: feat
date: 2026-10-18
origin: Assets/Docs/plans/2026-10-18-feat-net-002-gameplay-wire-contract-plan.md
---

# Validate commands, privacy and delivery faults (NET-001, NET-005, NET-006)

**Authored context:** Written and implemented in the same pass on 2026-10-18, at the user's request ("implement directly"), after NET-002 settled the wire contract. This file records the approach and its outcome together. Evidence: [verification report](../GAMEPLAY_INTEGRATION_VERIFICATION.md#net-001-net-005-net-006-validation-2026-10-18). Lessons: [netcode learning guide](../NETCODE_LEARNING_GUIDE.md).

## Approach

These tasks validate existing behavior; they add no rules. Production gameplay code was not changed for them. (The separately requested testing mute changed `SoundManager`; see the [sound plan](2026-10-18-disable-sound-during-testing-plan.md).)

- **Console layer (`Tests/GameplayIntegration`).** Real `ClientSession`s and the real `AuthoritativeMatchCoordinator` are connected through `LoopbackRelay` and the production `WireCodec`.
  - `RelayScenario` plays seeded matches. Fixtures search fixed seeds for the situation they need (an open challenge window, an empty boneyard, a provisional double that must be followed by a draw), so they are deterministic without hand-built decks.
  - Every delivered payload is checked for privacy: no other player's provisional tile, no other player's private event, no tile another player holds, own hand matching the public count, and all 90 tiles accounted for.
- **Real-transport layer (`Tests/UnityIntegration`).** Every CLI scenario now applies the same privacy invariants to each view it receives. New `--challenge` and `--challenge --penalty-none` scenarios play full matches without announcements and challenge over real Netcode RPCs.

## Units

### U1. NET-001 adversarial matrix (`AdversarialChecks.cs`)

Each command is sent raw through the codec, bypassing `ClientSession` gating, and every rejection must leave every seat's projection byte-identical.

- **Out-of-turn:** every kind of command from every non-active seat, at 2, 3 and 8 players.
- **Stale and repeated:** a repeated draw with a fresh token and with the old token; other match identities and future revisions; a repeated end turn, both as an identical retry (replayed, no effect) and as a new request.
- **Invalid IDs:** unknown dominoes, another player's tile, the engine, unknown trains, a missing train, a missing position, unknown or self challenge targets, empty or overlong request IDs, and an unseated sender.
- **Unauthorized undo:** in Immediate mode, with nothing to undo, and by another seat.
- **Empty boneyard:** draw is rejected and pass is accepted.
- **Shared train:** exactly one shared train and one personal train per seat in every delivered view; a shared placement that names an owner still lands on the one shared train.
- **Recovery after rejection:** a stale client is rejected, catches up from the rejection reply, resynchronizes and acts again.
- **Convergence:** full 13-round matches (2p Immediate, 4p Draft, 8p Immediate) converge after every step.

### U2. NET-005 privacy and announcements (`PrivacyAnnouncementChecks.cs` and the CLI challenge scenarios)

- **Draft privacy:** draft placement, undo and re-placement are invisible to observers until commit.
- **Draw sealing:** a draw seals a provisional double publicly while the drawn tile stays private.
- **Competing challenges:** two challenges at once; exactly one wins and the penalty applies once, privately to the target, without using the active player's draw allowance.
- **Rejected challenges:** self, repeated and expired challenges are rejected.
- **Penalty settings:** the None penalty, and an empty-pile penalty with no substitute tile.
- **Announcements:** an announcement during the window closes it and prevents the challenge.
- **Real transport:** two-player challenge, penalty, self, repeat and expiry cases, with both penalty settings.

### U3. NET-006 delivery faults (`DeliveryFaultChecks.cs`)

- **Commands:** a delayed command; a dropped command retried after uncertainty.
- **Outcomes:** a dropped outcome recovered by retry (cached replay) and by synchronization (pending resubmission).
- **Duplicates and reordering:** triplicated projections; projections from six accepted steps reversed.
- **Draft gap:** place, undo and replace on another train while the owner misses the middle revisions, which forces a reconciling presentation.
- **Slow presenter:** the round ends while one peer's animations lag; an old generation does not release input, and scores are recorded once.
- **Suspension:** a command arrives late during suspension, and controls recover after resume.
- **Duplicated penalty:** the penalty packet is presented once.
- **Every case** ends with convergence, a "presented once" check, and no privacy violations.

## Outcome

- **Results:** all pass.
  - Console: NET-001 5,603 assertions (mostly per-step convergence), NET-005 79, NET-006 75.
  - CLI: `challenge` won 16 challenges and the target received 16 private penalty tiles; `challenge-none` won 11 challenges with no tiles.
  - The existing scenarios checked privacy on 500–990 views per player.
- **Mutation check:** removing the audience filter from `RecipientProjection.EventsFor`, or letting `ClientSession.Receive` accept older revisions, makes the suites fail. The mutations were reverted.
- **No production defect was found.** Three test-design corrections were needed, recorded as lessons in the learning guide:
  - A rejection reply deliberately carries the sender's current view.
  - A private draft legitimately changes the owner's view of its own train marker.
  - Real two-player concurrency means an "expired challenge" probe can be overtaken by the opponent's move; such runs are reported as inconclusive, never as a pass.
- **MAINT-001 was not needed.** NET-006 did not require separating request tracking from `ClientSession`.

## Not covered

- **Live services:** Relay/Lobby behavior (NET-003).
- **Reconnect:** reconnection with a new transport ID (NET-004).
- **Simulated only:** packet loss below the reliable transport layer.
- **Console only:** competing challenges with three or more players and empty-pile penalties, because the CLI harness is two-player.
- **Not reviewed by a person:** how feedback looks to players (VIEW-004).
