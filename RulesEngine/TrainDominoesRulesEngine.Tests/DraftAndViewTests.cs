using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class DraftAndViewTests
{
    [Test]
    public void Draft_final_double_remains_private_and_undoable_until_commit_EX007()
    {
        var initial = Fixture.Position(new[] { Fixture.Tile(4, 4) }, mode: CommitmentMode.Draft);
        Fixture.Train(initial, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        var oldObserverView = RulesEngine.GetPlayerView(initial, Fixture.B);
        var result = RulesEngine.Apply(initial, Fixture.A, new Place(initial.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
        var draft = Fixture.Accepted(result);
        Assert.That(draft.Phase, Is.EqualTo(MatchPhase.Playing));
        Assert.That(RulesEngine.GetPlayerView(draft, Fixture.B).HandCounts[Fixture.A], Is.EqualTo(1));
        Assert.That(RulesEngine.GetPlayerView(draft, Fixture.A).Hand.Count, Is.Zero);
        Assert.That(RulesEngine.GetEventsForPlayer(result, Fixture.B), Is.Empty);
        Assert.That(RulesEngine.GetEventsForPlayer(result, Fixture.A).Single().DominoId, Is.EqualTo(Fixture.Tile(4, 4)));
        var undone = Fixture.Accept(draft, Fixture.A, new Undo(draft.Token));
        Assert.That(undone.Round.GetHand(Fixture.A), Is.EqualTo(new[] { Fixture.Tile(4, 4) }));
        var ended = Fixture.Accept(draft, Fixture.A, new EndTurn(draft.Token));
        Assert.That(ended.Phase, Is.EqualTo(MatchPhase.RoundOver));
        Assert.That(ended.RoundResults.Single().EndingPlayer, Is.EqualTo(Fixture.A));
        Assert.That(oldObserverView.HandCounts[Fixture.A], Is.EqualTo(1));
        Assert.That(initial.RoundResults, Is.Empty);
    }

    [Test]
    public void Undoing_third_cover_restores_two_double_requirement_and_lifo_sequence()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(4, 4), Fixture.Tile(6, 6), Fixture.Tile(4, 2), Fixture.Tile(0, 0) }, mode: CommitmentMode.Draft);
        Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        Fixture.Train(state, TrainId.Shared, Fixture.Tile(12, 6));
        var first = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
        var second = Fixture.Accept(first, Fixture.A, new Place(first.Token, Fixture.Tile(6, 6), TrainId.Shared));
        var third = Fixture.Accept(second, Fixture.A, new Place(second.Token, Fixture.Tile(4, 2), TrainId.Personal(Fixture.A)));
        state = Fixture.Accept(third, Fixture.A, new Undo(third.Token));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.CoverOneOfTwo));
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.A).CanDraw, Is.False);
        state = Fixture.Accept(state, Fixture.A, new Undo(state.Token));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.AdditionalPlay));
        state = Fixture.Accept(state, Fixture.A, new Undo(state.Token));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.Ordinary));
        Assert.That(state.Round.GetHand(Fixture.A).Count, Is.EqualTo(4));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Undo(state.Token)).Error, Is.EqualTo(RuleError.UndoNotAllowed));
    }

    [Test]
    public void Draw_seals_previous_double_and_undo_cannot_renew_attempt_or_return_drawn_tile()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(4, 4), Fixture.Tile(0, 0) }, mode: CommitmentMode.Draft);
        Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        var draft = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
        int drawnId = Fixture.Tile(4, 2);
        draft.Round.Deck.Remove(drawnId); draft.Round.Deck.Insert(0, drawnId);
        var draw = RulesEngine.Apply(draft, Fixture.A, new Draw(draft.Token));
        state = Fixture.Accepted(draw);
        Assert.That(state.Round.Turn.UndoCount, Is.Zero);
        Assert.That(RulesEngine.GetPlayerView(state, Fixture.B).Trains.Single(t => t.Id.Owner == Fixture.A).Placements.Count, Is.EqualTo(2));
        Assert.That(RulesEngine.GetEventsForPlayer(draw, Fixture.B).Any(e => e.DominoId == drawnId), Is.False);
        var placed = Fixture.Accept(state, Fixture.A, new Place(state.Token, drawnId, TrainId.Personal(Fixture.A)));
        state = Fixture.Accept(placed, Fixture.A, new Undo(placed.Token));
        Assert.That(state.Round.Turn.DrawAttemptUsed, Is.True);
        Assert.That(state.Round.Turn.RequiredDrawnTile, Is.EqualTo(drawnId));
        Assert.That(state.Round.Boneyard.Contains(drawnId), Is.False);
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Draw(state.Token)).Error, Is.EqualTo(RuleError.DrawNotAllowed));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new EndTurn(state.Token)).Error, Is.EqualTo(RuleError.CommitNotAllowed));
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.A).CanUndo, Is.False);
    }

    [Test]
    public void Views_and_snapshots_expose_no_mutable_collections_or_opponent_hand_payloads()
    {
        var state = Fixture.Start();
        var view = RulesEngine.GetPlayerView(state, Fixture.A);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)view.Hand).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PlayerId>)state.Players).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)state.Round.GetHand(Fixture.A)).Clear());
        Assert.That(typeof(PlayerView).GetProperties().Any(p => p.Name is "Boneyard" or "Hands" or "State"), Is.False);
        Assert.That(view.Hand, Is.EqualTo(state.Round.GetHand(Fixture.A)));
        Assert.That(view.LegalPlacements.All(p => view.Hand.Contains(p.DominoId)), Is.True);
        Assert.That(RulesEngine.GetPlayerView(state, Fixture.B).LegalPlacements, Is.Empty);
        Assert.Throws<ArgumentException>(() => RulesEngine.GetPlayerView(state, new PlayerId(999)));
    }

    [Test]
    public void Draft_draw_seal_view_publishes_markers_and_hides_unsealed_marker_removal()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(4, 4), Fixture.Tile(0, 0) }, mode: CommitmentMode.Draft);
        Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        state.Round.Tracks.Single(t => t.Id.Owner == Fixture.A).IsMarked = true;
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
        Assert.That(RulesEngine.GetPlayerView(state, Fixture.B).Trains.Single(t => t.Id.Owner == Fixture.A).IsMarked, Is.True);
        Assert.That(RulesEngine.GetPlayerView(state, Fixture.A).Trains.Single(t => t.Id.Owner == Fixture.A).IsMarked, Is.False);
        state = Fixture.Accept(state, Fixture.A, new Draw(state.Token));
        Assert.That(RulesEngine.GetPlayerView(state, Fixture.B).Trains.Single(t => t.Id.Owner == Fixture.A).IsMarked, Is.False);
    }
}
