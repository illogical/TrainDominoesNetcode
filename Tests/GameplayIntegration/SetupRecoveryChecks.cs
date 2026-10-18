using System;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

internal static class SetupRecoveryChecks
{
    public static void Run()
    {
        int calls = 0;
        bool broken = true, invalid = false;
        var host = new AuthoritativeMatchCoordinator("setup-recovery", new ulong[] { 0, 10 }, deckSupplier: round =>
        {
            calls++;
            if (broken) throw new InvalidOperationException("private supplier detail");
            return invalid ? new int[] { 1 } : DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id);
        });
        CommandRequest Ready(ulong peer) => new CommandRequest { RequestId = Guid.NewGuid().ToString("N"), Kind = CommandKind.Ready, Expected = host.GetProjection(peer).View.Token };
        void Check(bool value, string name) { if (!value) throw new Exception("Setup recovery: " + name); }
        host.Submit(0, Ready(0)); host.Submit(10, Ready(10));
        var before = host.GetProjection(0).View;
        Check(before.Phase == MatchPhase.AwaitingDeck && before.ReadyPlayers.Length == 2 && before.Hand.Length == 0 && calls == 1, "failure is atomic");
        var foreign = host.Submit(10, Ready(10));
        Check(!foreign[0].Packet.Outcome.Accepted && calls == 1, "only host retries setup");
        var failed = Ready(0);
        host.Submit(0, failed);
        Check(calls == 2, "host retries failed supplier");
        host.Submit(0, failed);
        Check(calls == 2, "repeated retry does not call supplier again");
        Check(host.GetProjection(0).View.Token.Revision == before.Token.Revision, "persistent failure preserves revision");
        broken = false; invalid = true;
        host.Submit(0, Ready(0));
        Check(calls == 3 && host.GetProjection(0).View.Hand.Length == 0, "invalid deck never partially deals");
        var stale = Ready(0);
        host.Suspend(true); host.Submit(0, Ready(0));
        Check(calls == 3, "suspension blocks retry");
        invalid = false;
        host.Suspend(false);
        var started = host.GetProjection(0).View;
        Check(started.Phase == MatchPhase.Playing && started.Hand.Length == DominoCatalog.HandSize(2) && calls == 4, "corrected supplier deals once on resume");
        host.Submit(0, stale);
        Check(calls == 4 && host.GetProjection(0).View.Token.Revision == started.Token.Revision, "stale retry cannot restart round");
        Check(started.Token.MatchId == before.Token.MatchId && started.RoundResults.Length == 0, "match and scores preserved");
        // Finish a real first round, then fail its successor: existing scores must survive.
        int moves = 0;
        while (host.GetProjection(0).View.Phase == MatchPhase.Playing && moves++ < 2000)
        {
            var view = host.GetProjection(0).View;
            ulong actor = host.Participants[view.ActivePlayer];
            var own = host.GetProjection(actor).View;
            var request = new CommandRequest { RequestId = Guid.NewGuid().ToString("N"), Expected = own.Token };
            if (own.LegalPlacements.Length > 0)
            { request.Kind = CommandKind.Place; request.DominoId = own.LegalPlacements[0].DominoId; request.Train = own.LegalPlacements[0].Train; }
            else request.Kind = own.Allowed.CanDraw ? CommandKind.Draw : CommandKind.Pass;
            host.Submit(actor, request);
        }
        Check(host.GetProjection(0).View.Phase == MatchPhase.RoundOver, "first round completes");
        var totals = host.GetProjection(0).View.Totals.Select(t => t.Count).ToArray();
        broken = true;
        host.Submit(0, Ready(0)); host.Submit(10, Ready(10));
        before = host.GetProjection(0).View;
        Check(before.Phase == MatchPhase.AwaitingDeck && before.RoundResults.Length == 1, "later setup fails after one score");
        var laterRetry = Ready(0); host.Submit(0, laterRetry); host.Submit(0, laterRetry);
        Check(host.GetProjection(0).View.Totals.Select(t => t.Count).SequenceEqual(totals), "failed retry never rescores");
        broken = false;
        var retryClient = new ClientSession(request => { });
        retryClient.Receive(host.GetProjection(0));
        retryClient.SetFeedback(ClientSession.SetupFailureMessage);
        retryClient.RetrySetup();
        foreach (var packet in host.Submit(0, retryClient.Pending).Where(p => p.Recipient == 0)) retryClient.Receive(packet.Packet);
        Check(retryClient.Pending == null && retryClient.LastRejection == RuleError.None && retryClient.Feedback == "", "retry settles on phase-changing view");
        var observer = new ClientSession(request => { });
        observer.Receive(new ProjectionPacket { View = before });
        observer.SetFeedback(ClientSession.SetupFailureMessage);
        observer.Receive(host.GetProjection(10));
        Check(observer.Feedback == "", "recovered observer clears obsolete setup failure");
        started = host.GetProjection(0).View;
        Check(started.Phase == MatchPhase.Playing && started.RoundResults.Length == 1 && started.Token.RoundIndex == 1 && started.ActivePlayer == 1, "later retry preserves score and rotates starter");
        Check(started.Totals.Select(t => t.Count).SequenceEqual(totals), "successful retry never rescores");
        foreach (int players in Enumerable.Range(2, 7))
        {
            var match = new AuthoritativeMatchCoordinator("count-" + players, Enumerable.Range(0, players).Select(i => (ulong)i));
            foreach (ulong peer in match.Participants)
                match.Submit(peer, new CommandRequest { RequestId = Guid.NewGuid().ToString("N"), Kind = CommandKind.Ready, Expected = match.GetProjection(peer).View.Token });
            Check(match.Participants.All(peer => match.GetProjection(peer).View.Hand.Length == DominoCatalog.HandSize(players)), "deal count " + players);
        }
        Console.WriteLine("Setup recovery: initial failure, invalid deck, host-only, duplicate, suspension and stale retry passed.");
    }
}
