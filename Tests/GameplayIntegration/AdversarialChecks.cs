using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// NET-001 matrix: commands a modified client could send, submitted raw through the wire codec so no
/// ClientSession gating or disabled button helps. Each rejection must leave every seat's projection
/// byte-identical; accepted play must converge across peers. Seat 0 stands for the host-as-player seat:
/// the coordinator treats it like any other seat (the CLI player harness covers the real host process).
/// </summary>
internal static class AdversarialChecks
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

    /// <summary>Submits raw and asserts the expected rejection with no state change anywhere.</summary>
    private static void Rejected(RelayScenario s, ulong peer, CommandRequest request, RuleError expected, string name)
    {
        string before = s.Snapshot();
        var outcome = s.Relay.SubmitRaw(peer, request);
        Check(outcome != null && !outcome.Accepted && outcome.Error == expected, $"{name}: expected {expected}, got {outcome?.Error.ToString() ?? "no outcome"}");
        Check(s.Snapshot() == before, name + ": rejection left every seat's state unchanged");
    }

    public static void Run()
    {
        foreach (int players in new[] { 2, 3, 8 }) OutOfTurn(players);
        Stale();
        InvalidIdentifiers();
        Undo();
        EmptyBoneyard();
        SharedTrain();
        RejectionRecovery();
        Convergence();
        Console.WriteLine("Adversarial commands (NET-001): " + assertions + " assertions passed.");
    }

    private static void OutOfTurn(int players)
    {
        var s = RelayScenario.Start(players, 1, CommitmentMode.Draft);
        ulong active = s.Active;
        foreach (ulong peer in s.Observers)
        {
            int tile = s.HostView(peer).Hand[0];
            var own = TrainIdDto.From(TrainId.Personal(new PlayerId(s.HostView(peer).Player)));
            foreach (var kind in new[] { CommandKind.Draw, CommandKind.Pass, CommandKind.EndTurn, CommandKind.Undo })
                Rejected(s, peer, s.Request(peer, kind), RuleError.NotYourTurn, $"{players}p seat {s.HostView(peer).Player} out-of-turn {kind}");
            Rejected(s, peer, s.Request(peer, CommandKind.Place, tile, own), RuleError.NotYourTurn, $"{players}p out-of-turn Place");
            Rejected(s, peer, s.Request(peer, CommandKind.Place, tile, TrainIdDto.From(TrainId.Shared)), RuleError.NotYourTurn, $"{players}p out-of-turn shared Place");
        }
        Check(s.Converged(out var why), $"{players}p peers still converged after rejections: {why}");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
        Check(s.Relay[active].BoardEnabled, $"{players}p active player still able to act");
    }

    private static void Stale()
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(2, seed),
            x => x.HostView(x.Active).Allowed.CanDraw);
        Check(s != null, "found a position where the active player must draw");
        ulong active = s.Active;
        var old = s.HostView(active).Token;
        Check(s.Relay.SubmitRaw(active, s.Request(active, CommandKind.Draw)).Accepted, "first draw accepted");
        // Repeated draw with a fresh token: the ordinary draw allowance is used.
        Rejected(s, active, s.Request(active, CommandKind.Draw), RuleError.DrawNotAllowed, "repeated draw with fresh token");
        // Repeated draw with the old token: stale.
        Rejected(s, active, new CommandRequest { RequestId = "old-draw", Kind = CommandKind.Draw, Expected = old }, RuleError.StalePosition, "repeated draw with stale token");
        Rejected(s, active, new CommandRequest { RequestId = "foreign", Kind = CommandKind.Draw, Expected = new TokenDto { MatchId = "other", RoundIndex = old.RoundIndex, TurnNumber = old.TurnNumber, Revision = s.Revision } }, RuleError.StalePosition, "other match identity");
        Rejected(s, active, new CommandRequest { RequestId = "future", Kind = CommandKind.Draw, Expected = new TokenDto { MatchId = old.MatchId, RoundIndex = old.RoundIndex, TurnNumber = old.TurnNumber, Revision = s.Revision + 5 } }, RuleError.StalePosition, "future revision");

        // Repeated end turn in Draft: the second has a stale token, and a fresh one is out of turn.
        var d = RelayScenario.Find(seed => RelayScenario.Start(2, seed, CommitmentMode.Draft),
            x => x.HostView(x.Active).LegalPlacements.Any(p => !DominoCatalog.Get(p.DominoId).IsDouble));
        Check(d != null, "found a Draft position with an ordinary placement");
        ulong actor = d.Active;
        var place = d.HostView(actor).LegalPlacements.First(p => !DominoCatalog.Get(p.DominoId).IsDouble);
        Check(d.Relay.SubmitRaw(actor, d.Request(actor, CommandKind.Place, place.DominoId, place.Train)).Accepted, "draft placement accepted");
        var endTurn = d.Request(actor, CommandKind.EndTurn, id: "end-1");
        Check(d.Relay.SubmitRaw(actor, endTurn).Accepted, "end turn accepted");
        string after = d.Snapshot();
        var replay = d.Relay.SubmitRaw(actor, endTurn);
        Check(replay.Accepted && d.Snapshot() == after, "identical end-turn retry replays without a second effect");
        Rejected(d, actor, new CommandRequest { RequestId = "end-2", Kind = CommandKind.EndTurn, Expected = endTurn.Expected }, RuleError.StalePosition, "second end turn with the old token");
        Rejected(d, actor, d.Request(actor, CommandKind.EndTurn), RuleError.NotYourTurn, "second end turn with a fresh token");
    }

    private static void InvalidIdentifiers()
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(3, seed), x => x.HostView(x.Active).LegalPlacements.Length > 0);
        Check(s != null, "found a position with a legal placement");
        ulong active = s.Active;
        var legal = s.HostView(active).LegalPlacements[0];
        foreach (int bad in new[] { -1, 91, 999, int.MaxValue })
            Rejected(s, active, s.Request(active, CommandKind.Place, bad, legal.Train), RuleError.UnknownTile, "unknown domino " + bad);
        int foreign = s.HostView(s.Observers.First()).Hand[0];
        Rejected(s, active, s.Request(active, CommandKind.Place, foreign, legal.Train), RuleError.TileNotHeld, "another player's tile");
        int engine = s.HostView(active).EngineId;
        Rejected(s, active, s.Request(active, CommandKind.Place, engine, legal.Train), RuleError.TileNotHeld, "the engine tile");
        foreach (int owner in new[] { 3, 7, -1, 99 })
            Rejected(s, active, s.Request(active, CommandKind.Place, legal.DominoId, new TrainIdDto { IsShared = false, Owner = owner }), RuleError.UnknownTrain, "unknown train owner " + owner);
        Rejected(s, active, s.Request(active, CommandKind.Place, legal.DominoId, null), RuleError.InvalidCommand, "placement without a train");
        Rejected(s, active, new CommandRequest { RequestId = "no-token", Kind = CommandKind.Draw }, RuleError.InvalidCommand, "command without a position");
        Rejected(s, active, s.Request(active, CommandKind.Challenge, target: 7), RuleError.ChallengeNotAllowed, "challenge of an unknown seat");
        Rejected(s, active, s.Request(active, CommandKind.Challenge, target: s.HostView(active).Player), RuleError.ChallengeNotAllowed, "self challenge");
        Rejected(s, active, new CommandRequest { RequestId = "", Kind = CommandKind.Draw, Expected = s.HostView(active).Token }, RuleError.InvalidCommand, "empty request identity");
        Rejected(s, active, new CommandRequest { RequestId = new string('x', 129), Kind = CommandKind.Draw, Expected = s.HostView(active).Token }, RuleError.InvalidCommand, "overlong request identity");
        Rejected(s, 999, s.Request(active, CommandKind.Draw), RuleError.UnknownPlayer, "unseated sender");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    private static void Undo()
    {
        var s = RelayScenario.Start(2, 3, CommitmentMode.Immediate);
        Rejected(s, s.Active, s.Request(s.Active, CommandKind.Undo), RuleError.UndoNotAllowed, "undo in Immediate mode");

        var d = RelayScenario.Find(seed => RelayScenario.Start(3, seed, CommitmentMode.Draft), x => x.HostView(x.Active).LegalPlacements.Length > 0);
        Check(d != null, "found a Draft position with a legal placement");
        Rejected(d, d.Active, d.Request(d.Active, CommandKind.Undo), RuleError.UndoNotAllowed, "undo with nothing to undo");
        var place = d.HostView(d.Active).LegalPlacements[0];
        Check(d.Relay.SubmitRaw(d.Active, d.Request(d.Active, CommandKind.Place, place.DominoId, place.Train)).Accepted, "draft placement accepted");
        foreach (ulong observer in d.Observers)
            Rejected(d, observer, d.Request(observer, CommandKind.Undo), RuleError.NotYourTurn, "another player's undo");
        Check(d.Relay.SubmitRaw(d.Active, d.Request(d.Active, CommandKind.Undo)).Accepted, "owner's undo accepted");
        Check(d.HostView(d.Active).Hand.Contains(place.DominoId), "undo returned the tile to its owner");
    }

    private static void EmptyBoneyard()
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(8, seed),
            x => x.HostView(x.Active).Phase == MatchPhase.Playing && x.HostView(x.Active).BoneyardCount == 0 && x.HostView(x.Active).LegalPlacements.Length == 0);
        Check(s != null, "found an empty boneyard with a blocked active player");
        ulong active = s.Active;
        Check(!s.HostView(active).Allowed.CanDraw && s.HostView(active).Allowed.CanPass, "empty boneyard offers pass, not draw");
        Rejected(s, active, s.Request(active, CommandKind.Draw), RuleError.DrawNotAllowed, "draw from an empty boneyard");
        Check(s.Relay.SubmitRaw(active, s.Request(active, CommandKind.Pass)).Accepted, "pass accepted on an empty boneyard");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    private static void SharedTrain()
    {
        foreach (int players in new[] { 2, 5, 8 })
        {
            var s = RelayScenario.Start(players, 11);
            int checkedViews = 0;
            bool ok = true;
            s.Relay.Delivered += (peer, packet, json) =>
            {
                if (packet.View == null || packet.View.Phase != MatchPhase.Playing) return;
                checkedViews++;
                var trains = packet.View.Trains;
                ok &= trains.Count(t => t.Id.IsShared) == 1 && trains.Length == players + 1
                      && trains.Where(t => !t.Id.IsShared).Select(t => t.Id.Owner).OrderBy(o => o).SequenceEqual(Enumerable.Range(0, players));
            };
            // A shared train that names an owner still means the one shared train.
            var r = RelayScenario.Find(seed => RelayScenario.Start(players, seed), x => x.HostView(x.Active).LegalPlacements.Any(p => p.Train.IsShared));
            Check(r != null, $"{players}p found a legal shared-train placement");
            var shared = r.HostView(r.Active).LegalPlacements.First(p => p.Train.IsShared);
            Check(r.Relay.SubmitRaw(r.Active, r.Request(r.Active, CommandKind.Place, shared.DominoId, new TrainIdDto { IsShared = true, Owner = 5 })).Accepted, $"{players}p shared placement with a stray owner lands on the one shared train");
            Check(r.HostView(r.Active).Trains.Count(t => t.Id.IsShared) == 1, $"{players}p still exactly one shared train");
            s.RunUntil(() => s.Any.Phase == MatchPhase.RoundOver, 3000);
            Check(ok && checkedViews > 0, $"{players}p every delivered view has one shared train and one personal train per seat");
        }
    }

    private static void RejectionRecovery()
    {
        // A client acting on a stale view is rejected, recovers its controls and succeeds with the latest view.
        var s = RelayScenario.Find(seed => RelayScenario.Start(2, seed), x => x.HostView(x.Active).Allowed.CanDraw);
        Check(s != null, "found a draw position");
        ulong active = s.Active;
        var client = s.Relay[active];
        var stale = client.View;
        // The host moves on (a raw draw) while the client's copy of that update is lost.
        s.Relay.HoldProjections = true;
        Check(s.Relay.SubmitRaw(active, s.Request(active, CommandKind.Draw)).Accepted, "raw draw accepted while the client view is held");
        s.Relay.HoldProjections = false;
        s.Relay.HeldProjections.Clear(); // Dropped: the client still shows the pre-draw view.
        Check(client.View.Token.Revision == stale.Token.Revision && client.BoardEnabled, "client shows a stale but enabled board");
        Check(client.Submit(CommandKind.Draw), "client submits from the stale view");
        Check(client.Pending == null && client.Feedback == RuleError.StalePosition.ToString(), "stale rejection settles the request with feedback");
        // The rejection reply carries the sender's current view, so the client catches up from the rejection itself.
        Check(client.View.Token.Revision == s.HostView(active).Token.Revision, "a rejection reply installs the current view, not an older or blank one");
        // The observer lost its copy of the draw too; it recovers through its own synchronization.
        foreach (ulong peer in s.Relay.Peers) s.Relay.Resynchronize(peer);
        s.Relay.PresentAll();
        Check(s.Converged(out var why), "resynchronization converges: " + why);
        Check(client.BoardEnabled, "controls recover after resynchronization");
        if (client.View.LegalPlacements.Length > 0) { var p = client.View.LegalPlacements[0]; Check(client.Submit(CommandKind.Place, p.DominoId, p.Train), "client acts again"); }
        else Check(client.Submit(CommandKind.Pass), "client acts again");
        Check(client.Pending == null && client.Feedback == "", "the next command is accepted");
    }

    private static void Convergence()
    {
        // Full matches with 2, 4 and 8 players and both modes: every accepted step converges and stays private.
        foreach (var (players, mode) in new[] { (2, CommitmentMode.Immediate), (4, CommitmentMode.Draft), (8, CommitmentMode.Immediate) })
        {
            var s = RelayScenario.Start(players, 21, mode);
            int steps = 0;
            while (s.Step())
            {
                steps++;
                Check(s.Converged(out var why), $"{players}p {mode} step {steps}: {why}");
                if (steps > 20000) throw new Exception("match did not finish");
            }
            Check(s.Any.Phase == MatchPhase.MatchOver && s.Relay.Peers.All(p => s.View(p).RoundResults.Length == 13), $"{players}p {mode} thirteen rounds complete at every peer");
            Check(s.Violations.Count == 0, $"{players}p {mode} privacy: " + string.Join("; ", s.Violations.Take(3)));
            Check(s.Deliveries > steps, $"{players}p {mode} payloads were delivered and inspected");
        }
    }
}
