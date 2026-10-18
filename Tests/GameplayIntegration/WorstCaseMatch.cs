using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// Plays complete deterministic matches through the real coordinator and reports every packet it
/// produces, so wire-size measurements use real late-round states rather than a hand-built guess.
/// Shared by the console checks and the Unity batch wire check (which supplies JsonUtility).
/// </summary>
internal static class WorstCaseMatch
{
    /// <summary>Plays one match to completion. Each produced packet and each submitted command is reported.</summary>
    public static void Play(int players, int seed, CommitmentMode mode, Action<ProjectionPacket> packet, Action<CommandRequest> command)
    {
        var random = new Random(seed);
        var host = new AuthoritativeMatchCoordinator("m" + seed.ToString("x8") + new string('0', 24), Enumerable.Range(1, players).Select(i => (ulong)i),
            random.Next(players), new MatchSettings(mode, AnnouncementPenalty.DrawOneTile), round => Shuffled(round, random));
        int requests = 0;
        void Submit(ulong peer, CommandKind kind, int domino = -1, TrainIdDto train = null)
        {
            var request = new CommandRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Expected = host.GetProjection(peer).View.Token,
                Kind = kind,
                DominoId = domino,
                Train = train,
            };
            command(request);
            foreach (var p in host.Submit(peer, request)) packet(p.Packet);
            if (++requests > 20000) throw new InvalidOperationException("match did not finish");
        }

        while (true)
        {
            var view = host.GetProjection(host.Participants[0]).View;
            foreach (ulong peer in host.Participants) packet(host.GetProjection(peer));
            if (view.Phase == MatchPhase.MatchOver) return;
            if (view.Phase != MatchPhase.Playing)
            {
                foreach (ulong peer in host.Participants)
                    if (host.GetProjection(peer).View.Allowed.CanReady) Submit(peer, CommandKind.Ready);
                continue;
            }
            ulong active = host.Participants[view.ActivePlayer];
            var own = host.GetProjection(active).View;
            if (own.LegalPlacements.Length > 0)
            {
                var choice = own.LegalPlacements[random.Next(own.LegalPlacements.Length)];
                Submit(active, CommandKind.Place, choice.DominoId, choice.Train);
            }
            else if (own.Allowed.CanEndTurn) Submit(active, CommandKind.EndTurn);
            else if (own.Allowed.CanDraw) Submit(active, CommandKind.Draw);
            else if (own.Allowed.CanPass) Submit(active, CommandKind.Pass);
            else throw new InvalidOperationException("no action available for the active player");
        }
    }

    private static IEnumerable<int> Shuffled(int round, Random random)
    {
        var ids = DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id).ToArray();
        for (int i = ids.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        return ids;
    }
}
