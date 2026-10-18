using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrainDominoesRulesEngine.Tests")]

namespace TrainDominoesRulesEngine
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public int Value { get; }
        public PlayerId(int value) { Value = value; }
        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(PlayerId a, PlayerId b) => a.Equals(b);
        public static bool operator !=(PlayerId a, PlayerId b) => !a.Equals(b);
    }

    public readonly struct TrainId : IEquatable<TrainId>
    {
        public bool IsShared { get; }
        public PlayerId? Owner { get; }
        private TrainId(bool shared, PlayerId? owner) { IsShared = shared; Owner = owner; }
        public static TrainId Shared => new TrainId(true, null);
        public static TrainId Personal(PlayerId owner) => new TrainId(false, owner);
        public bool Equals(TrainId other) => IsShared == other.IsShared && Owner == other.Owner;
        public override bool Equals(object obj) => obj is TrainId other && Equals(other);
        public override int GetHashCode() => IsShared ? -1 : Owner.GetHashCode();
        public override string ToString() => IsShared ? "Shared" : "Personal:" + Owner;
        public static bool operator ==(TrainId a, TrainId b) => a.Equals(b);
        public static bool operator !=(TrainId a, TrainId b) => !a.Equals(b);
    }

    public sealed class Domino
    {
        public int Id { get; }
        public int Low { get; }
        public int High { get; }
        public bool IsDouble => Low == High;
        public int PipTotal => Low + High;
        internal Domino(int id, int low, int high) { Id = id; Low = low; High = high; }
    }

    public static class DominoCatalog
    {
        private static readonly Domino[] Tiles = MakeTiles();
        public static IReadOnlyList<Domino> All { get; } = Array.AsReadOnly(Tiles);
        private static Domino[] MakeTiles()
        {
            var tiles = new List<Domino>();
            for (int low = 0; low <= 12; low++)
                for (int high = low; high <= 12; high++)
                    tiles.Add(new Domino(tiles.Count, low, high));
            return tiles.ToArray();
        }
        public static Domino Get(int id)
        {
            if (id < 0 || id >= Tiles.Length) throw new ArgumentOutOfRangeException(nameof(id));
            return Tiles[id];
        }
        public static int Id(int a, int b)
        {
            if (a < 0 || a > 12 || b < 0 || b > 12) throw new ArgumentOutOfRangeException();
            int low = Math.Min(a, b), high = Math.Max(a, b);
            return Tiles.First(t => t.Low == low && t.High == high).Id;
        }
        public static int EngineForRound(int roundIndex)
        {
            if (roundIndex < 0 || roundIndex > 12) throw new ArgumentOutOfRangeException(nameof(roundIndex));
            return Id(12 - roundIndex, 12 - roundIndex);
        }
        public static int HandSize(int players)
        {
            if (players < 2 || players > 8) throw new ArgumentOutOfRangeException(nameof(players));
            return new[] { 16, 15, 14, 12, 11, 10, 9 }[players - 2];
        }
    }

    public enum CommitmentMode { Immediate, Draft }
    public enum AnnouncementPenalty { DrawOneTile, None }
    public enum MatchPhase { AwaitingReady, AwaitingDeck, Playing, RoundOver, MatchOver }
    public enum PlayRequirement { Ordinary, AdditionalPlay, CoverInheritedDouble, CoverOneOfTwo, Complete }
    public enum RoundEndReason { EmptyHand, Blocked }

    public sealed class MatchSettings
    {
        public CommitmentMode Commitment { get; }
        public AnnouncementPenalty MissedAnnouncementPenalty { get; }
        public MatchSettings(CommitmentMode commitment = CommitmentMode.Immediate,
            AnnouncementPenalty missedAnnouncementPenalty = AnnouncementPenalty.DrawOneTile)
        {
            if (!Enum.IsDefined(typeof(CommitmentMode), commitment) ||
                !Enum.IsDefined(typeof(AnnouncementPenalty), missedAnnouncementPenalty))
                throw new ArgumentOutOfRangeException();
            Commitment = commitment;
            MissedAnnouncementPenalty = missedAnnouncementPenalty;
        }
    }

    public readonly struct PositionToken : IEquatable<PositionToken>
    {
        public string MatchId { get; }
        public int RoundIndex { get; }
        public long TurnNumber { get; }
        public long Revision { get; }
        public PositionToken(string matchId, int round, long turn, long revision)
        { MatchId = matchId; RoundIndex = round; TurnNumber = turn; Revision = revision; }
        public bool Equals(PositionToken other) => MatchId == other.MatchId && RoundIndex == other.RoundIndex
            && TurnNumber == other.TurnNumber && Revision == other.Revision;
        public override bool Equals(object obj) => obj is PositionToken other && Equals(other);
        public override int GetHashCode() => Revision.GetHashCode() ^ (MatchId?.GetHashCode() ?? 0);
    }

    public sealed class Placement
    {
        public int DominoId { get; }
        public int Inward { get; }
        public int Outward { get; }
        public PlayerId PlayedBy { get; }
        public bool IsProvisional { get; }
        internal Placement(int id, int inward, int outward, PlayerId player, bool provisional)
        { DominoId = id; Inward = inward; Outward = outward; PlayedBy = player; IsProvisional = provisional; }
        internal Placement Seal() => new Placement(DominoId, Inward, Outward, PlayedBy, false);
    }

    public sealed class TrainState
    {
        public TrainId Id { get; }
        public bool IsMarked { get; internal set; }
        internal List<Placement> Tiles = new List<Placement>();
        public IReadOnlyList<Placement> Placements => Tiles.AsReadOnly();
        public int Endpoint(int enginePips) => Tiles.Count == 0 ? enginePips : Tiles[Tiles.Count - 1].Outward;
        internal TrainState(TrainId id) { Id = id; }
        internal TrainState Copy() => new TrainState(Id) { IsMarked = IsMarked, Tiles = new List<Placement>(Tiles) };
    }

    public sealed class TurnState
    {
        public PlayerId Actor { get; internal set; }
        public long Number { get; internal set; }
        public PlayRequirement Requirement { get; internal set; }
        public bool DrawAttemptUsed { get; internal set; }
        public int? RequiredDrawnTile { get; internal set; }
        public TrainId? InheritedDouble { get; internal set; }
        internal List<TrainId> Doubles = new List<TrainId>();
        public IReadOnlyList<TrainId> OpenDoubles => Doubles.AsReadOnly();
        internal List<RoundState> Undo = new List<RoundState>();
        public int UndoCount => Undo.Count;
        internal TurnState Copy(bool includeUndo) => new TurnState
        {
            Actor = Actor, Number = Number, Requirement = Requirement, DrawAttemptUsed = DrawAttemptUsed,
            RequiredDrawnTile = RequiredDrawnTile, InheritedDouble = InheritedDouble,
            Doubles = new List<TrainId>(Doubles), Undo = includeUndo ? new List<RoundState>(Undo) : new List<RoundState>()
        };
    }

    public sealed class AnnouncementStatus
    {
        public PlayerId Player { get; }
        public bool Announced { get; internal set; }
        public bool ChallengeOpen { get; internal set; }
        public long QualifyingTurn { get; internal set; }
        internal AnnouncementStatus(PlayerId player) { Player = player; }
        internal AnnouncementStatus Copy() => new AnnouncementStatus(Player)
        { Announced = Announced, ChallengeOpen = ChallengeOpen, QualifyingTurn = QualifyingTurn };
    }

    public sealed class RoundState
    {
        public int Index { get; internal set; }
        public int EngineId => DominoCatalog.EngineForRound(Index);
        public int StarterSeat { get; internal set; }
        public TurnState Turn { get; internal set; }
        internal Dictionary<PlayerId, List<int>> Hands = new Dictionary<PlayerId, List<int>>();
        internal List<int> Deck = new List<int>();
        internal List<TrainState> Tracks = new List<TrainState>();
        internal Dictionary<PlayerId, AnnouncementStatus> Notices = new Dictionary<PlayerId, AnnouncementStatus>();
        public IReadOnlyList<int> GetHand(PlayerId player) => Hands[player].AsReadOnly();
        public IReadOnlyList<int> Boneyard => Deck.AsReadOnly();
        public IReadOnlyList<TrainState> Trains => Tracks.AsReadOnly();
        public IReadOnlyList<AnnouncementStatus> Announcements => Array.AsReadOnly(Notices.Values.ToArray());
        internal RoundState Copy(bool includeUndo = true) => new RoundState
        {
            Index = Index, StarterSeat = StarterSeat, Turn = Turn.Copy(includeUndo),
            Hands = Hands.ToDictionary(p => p.Key, p => new List<int>(p.Value)), Deck = new List<int>(Deck),
            Tracks = Tracks.Select(t => t.Copy()).ToList(), Notices = Notices.ToDictionary(p => p.Key, p => p.Value.Copy())
        };
    }

    public sealed class RoundResult
    {
        public int RoundIndex { get; }
        public RoundEndReason Reason { get; }
        public PlayerId? EndingPlayer { get; }
        public IReadOnlyDictionary<PlayerId, int> Scores { get; }
        internal RoundResult(int index, RoundEndReason reason, PlayerId? endingPlayer, Dictionary<PlayerId, int> scores)
        {
            RoundIndex = index; Reason = reason; EndingPlayer = endingPlayer;
            Scores = new System.Collections.ObjectModel.ReadOnlyDictionary<PlayerId, int>(new Dictionary<PlayerId, int>(scores));
        }
    }

    public sealed class MatchResult
    {
        public IReadOnlyList<PlayerId> Winners { get; }
        internal MatchResult(IEnumerable<PlayerId> winners) { Winners = Array.AsReadOnly(winners.ToArray()); }
    }

    /// <summary>Full authoritative snapshot. Never broadcast this object to clients.</summary>
    public sealed class MatchState
    {
        public string ProfileVersion => RulesEngine.ProfileVersion;
        public string Id { get; internal set; }
        public MatchSettings Settings { get; internal set; }
        public MatchPhase Phase { get; internal set; }
        public bool IsSuspended { get; internal set; }
        public long Revision { get; internal set; }
        public int InitialStarterSeat { get; internal set; }
        public RoundState Round { get; internal set; }
        public MatchResult Result { get; internal set; }
        internal List<PlayerId> Seats = new List<PlayerId>();
        internal HashSet<PlayerId> ReadySeats = new HashSet<PlayerId>();
        internal List<RoundResult> Results = new List<RoundResult>();
        public IReadOnlyList<PlayerId> Players => Seats.AsReadOnly();
        public IReadOnlyList<PlayerId> ReadyPlayers => Array.AsReadOnly(Seats.Where(ReadySeats.Contains).ToArray());
        public IReadOnlyList<RoundResult> RoundResults => Results.AsReadOnly();
        public IReadOnlyDictionary<PlayerId, int> Totals => new System.Collections.ObjectModel.ReadOnlyDictionary<PlayerId, int>(
            Seats.ToDictionary(p => p, p => Results.Sum(r => r.Scores[p])));
        public PositionToken Token => new PositionToken(Id, Round?.Index ?? -1, Round?.Turn.Number ?? -1, Revision);
        internal MatchState Copy() => new MatchState
        {
            Id = Id, Settings = Settings, Phase = Phase, IsSuspended = IsSuspended, Revision = Revision,
            InitialStarterSeat = InitialStarterSeat, Round = Round?.Copy(), Result = Result,
            Seats = new List<PlayerId>(Seats), ReadySeats = new HashSet<PlayerId>(ReadySeats), Results = new List<RoundResult>(Results)
        };
    }
}
