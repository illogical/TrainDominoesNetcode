using System;
using System.Collections.Generic;
using System.Linq;

namespace TrainDominoesRulesEngine
{
    public static partial class RulesEngine
    {
        private static RuleError ApplyPlacement(MatchState state, PlayerId actor, Place place, List<DomainEvent> events)
        {
            var r = state.Round;
            if (place.DominoId < 0 || place.DominoId >= 91) return RuleError.UnknownTile;
            var train = r.Tracks.FirstOrDefault(t => t.Id == place.Train);
            if (train == null) return RuleError.UnknownTrain;
            if (!r.Hands[actor].Contains(place.DominoId)) return RuleError.TileNotHeld;
            if (!IsLegalPlacement(r, actor, place.DominoId, train)) return RuleError.PlacementNotLegal;
            ExpireChallenges(r);
            bool draft = state.Settings.Commitment == CommitmentMode.Draft;
            if (draft) r.Turn.Undo.Add(r.Copy(false));
            int inward = train.Endpoint(DominoCatalog.Get(r.EngineId).Low);
            var tile = DominoCatalog.Get(place.DominoId);
            int outward = tile.Low == inward ? tile.High : tile.Low;
            train.Tiles.Add(new Placement(tile.Id, inward, outward, actor, draft));
            r.Hands[actor].Remove(tile.Id);
            if (train.Id.Owner == actor) train.IsMarked = false;
            r.Turn.RequiredDrawnTile = null;
            if (tile.IsDouble)
            {
                r.Turn.Doubles.Add(train.Id);
                r.Turn.Requirement = r.Turn.Doubles.Count == 2 ? PlayRequirement.CoverOneOfTwo : PlayRequirement.AdditionalPlay;
                r.Turn.DrawAttemptUsed = false;
            }
            else
            {
                r.Turn.Doubles.Remove(train.Id);
                r.Turn.InheritedDouble = null;
                r.Turn.Requirement = PlayRequirement.Complete;
            }
            if (r.Hands[actor].Count == 0) r.Turn.Requirement = PlayRequirement.Complete;
            events.Add(new DomainEvent(draft ? EventKind.DraftChanged : EventKind.TilePlaced, actor,
                draft ? actor : (PlayerId?)null, tile.Id));
            if (!draft)
            {
                RefreshAnnouncement(r, actor);
                if (r.Turn.Requirement == PlayRequirement.Complete) FinishTurn(state, events);
            }
            return RuleError.None;
        }

        private static RuleError ApplyDraw(MatchState state, PlayerId actor, List<DomainEvent> events)
        {
            var r = state.Round;
            if (!CanDraw(r, actor)) return RuleError.DrawNotAllowed;
            ExpireChallenges(r);
            SealPlacements(r, events);
            int id = r.Deck[0]; r.Deck.RemoveAt(0); r.Hands[actor].Add(id);
            r.Turn.DrawAttemptUsed = true;
            // A legal draw starts with no playable held tile, so only the new tile can satisfy this requirement.
            if (r.Tracks.Any(t => IsLegalPlacement(r, actor, id, t))) r.Turn.RequiredDrawnTile = id;
            RefreshAnnouncement(r, actor);
            events.Add(new DomainEvent(EventKind.TileDrawn, actor, actor, id));
            return RuleError.None;
        }

        private static RuleError ApplyPass(MatchState state, PlayerId actor, List<DomainEvent> events)
        {
            var r = state.Round;
            if (!CanPass(r, actor)) return RuleError.PassNotAllowed;
            ExpireChallenges(r);
            SealPlacements(r, events);
            r.Tracks.Single(t => t.Id.Owner == actor).IsMarked = true;
            FinishTurn(state, events);
            return RuleError.None;
        }

        private static RuleError ApplyEndTurn(MatchState state, PlayerId actor, List<DomainEvent> events)
        {
            if (state.Settings.Commitment != CommitmentMode.Draft || state.Round.Turn.Requirement != PlayRequirement.Complete)
                return RuleError.CommitNotAllowed;
            SealPlacements(state.Round, events);
            FinishTurn(state, events);
            return RuleError.None;
        }

        private static RuleError ApplyUndo(MatchState state, List<DomainEvent> events)
        {
            var r = state.Round;
            if (state.Settings.Commitment != CommitmentMode.Draft || r.Turn.Undo.Count == 0) return RuleError.UndoNotAllowed;
            var history = r.Turn.Undo;
            var restored = history[history.Count - 1].Copy(false);
            restored.Turn.Undo = history.Take(history.Count - 1).ToList();
            // Announcements are separate irreversible actions, not part of a reversible placement.
            restored.Notices = r.Notices.ToDictionary(p => p.Key, p => p.Value.Copy());
            state.Round = restored;
            events.Add(new DomainEvent(EventKind.DraftChanged, restored.Turn.Actor, restored.Turn.Actor));
            return RuleError.None;
        }

        private static void SealPlacements(RoundState r, List<DomainEvent> events)
        {
            bool changed = false;
            foreach (var train in r.Tracks)
                for (int i = 0; i < train.Tiles.Count; i++)
                    if (train.Tiles[i].IsProvisional)
                    { train.Tiles[i] = train.Tiles[i].Seal(); changed = true; }
            r.Turn.Undo.Clear();
            if (changed)
            {
                RefreshAnnouncement(r, r.Turn.Actor);
                events.Add(new DomainEvent(EventKind.PlacementsSealed, r.Turn.Actor));
            }
        }

        private static void FinishTurn(MatchState state, List<DomainEvent> events)
        {
            var r = state.Round;
            var actor = r.Turn.Actor;
            RefreshAnnouncement(r, actor);
            if (r.Hands[actor].Count == 0)
            { ScoreRound(state, RoundEndReason.EmptyHand, actor, events); return; }
            var notice = r.Notices[actor];
            if (r.Hands[actor].Count == 1 && !notice.Announced)
            { notice.ChallengeOpen = true; notice.QualifyingTurn = r.Turn.Number; }
            if (r.Turn.Doubles.Count > 1) throw new InvalidOperationException("Unresolved two-double cover.");
            var inherited = r.Turn.Doubles.Count == 1 ? r.Turn.Doubles[0] : r.Turn.InheritedDouble;
            int seat = state.Seats.IndexOf(actor);
            r.Turn = new TurnState { Actor = state.Seats[(seat + 1) % state.Seats.Count], Number = r.Turn.Number + 1,
                InheritedDouble = inherited, Requirement = inherited.HasValue ? PlayRequirement.CoverInheritedDouble : PlayRequirement.Ordinary };
            events.Add(new DomainEvent(EventKind.TurnEnded, actor));
            if (IsBlocked(state))
                ScoreRound(state, RoundEndReason.Blocked, null, events);
        }

        private static bool IsBlocked(MatchState state)
        {
            if (state.Round.Deck.Count != 0) return false;
            // Mandatory passes can still open private destinations. Only end when even those
            // access changes cannot enable play. Inherited doubles still restrict every player.
            var afterPossiblePasses = state.Round.Copy(false);
            foreach (var train in afterPossiblePasses.Tracks)
                if (!train.Id.IsShared) train.IsMarked = true;
            return !state.Seats.Any(p => LegalPlacements(afterPossiblePasses, p).Any());
        }

        private static void RefreshAnnouncement(RoundState r, PlayerId player)
        {
            var notice = r.Notices[player];
            if (r.Hands[player].Count != 1) { notice.Announced = false; notice.ChallengeOpen = false; }
        }

        private static void ExpireChallenges(RoundState r)
        {
            foreach (var notice in r.Notices.Values) notice.ChallengeOpen = false;
        }

        private static TransitionResult ApplyAnnouncement(MatchState state, PlayerId actor)
        {
            var publicRound = PublicRound(state.Round);
            if (publicRound.Hands[actor].Count != 1 || state.Round.Notices[actor].Announced)
                return Reject(state, RuleError.AnnouncementNotRequired);
            var next = state.Copy();
            next.Round.Notices[actor].Announced = true; next.Round.Notices[actor].ChallengeOpen = false; next.Revision++;
            return new TransitionResult(next, RuleError.None, new[] { new DomainEvent(EventKind.OneTileAnnounced, actor) });
        }

        private static TransitionResult ApplyChallenge(MatchState state, PlayerId actor, PlayerId target)
        {
            var r = state.Round;
            if (actor == target || !r.Notices.TryGetValue(target, out var notice) || !notice.ChallengeOpen ||
                notice.Announced || r.Hands[target].Count != 1) return Reject(state, RuleError.ChallengeNotAllowed);
            var next = state.Copy(); r = next.Round;
            r.Notices[target].ChallengeOpen = false;
            var events = new List<DomainEvent> { new DomainEvent(EventKind.MissedAnnouncement, target) };
            if (state.Settings.MissedAnnouncementPenalty == AnnouncementPenalty.DrawOneTile && r.Deck.Count > 0)
            {
                int id = r.Deck[0]; r.Deck.RemoveAt(0); r.Hands[target].Add(id);
                RefreshAnnouncement(r, target);
                events.Add(new DomainEvent(EventKind.PenaltyTileDrawn, target, target, id));
            }
            next.Revision++;
            return new TransitionResult(next, RuleError.None, events);
        }
    }
}
