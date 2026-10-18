---
artifact_contract: "ce-handoff/v1"
type: "session-handoff"
status: "stable"
created_at: "2026-10-18T03:47:53Z"
title: "Integrate the standalone rules engine into Unity gameplay"
summary: "Portable core is implemented and verified; continue with runtime characterization and the authoritative Unity adapter/state integration."
keywords: ["TrainDominoesNetcode", "Puremco", "rules-engine", "Unity", "ENG-006", "STATE-002"]
cwd: "/Users/matt/dev/projects/TrainDominoesNetcode"
resume_focus: "Continue integrating the rules engine into gameplay, starting with STATE-002 and the U6 adapter/state boundary."
repository: "TrainDominoesNetcode"
repo_root_sha: "92a0cc508c62923b175b7ced48cc0bb801485153"
branch: "fix/relay-tester-unity6"
head: "0eec4e56dd974d84a02d2b33f7aa198484f180e8"
---

# Rules engine → Unity gameplay handoff

## Objective and captured state

The user requested this handoff to start a fresh session continuing integration of the standalone rules engine into gameplay. They previously approved implementing the standalone core first, then integrating it separately. This file captures context; the receiving session's current user instruction and repository guidance determine execution authority.

At capture, HEAD is `0eec4e5` (`Implement TrainDominoesRulesEngine with initial rules and tests`) on `fix/relay-tester-unity6`. The working tree was clean before creating this handoff. Core code, tests, updated policies, and TODOs are in that commit. No in-progress gameplay integration or partially edited adapter exists. Recheck branch, status, source, and scene settings before relying on this snapshot; do not discard subsequent user work.

**The portable core is complete for the accepted scope. Live Unity gameplay still uses the legacy managers/RPCs and interaction states.** A successful core test or temporary Unity import does not mean the main game scene implements Puremco yet.

## Read these sources for the next decision

All source locations below are repository-relative; links resolve from this handoff.

| Reference | What matters |
| --- | --- |
| [AGENTS.md](../../../AGENTS.md) | Source/state workflow, server authority, metadata/scene care, and verification limits |
| [TASKS.md](../../../TASKS.md) | STATE-001/RULES-001 and ENG-001–005/007 are complete; STATE-002, RULES-002–004, ENG-006, STATE-003/004 and NET-001 remain open |
| [Rulebook](../TRAIN_DOMINOES_RULEBOOK.md) | DEC-001–008 and R-001–018: selected Puremco edition plus user-reviewed software policies; do not restore superseded variants |
| [Engine implementation plan](../RULES_ENGINE_IMPLEMENTATION_PLAN.md) | U6 is the integration unit; U1–U5/U7 define the existing core. Preserve stable unit/task IDs |
| [Developer guide](../../../RulesEngine/README.md) | Actual API, host responsibilities, match settings, deck order, request tokens, view/event privacy and runnable commands |
| [Verification report](../RULES_ENGINE_VERIFICATION.md) | Exact prior evidence, two fixed review regressions, and what has never been verified |
| [Player-state matrix](../PLAYER_STATE_REVIEW.md) | Agreed distinction between authoritative conditions, turn requirements and local interaction modes |
| [Anticipated states](../ANTICIPATED_PLAYER_STATES.md) | Selected seven normal modes plus SessionSuspended; Acting replaces Turn Started/Made Move; shared result/pending routing and mapper coverage caveat |
| [Current flow](../PlayerStateFlow/current.md) | Existing legacy topology. Regenerate it from source before changing states |
| [State debugging](../STATE_MACHINE_DEBUGGING.md) | Existing source-linked state checks, diagnostics and lifecycle/subscription semantics |
| [Player-state workflow skill](../../../.agents/skills/player-state-workflow/SKILL.md) | Mandatory source-derived before/proposed-after diagrams and actual-after comparison for state/wiring changes |

## User decisions already settled

These were reviewed with the user in this session; preserve them rather than reopening variant choices without concrete evidence.

- Puremco web-edition baseline, double twelve, 2–8 fixed seats, thirteen engines descending 12→0, one personal train per player plus exactly one shared Mexican Train. Sequential play from the beginning; no simultaneous opening/group submission.
- Additional play after a double may be on the double or another eligible train. An inherited open double restricts subsequent players, including access to a normally private train. A second double needs a held third tile covering one of the two on different eligible trains; no third double or draw for that cover. Final-tile termination takes precedence.
- One draw attempt per distinct play requirement. A drawn double can create a new follow-up draw opportunity; undo cannot renew attempts.
- **Immediate** placement is the default: accepted placement is public and irreversible; a completed requirement automatically advances the turn or ends the round. A legal Pass explicitly ends an unsuccessful turn. No ordinary End Turn/Undo in this mode.
- Optional **Draft** mode is fixed before a match. Unsealed placements are private, LIFO undo is allowed, and End Turn commits a complete draft. Draw seals/publishes earlier placements and clears their undo history. Drawn tiles never return to the boneyard. A provisional final tile ends the round only at commitment.
- Everyone readies before initial play and each next round. Readiness is a set; the coordinator supplies the next shuffled deck after the gate. New matches use fresh readiness.
- Joint lowest-score players are joint winners. No tiebreaker is enabled. The user asked for an optional future tiebreaker-design TODO.
- Disconnect suspends the match. Resume only after usable synchronization. A host may abandon the suspended match and create a fresh match with 2–8 remaining players, fresh scores, identity and readiness; do not remove seats from an active match. Detailed reconnect/abandonment UI remains deferred.
- One-tile announcement is **manual**, with an always-available button and harmless feedback when unnecessary. Announce and Challenge are possible while observing another turn, not only while acting.
- A completed unannounced one-tile turn opens a challenge window. Any opponent can catch it before the next player's first accepted play, draw or pass. Announcement or one successful challenge closes the window; rejected commands do not expire it. Host command order decides races; there is no timer or automatic punishment.
- Default missed-announcement penalty is **DrawOneTile**, fixed per match; **None** is also supported. A penalty tile is private and has no immediate-play obligation or effect on ordinary draw attempts. Empty boneyard: record the infraction, close the opportunity, no substitute punishment. Private draft previews do not create announcement obligations; going out within a turn ends the round first.
- Settings GUI is a late-priority task, after rule/state/network correctness and final art. Do not build it incidentally during U6. The core already accepts match-fixed settings.

## Existing core and adapter boundary

Runtime sources are [Assets/Scripts/RulesEngine](../../Scripts/RulesEngine), compiled through `TrainDominoesRulesEngine.asmdef` and a source-linked external library. Namespace/assembly is `TrainDominoesRulesEngine`; target is .NET Standard 2.1 / supported C# 9. No Unity, Netcode, UI, animation, transport, persistence or randomization dependencies exist in the core.

Key entrypoints and integration implications:

- `RulesEngine.CreateMatch`, `StartRound`, `CreateNewMatch`, `SetSuspended`: trusted coordinator APIs. Supply unique match IDs, stable participant IDs and explicit shuffled deck permutations. The host selects initial starter; round rotation is in the core. Deck index zero is consumed first; dealing is round-robin in seat order.
- `Apply(state, authenticatedActor, command)`: returns acceptance/error, next immutable snapshot, events. Rejections return the input unchanged. Active-turn commands are Place/Draw/Pass/EndTurn/Undo; Ready/Announce/Challenge have their own permitted phases/actors.
- `PlayerId` is independent of `ulong` Netcode IDs; `TrainId` is explicit Personal(owner)/Shared, not a mesh/station index. Preserve existing domino IDs and presentation mapping.
- `PositionToken` contains match, round, turn, revision and has a public value constructor. An incoming adapter must reconstruct the **client-submitted** token. Replacing it with the latest server token would bypass stale-command validation.
- The host must authenticate from RPC sender metadata, serialize submissions, and cache outcomes by participant/request ID. A retry returns its original response. Core revision checks prevent stale effects but do not implement delivery correlation or transport deduplication.
- `MatchState` and `TransitionResult.State` are **host-only**. Send `GetPlayerView` and `GetEventsForPlayer` projections. Do not broadcast raw snapshots/unfiltered events. Opponents must never receive hand IDs, boneyard order, unsealed draft IDs or private draw events.
- `GetAllowedActions`/`GetLegalPlacements` derive permissions from the same policy as validation. Buttons display permissions; they are not authority. The announcement button's always-available UX still needs harmless unnecessary-action feedback.
- Pure transition results precede optional animation completion. Publish current per-player views/tokens and deduplicate presentation effects; stale/duplicate acknowledgments cannot revive an old turn/result phase. Unknown delivery means reconcile, not roll back an irreversible draw.

Two review bugs already fixed: blocked-round analysis now considers private destinations that later mandatory passes can open, retaining inherited-double restrictions without changing saved markers; token construction is public for external hosts. Their reproductions are in `RegressionTests.cs`.

## Gameplay still awaiting the controlled switch

Source starting points:

- [GameSession.cs](../../Scripts/GameSession.cs): current RPC orchestration for join, initial/manual draw, selection/placement/undo, group/individual end-turn, scores/readiness/reset. This becomes the authoritative adapter, not a second rules implementation.
- [GameplayManager.cs](../../Scripts/Game/GameplayManager.cs): mixed rule/presentation calls and event bridge; keep rendering/input/audio concerns, remove replaced acceptance logic after the switch.
- [DominoTracker.cs](../../Scripts/Game/DominoTracker.cs), [TurnManager.cs](../../Scripts/Game/TurnManager.cs), [RoundManager.cs](../../Scripts/Game/RoundManager.cs): legacy catalog/hands/boneyard/station, group/turn flags and scores. These still own live mutations today. Avoid interleaving their mutations with core mutations; migrate to projections/remove replaced authorities at a controlled boundary.
- [States](../../Scripts/Game/States): existing eight concrete classes and 21 expanded transitions. The unchanged source hash at verification was `c9953e65a6c184b49351329c1db4999e4cd92bc43ef7000008d75ac51338dda6`.
- [DTOs](../../Scripts/Models/DTO): existing payload semantics can be retained where compatible, but explicit shared train, revision/request correlation, filtered views, obligations, blocked result and winner lists need focused additions. NET-002 separately tracks BinaryFormatter replacement.

Selected future responsibilities: Pregame, RoundInitializing, AwaitingTurn, Acting, ActionPending, RoundOver, GameOver, plus SessionSuspended. No extra state is needed for doubles, drawing, selection, announcements/challenges or the two commitment modes. Shared authoritative update routing must cover terminal/suspension outcomes from initialization, acting, waiting and pending. Install pending identity/handlers before transport can reply immediately on a host. If transitions move into a coordinator, extend the mapper and its tests before accepting a sparse graph. Preserve factory cache/fresh restart/self re-entry semantics and balanced subscriptions unless deliberately changed and tested.

## Continuation sequence and remaining proof

The recommended sequence follows existing task dependencies; it is not a claim that any of these steps is already done.

1. Recheck source/settings and establish **STATE-002**'s reproducible local host/client characterization of the existing startup/deal, legacy group submission, ordinary placement/undo/draw/end, scoring and next-round paths. Label legacy defects versus selected target behavior. No live Lobby/Relay testing is required for the initial local baseline.
2. Regenerate the current state map and prepare the U6 before/proposed-after preview, shared exits, pending/rejection behavior and test ramifications using the repository skill. The preview is a communication requirement, not a new approval gate when integration is authorized.
3. Build adapter/DTO and request-correlation tests, then connect the core through one controlled server mutation boundary. Retire group opening, support both commitment modes, and remove replaced rule authorities once the core owns them. Keep GUIDs and serialized wiring intact.
4. Integrate the selected local modes, filtered public/private views, one-tile Announce/Challenge controls from Acting and Waiting, result lists/reasons, readiness/reset, suspension and fresh-roster new matches. Settings GUI and detailed reconnect UX remain deferred.
5. Verify real RPC sender validation, stale/repeated requests, host immediate replies, uncertain delivery/rejections, private draw/draft filtering, exact-once effects/turn/score advancement, blocked results and joint winners. Run actual local host/client checks, including nine logical trains for eight players; report visual layout limitations separately.
6. Compare actual state changes against the preview, refresh the checked-in current flow after source changes, update the matrix/overview and task evidence. Close integration/network tasks only when their own completion criteria are met.

STATE-002 and relevant adapter characterization under RULES-002 remain open. RULES-003/004, ENG-006, STATE-003/004 and NET-001 cannot be closed by core tests alone. SETTINGS-001 and RULES-005 are the later GUI/tiebreaker TODOs already in TASKS.md. Final art and NET-002 serializer replacement are separate work.

## Verification already observed and commands to refresh

Observed in the preceding implementation session (not rerun solely for this handoff):

- 48 NUnit rules tests passed, none skipped; complete thirteen-round deterministic replay at 2/3/8 players in both modes, all 2–8-player setup counts, legality/query agreement, rejection/undo/privacy/lifecycle/announcement checks.
- Standalone solution build passed with warnings as errors: zero warnings/errors.
- Unity 6000.6.6f1 compiler check passed with C# 9 and only .NET Standard references.
- Actual Unity batch import in a **temporary minimal project** passed. A non-test consumer initialized/dealt a round and reconstructed a token; imported runtime referenced only `netstandard`.
- Nine source-linked legacy state checks passed. Strict map comparison found no topology/reaction changes or extractor warnings. Main game states were not edited during core work.
- A focused independent static review's two findings were fixed with observed red/green regressions. Dedicated `ce-code-review` was unavailable; the verification report records that limit.

Core commands from repository root (the `cd` enforces the nested SDK pin):

```sh
cd RulesEngine
dotnet restore TrainDominoesRulesEngine.slnx
dotnet build TrainDominoesRulesEngine.slnx --no-restore --disable-build-servers -p:UseSharedCompilation=false -warnaserror
dotnet test TrainDominoesRulesEngine.Tests/TrainDominoesRulesEngine.Tests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false
```

Other checks, from repository root:

```sh
python3 RulesEngine/verify_unity_compile.py
dotnet run --project Tests/StateMachine/StateMachine.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
python3 .agents/skills/player-state-workflow/scripts/state_map.py map --repo . --output /tmp/player-state-before --strict
```

Installed .NET SDK was 10.0.402; core .NET Standard 2.1/C# 9, tests .NET 10 with pinned NUnit/adapter/Test SDK packages. Sandboxed compiler processes stalled in the prior environment; the same .NET commands completed with sandbox escalation. Treat that as an environment observation, not a test failure or reason to weaken checks.

**Not verified:** main-scene Play Mode, real host/client DTO/RPC routing/privacy, live services, eight-player presentation, settings GUI, Announce/Challenge buttons or suspension/reconnect UI. Temporary import artifacts/logs and maps are machine-local and ephemeral; no continuation depends on retaining them.

## Environment and repository care

- Unity editor `6000.6.6f1`; installed packages remain unchanged (Netcode 2.13.3). Read the current manifests/settings rather than upgrading them incidentally.
- Rechecked saved `Assets/Scenes/GameScene.unity`: `IgnoreDominoComparisons: 1`, `StartAtRound: 2`, `DominoStartCount: 12`. These are development settings, not target rules. Matching verification needs bypass off in the test setup; do not silently rewrite the saved scene to get a passing result.
- Preserve existing GUIDs/metadata, scene/prefab references, user changes and original art. Keep generated projects/caches out of changes. Do not import the external core DLL alongside the source `.asmdef`.
- This handoff is repository-local, not temporary managed storage. The receiving session needs this checkout/commit (or a transferred copy); it supplements the authoritative docs rather than replacing them.
