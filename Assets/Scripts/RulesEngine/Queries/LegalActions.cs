using System;
using System.Collections.Generic;
using System.Linq;

namespace TrainDominoesRulesEngine
{
    public sealed class LegalPlacement
    {
        public int DominoId { get; }
        public TrainId Train { get; }
        internal LegalPlacement(int id, TrainId train) { DominoId = id; Train = train; }
    }
    public sealed class AllowedActions
    {
        public bool CanDraw { get; internal set; }
        public bool CanPass { get; internal set; }
        public bool CanEndTurn { get; internal set; }
        public bool CanUndo { get; internal set; }
        public bool CanReady { get; internal set; }
        public bool CanAnnounce { get; internal set; }
        public IReadOnlyList<PlayerId> ChallengeTargets { get; internal set; } = Array.AsReadOnly(Array.Empty<PlayerId>());
    }

    public static partial class RulesEngine
    {
        public static IReadOnlyList<LegalPlacement> GetLegalPlacements(MatchState state, PlayerId player)
        {
            if (state.IsSuspended || state.Phase != MatchPhase.Playing || state.Round.Turn.Actor != player)
                return Array.AsReadOnly(Array.Empty<LegalPlacement>());
            return Array.AsReadOnly(LegalPlacements(state.Round, player).ToArray());
        }

        public static AllowedActions GetAllowedActions(MatchState state, PlayerId player)
        {
            var actions = new AllowedActions();
            if (state.IsSuspended || !state.Seats.Contains(player)) return actions;
            actions.CanReady = (state.Phase == MatchPhase.AwaitingReady || state.Phase == MatchPhase.RoundOver)
                && !state.ReadySeats.Contains(player);
            if (state.Phase != MatchPhase.Playing) return actions;
            var r = state.Round;
            actions.CanAnnounce = PublicRound(r).Hands[player].Count == 1 && !r.Notices[player].Announced;
            actions.ChallengeTargets = Array.AsReadOnly(state.Seats.Where(p => p != player && r.Notices[p].ChallengeOpen).ToArray());
            if (r.Turn.Actor != player) return actions;
            actions.CanDraw = CanDraw(r, player);
            actions.CanPass = CanPass(r, player);
            actions.CanUndo = state.Settings.Commitment == CommitmentMode.Draft && r.Turn.Undo.Count > 0;
            actions.CanEndTurn = state.Settings.Commitment == CommitmentMode.Draft && r.Turn.Requirement == PlayRequirement.Complete;
            return actions;
        }

        public static bool CanAccessTrain(MatchState state, PlayerId player, TrainId train)
        {
            if (!state.Seats.Contains(player) || state.Round == null) return false;
            var target = state.Round.Tracks.FirstOrDefault(t => t.Id == train);
            return target != null && HasAccess(state.Round, player, target);
        }

        private static IEnumerable<LegalPlacement> LegalPlacements(RoundState r, PlayerId player)
        {
            foreach (int id in r.Hands[player])
                foreach (var train in r.Tracks)
                    if (IsLegalPlacement(r, player, id, train)) yield return new LegalPlacement(id, train.Id);
        }

        private static bool HasAccess(RoundState r, PlayerId actor, TrainState train)
        {
            if (r.Turn.Requirement == PlayRequirement.CoverInheritedDouble)
                return train.Id == r.Turn.InheritedDouble;
            if (r.Turn.Requirement == PlayRequirement.CoverOneOfTwo)
                return r.Turn.Doubles.Contains(train.Id);
            return train.Id.IsShared || train.Id.Owner == actor || train.IsMarked;
        }

        private static bool IsLegalPlacement(RoundState r, PlayerId actor, int id, TrainState train)
        {
            if (r.Turn.Requirement == PlayRequirement.Complete || !HasAccess(r, actor, train) ||
                (r.Turn.RequiredDrawnTile.HasValue && r.Turn.RequiredDrawnTile.Value != id)) return false;
            var tile = DominoCatalog.Get(id);
            int end = train.Endpoint(DominoCatalog.Get(r.EngineId).Low);
            if (tile.Low != end && tile.High != end) return false;
            // Last tile wins before imposing an additional-play or held-third-tile requirement.
            if (r.Hands[actor].Count == 1) return true;
            if (r.Turn.Requirement == PlayRequirement.CoverOneOfTwo) return !tile.IsDouble;
            if (tile.IsDouble && r.Turn.Requirement == PlayRequirement.AdditionalPlay)
            {
                if (r.Turn.Doubles.Count != 1 || r.Turn.Doubles.Contains(train.Id)) return false;
                int firstPips = r.Tracks.Single(t => t.Id == r.Turn.Doubles[0]).Endpoint(DominoCatalog.Get(r.EngineId).Low);
                return r.Hands[actor].Where(other => other != id).Select(DominoCatalog.Get)
                    .Any(cover => !cover.IsDouble && (cover.Low == firstPips || cover.High == firstPips ||
                        cover.Low == tile.Low || cover.High == tile.Low));
            }
            return true;
        }

        private static bool CanDraw(RoundState r, PlayerId actor) =>
            r.Turn.Requirement != PlayRequirement.Complete && r.Turn.Requirement != PlayRequirement.CoverOneOfTwo &&
            !r.Turn.DrawAttemptUsed && r.Deck.Count > 0 && !LegalPlacements(r, actor).Any();

        private static bool CanPass(RoundState r, PlayerId actor) =>
            r.Turn.Requirement != PlayRequirement.Complete && r.Turn.Requirement != PlayRequirement.CoverOneOfTwo &&
            (r.Turn.DrawAttemptUsed || r.Deck.Count == 0) && !LegalPlacements(r, actor).Any();

        private static RoundState PublicRound(RoundState r) => r.Turn.Undo.Count == 0 ? r : r.Turn.Undo[0];
    }
}
