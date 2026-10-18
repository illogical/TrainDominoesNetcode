using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// NET-006 matrix: delayed, dropped, duplicated and reordered commands, outcomes and views between real
/// ClientSessions and the coordinator (LoopbackRelay over WireCodec). These are simulated faults, not
/// live-transport evidence. Each case ends by proving convergence and that nothing was presented twice.
/// </summary>
internal static class DeliveryFaultChecks
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

    private static void Settled(RelayScenario s, string name)
    {
        s.Relay.PresentAll();
        Check(s.Converged(out var why), name + ": peers converge: " + why);
        Check(s.PresentedOnce(out var twice), name + ": nothing presented twice: " + twice);
        Check(s.Violations.Count == 0, name + ": " + string.Join("; ", s.Violations));
    }

    private static RelayScenario MustDraw(int players = 3, CommitmentMode mode = CommitmentMode.Immediate)
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(players, seed, mode), x => x.HostView(x.Active).Allowed.CanDraw);
        Check(s != null, "found a position where the active player must draw");
        return s;
    }

    public static void Run()
    {
        DelayedCommand();
        DroppedCommandRetried();
        DroppedOutcomeRetried();
        DuplicatedProjections();
        ReorderedAcrossSteps();
        UndoReplacementGap();
        RoundEndWhilePresenting();
        SuspensionWhileQueued();
        DuplicatedPenalty();
        Console.WriteLine("Delivery faults (NET-006): " + assertions + " assertions passed.");
    }

    private static void DelayedCommand()
    {
        var s = MustDraw();
        ulong active = s.Active;
        int hand = s.View(active).Hand.Length;
        s.Relay.HoldCommands = true;
        Check(s.Relay[active].Submit(CommandKind.Draw), "draw submitted");
        s.Relay.HoldCommands = false;
        Check(s.Relay[active].Pending != null && !s.Relay[active].BoardEnabled, "a delayed command keeps the board locked while pending");
        s.Relay.ReleaseCommands();
        Check(s.View(active).Hand.Length == hand + 1 && s.Relay[active].Pending == null, "the delayed command applies once on arrival");
        Settled(s, "delayed command");
    }

    private static void DroppedCommandRetried()
    {
        var s = MustDraw();
        ulong active = s.Active;
        int hand = s.View(active).Hand.Length;
        long revision = s.Revision;
        s.Relay.HoldCommands = true;
        s.Relay[active].Submit(CommandKind.Draw);
        s.Relay.HoldCommands = false;
        s.Relay.HeldCommands.Clear();
        Check(s.Revision == revision, "a dropped command never reaches the host");
        s.Relay[active].MarkUncertain(); // GameSession does this after five seconds without an answer.
        Check(s.Relay[active].IsUncertain, "the client marks the request uncertain");
        s.Relay[active].Retry();
        Check(s.View(active).Hand.Length == hand + 1 && s.Revision == revision + 1, "the retry applies exactly once");
        Settled(s, "dropped command");
    }

    private static void DroppedOutcomeRetried()
    {
        var s = MustDraw();
        ulong active = s.Active;
        int hand = s.HostView(active).Hand.Length;
        s.Relay.HoldProjections = true;
        s.Relay[active].Submit(CommandKind.Draw);
        s.Relay.HoldProjections = false;
        var mine = s.Relay.HeldProjections.Where(m => m.Peer == active).ToList();
        s.Relay.ReleaseProjections(s.Relay.HeldProjections.Except(mine)); // The actor's outcome and view are lost.
        Check(s.Relay[active].Pending != null && s.HostView(active).Hand.Length == hand + 1, "the host applied the draw but the actor never heard");
        long revision = s.Revision;
        s.Relay[active].MarkUncertain();
        s.Relay[active].Retry();
        Check(s.Revision == revision && s.HostView(active).Hand.Length == hand + 1, "the retry replays the cached outcome without a second draw");
        Check(s.Relay[active].Pending == null && s.View(active).Hand.Length == hand + 1, "the replayed outcome settles the client and brings its view current");
        Settled(s, "dropped outcome");

        // The same loss recovered by synchronization, which resubmits the pending request.
        var r = MustDraw();
        ulong actor = r.Active;
        r.Relay.HoldProjections = true;
        r.Relay[actor].Submit(CommandKind.Draw);
        r.Relay.HoldProjections = false;
        r.Relay.ReleaseProjections(r.Relay.HeldProjections.Where(m => m.Peer != actor).ToList());
        r.Relay.HeldProjections.Clear();
        long before = r.Revision;
        r.Relay.Resynchronize(actor);
        Check(r.Revision == before && r.Relay[actor].Pending == null, "resynchronization settles the pending request without reapplying it");
        Settled(r, "dropped outcome via synchronization");
    }

    private static void DuplicatedProjections()
    {
        var s = MustDraw();
        s.Relay.HoldProjections = true;
        s.Relay[s.Active].Submit(CommandKind.Draw);
        s.Relay.HoldProjections = false;
        var held = s.Relay.HeldProjections.ToList();
        s.Relay.ReleaseProjections(held.Concat(held).Concat(held));
        Settled(s, "triplicated projections");
    }

    private static void ReorderedAcrossSteps()
    {
        var s = RelayScenario.Start(4, 5);
        s.Relay.HoldProjections = true;
        for (int i = 0; i < 6; i++)
        {
            ulong active = s.Active;
            var v = s.HostView(active);
            CommandRequest request = v.LegalPlacements.Length > 0
                ? s.Request(active, CommandKind.Place, v.LegalPlacements[0].DominoId, v.LegalPlacements[0].Train)
                : s.Request(active, v.Allowed.CanDraw ? CommandKind.Draw : CommandKind.Pass);
            Check(s.Relay.SubmitRaw(active, request).Accepted, "accepted step " + i);
        }
        s.Relay.HoldProjections = false;
        var held = s.Relay.HeldProjections.ToList();
        held.Reverse();
        s.Relay.ReleaseProjections(held);
        foreach (ulong p in s.Relay.Peers)
            Check(s.Presentations[p].Last().Revision == s.HostView(p).Token.Revision, "the newest view wins and older ones are ignored");
        Settled(s, "reordered across six steps");
    }

    /// <summary>Draft: place on one train, undo, place on another; the owner misses the middle revisions.</summary>
    private static void UndoReplacementGap()
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(3, seed, CommitmentMode.Draft),
            x => x.HostView(x.Active).LegalPlacements.GroupBy(p => p.DominoId).Any(g => g.Count() > 1 && !DominoCatalog.Get(g.Key).IsDouble));
        Check(s != null, "found a tile with two legal trains");
        ulong actor = s.Active;
        var options = s.HostView(actor).LegalPlacements.GroupBy(p => p.DominoId).First(g => g.Count() > 1 && !DominoCatalog.Get(g.Key).IsDouble).ToList();
        int tile = options[0].DominoId;
        long start = s.View(actor).Token.Revision;
        s.Relay.HoldProjections = true;
        Check(s.Relay.SubmitRaw(actor, s.Request(actor, CommandKind.Place, tile, options[0].Train)).Accepted, "placed on the first train");
        Check(s.Relay.SubmitRaw(actor, s.Request(actor, CommandKind.Undo)).Accepted, "undone");
        Check(s.Relay.SubmitRaw(actor, s.Request(actor, CommandKind.Place, tile, options[1].Train)).Accepted, "placed on the second train");
        s.Relay.HoldProjections = false;
        var mine = s.Relay.HeldProjections.Where(m => m.Peer == actor).ToList();
        s.Relay.ReleaseProjections(s.Relay.HeldProjections.Except(mine.Take(mine.Count - 1))); // Owner gets only the last.
        var last = s.Presentations[actor].Last();
        Check(last.Revision == start + 3 && last.Reconcile, "the gap forces a reconciling presentation instead of an incremental one");
        var train = s.View(actor).Trains.Single(t => t.Placements.Any(p => p.DominoId == tile));
        Check(train.Id.ToId() == options[1].Train.ToId() && s.View(actor).Trains.Count(t => t.Placements.Any(p => p.DominoId == tile)) == 1, "the tile appears once, on the replacement train");
        Settled(s, "undo replacement gap");
    }

    private static void RoundEndWhilePresenting()
    {
        var s = RelayScenario.Start(3, 9);
        ulong slow = s.Relay.Peers.First();
        s.Slow.Add(slow);
        Check(s.RunUntil(() => s.Any.Phase == MatchPhase.RoundOver, 4000), "the round ends");
        var client = s.Relay[slow];
        Check(client.IsPresentationBusy, "the slow peer still has presentation queued at round end");
        Check(client.View.Phase == MatchPhase.RoundOver && client.View.RoundResults.Length == 1, "the slow peer already holds the round result");
        long stale = client.PresentationGeneration - 1;
        client.CompletePresentation(stale);
        Check(client.IsPresentationBusy, "completing an old generation does not release input");
        s.Slow.Clear();
        Check(s.RunUntil(() => s.Any.Phase == MatchPhase.Playing && s.Any.Token.RoundIndex == 1, 50), "the next round starts");
        Check(s.Relay.Peers.All(p => s.View(p).RoundResults.Length == 1 && s.View(p).Totals.Sum(t => t.Count) == s.Any.Totals.Sum(t => t.Count)), "every peer records the round's scores once");
        Settled(s, "round end while presenting");
    }

    private static void SuspensionWhileQueued()
    {
        var s = MustDraw();
        ulong active = s.Active;
        int hand = s.HostView(active).Hand.Length;
        s.Relay.HoldCommands = true;
        s.Relay[active].Submit(CommandKind.Draw);
        s.Relay.HoldCommands = false;
        s.Relay.HoldProjections = true;
        s.Relay.Dispatch(s.Host.Suspend(true));
        s.Relay.HoldProjections = false;
        s.Relay.ReleaseProjections();
        Check(s.Relay.Peers.All(p => s.View(p).IsSuspended), "every peer is suspended");
        Check(s.Relay[active].Pending == null && !s.Relay[active].BoardEnabled, "suspension clears the pending request and locks input");
        s.Relay.ReleaseCommands(); // The old draw arrives during suspension and is rejected.
        Check(s.HostView(active).IsSuspended && s.HostView(active).Hand.Length == hand, "the late command did not act during suspension");
        s.Relay.Dispatch(s.Host.Suspend(false));
        foreach (ulong p in s.Relay.Peers) s.Relay.Resynchronize(p);
        s.Relay.PresentAll();
        Check(s.Relay[active].BoardEnabled && s.View(active).Allowed.CanDraw, "controls recover after resuming");
        Settled(s, "suspension while queued");
    }

    private static void DuplicatedPenalty()
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(3, seed),
            x => x.Any.BoneyardCount > 0 && x.Relay.Peers.Any(p => x.HostView(p).Allowed.ChallengeTargets.Length > 0), 80);
        Check(s != null, "found an open challenge window");
        ulong challenger = s.Relay.Peers.First(p => s.HostView(p).Allowed.ChallengeTargets.Length > 0);
        int target = s.HostView(challenger).Allowed.ChallengeTargets[0];
        ulong targetPeer = s.PeerOf(target);
        s.Relay.HoldProjections = true;
        s.Relay[challenger].Submit(CommandKind.Challenge, target: target);
        s.Relay.HoldProjections = false;
        var held = s.Relay.HeldProjections.ToList();
        s.Relay.ReleaseProjections(held.Concat(held));
        int penalties = s.Presentations[targetPeer].Sum(p => p.Events.Count(e => e.Kind == EventKind.PenaltyTileDrawn));
        Check(penalties == 1, "a duplicated penalty packet is presented once, saw " + penalties);
        Check(s.View(targetPeer).Hand.Length == 2, "the penalty tile is held once");
        Settled(s, "duplicated penalty");
    }
}
