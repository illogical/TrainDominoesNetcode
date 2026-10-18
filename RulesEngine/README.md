# TrainDominoesRulesEngine

A standalone, deterministic Puremco Mexican Train rules library. The runtime targets .NET Standard 2.1 and C# 9, has no NuGet/runtime dependencies, and references no Unity or Netcode APIs. Unity compiles the same sources through a dedicated assembly definition. The existing game's live mutation path has **not** been switched to this library.

Behavioral authority: [rulebook](../Assets/Docs/TRAIN_DOMINOES_RULEBOOK.md). Build/integration sequence: [implementation plan](../Assets/Docs/RULES_ENGINE_IMPLEMENTATION_PLAN.md). Evidence and limits: [verification report](../Assets/Docs/RULES_ENGINE_VERIFICATION.md).

## Build and test

Install .NET SDK 10.0.402 (the nested `global.json` allows newer patches in that feature band). From the repository root:

```sh
cd RulesEngine
dotnet restore TrainDominoesRulesEngine.slnx
dotnet build TrainDominoesRulesEngine.slnx --no-restore --disable-build-servers -p:UseSharedCompilation=false
dotnet test TrainDominoesRulesEngine.Tests/TrainDominoesRulesEngine.Tests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false
```

Restore needs NuGet access. Tests pin NUnit 4.4.0, NUnit3TestAdapter 5.2.0, and Microsoft.NET.Test.Sdk 18.10.1. They use actual runtime sources, not Unity stubs or a second rule implementation. Build/test outputs remain outside Assets.

Optional installed-Unity compiler check, from the repository root:

```sh
python3 RulesEngine/verify_unity_compile.py
```

The script uses the editor version in `ProjectVersion.txt`, or accepts `--editor /path/to/Unity.app`. It compiles into a temporary directory with C# 9, warnings as errors, and only Unity's .NET Standard 2.1 reference assemblies. This command alone does not import the project or run Play Mode.

## Use from another host

Reference `TrainDominoesRulesEngine/TrainDominoesRulesEngine.csproj` from a .NET host. Its source include points at `Assets/Scripts/RulesEngine/**/*.cs`; the Assets location is only a Unity packaging convention. Another engine can reference the built library, or bring the same `.cs` sources into its own build. It needs neither Unity nor this repository's existing models/managers. Do not also import the external DLL into this Unity project: the assembly definition already compiles those types.

One match is owned by one authoritative coordinator. Authenticate requests and serialize calls before invoking the engine. Supply globally fresh match IDs, stable participant IDs, initial starting seat, and explicit shuffled deck orders. Randomization belongs to the host. A deck contains every tile except that round's engine exactly once, with index zero consumed first. Initial dealing is round-robin in fixed seat order.

```csharp
using System.Linq;
using TrainDominoesRulesEngine;

var players = new[] { new PlayerId(10), new PlayerId(20) };
var match = RulesEngine.CreateMatch("host-generated-unique-match-id", players, startingSeat: 0);
foreach (var player in players)
    match = RulesEngine.Apply(match, player, new Ready(match.Token)).State;

// Example order only: shuffle this complete permutation in a real host.
var deck = DominoCatalog.All
    .Where(tile => tile.Id != DominoCatalog.EngineForRound(0))
    .Select(tile => tile.Id).ToArray();
var setup = RulesEngine.StartRound(match, match.Token, deck);
if (!setup.Accepted) throw new System.InvalidOperationException(setup.Error.ToString());
match = setup.State;

var actor = match.Round.Turn.Actor;
var choices = RulesEngine.GetLegalPlacements(match, actor);
if (choices.Count > 0)
{
    var move = choices[0];
    var result = RulesEngine.Apply(match, actor, new Place(match.Token, move.DominoId, move.Train));
    if (result.Accepted) match = result.State;
}
var view = RulesEngine.GetPlayerView(match, actor);
```

`MatchState` is a full host-only snapshot. Apply returns a new snapshot on change; previous snapshots remain usable and unchanged. Collections are read-only. Illegal commands return `RuleError` with the original state and no events. Null state/invalid construction arguments are caller programming errors. Internal invalid invariants throw rather than pretending to be legal player outcomes.

The profile identifier is `PuremcoWeb-App-v1`; expose it with match settings when describing a session. The core implements one selected Puremco profile, not a configurable framework of arbitrary domino games.

## Host contract

| API / command | Responsibility |
| --- | --- |
| `CreateMatch` | Validate 2–8 unique seats; create a fresh readiness gate |
| `Ready` | Set readiness once per participant; all-ready changes phase to AwaitingDeck |
| `StartRound` | Trusted coordinator supplies the next complete 90-tile permutation only when AwaitingDeck |
| `Place`, `Draw`, `Pass` | Active actor only; validate tile ownership, train, requirement, access, and bounded draws |
| `EndTurn`, `Undo` | Draft mode only; permitted by current requirement/checkpoints |
| `AnnounceOneTile` | Participant may announce a publicly irreversible one-tile hand, including between turns |
| `ChallengeMissedAnnouncement` | An opponent challenges an open omission window before the next accepted play/draw/pass |
| `SetSuspended` | Trusted coordinator blocks progression; resume only after usable host synchronization |
| `CreateNewMatch` | Trusted coordinator replaces an ended or suspended match, with fresh identity, readiness, scores, and optionally remaining seats |

Every command includes `PositionToken` (match, round, turn, revision). Transport adapters reconstruct the **submitted** token using its public value constructor; never replace it with the latest server token before validation. Stale commands reject without mutation. Reconstruct `PlayerId`/`TrainId` through their public constructors/factories rather than treating a layout index or transport client ID as a train.

The host keeps request IDs and an outcome cache. Retries return the cached response instead of applying a new command. Revision checks prevent stale effects, but the library is not a transport retry/delivery manager. Dispatch effects once per accepted revision/event index. Acknowledgment loss is uncertain delivery, not permission to roll back a draw.

Only send `GetPlayerView` and `GetEventsForPlayer` results to a participant. Never broadcast `TransitionResult.State` or the unfiltered event list. Private draw and draft events identify their audience. Public events do not expose hidden drawn tile IDs. Views include the requesting player's hand, legal placements, settings, allowed actions, public counts/trains/requirements, readiness, and results. There is no boneyard order or opponent-hand collection in a player view.

## Match-fixed settings

- `CommitmentMode.Immediate` (default): each placement is public and irreversible. A completed requirement automatically advances the turn or ends the round. Failed-play outcomes use an explicit permitted Pass. Draw itself does not automatically pass.
- `CommitmentMode.Draft`: unsealed placements and marker effects are private to the actor. End Turn seals and commits a complete draft. A legal Pass seals an unsuccessful turn. Draw seals earlier placements, clears their undo history, and permanently removes its tile from the boneyard. Undo restores only the last unsealed placement and cannot renew a draw attempt. Provisional final tiles do not score before commitment.
- `AnnouncementPenalty.DrawOneTile` (default): successful opponent challenge draws one tile for the offender if available, with no immediate-play obligation or consumption of an ordinary draw attempt. Empty pile records the infraction without a substitute penalty.
- `AnnouncementPenalty.None`: record and close a successful challenge without punishment.

The one-tile notification is manual. Useful-action queries expose `CanAnnounce`; the later GUI must still keep the announcement button available and show harmless `AnnouncementNotRequired` feedback on unnecessary clicks. There is no timer. A completed one-tile turn opens its challenge window unless already announced. Announcing or one successful challenge closes it; the next accepted play, draw, or pass expires it. Rejected commands do not expire it. Host ordering resolves races. Private draft previews do not create announcement obligations. Going out within a turn ends the round first.

An empty boneyard ends a blocked round only when no placement is possible even after future mandatory passes open the remaining private trains. This analysis retains an inherited-double restriction and does not actually change any saved marker. Scores are remaining pips (blank zero); final lowest totals may yield joint winners.

## Integration still required

The API is ready for a Unity or other .NET host. This delivery does not implement DTO serialization, RPC authentication/deduplication, UI controls, settings GUI, reconnection transport, persistence, or production bots. See ENG-006/STATE-003/NET-001 in `TASKS.md`. Keep the existing legacy authorities until that controlled switch, then remove replaced mutations so the live session has a single rule authority.
