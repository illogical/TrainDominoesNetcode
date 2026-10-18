using System;
using System.Collections.Generic;
using System.Linq;

namespace TrainDominoesRulesEngine
{
    /// <summary>Pure transitions. The caller authenticates participants and serializes calls.</summary>
    public static partial class RulesEngine
    {
        public const string ProfileVersion = "PuremcoWeb-App-v1";

        public static MatchState CreateMatch(string matchId, IEnumerable<PlayerId> players, int startingSeat,
            MatchSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("A match identity is required.", nameof(matchId));
            var seats = players?.ToList() ?? throw new ArgumentNullException(nameof(players));
            if (seats.Count < 2 || seats.Count > 8 || seats.Distinct().Count() != seats.Count)
                throw new ArgumentException("Use 2–8 distinct participants.", nameof(players));
            if (startingSeat < 0 || startingSeat >= seats.Count) throw new ArgumentOutOfRangeException(nameof(startingSeat));
            return new MatchState { Id = matchId, Settings = settings ?? new MatchSettings(), Seats = seats,
                InitialStarterSeat = startingSeat, Phase = MatchPhase.AwaitingReady };
        }

        /// <summary>Trusted coordinator API; use a new identity and collect fresh readiness, including after abandonment.</summary>
        public static MatchState CreateNewMatch(MatchState previous, string matchId, IEnumerable<PlayerId> players,
            int startingSeat, MatchSettings settings = null)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (!previous.IsSuspended && previous.Phase != MatchPhase.MatchOver)
                throw new InvalidOperationException("Finish or suspend the old match before replacing it.");
            if (matchId == previous.Id) throw new ArgumentException("A new match needs a new identity.", nameof(matchId));
            return CreateMatch(matchId, players, startingSeat, settings ?? previous.Settings);
        }

        /// <summary>The host calls this only after establishing connection loss or usable resynchronization.</summary>
        public static TransitionResult SetSuspended(MatchState state, PositionToken expected, bool suspended)
        {
            if (!state.Token.Equals(expected)) return Reject(state, RuleError.StalePosition);
            if (state.IsSuspended == suspended) return new TransitionResult(state, RuleError.None);
            var next = state.Copy(); next.IsSuspended = suspended; next.Revision++;
            return new TransitionResult(next, RuleError.None, new[] { new DomainEvent(EventKind.SuspensionChanged) });
        }

        /// <summary>Deck index zero is drawn first. Deal round-robin in fixed seat order, then retain the remainder.</summary>
        public static TransitionResult StartRound(MatchState state, PositionToken expected, IEnumerable<int> orderedDeck)
        {
            if (!state.Token.Equals(expected)) return Reject(state, RuleError.StalePosition);
            if (state.IsSuspended) return Reject(state, RuleError.Suspended);
            if (state.Phase != MatchPhase.AwaitingDeck) return Reject(state, RuleError.WrongPhase);
            int index = state.Results.Count;
            var deck = orderedDeck?.ToArray();
            int engine = DominoCatalog.EngineForRound(index);
            if (deck == null || deck.Length != 90 || deck.Distinct().Count() != 90 ||
                deck.Any(id => id < 0 || id >= 91 || id == engine)) return Reject(state, RuleError.InvalidDeck);
            var next = state.Copy();
            int starter = (state.InitialStarterSeat + index) % state.Seats.Count;
            var round = new RoundState { Index = index, StarterSeat = starter,
                Tracks = state.Seats.Select(p => new TrainState(TrainId.Personal(p))).Append(new TrainState(TrainId.Shared)).ToList(),
                Hands = state.Seats.ToDictionary(p => p, p => new List<int>()),
                Notices = state.Seats.ToDictionary(p => p, p => new AnnouncementStatus(p)),
                Turn = new TurnState { Actor = state.Seats[starter], Number = 0, Requirement = PlayRequirement.Ordinary } };
            int dealt = DominoCatalog.HandSize(state.Seats.Count) * state.Seats.Count;
            for (int i = 0; i < dealt; i++) round.Hands[state.Seats[i % state.Seats.Count]].Add(deck[i]);
            round.Deck = deck.Skip(dealt).ToList();
            next.Round = round; next.Phase = MatchPhase.Playing; next.ReadySeats.Clear(); next.Revision++;
            return new TransitionResult(next, RuleError.None, new[] { new DomainEvent(EventKind.RoundStarted) });
        }

        public static TransitionResult Apply(MatchState state, PlayerId authenticatedActor, GameCommand command)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (command == null) return Reject(state, RuleError.InvalidCommand);
            if (!state.Seats.Contains(authenticatedActor)) return Reject(state, RuleError.UnknownPlayer);
            if (!state.Token.Equals(command.Expected)) return Reject(state, RuleError.StalePosition);
            if (state.IsSuspended) return Reject(state, RuleError.Suspended);
            if (command is Ready) return ApplyReady(state, authenticatedActor);
            if (state.Phase != MatchPhase.Playing) return Reject(state, RuleError.WrongPhase);
            // Announcements and challenges intentionally accept authenticated non-active participants.
            if (command is AnnounceOneTile) return ApplyAnnouncement(state, authenticatedActor);
            if (command is ChallengeMissedAnnouncement challenge) return ApplyChallenge(state, authenticatedActor, challenge.Target);
            if (state.Round.Turn.Actor != authenticatedActor) return Reject(state, RuleError.NotYourTurn);
            var next = state.Copy();
            var events = new List<DomainEvent>();
            RuleError error;
            if (command is Place place) error = ApplyPlacement(next, authenticatedActor, place, events);
            else if (command is Draw) error = ApplyDraw(next, authenticatedActor, events);
            else if (command is Pass) error = ApplyPass(next, authenticatedActor, events);
            else if (command is EndTurn) error = ApplyEndTurn(next, authenticatedActor, events);
            else if (command is Undo) error = ApplyUndo(next, events);
            else error = RuleError.InvalidCommand;
            if (error != RuleError.None) return Reject(state, error);
            next.Revision++;
            return new TransitionResult(next, RuleError.None, events);
        }

        private static TransitionResult ApplyReady(MatchState state, PlayerId player)
        {
            if (state.Phase != MatchPhase.AwaitingReady && state.Phase != MatchPhase.RoundOver)
                return Reject(state, RuleError.WrongPhase);
            if (state.ReadySeats.Contains(player)) return new TransitionResult(state, RuleError.None);
            var next = state.Copy(); next.ReadySeats.Add(player); next.Revision++;
            if (next.ReadySeats.Count == next.Seats.Count) next.Phase = MatchPhase.AwaitingDeck;
            return new TransitionResult(next, RuleError.None, new[] { new DomainEvent(EventKind.ReadyChanged, player) });
        }

        private static TransitionResult Reject(MatchState state, RuleError error) => new TransitionResult(state, error);

        private static void ScoreRound(MatchState state, RoundEndReason reason, PlayerId? endingPlayer, List<DomainEvent> events)
        {
            var r = state.Round;
            if (state.Results.Any(result => result.RoundIndex == r.Index))
                throw new InvalidOperationException("Round already scored.");
            state.Results.Add(new RoundResult(r.Index, reason, endingPlayer,
                r.Hands.ToDictionary(p => p.Key, p => p.Value.Sum(id => DominoCatalog.Get(id).PipTotal))));
            state.ReadySeats.Clear();
            foreach (var notice in r.Notices.Values) notice.ChallengeOpen = false;
            events.Add(new DomainEvent(EventKind.RoundEnded, endingPlayer));
            if (r.Index == 12)
            {
                var totals = state.Totals;
                int lowest = totals.Values.Min();
                state.Result = new MatchResult(state.Seats.Where(p => totals[p] == lowest));
                state.Phase = MatchPhase.MatchOver;
                events.Add(new DomainEvent(EventKind.MatchEnded));
            }
            else state.Phase = MatchPhase.RoundOver;
        }
    }
}
