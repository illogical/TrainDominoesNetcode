using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class TurnRulesTests
{
    [Test]
    public void R003_exposed_endpoint_rejects_inward_match_and_orients_correctly()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(6, 5), Fixture.Tile(3, 5), Fixture.Tile(0, 0) });
        Fixture.Train(state, TrainId.Shared, Fixture.Tile(12, 6), Fixture.Tile(6, 3));
        var bad = RulesEngine.Apply(state, Fixture.A, new Place(state.Token, Fixture.Tile(6, 5), TrainId.Shared));
        Assert.That(bad.Error, Is.EqualTo(RuleError.PlacementNotLegal));
        Assert.That(bad.State, Is.SameAs(state));
        var next = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(3, 5), TrainId.Shared));
        var placed = next.Round.Trains.Single(t => t.Id.IsShared).Placements.Last();
        Assert.That((placed.Inward, placed.Outward), Is.EqualTo((3, 5)));
        Assert.That(next.Round.Turn.Actor, Is.EqualTo(Fixture.B));
        Assert.That(state.Round.GetHand(Fixture.A).Count, Is.EqualTo(3));
    }

    [Test]
    public void R011_extra_play_elsewhere_leaves_inherited_double_cover()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(4, 4), Fixture.Tile(12, 5), Fixture.Tile(0, 0) },
            new[] { Fixture.Tile(4, 2), Fixture.Tile(1, 1) });
        Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.AdditionalPlay));
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(12, 5), TrainId.Shared));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.CoverInheritedDouble));
        Assert.That(RulesEngine.GetLegalPlacements(state, Fixture.B).All(p => p.Train == TrainId.Personal(Fixture.A)), Is.True);
        state = Fixture.Accept(state, Fixture.B, new Place(state.Token, Fixture.Tile(4, 2), TrainId.Personal(Fixture.A)));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.Ordinary));
    }

    [Test]
    public void R017_second_double_requires_held_cover_and_third_play_cannot_draw_or_double()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(4, 4), Fixture.Tile(6, 6), Fixture.Tile(4, 2), Fixture.Tile(1, 1) });
        Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        Fixture.Train(state, TrainId.Shared, Fixture.Tile(12, 6));
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(6, 6), TrainId.Shared));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.CoverOneOfTwo));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Draw(state.Token)).Error, Is.EqualTo(RuleError.DrawNotAllowed));
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 2), TrainId.Personal(Fixture.A)));
        Assert.That(state.Round.Turn.InheritedDouble, Is.EqualTo(TrainId.Shared));
    }

    [Test]
    public void Strategic_draw_foreign_tile_unknown_train_and_wrong_actor_are_rejected()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(12, 5), Fixture.Tile(0, 0) });
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Draw(state.Token)).Error, Is.EqualTo(RuleError.DrawNotAllowed));
        Assert.That(RulesEngine.Apply(state, Fixture.B, new Place(state.Token, Fixture.Tile(12, 5), TrainId.Shared)).Error, Is.EqualTo(RuleError.NotYourTurn));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Place(state.Token, Fixture.Tile(1, 2), TrainId.Shared)).Error, Is.EqualTo(RuleError.TileNotHeld));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Place(state.Token, Fixture.Tile(12, 5), TrainId.Personal(new PlayerId(999)))).Error, Is.EqualTo(RuleError.UnknownTrain));
    }

    [Test]
    public void Second_double_without_third_tile_cover_is_illegal_unless_it_is_the_final_tile()
    {
        foreach (bool final in new[] { false, true })
        {
            var hand = new[] { Fixture.Tile(4, 4), Fixture.Tile(6, 6) };
            var state = Fixture.Position(final ? hand : hand.Append(Fixture.Tile(0, 0)).ToArray());
            Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
            Fixture.Train(state, TrainId.Shared, Fixture.Tile(12, 6));
            state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 4), TrainId.Personal(Fixture.A)));
            var result = RulesEngine.Apply(state, Fixture.A, new Place(state.Token, Fixture.Tile(6, 6), TrainId.Shared));
            Assert.That(result.Accepted, Is.EqualTo(final));
            if (final)
            {
                Fixture.Accepted(result);
                Assert.That(result.State.Phase, Is.EqualTo(MatchPhase.RoundOver));
            }
            else Assert.That(result.State, Is.SameAs(state));
        }
    }

    [Test]
    public void R017_drawn_double_creates_one_separate_follow_up_attempt_EX012()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(0, 0) });
        Fixture.Train(state, TrainId.Personal(Fixture.A), Fixture.Tile(12, 4));
        int drawnDouble = Fixture.Tile(4, 4), failedFollowUp = Fixture.Tile(1, 5);
        state.Round.Deck.Remove(drawnDouble); state.Round.Deck.Insert(0, drawnDouble);
        state.Round.Deck.Remove(failedFollowUp); state.Round.Deck.Insert(1, failedFollowUp);
        state = Fixture.Accept(state, Fixture.A, new Draw(state.Token));
        Assert.That(state.Round.Turn.DrawAttemptUsed, Is.True);
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, drawnDouble, TrainId.Personal(Fixture.A)));
        Assert.That(state.Round.Turn.DrawAttemptUsed, Is.False);
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.AdditionalPlay));
        state = Fixture.Accept(state, Fixture.A, new Draw(state.Token));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new Draw(state.Token)).Error, Is.EqualTo(RuleError.DrawNotAllowed));
        state = Fixture.Accept(state, Fixture.A, new Pass(state.Token));
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.CoverInheritedDouble));
        Assert.That(state.Round.Trains.Single(t => t.Id.Owner == Fixture.A).IsMarked, Is.True);
    }

    [Test]
    public void Markers_do_not_transfer_ownership_and_only_owner_play_closes_train_EX004()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(6, 2), Fixture.Tile(0, 0) },
            new[] { Fixture.Tile(12, 6), Fixture.Tile(1, 1) });
        var train = TrainId.Personal(Fixture.A);
        state.Round.Tracks.Single(t => t.Id == train).IsMarked = true;
        state.Round.Turn.Actor = Fixture.B;
        state = Fixture.Accept(state, Fixture.B, new Place(state.Token, Fixture.Tile(12, 6), train));
        Assert.That(state.Round.Trains.Single(t => t.Id == train).IsMarked, Is.True);
        Assert.That(state.Round.Trains.Single(t => t.Id == train).Id.Owner, Is.EqualTo(Fixture.A));
        state.Round.Turn.Actor = Fixture.A;
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(6, 2), train));
        Assert.That(state.Round.Trains.Single(t => t.Id == train).IsMarked, Is.False);
    }

    [Test]
    public void Covering_opponent_private_double_does_not_close_actors_own_marked_train_EX011()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(4, 2), Fixture.Tile(0, 0) });
        var target = TrainId.Personal(Fixture.B);
        Fixture.Train(state, target, Fixture.Tile(12, 4), Fixture.Tile(4, 4));
        state.Round.Turn.Requirement = PlayRequirement.CoverInheritedDouble;
        state.Round.Turn.InheritedDouble = target;
        state.Round.Tracks.Single(t => t.Id.Owner == Fixture.A).IsMarked = true;
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(4, 2), target));
        Assert.That(state.Round.Trains.Single(t => t.Id.Owner == Fixture.A).IsMarked, Is.True);
        Assert.That(state.Round.Trains.Single(t => t.Id == target).IsMarked, Is.False);
        Assert.That(state.Round.Turn.Requirement, Is.EqualTo(PlayRequirement.Ordinary));
    }
}
