using System;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

internal static class AdmissionChecks
{
    public static void Run()
    {
        var seats = Enumerable.Range(0, 8).Select(i => (ulong)i).ToArray();
        var match = new AuthoritativeMatchCoordinator("frozen-eight", seats);
        var credential = match.GetProjection(7);
        var before = match.GetProjection(0).View.Token;
        if (match.GetProjection(99) != null) throw new Exception("unseated arrival received projection");
        var rejection = match.Submit(99, new CommandRequest { RequestId = "late-ready", Expected = before, Kind = CommandKind.Ready });
        if (rejection.Count != 1 || rejection[0].Packet.View != null || rejection[0].Packet.Outcome.Error != RuleError.UnknownPlayer)
            throw new Exception("unseated arrival could mutate or observe frozen match");
        if (match.GetProjection(0).View.Token.Revision != before.Revision) throw new Exception("late arrival mutated readiness");
        match.Suspend(true);
        var claim = new ReclaimRequest { MatchId = match.MatchId, Seat = 7, Secret = credential.SeatSecret };
        if (match.Reclaim(100, claim, id => id != 7) != ReclaimError.None || match.GetProjection(100).View.Player != 7)
            throw new Exception("eight-seat reclaim failed while ordinary admission closed");
        if (match.Participants.Count != 8 || match.GetProjection(7) != null || match.GetProjection(99) != null)
            throw new Exception("reclaim changed fixed seat capacity");
        var replacement = new AuthoritativeMatchCoordinator("fresh-eight", new ulong[] { 0, 1, 2, 3, 4, 5, 99, 100 });
        if (replacement.GetProjection(99) == null || replacement.GetProjection(100) == null || replacement.GetProjection(6) != null)
            throw new Exception("fresh roster does not seat intended connected peers");
        Console.WriteLine("Admission: frozen eight-seat privacy, rejection, reclaim and fresh roster passed (coordinator only; no Lobby service).");
    }
}
