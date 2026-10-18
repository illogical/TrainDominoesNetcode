using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// NET-005 fixtures over serialized recipient payloads (WireCodec through LoopbackRelay): private Draft
/// placement/undo/draw sealing, announcements, and challenges with both penalty settings. Every delivered
/// payload is also checked by RelayScenario's privacy invariants. The CLI challenge scenario repeats the
/// two-player cases over real Netcode transport.
/// </summary>
internal static class PrivacyAnnouncementChecks
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

    private sealed class Seen { public ulong Peer; public long Revision; public PresentationEventDto Event; }

    private static List<Seen> Record(RelayScenario s)
    {
        var seen = new List<Seen>();
        s.Relay.Delivered += (peer, packet, json) =>
        {
            if (packet.View == null) return;
            foreach (var e in packet.Events) seen.Add(new Seen { Peer = peer, Revision = packet.View.Token.Revision, Event = e });
        };
        return seen;
    }

    private static string Board(PlayerViewDto v) => string.Join(",", v.Trains.Select(t => string.Join(".", t.Placements.Select(p => p.DominoId + (p.IsProvisional ? "p" : "")))))
        + "|" + string.Join(",", v.HandCounts.Select(c => c.Count));

    private static int Count(PlayerViewDto v, int seat) => v.HandCounts.Single(c => c.Player == seat).Count;

    public static void Run()
    {
        DraftPrivacy();
        DrawSealing();
        ChallengeWithPenalty();
        ChallengeExpires();
        ChallengeWithoutPenalty();
        EmptyPilePenalty();
        Announcement();
        Console.WriteLine("Privacy and announcements (NET-005): " + assertions + " assertions passed.");
    }

    private static void DraftPrivacy()
    {
        var s = RelayScenario.Find(seed => RelayScenario.Start(3, seed, CommitmentMode.Draft),
            x => x.HostView(x.Active).LegalPlacements.Any(p => !DominoCatalog.Get(p.DominoId).IsDouble));
        Check(s != null, "found a Draft position with an ordinary placement");
        ulong actor = s.Active;
        var observers = s.Observers.ToList();
        var before = observers.ToDictionary(p => p, p => Board(s.View(p)));
        var choice = s.View(actor).LegalPlacements.First(p => !DominoCatalog.Get(p.DominoId).IsDouble);

        Check(s.Relay[actor].Submit(CommandKind.Place, choice.DominoId, choice.Train), "draft placement submitted");
        s.Relay.PresentAll();
        Check(s.View(actor).Trains.Any(t => t.Placements.Any(p => p.DominoId == choice.DominoId && p.IsProvisional)), "owner sees its provisional placement");
        Check(!s.View(actor).Hand.Contains(choice.DominoId), "owner's hand no longer shows the drafted tile");
        foreach (ulong o in observers)
        {
            Check(Board(s.View(o)) == before[o], "observer's board and hand counts are unchanged by a private draft");
            Check(s.View(o).Token.Revision == s.HostView(o).Token.Revision, "observer still receives the new revision");
        }

        Check(s.Relay[actor].Submit(CommandKind.Undo), "undo submitted");
        s.Relay.PresentAll();
        Check(s.View(actor).Hand.Contains(choice.DominoId) && !s.View(actor).Trains.Any(t => t.Placements.Any(p => p.IsProvisional)), "undo restores the owner's tile");
        foreach (ulong o in observers) Check(Board(s.View(o)) == before[o], "undo is invisible to observers");

        Check(s.Relay[actor].Submit(CommandKind.Place, choice.DominoId, choice.Train), "placement submitted again");
        Check(!s.Relay[actor].Submit(CommandKind.EndTurn), "input waits for the placement presentation");
        s.Relay.PresentAll();
        Check(s.Relay[actor].Submit(CommandKind.EndTurn), "end turn submitted");
        s.Relay.PresentAll();
        foreach (ulong o in observers)
            Check(s.View(o).Trains.Any(t => t.Placements.Any(p => p.DominoId == choice.DominoId && !p.IsProvisional)), "observers see the placement once it is committed");
        Check(s.Converged(out var why), "peers converge after commit: " + why);
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    /// <summary>A draw after a provisional double seals it publicly, while the drawn tile stays private.</summary>
    private static void DrawSealing()
    {
        RelayScenario s = null;
        int doubleId = -1;
        for (int seed = 0; seed < 200 && s == null; seed++)
        {
            var x = RelayScenario.Start(3, seed, CommitmentMode.Draft);
            for (int step = 0; step < 3000 && s == null; step++)
            {
                var view = x.HostView(x.Active);
                if (view.Phase == MatchPhase.Playing && x.Relay[x.Active].BoardEnabled)
                {
                    var dbl = view.LegalPlacements.FirstOrDefault(p => DominoCatalog.Get(p.DominoId).IsDouble);
                    if (dbl != null)
                    {
                        x.Relay[x.Active].Submit(CommandKind.Place, dbl.DominoId, dbl.Train);
                        x.Relay.PresentAll();
                        if (x.HostView(x.Active).Allowed.CanDraw) { s = x; doubleId = dbl.DominoId; break; }
                        x.Relay[x.Active].Submit(CommandKind.Undo);
                        x.Relay.PresentAll();
                    }
                }
                if (!x.Step()) break;
            }
        }
        Check(s != null, "found a provisional double that must be followed by a draw");
        var seen = Record(s);
        ulong actor = s.Active;
        foreach (ulong o in s.Observers)
            Check(!s.View(o).Trains.Any(t => t.Placements.Any(p => p.DominoId == doubleId)), "observers do not see the provisional double");
        var hand = s.View(actor).Hand.ToArray();
        Check(s.Relay[actor].Submit(CommandKind.Draw), "draw submitted");
        s.Relay.PresentAll();
        int drawn = s.View(actor).Hand.Except(hand).Single();
        foreach (ulong o in s.Observers)
        {
            Check(s.View(o).Trains.Any(t => t.Placements.Any(p => p.DominoId == doubleId && !p.IsProvisional)), "the draw seals the double for observers");
            Check(!seen.Any(e => e.Peer == o && e.Event.Kind == EventKind.TileDrawn), "observers receive no private draw event");
            Check(!seen.Any(e => e.Peer == o && e.Event.DominoId == drawn), "observers never receive the drawn tile's identity");
            Check(seen.Any(e => e.Peer == o && e.Event.Kind == EventKind.PlacementsSealed), "observers receive the public sealing event");
        }
        Check(seen.Count(e => e.Peer == actor && e.Event.Kind == EventKind.TileDrawn && e.Event.DominoId == drawn) == 1, "the owner receives its draw once");
        Check(!s.View(actor).Allowed.CanUndo, "the draw also seals the owner's undo history");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    private static RelayScenario OpenWindow(int players, AnnouncementPenalty penalty, Func<RelayScenario, bool> extra = null, int seeds = 80)
        => RelayScenario.Find(seed => RelayScenario.Start(players, seed, CommitmentMode.Immediate, penalty),
            x => x.Relay.Peers.Any(p => x.HostView(p).Allowed.ChallengeTargets.Length > 0) && (extra == null || extra(x)), seeds);

    private static int Target(RelayScenario s) => s.Relay.Peers.Select(p => s.HostView(p).Allowed.ChallengeTargets).First(t => t.Length > 0)[0];

    private static void ChallengeWithPenalty()
    {
        var s = OpenWindow(3, AnnouncementPenalty.DrawOneTile, x => x.Any.BoneyardCount > 0);
        Check(s != null, "found an open challenge window with tiles left");
        var seen = Record(s);
        int target = Target(s);
        ulong targetPeer = s.PeerOf(target);
        var challengers = s.Relay.Peers.Where(p => p != targetPeer).ToList();
        ulong active = s.Active;
        var activeBefore = s.HostView(active);
        int boneyard = s.Any.BoneyardCount;
        var targetHand = s.HostView(targetPeer).Hand.ToArray();

        Check(s.Relay.SubmitRaw(targetPeer, s.Request(targetPeer, CommandKind.Challenge, target: target)).Error == RuleError.ChallengeNotAllowed, "self challenge rejected");

        // Two opponents challenge at once: one wins, the other is rejected, and the penalty applies once.
        s.Relay.HoldCommands = true;
        foreach (ulong c in challengers) Check(s.Relay[c].Submit(CommandKind.Challenge, target: target), "competing challenge submitted");
        s.Relay.HoldCommands = false;
        s.Relay.ReleaseCommands();
        s.Relay.PresentAll();
        var feedback = challengers.Select(c => s.Relay[c].LastRejection).ToList();
        Check(feedback.Count(f => f == RuleError.None) == 1 && feedback.Count(f => f == RuleError.StalePosition || f == RuleError.ChallengeNotAllowed) == 1, "exactly one competing challenge wins: " + string.Join(",", feedback));
        Check(s.Relay.Peers.All(p => Count(s.View(p), target) == 2), "every peer sees the target holding the penalty tile");
        Check(s.Any.BoneyardCount == boneyard - 1, "the penalty takes exactly one tile from the boneyard");
        int penalty = s.HostView(targetPeer).Hand.Except(targetHand).Single();
        Check(seen.Count(e => e.Peer == targetPeer && e.Event.Kind == EventKind.PenaltyTileDrawn && e.Event.DominoId == penalty) == 1, "the target receives its penalty tile once");
        Check(!seen.Any(e => e.Peer != targetPeer && (e.Event.Kind == EventKind.PenaltyTileDrawn || e.Event.DominoId == penalty)), "no one else learns the penalty tile");
        foreach (ulong p in s.Relay.Peers)
            Check(seen.Count(e => e.Peer == p && e.Event.Kind == EventKind.MissedAnnouncement && e.Event.Player == target) == 1, "every peer is told about the missed announcement once");

        var activeAfter = s.HostView(active);
        Check(activeAfter.DrawAttemptUsed == activeBefore.DrawAttemptUsed && activeAfter.Allowed.CanDraw == activeBefore.Allowed.CanDraw, "the penalty does not use the active player's draw allowance");
        Check(s.Relay.SubmitRaw(challengers[0], s.Request(challengers[0], CommandKind.Challenge, target: target)).Error == RuleError.ChallengeNotAllowed, "a repeated challenge is rejected");
        Check(s.Relay.Peers.All(p => s.View(p).Allowed.ChallengeTargets.Length == 0), "the window is closed at every peer");
        Check(s.Converged(out var why), "peers converge after the challenge: " + why);
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    private static void ChallengeExpires()
    {
        // The next play must not end the round, or the rejection would be WrongPhase rather than an expired window.
        var s = OpenWindow(2, AnnouncementPenalty.DrawOneTile, x => x.Any.BoneyardCount > 0 && x.HostView(x.Active).Hand.Length > 1);
        Check(s != null, "found an open challenge window");
        int target = Target(s);
        ulong challenger = s.Relay.Peers.First(p => p != s.PeerOf(target));
        Check(s.Active == challenger, "in two-player play the challenger is the next actor");
        var window = s.Request(challenger, CommandKind.Challenge, target: target);
        Check(s.Step(), "the next player acts instead of challenging");
        Check(s.Relay.Peers.All(p => s.View(p).Allowed.ChallengeTargets.Length == 0), "the next accepted play, draw or pass closes the window at every peer");
        var expired = s.Relay.SubmitRaw(challenger, s.Request(challenger, CommandKind.Challenge, target: target)).Error;
        Check(expired == RuleError.ChallengeNotAllowed, "an expired challenge is rejected, got " + expired + " phase " + s.Any.Phase);
        Check(s.Relay.SubmitRaw(challenger, window).Error == RuleError.StalePosition, "a challenge prepared before the play is stale");
    }

    private static void ChallengeWithoutPenalty()
    {
        var s = OpenWindow(3, AnnouncementPenalty.None, x => x.Any.BoneyardCount > 0);
        Check(s != null, "found an open window under the None penalty");
        var seen = Record(s);
        int target = Target(s);
        ulong challenger = s.Relay.Peers.First(p => s.HostView(p).Allowed.ChallengeTargets.Contains(target));
        int boneyard = s.Any.BoneyardCount;
        Check(s.Relay[challenger].Submit(CommandKind.Challenge, target: target), "challenge submitted");
        s.Relay.PresentAll();
        Check(s.Relay[challenger].Feedback == "", "challenge accepted under None");
        Check(s.Relay.Peers.All(p => Count(s.View(p), target) == 1) && s.Any.BoneyardCount == boneyard, "no tile is drawn under None");
        Check(!seen.Any(e => e.Event.Kind == EventKind.PenaltyTileDrawn), "no penalty event under None");
        Check(s.Relay.Peers.All(p => seen.Count(e => e.Peer == p && e.Event.Kind == EventKind.MissedAnnouncement) == 1), "the missed announcement is still recorded for everyone");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    private static void EmptyPilePenalty()
    {
        var s = OpenWindow(8, AnnouncementPenalty.DrawOneTile, x => x.Any.BoneyardCount == 0, 200);
        Check(s != null, "found an open window with an empty boneyard");
        var seen = Record(s);
        int target = Target(s);
        ulong challenger = s.Relay.Peers.First(p => s.HostView(p).Allowed.ChallengeTargets.Contains(target));
        Check(s.Relay[challenger].Submit(CommandKind.Challenge, target: target), "challenge submitted");
        s.Relay.PresentAll();
        Check(s.Relay[challenger].Feedback == "", "challenge accepted with an empty boneyard");
        Check(s.Relay.Peers.All(p => Count(s.View(p), target) == 1), "no substitute penalty when the boneyard is empty");
        Check(!seen.Any(e => e.Event.Kind == EventKind.PenaltyTileDrawn) && seen.Any(e => e.Event.Kind == EventKind.MissedAnnouncement), "the miss is recorded without a penalty tile");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }

    private static void Announcement()
    {
        var s = OpenWindow(3, AnnouncementPenalty.DrawOneTile);
        Check(s != null, "found an open window to answer with an announcement");
        var seen = Record(s);
        int target = Target(s);
        ulong targetPeer = s.PeerOf(target);
        Check(s.View(targetPeer).Allowed.CanAnnounce, "a one-tile player may still announce during the window");
        Check(s.Relay[targetPeer].Submit(CommandKind.Announce), "announcement submitted");
        s.Relay.PresentAll();
        Check(s.Relay[targetPeer].Feedback == "", "announcement accepted");
        Check(s.Relay.Peers.All(p => s.View(p).AnnouncedPlayers.Contains(target) && s.View(p).Allowed.ChallengeTargets.Length == 0), "every peer sees the announcement and the window closes");
        Check(s.Relay.Peers.All(p => seen.Count(e => e.Peer == p && e.Event.Kind == EventKind.OneTileAnnounced) == 1), "the announcement event reaches everyone once");
        ulong other = s.Relay.Peers.First(p => p != targetPeer);
        Check(s.Relay.SubmitRaw(other, s.Request(other, CommandKind.Challenge, target: target)).Error == RuleError.ChallengeNotAllowed, "an announced player cannot be challenged");
        Check(s.Relay.SubmitRaw(targetPeer, s.Request(targetPeer, CommandKind.Announce)).Error == RuleError.AnnouncementNotRequired, "a repeated announcement is rejected");
        Check(s.Violations.Count == 0, string.Join("; ", s.Violations));
    }
}
