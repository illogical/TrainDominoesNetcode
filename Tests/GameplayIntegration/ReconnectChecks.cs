using System;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// NET-004 seat recovery through LoopbackRelay, mirroring GameSession's reclaim path: host-issued seat
/// secrets, reconnection under a new transport ID, impersonation and repeated attempts. Simulated delivery;
/// the CLI reconnect scenario repeats the main flow over real Netcode transport.
/// </summary>
internal static class ReconnectChecks
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

    public static void Run()
    {
        SecretsArePrivate();
        DisconnectDuringAction(dropCommand: false);
        DisconnectDuringAction(dropCommand: true);
        Impersonation();
        RepeatedAttempts();
        Console.WriteLine("Seat recovery (NET-004): " + assertions + " assertions passed.");
    }

    private static void SecretsArePrivate()
    {
        var s = RelayScenario.Start(4, 2, CommitmentMode.Draft, AnnouncementPenalty.None);
        var secrets = s.Relay.Peers.Select(p => s.Relay[p].Credential?.Secret).ToList();
        Check(secrets.All(x => x != null && x.Length == 32 && x.All(Uri.IsHexDigit)), "every seat received a 128-bit secret");
        Check(secrets.Distinct().Count() == secrets.Count, "every seat's secret is different");
        foreach (ulong p in s.Relay.Peers)
            Check(s.Relay[p].Credential.Seat == s.View(p).Player && s.Relay[p].Credential.MatchId == s.View(p).Token.MatchId, "the credential names the client's own seat and match");
        s.RunUntil(() => s.Any.Phase == MatchPhase.RoundOver, 3000);
        Check(s.Violations.Count == 0, "no payload carried another seat's secret: " + string.Join("; ", s.Violations.Take(2)));
    }

    /// <summary>The active player drops mid-action, comes back on a new transport and keeps its seat, hand and request.</summary>
    private static void DisconnectDuringAction(bool dropCommand)
    {
        string name = dropCommand ? "command lost with the connection" : "outcome lost with the connection";
        var s = RelayScenario.Find(seed => RelayScenario.Start(3, seed, CommitmentMode.Draft, AnnouncementPenalty.None), x => x.HostView(x.Active).Allowed.CanDraw);
        Check(s != null, "found a draw position");
        ulong old = s.Active;
        int seat = s.View(old).Player;
        int hand = s.HostView(old).Hand.Length;
        var settings = s.View(old).Settings;

        if (dropCommand) s.Relay.HoldCommands = true; else s.Relay.HoldProjections = true;
        Check(s.Relay[old].Submit(CommandKind.Draw), name + ": draw submitted");
        s.Relay.HoldCommands = false;
        s.Relay.HoldProjections = false;
        s.Relay.HeldCommands.Clear();
        s.Relay.Disconnect(old);
        s.Relay.ReleaseProjections(); // Anything addressed to the dropped transport is lost.
        Check(s.HostView(s.Relay.Peers.First(p => p != old)).IsSuspended, name + ": the host suspended the match");
        Check(s.HostView(old).Hand.Length == (dropCommand ? hand : hand + 1), name + ": the host state reflects whether the draw arrived");

        const ulong fresh = 900;
        var result = s.Relay.Reconnect(old, fresh);
        Check(result == ReclaimError.None, name + ": reclaim accepted, got " + result);
        var client = s.Relay[fresh];
        Check(client.View.Player == seat && s.Host.Participants[seat] == fresh, name + ": the same seat now belongs to the new transport");
        // A request that reached the host before the drop replays its cached outcome. One that never arrived is
        // resubmitted during the suspension and resolved as rejected (Suspended): it is never applied later.
        int expected = dropCommand ? hand : hand + 1;
        Check(s.HostView(fresh).Hand.Length == expected && client.View.Hand.Length == expected, name + ": the draw applied at most once");
        Check(client.View.Settings.Commitment == settings.Commitment && client.View.Settings.MissedAnnouncementPenalty == settings.MissedAnnouncementPenalty, name + ": match-fixed settings are preserved");
        Check(s.Host.Submit(old, s.Request(fresh, CommandKind.Pass))[0].Packet.Outcome.Error == RuleError.UnknownPlayer, name + ": the old transport no longer speaks for the seat");
        Check(s.Relay.Disconnected.Count == 0, name + ": no seat is missing after the reclaim");

        s.Relay.Dispatch(s.Host.Suspend(false));
        foreach (ulong p in s.Relay.Peers) s.Relay.Resynchronize(p);
        s.Relay.PresentAll();
        Check(s.Converged(out var why), name + ": peers converge after resuming: " + why);
        Check(client.BoardEnabled, name + ": the returning player can act again");
        if (dropCommand)
        {
            Check(client.View.Allowed.CanDraw && client.Submit(CommandKind.Draw), name + ": the player draws again after resuming");
            Check(s.HostView(fresh).Hand.Length == hand + 1, name + ": that draw applies once");
        }
        Check(s.Violations.Count == 0, name + ": " + string.Join("; ", s.Violations));
    }

    private static void Impersonation()
    {
        var s = RelayScenario.Start(3, 4);
        ulong victim = s.Relay.Peers.First();
        ulong other = s.Relay.Peers.Last();
        var real = s.Relay[victim].Credential;
        string before = s.Snapshot();

        ReclaimRequest Claim(string match, int seat, string secret) => new ReclaimRequest { MatchId = match, Seat = seat, Secret = secret };
        var wrongSecret = new string(real.Secret.Reverse().ToArray());
        Check(s.Host.Reclaim(777, Claim(real.MatchId, real.Seat, wrongSecret), id => false) == ReclaimError.WrongSecret, "a wrong secret is rejected");
        Check(s.Host.Reclaim(777, Claim(real.MatchId, real.Seat, real.Secret.Substring(1)), id => false) == ReclaimError.WrongSecret, "a truncated secret is rejected");
        Check(s.Host.Reclaim(777, Claim("other-match", real.Seat, real.Secret), id => false) == ReclaimError.WrongMatch, "a claim for another match is rejected");
        Check(s.Host.Reclaim(777, Claim(real.MatchId, 9, real.Secret), id => false) == ReclaimError.UnknownSeat, "a claim for a seat that does not exist is rejected");
        Check(s.Host.Reclaim(777, real, id => true) == ReclaimError.SeatStillConnected, "a correct secret cannot take a seat whose player is still connected");
        Check(s.Host.Reclaim(other, real, id => id != victim) == ReclaimError.AlreadySeated, "a seated player cannot take a second seat");
        Check(s.Host.GetProjection(777) == null, "a rejected claimant receives no projection");
        Check(s.Snapshot() == before, "rejected claims change nothing");

        // A rejected reconnect through the relay leaves the client with no view at all.
        s.Relay.Disconnect(victim);
        var rejected = s.Relay.Reconnect(victim, 778, Claim(real.MatchId, real.Seat, wrongSecret));
        Check(rejected == ReclaimError.WrongSecret && s.Relay[778].View == null, "a client whose claim fails sees no hand or board");
        Check(s.Relay.Reconnect(778, 779) == ReclaimError.None && s.Relay[779].View.Player == real.Seat, "the real credential still works afterwards");
    }

    private static void RepeatedAttempts()
    {
        var s = RelayScenario.Start(2, 6);
        ulong first = s.Relay.Peers.First();
        var original = s.Relay[first].Credential;
        s.Relay.Disconnect(first);
        Check(s.Relay.Reconnect(first, 901) == ReclaimError.None, "first reconnect accepted");
        var rotated = s.Relay[901].Credential;
        Check(rotated.Secret != original.Secret, "the secret rotates after a reclaim");
        Check(s.Host.Reclaim(950, original, id => false) != ReclaimError.None, "the old secret cannot be replayed");
        string before = s.Snapshot();
        Check(s.Host.Reclaim(901, rotated, id => true) == ReclaimError.None && s.Snapshot() == before, "a repeated claim from the same connection is harmless");

        s.Relay.Disconnect(901);
        Check(s.Relay.Reconnect(901, 902) == ReclaimError.None && s.Relay[902].View.Player == original.Seat, "a second drop and reconnect also recovers the seat");
        s.Relay.Dispatch(s.Host.Suspend(false));
        foreach (ulong p in s.Relay.Peers) s.Relay.Resynchronize(p);
        s.Relay.PresentAll();
        Check(s.Converged(out var why), "peers converge after repeated reconnects: " + why);
        Check(s.PresentedOnce(out var twice), "nothing was presented twice: " + twice);
    }
}
