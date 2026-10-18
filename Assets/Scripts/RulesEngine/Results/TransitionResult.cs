using System;
using System.Collections.Generic;
using System.Linq;

namespace TrainDominoesRulesEngine
{
    public enum RuleError
    {
        None, UnknownPlayer, StalePosition, Suspended, WrongPhase, NotYourTurn, UnknownTile,
        UnknownTrain, TileNotHeld, PlacementNotLegal, DrawNotAllowed, PassNotAllowed,
        CommitNotAllowed, UndoNotAllowed, AnnouncementNotRequired, ChallengeNotAllowed, InvalidDeck, InvalidCommand
    }
    public enum EventKind
    {
        ReadyChanged, RoundStarted, TilePlaced, DraftChanged, PlacementsSealed, TileDrawn,
        TurnEnded, RoundEnded, MatchEnded, OneTileAnnounced, MissedAnnouncement, PenaltyTileDrawn,
        SuspensionChanged
    }
    /// <summary>Private events must be routed only to Audience. Public events never contain hidden tile IDs.</summary>
    public sealed class DomainEvent
    {
        public EventKind Kind { get; }
        public PlayerId? Player { get; }
        public PlayerId? Audience { get; }
        public int? DominoId { get; }
        internal DomainEvent(EventKind kind, PlayerId? player = null, PlayerId? audience = null, int? dominoId = null)
        { Kind = kind; Player = player; Audience = audience; DominoId = dominoId; }
    }
    public sealed class TransitionResult
    {
        public bool Accepted => Error == RuleError.None;
        public RuleError Error { get; }
        public MatchState State { get; }
        public IReadOnlyList<DomainEvent> Events { get; }
        internal TransitionResult(MatchState state, RuleError error, IEnumerable<DomainEvent> events = null)
        { State = state; Error = error; Events = Array.AsReadOnly((events ?? Enumerable.Empty<DomainEvent>()).ToArray()); }
    }
}
