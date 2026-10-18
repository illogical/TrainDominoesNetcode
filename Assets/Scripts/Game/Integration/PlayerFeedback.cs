using System;
using System.Collections.Generic;
using System.Linq;
using TrainDominoesRulesEngine;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>Player-facing wording (VIEW-002/VIEW-004). Presentation only: it never decides what is legal.</summary>
    public static class PlayerText
    {
        public static string Name(int player) => "Player " + (player + 1);

        /// <summary>Text for a rejected command. Unknown values fall back to a generic sentence, never the enum name.</summary>
        public static string Rejection(RuleError error)
        {
            switch (error)
            {
                case RuleError.None: return "";
                case RuleError.UnknownPlayer: return "You are not seated in this match.";
                case RuleError.StalePosition: return "The board changed before your move arrived. Try again from the current position.";
                case RuleError.Suspended: return "The match is paused until everyone is back.";
                case RuleError.WrongPhase: return "That action is not available right now.";
                case RuleError.NotYourTurn: return "It is not your turn.";
                case RuleError.UnknownTile: return "That tile is not part of this match.";
                case RuleError.UnknownTrain: return "That train is not part of this match.";
                case RuleError.TileNotHeld: return "That tile is no longer in your hand.";
                case RuleError.PlacementNotLegal: return "That tile cannot be played on that train.";
                case RuleError.DrawNotAllowed: return "You cannot draw right now.";
                case RuleError.PassNotAllowed: return "You cannot pass right now.";
                case RuleError.CommitNotAllowed: return "Your turn cannot end yet.";
                case RuleError.UndoNotAllowed: return "There is nothing to undo.";
                case RuleError.AnnouncementNotRequired: return "No announcement is needed.";
                case RuleError.ChallengeNotAllowed: return "That challenge is not allowed now. The chance may have passed.";
                case RuleError.InvalidDeck: return "The host could not deal this round.";
                case RuleError.InvalidCommand: return "That request was not understood.";
                default: return "That action was not accepted.";
            }
        }

        public static string Tile(int dominoId)
        {
            if (dominoId < 0) return "a tile";
            var tile = DominoCatalog.Get(dominoId);
            return "[" + tile.Low + "|" + tile.High + "]";
        }

        /// <summary>Round result heading. A blocked round has no ending player.</summary>
        public static string RoundEnding(int endingPlayer) =>
            endingPlayer < 0 ? "Blocked round: no one could play" : Name(endingPlayer) + " went out";

        /// <summary>Match result heading. Every tied lowest total is a joint winner; there is no tiebreaker (RULES-005).</summary>
        public static string Winners(IReadOnlyList<int> winners)
        {
            if (winners == null || winners.Count == 0) return "No winner";
            if (winners.Count == 1) return Name(winners[0]) + " wins";
            return "Joint winners: " + string.Join(" & ", winners.Select(Name));
        }
    }

    /// <summary>
    /// Turns recipient-filtered presentation events into short notices, each exactly once (VIEW-004).
    /// Events are keyed by (match, revision, source index): every accepted transition has its own revision, and the
    /// source index identifies an event within it. A resynchronization replays nothing already shown; events that the
    /// client never received are not reconstructed, because the latest view stands on its own.
    /// </summary>
    public sealed class EventFeedback
    {
        private readonly HashSet<(long Revision, int Source)> shown = new HashSet<(long, int)>();
        private string match;

        public IReadOnlyList<string> Consume(PlayerViewDto before, PlayerViewDto after, PresentationEventDto[] events)
        {
            var notices = new List<string>();
            if (after == null || events == null || events.Length == 0) return notices;
            if (after.Token.MatchId != match) { shown.Clear(); match = after.Token.MatchId; }
            bool sameMatch = before != null && before.Token.MatchId == after.Token.MatchId && before.Token.RoundIndex == after.Token.RoundIndex;
            var fresh = events.Where(e => shown.Add((after.Token.Revision, e.SourceIndex))).ToArray();
            foreach (var e in fresh)
            {
                switch (e.Kind)
                {
                    case EventKind.OneTileAnnounced:
                        notices.Add(e.Player == after.Player ? "You announced your last tile." : PlayerText.Name(e.Player) + " announced one tile left.");
                        break;
                    case EventKind.MissedAnnouncement:
                        notices.Add(Missed(e.Player, after, sameMatch ? before : null, fresh));
                        break;
                    case EventKind.PenaltyTileDrawn:
                        // Normally reported with its MissedAnnouncement; alone, it is still the owner's private tile.
                        if (!fresh.Any(m => m.Kind == EventKind.MissedAnnouncement && m.Player == e.Player))
                            notices.Add("You drew a penalty tile " + PlayerText.Tile(e.DominoId) + ".");
                        break;
                }
            }
            return notices;
        }

        private static string Missed(int target, PlayerViewDto after, PlayerViewDto before, PresentationEventDto[] events)
        {
            bool none = after.Settings != null && after.Settings.MissedAnnouncementPenalty == AnnouncementPenalty.None;
            if (target == after.Player)
            {
                var penalty = events.FirstOrDefault(e => e.Kind == EventKind.PenaltyTileDrawn && e.Player == target);
                string head = "You were challenged for not announcing your last tile";
                if (penalty != null) return head + ": you drew a penalty tile " + PlayerText.Tile(penalty.DominoId) + ".";
                return head + (none ? ". This match has no penalty." : ". No penalty: the boneyard is empty.");
            }
            string them = PlayerText.Name(target) + " was challenged for not announcing one tile";
            if (none) return them + ". This match has no penalty.";
            // Opponents never see the penalty tile, only the public counts.
            int? drawn = before == null ? (int?)null : Count(after, target) - Count(before, target);
            if (drawn == 1) return them + " and drew a penalty tile.";
            if (drawn == 0 && before.BoneyardCount == 0) return them + ". No penalty: the boneyard is empty.";
            return them + ".";
        }

        private static int Count(PlayerViewDto view, int player) => view.HandCounts?.FirstOrDefault(c => c.Player == player)?.Count ?? 0;
    }
}
