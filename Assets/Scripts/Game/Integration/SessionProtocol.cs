using System;
using System.Linq;
using TrainDominoesRulesEngine;

namespace Assets.Scripts.Game.Integration
{
    public enum CommandKind { Ready, Place, Draw, Pass, Undo, EndTurn, Announce, Challenge }
    [Serializable] public sealed class TokenDto
    {
        public string MatchId;
        public int RoundIndex;
        public long TurnNumber;
        public long Revision;
        public PositionToken ToToken() => new PositionToken(MatchId, RoundIndex, TurnNumber, Revision);
        public static TokenDto From(PositionToken value) => new TokenDto { MatchId = value.MatchId, RoundIndex = value.RoundIndex, TurnNumber = value.TurnNumber, Revision = value.Revision };
    }
    [Serializable] public sealed class TrainIdDto
    {
        public bool IsShared;
        public int Owner = -1;
        public TrainId ToId() => IsShared ? TrainId.Shared : TrainId.Personal(new PlayerId(Owner));
        public static TrainIdDto From(TrainId id) => new TrainIdDto { IsShared = id.IsShared, Owner = id.Owner?.Value ?? -1 };
    }
    [Serializable] public sealed class CommandRequest
    {
        public string RequestId;
        public TokenDto Expected;
        public CommandKind Kind;
        public int DominoId = -1;
        public TrainIdDto Train;
        public int Target = -1;
    }
    [Serializable] public sealed class CommandOutcome
    {
        public string RequestId;
        public bool Accepted;
        public RuleError Error;
        public string Detail;
        public TokenDto Position;
    }
    [Serializable] public sealed class PlayerCountDto { public int Player; public int Count; }
    [Serializable] public sealed class PlacementDto { public int DominoId; public int Inward; public int Outward; public int PlayedBy; public bool IsProvisional; }
    [Serializable] public sealed class TrainViewDto { public TrainIdDto Id; public bool IsMarked; public PlacementDto[] Placements; }
    [Serializable] public sealed class LegalPlacementDto { public int DominoId; public TrainIdDto Train; }
    [Serializable] public sealed class AllowedActionsDto
    {
        public bool CanDraw, CanPass, CanEndTurn, CanUndo, CanReady, CanAnnounce;
        public int[] ChallengeTargets;
    }
    [Serializable] public sealed class SettingsDto { public CommitmentMode Commitment; public AnnouncementPenalty MissedAnnouncementPenalty; }
    [Serializable] public sealed class RoundResultDto { public int RoundIndex; public RoundEndReason Reason; public int EndingPlayer = -1; public PlayerCountDto[] Scores; }
    [Serializable] public sealed class PlayerViewDto
    {
        public string ProfileVersion;
        public TokenDto Token;
        public SettingsDto Settings;
        public MatchPhase Phase;
        public bool IsSuspended;
        public int Player, ActivePlayer = -1, EngineId = -1, BoneyardCount;
        public int[] Players, ReadyPlayers, Hand, AnnouncedPlayers;
        public PlayerCountDto[] HandCounts, Totals;
        public TrainViewDto[] Trains;
        public bool HasRequirement;
        public PlayRequirement Requirement;
        public TrainIdDto RequiredTrain;
        public bool DrawAttemptUsed;
        public int RequiredDrawnTile = -1;
        public TrainIdDto[] OpenDoubles;
        public AllowedActionsDto Allowed;
        public LegalPlacementDto[] LegalPlacements;
        public RoundResultDto[] RoundResults;
        public int[] Winners;
        public static PlayerViewDto From(PlayerView v) => new PlayerViewDto {
            ProfileVersion = v.ProfileVersion, Token = TokenDto.From(v.Token), Settings = new SettingsDto { Commitment = v.Settings.Commitment, MissedAnnouncementPenalty = v.Settings.MissedAnnouncementPenalty },
            Phase = v.Phase, IsSuspended = v.IsSuspended, Player = v.Player.Value, ActivePlayer = v.ActivePlayer?.Value ?? -1, EngineId = v.EngineId ?? -1, BoneyardCount = v.BoneyardCount,
            Players = v.Players.Select(p => p.Value).ToArray(), ReadyPlayers = v.ReadyPlayers.Select(p => p.Value).ToArray(), Hand = v.Hand.ToArray(), AnnouncedPlayers = v.AnnouncedPlayers.Select(p => p.Value).ToArray(),
            HandCounts = v.HandCounts.Select(p => new PlayerCountDto { Player = p.Key.Value, Count = p.Value }).ToArray(), Totals = v.Totals.Select(p => new PlayerCountDto { Player = p.Key.Value, Count = p.Value }).ToArray(),
            Trains = v.Trains.Select(t => new TrainViewDto { Id = TrainIdDto.From(t.Id), IsMarked = t.IsMarked, Placements = t.Placements.Select(p => new PlacementDto { DominoId = p.DominoId, Inward = p.Inward, Outward = p.Outward, PlayedBy = p.PlayedBy.Value, IsProvisional = p.IsProvisional }).ToArray() }).ToArray(),
            HasRequirement = v.Requirement.HasValue, Requirement = v.Requirement ?? PlayRequirement.Ordinary, RequiredTrain = v.RequiredTrain.HasValue ? TrainIdDto.From(v.RequiredTrain.Value) : null,
            DrawAttemptUsed = v.DrawAttemptUsed, RequiredDrawnTile = v.RequiredDrawnTile ?? -1, OpenDoubles = v.OpenDoubles.Select(TrainIdDto.From).ToArray(),
            Allowed = new AllowedActionsDto { CanDraw = v.Allowed.CanDraw, CanPass = v.Allowed.CanPass, CanEndTurn = v.Allowed.CanEndTurn, CanUndo = v.Allowed.CanUndo, CanReady = v.Allowed.CanReady, CanAnnounce = v.Allowed.CanAnnounce, ChallengeTargets = v.Allowed.ChallengeTargets.Select(p => p.Value).ToArray() },
            LegalPlacements = v.LegalPlacements.Select(p => new LegalPlacementDto { DominoId = p.DominoId, Train = TrainIdDto.From(p.Train) }).ToArray(),
            RoundResults = v.RoundResults.Select(r => new RoundResultDto { RoundIndex = r.RoundIndex, Reason = r.Reason, EndingPlayer = r.EndingPlayer?.Value ?? -1, Scores = r.Scores.Select(p => new PlayerCountDto { Player = p.Key.Value, Count = p.Value }).ToArray() }).ToArray(), Winners = v.Result?.Winners.Select(p => p.Value).ToArray() ?? Array.Empty<int>()
        };
    }
    [Serializable] public sealed class PresentationEventDto { public int SourceIndex; public EventKind Kind; public int Player = -1; public int DominoId = -1; }
    [Serializable] public sealed class ProjectionPacket { public PlayerViewDto View; public PresentationEventDto[] Events = Array.Empty<PresentationEventDto>(); public CommandOutcome Outcome; }
    public sealed class RecipientPacket { public ulong Recipient; public ProjectionPacket Packet; }
}
