using System;
using System.Collections.Generic;
using System.Linq;
using TrainDominoesRulesEngine;
using Core = TrainDominoesRulesEngine.RulesEngine;
namespace Assets.Scripts.Game.Integration
{
    /// <summary>Host-only serialized authority. It exposes recipient projections, never a full snapshot.</summary>
    public sealed class AuthoritativeMatchCoordinator
    {
        private readonly object gate = new object();
        private readonly Dictionary<ulong, PlayerId> seats;
        private readonly Dictionary<ulong, Dictionary<string, Cached>> cache = new Dictionary<ulong, Dictionary<string, Cached>>();
        private readonly Func<int, IEnumerable<int>> deckSupplier;
        private MatchState state;
        public string LastLifecycleError { get; private set; }
        private sealed class Cached { public string Fingerprint; public CommandOutcome Outcome; }
        public AuthoritativeMatchCoordinator(string matchId, IEnumerable<ulong> transportIds, int startingSeat = 0, MatchSettings settings = null, Func<int, IEnumerable<int>> deckSupplier = null)
        {
            var ids = transportIds?.OrderBy(id => id).ToArray() ?? throw new ArgumentNullException(nameof(transportIds));
            if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Duplicate transport identity.");
            seats = ids.Select((id, index) => new { id, player = new PlayerId(index) }).ToDictionary(p => p.id, p => p.player);
            state = Core.CreateMatch(matchId, ids.Select(id => seats[id]), startingSeat, settings);
            foreach (var id in ids) cache[id] = new Dictionary<string, Cached>(StringComparer.Ordinal);
            this.deckSupplier = deckSupplier ?? ShuffledDeck;
        }
        public ProjectionPacket GetProjection(ulong recipient) { lock (gate) return seats.ContainsKey(recipient) ? Packet(recipient, null, null) : null; }
        public IReadOnlyList<RecipientPacket> Submit(ulong authenticatedSender, CommandRequest request)
        {
            lock (gate)
            {
                if (!seats.TryGetValue(authenticatedSender, out var actor)) return Reply(authenticatedSender, Failure(request?.RequestId, RuleError.UnknownPlayer));
                if (request == null || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128) return Reply(authenticatedSender, Failure(request?.RequestId, RuleError.InvalidCommand));
                string fingerprint = Fingerprint(request);
                if (cache[authenticatedSender].TryGetValue(request.RequestId, out var prior)) return Reply(authenticatedSender, prior.Fingerprint == fingerprint ? Copy(prior.Outcome) : Failure(request.RequestId, RuleError.InvalidCommand, "Request identity reused with different payload."));
                var result = Core.Apply(state, actor, ToCommand(request));
                state = result.State;
                var outcome = new CommandOutcome { RequestId = request.RequestId, Accepted = result.Accepted, Error = result.Error, Position = TokenDto.From(state.Token) };
                cache[authenticatedSender][request.RequestId] = new Cached { Fingerprint = fingerprint, Outcome = Copy(outcome) };
                var packets = new List<RecipientPacket>();
                if (!result.Accepted) return Reply(authenticatedSender, outcome);
                foreach (var id in seats.Keys) packets.Add(new RecipientPacket { Recipient = id, Packet = Packet(id, result, id == authenticatedSender ? outcome : null) });
                // Preserve both Ready and StartRound revisions as independently ordered updates.
                TryStartRound(packets);
                return packets.AsReadOnly();
            }
        }
        public IReadOnlyList<RecipientPacket> Suspend(bool suspended)
        {
            lock (gate)
            {
                var result = Core.SetSuspended(state, state.Token, suspended); state = result.State;
                var packets = seats.Keys.Select(id => new RecipientPacket { Recipient = id, Packet = Packet(id, result, null) }).ToList();
                if (!suspended) TryStartRound(packets);
                return packets.AsReadOnly();
            }
        }
        private void TryStartRound(List<RecipientPacket> packets)
        {
            if (state.Phase != MatchPhase.AwaitingDeck || state.IsSuspended) return;
            TransitionResult result;
            try { result = Core.StartRound(state, state.Token, deckSupplier(state.RoundResults.Count)); }
            catch (Exception error) { LastLifecycleError = "Deck supplier failed: " + error.GetType().Name; return; }
            if (!result.Accepted) { LastLifecycleError = result.Error.ToString(); return; }
            LastLifecycleError = null; state = result.State;
            foreach (var id in seats.Keys) packets.Add(new RecipientPacket { Recipient = id, Packet = Packet(id, result, null) });
        }
        private ProjectionPacket Packet(ulong recipient, TransitionResult result, CommandOutcome outcome)
        {
            var player = seats[recipient];
            return new ProjectionPacket { View = PlayerViewDto.From(Core.GetPlayerView(state, player)), Outcome = Copy(outcome), Events = result == null ? Array.Empty<PresentationEventDto>() : result.Events.Select((e, i) => new { e, i }).Where(p => !p.e.Audience.HasValue || p.e.Audience.Value == player).Select(p => new PresentationEventDto { SourceIndex = p.i, Kind = p.e.Kind, Player = p.e.Player?.Value ?? -1, DominoId = p.e.DominoId ?? -1 }).ToArray() };
        }
        private IReadOnlyList<RecipientPacket> Reply(ulong id, CommandOutcome outcome) => new[] { new RecipientPacket { Recipient = id, Packet = seats.ContainsKey(id) ? Packet(id, null, outcome) : new ProjectionPacket { Outcome = outcome } } };
        private CommandOutcome Failure(string id, RuleError error, string detail = null) => new CommandOutcome { RequestId = id, Error = error, Detail = detail, Position = TokenDto.From(state.Token) };
        private static CommandOutcome Copy(CommandOutcome o) => o == null ? null : new CommandOutcome { RequestId = o.RequestId, Accepted = o.Accepted, Error = o.Error, Detail = o.Detail, Position = TokenDto.From(o.Position.ToToken()) };
        private static GameCommand ToCommand(CommandRequest r)
        {
            if (r.Expected == null || string.IsNullOrWhiteSpace(r.Expected.MatchId)) return null;
            var token = r.Expected.ToToken();
            switch (r.Kind) {
                case CommandKind.Ready: return new Ready(token);
                case CommandKind.Place: return r.Train == null ? null : new Place(token, r.DominoId, r.Train.ToId());
                case CommandKind.Draw: return new Draw(token);
                case CommandKind.Pass: return new Pass(token);
                case CommandKind.Undo: return new Undo(token);
                case CommandKind.EndTurn: return new EndTurn(token);
                case CommandKind.Announce: return new AnnounceOneTile(token);
                case CommandKind.Challenge: return new ChallengeMissedAnnouncement(token, new PlayerId(r.Target));
                default: return null;
            }
        }
        private static string Fingerprint(CommandRequest r) => string.Join("|", new[] { ((int)r.Kind).ToString(), r.Expected?.MatchId?.Length.ToString() ?? "null", r.Expected?.MatchId ?? "", r.Expected?.RoundIndex.ToString() ?? "null", r.Expected?.TurnNumber.ToString() ?? "null", r.Expected?.Revision.ToString() ?? "null", r.DominoId.ToString(), r.Train == null ? "null" : r.Train.IsShared + ":" + r.Train.Owner, r.Target.ToString() });
        private static IEnumerable<int> ShuffledDeck(int round) {
            var ids = DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id).ToArray(); var random = new Random();
            for (int i = ids.Length - 1; i > 0; i--) { int j = random.Next(i + 1); int tile = ids[i]; ids[i] = ids[j]; ids[j] = tile; } return ids;
        }
    }
}
