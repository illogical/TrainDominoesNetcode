using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TrainDominoesRulesEngine
{
    public sealed class TrainView
    {
        public TrainId Id { get; }
        public bool IsMarked { get; }
        public IReadOnlyList<Placement> Placements { get; }
        internal TrainView(TrainState train)
        { Id = train.Id; IsMarked = train.IsMarked; Placements = Array.AsReadOnly(train.Tiles.ToArray()); }
    }

    /// <summary>Transport-neutral projection; contains only this player's private hand and permitted public data.</summary>
    public sealed class PlayerView
    {
        public string ProfileVersion => RulesEngine.ProfileVersion;
        public PositionToken Token { get; internal set; }
        public MatchSettings Settings { get; internal set; }
        public MatchPhase Phase { get; internal set; }
        public bool IsSuspended { get; internal set; }
        public PlayerId Player { get; internal set; }
        public PlayerId? ActivePlayer { get; internal set; }
        public int? EngineId { get; internal set; }
        public int BoneyardCount { get; internal set; }
        public IReadOnlyList<PlayerId> Players { get; internal set; }
        public IReadOnlyList<PlayerId> ReadyPlayers { get; internal set; }
        public IReadOnlyList<int> Hand { get; internal set; }
        public IReadOnlyDictionary<PlayerId, int> HandCounts { get; internal set; }
        public IReadOnlyList<TrainView> Trains { get; internal set; }
        public PlayRequirement? Requirement { get; internal set; }
        public TrainId? RequiredTrain { get; internal set; }
        public bool DrawAttemptUsed { get; internal set; }
        public int? RequiredDrawnTile { get; internal set; }
        public IReadOnlyList<TrainId> OpenDoubles { get; internal set; }
        public IReadOnlyList<PlayerId> AnnouncedPlayers { get; internal set; }
        public AllowedActions Allowed { get; internal set; }
        public IReadOnlyList<LegalPlacement> LegalPlacements { get; internal set; }
        public IReadOnlyList<RoundResult> RoundResults { get; internal set; }
        public IReadOnlyDictionary<PlayerId, int> Totals { get; internal set; }
        public MatchResult Result { get; internal set; }
    }

    public static partial class RulesEngine
    {
        public static PlayerView GetPlayerView(MatchState state, PlayerId player)
        {
            if (!state.Seats.Contains(player)) throw new ArgumentException("Unknown participant.", nameof(player));
            var r = state.Round;
            // The oldest remaining undo checkpoint is the last public board and public hand-count boundary.
            var visible = r == null ? null : r.Turn.Actor == player ? r : PublicRound(r);
            return new PlayerView
            {
                Token = state.Token, Settings = state.Settings, Phase = state.Phase, IsSuspended = state.IsSuspended,
                Player = player, ActivePlayer = r?.Turn.Actor, EngineId = r?.EngineId,
                BoneyardCount = r?.Deck.Count ?? 0, Players = Array.AsReadOnly(state.Seats.ToArray()),
                ReadyPlayers = state.ReadyPlayers, Hand = Array.AsReadOnly(r == null ? Array.Empty<int>() : r.Hands[player].ToArray()),
                HandCounts = new ReadOnlyDictionary<PlayerId, int>(visible == null ? new Dictionary<PlayerId, int>() :
                    visible.Hands.ToDictionary(p => p.Key, p => p.Value.Count)),
                Trains = Array.AsReadOnly(visible == null ? Array.Empty<TrainView>() : visible.Tracks.Select(t => new TrainView(t)).ToArray()),
                Requirement = visible?.Turn.Requirement, RequiredTrain = visible?.Turn.InheritedDouble,
                DrawAttemptUsed = visible?.Turn.DrawAttemptUsed ?? false,
                RequiredDrawnTile = r?.Turn.Actor == player ? r.Turn.RequiredDrawnTile : null,
                OpenDoubles = Array.AsReadOnly(visible == null ? Array.Empty<TrainId>() : visible.Turn.Doubles.ToArray()),
                AnnouncedPlayers = Array.AsReadOnly(r == null ? Array.Empty<PlayerId>() : r.Notices.Values.Where(n => n.Announced).Select(n => n.Player).ToArray()),
                Allowed = GetAllowedActions(state, player), LegalPlacements = GetLegalPlacements(state, player),
                RoundResults = Array.AsReadOnly(state.Results.ToArray()), Totals = state.Totals, Result = state.Result
            };
        }

        public static IReadOnlyList<DomainEvent> GetEventsForPlayer(TransitionResult result, PlayerId player)
        {
            if (!result.State.Seats.Contains(player)) throw new ArgumentException("Unknown participant.", nameof(player));
            return Array.AsReadOnly(result.Events.Where(e => !e.Audience.HasValue || e.Audience.Value == player).ToArray());
        }
    }
}
