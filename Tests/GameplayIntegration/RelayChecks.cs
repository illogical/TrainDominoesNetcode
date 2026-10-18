using System;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>Delivery checks through LoopbackRelay. They establish the fault seam, not the full NET-006 matrix.</summary>
internal static class RelayChecks
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

    // Twelves are dealt last, so the opening player cannot match the 12-12 engine and must draw then pass.
    private static AuthoritativeMatchCoordinator Host() => new AuthoritativeMatchCoordinator("relay", new ulong[] { 10, 50 },
        deckSupplier: round => DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).OrderBy(d => d.High == 12 ? 1 : 0).Select(d => d.Id));

    private static LoopbackRelay Started()
    {
        var relay = new LoopbackRelay(Host());
        foreach (ulong peer in relay.Peers) relay.Synchronize(peer);
        foreach (ulong peer in relay.Peers) relay[peer].Ready();
        relay.PresentAll();
        return relay;
    }

    private static long HostRevision(LoopbackRelay relay) => relay.Host.GetProjection(relay.Peers.First()).View.Token.Revision;

    private static bool Converged(LoopbackRelay relay)
    {
        long revision = HostRevision(relay);
        return relay.Peers.All(p => relay[p].View.Token.Revision == revision && relay[p].Pending == null);
    }

    public static void Run()
    {
        var relay = Started();
        Check(relay.Peers.All(p => relay[p].View.Phase == MatchPhase.Playing), "ready and deal reach every peer");
        Check(Converged(relay), "peers converge after setup");
        ulong active = relay.PeerForSeat(relay[relay.Peers.First()].View.ActivePlayer);
        ulong observer = relay.Peers.Single(p => p != active);
        Check(relay[active].BoardEnabled && relay[active].View.Allowed.CanDraw, "fixture opening player must draw");

        // Duplicate delivery of one command has one effect and one settlement.
        int hand = relay[active].View.Hand.Length;
        relay.HoldCommands = true;
        Check(relay[active].Submit(CommandKind.Draw), "draw submitted");
        var held = relay.HeldCommands.Single();
        relay.HoldCommands = false;
        relay.ReleaseCommands(new[] { held, held });
        Check(relay[active].View.Hand.Length == hand + 1, "duplicated draw delivery applies once");
        Check(Converged(relay), "peers converge after duplicate delivery");
        relay.PresentAll();

        // Reordered projections cannot let an older view overwrite a newer one.
        Check(relay[active].View.Allowed.CanPass, "fixture player passes after an unplayable draw");
        relay.HoldProjections = true;
        relay[active].Submit(CommandKind.Pass);
        relay.HoldProjections = false;
        var toObserver = relay.HeldProjections.Where(m => m.Peer == observer).ToList();
        var toActive = relay.HeldProjections.Where(m => m.Peer == active).ToList();
        Check(toObserver.Count >= 1 && toActive.Count >= 1, "pass produces projections for both peers");
        relay.ReleaseProjections(toActive.Concat(Enumerable.Reverse(toObserver)));
        Check(Converged(relay), "reordered projections converge to the host revision");
        Check(relay[observer].View.ActivePlayer == relay[observer].View.Player, "turn advanced to the observer");

        // A dropped projection leaves a peer behind until it synchronizes.
        relay.PresentAll();
        relay.HoldProjections = true;
        relay[observer].Submit(CommandKind.Draw);
        relay.HoldProjections = false;
        var dropped = relay.HeldProjections.Where(m => m.Peer == active).ToList();
        relay.ReleaseProjections(relay.HeldProjections.Except(dropped));
        Check(relay[active].View.Token.Revision < HostRevision(relay), "dropped projection leaves the peer stale");
        relay.Synchronize(active);
        Check(Converged(relay), "synchronization recovers a dropped projection");

        Console.WriteLine("Relay delivery: " + assertions + " assertions passed.");
    }
}
