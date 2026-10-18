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
        private const int MaximumRequestIdLength = 128;
        private readonly object gate = new object();
        private readonly SeatRegistry seats;
        private readonly Dictionary<PlayerId, Dictionary<string, Cached>> cache = new Dictionary<PlayerId, Dictionary<string, Cached>>();
        private readonly Func<int, IEnumerable<int>> deckSupplier;
        private MatchState state;

        public string LastLifecycleError { get; private set; }

        /// <summary>Transport identities currently bound to the frozen seats, in seat order. A reclaimed seat shows its new transport.</summary>
        public IReadOnlyList<ulong> Participants { get { lock (gate) return seats.Transports.ToArray(); } }

        public string MatchId { get { lock (gate) return state.Id; } }

        private sealed class Cached
        {
            public string Fingerprint;
            public CommandOutcome Outcome;
        }

        public AuthoritativeMatchCoordinator(string matchId, IEnumerable<ulong> transportIds, int startingSeat = 0, MatchSettings settings = null, Func<int, IEnumerable<int>> deckSupplier = null)
        {
            seats = new SeatRegistry(transportIds);
            state = Core.CreateMatch(matchId, seats.Seats, startingSeat, settings);
            foreach (var seat in seats.Seats) cache[seat] = new Dictionary<string, Cached>(StringComparer.Ordinal);
            this.deckSupplier = deckSupplier ?? ShuffledDeck;
        }

        public ProjectionPacket GetProjection(ulong recipient)
        {
            lock (gate) return seats.TryGetSeat(recipient, out var seat) ? Packet(seat, null, null) : null;
        }

        public IReadOnlyList<RecipientPacket> Submit(ulong authenticatedSender, CommandRequest request)
        {
            lock (gate)
            {
                if (!seats.TryGetSeat(authenticatedSender, out var actor))
                    return Unseated(authenticatedSender, Failure(request?.RequestId, RuleError.UnknownPlayer));
                if (request == null || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > MaximumRequestIdLength)
                    return Reply(actor, Failure(request?.RequestId, RuleError.InvalidCommand));

                string fingerprint = Fingerprint(request);
                if (cache[actor].TryGetValue(request.RequestId, out var prior))
                {
                    var replay = prior.Fingerprint == fingerprint
                        ? Copy(prior.Outcome)
                        : Failure(request.RequestId, RuleError.InvalidCommand, "Request identity reused with different payload.");
                    return Reply(actor, replay);
                }

                var result = Core.Apply(state, actor, ToCommand(request));
                state = result.State;
                var outcome = new CommandOutcome { RequestId = request.RequestId, Accepted = result.Accepted, Error = result.Error, Position = TokenDto.From(state.Token) };
                cache[actor][request.RequestId] = new Cached { Fingerprint = fingerprint, Outcome = Copy(outcome) };
                if (!result.Accepted) return Reply(actor, outcome);

                var packets = Broadcast(result, actor, outcome);
                // Preserve both Ready and StartRound revisions as independently ordered updates.
                TryStartRound(packets);
                return packets.AsReadOnly();
            }
        }

        /// <summary>
        /// NET-004: a reconnecting client proves ownership of its earlier seat with the secret it last received.
        /// On success the seat (with its history, hand and cached outcomes) moves to the new transport.
        /// </summary>
        public ReclaimError Reclaim(ulong transport, ReclaimRequest request, Func<ulong, bool> isConnected)
        {
            lock (gate)
            {
                if (request == null || request.MatchId != state.Id) return ReclaimError.WrongMatch;
                return seats.TryRebind(request.Seat, request.Secret, transport, isConnected);
            }
        }

        public IReadOnlyList<RecipientPacket> Suspend(bool suspended)
        {
            lock (gate)
            {
                var result = Core.SetSuspended(state, state.Token, suspended);
                state = result.State;
                var packets = Broadcast(result, null, null);
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
            LastLifecycleError = null;
            state = result.State;
            packets.AddRange(Broadcast(result, null, null));
        }

        /// <summary>One packet per seat in seat order; only the actor receives its correlated outcome.</summary>
        private List<RecipientPacket> Broadcast(TransitionResult result, PlayerId? actor, CommandOutcome outcome)
        {
            return seats.Seats
                .Select(seat => new RecipientPacket { Recipient = seats.TransportFor(seat), Packet = Packet(seat, result, seat == actor ? outcome : null) })
                .ToList();
        }

        private ProjectionPacket Packet(PlayerId seat, TransitionResult result, CommandOutcome outcome)
        {
            var packet = RecipientProjection.Build(state, seat, result, Copy(outcome));
            packet.SeatSecret = seats.SecretFor(seat); // Only ever this seat's own secret.
            return packet;
        }

        private IReadOnlyList<RecipientPacket> Reply(PlayerId seat, CommandOutcome outcome)
            => new[] { new RecipientPacket { Recipient = seats.TransportFor(seat), Packet = Packet(seat, null, outcome) } };

        private static IReadOnlyList<RecipientPacket> Unseated(ulong transport, CommandOutcome outcome)
            => new[] { new RecipientPacket { Recipient = transport, Packet = new ProjectionPacket { Outcome = outcome } } };

        private CommandOutcome Failure(string id, RuleError error, string detail = null)
            => new CommandOutcome { RequestId = id, Error = error, Detail = detail, Position = TokenDto.From(state.Token) };

        private static CommandOutcome Copy(CommandOutcome o)
        {
            if (o == null) return null;
            return new CommandOutcome { RequestId = o.RequestId, Accepted = o.Accepted, Error = o.Error, Detail = o.Detail, Position = TokenDto.From(o.Position.ToToken()) };
        }

        private static GameCommand ToCommand(CommandRequest r)
        {
            if (r.Expected == null || string.IsNullOrWhiteSpace(r.Expected.MatchId)) return null;
            var token = r.Expected.ToToken();
            switch (r.Kind)
            {
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

        /// <summary>Identifies command content for request-ID reuse. The match ID is length-prefixed so fields cannot alias.</summary>
        private static string Fingerprint(CommandRequest r)
        {
            var e = r.Expected;
            return string.Join("|", new[]
            {
                ((int)r.Kind).ToString(),
                e?.MatchId?.Length.ToString() ?? "null",
                e?.MatchId ?? "",
                e?.RoundIndex.ToString() ?? "null",
                e?.TurnNumber.ToString() ?? "null",
                e?.Revision.ToString() ?? "null",
                r.DominoId.ToString(),
                r.Train == null ? "null" : r.Train.IsShared + ":" + r.Train.Owner,
                r.Target.ToString(),
            });
        }

        private static IEnumerable<int> ShuffledDeck(int round)
        {
            var ids = DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id).ToArray();
            var random = new Random();
            for (int i = ids.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
            return ids;
        }
    }
}
