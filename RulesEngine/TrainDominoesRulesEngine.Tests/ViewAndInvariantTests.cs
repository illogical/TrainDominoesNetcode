using System;
using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class ViewAndInvariantTests
{
    [TestCase(CommitmentMode.Immediate)]
    [TestCase(CommitmentMode.Draft)]
    public void Sampled_positions_query_all_tile_train_pairs_agree_with_command_validation(CommitmentMode mode)
    {
        var state = Fixture.Start(4, new MatchSettings(mode));
        for (int step = 0; step < 30 && state.Phase == MatchPhase.Playing; step++)
        {
            var actor = state.Round.Turn.Actor;
            var legal = RulesEngine.GetLegalPlacements(state, actor);
            var pairs = legal.Select(p => (p.DominoId, p.Train)).ToHashSet();
            foreach (var train in state.Round.Trains)
            foreach (var tile in DominoCatalog.All)
            {
                var result = RulesEngine.Apply(state, actor, new Place(state.Token, tile.Id, train.Id));
                Assert.That(result.Accepted, Is.EqualTo(pairs.Contains((tile.Id, train.Id))), "Query/command disagreement");
                if (result.Accepted) Fixture.AssertConserved(result.State);
                else
                {
                    Assert.That(result.State, Is.SameAs(state));
                    Assert.That(result.Events, Is.Empty);
                }
            }
            var allowed = RulesEngine.GetAllowedActions(state, actor);
            foreach (var pair in new (GameCommand Command, bool Allowed)[]
                     { (new Draw(state.Token), allowed.CanDraw), (new Pass(state.Token), allowed.CanPass),
                         (new EndTurn(state.Token), allowed.CanEndTurn), (new Undo(state.Token), allowed.CanUndo) })
                Assert.That(RulesEngine.Apply(state, actor, pair.Command).Accepted, Is.EqualTo(pair.Allowed));
            GameCommand next = legal.Count > 0 ? new Place(state.Token, legal[0].DominoId, legal[0].Train) :
                allowed.CanEndTurn ? new EndTurn(state.Token) : allowed.CanDraw ? new Draw(state.Token) : new Pass(state.Token);
            var previous = state;
            int[] oldHand = previous.Round.GetHand(actor).ToArray();
            state = Fixture.Accept(previous, actor, next);
            Assert.That(previous.Round.GetHand(actor), Is.EqualTo(oldHand));
        }
    }

    [Test]
    public void Engine_assembly_has_only_standard_library_dependencies()
    {
        var refs = typeof(RulesEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.That(refs, Does.Not.Contain("UnityEngine"));
        Assert.That(refs.All(n => n == "netstandard" || n == "mscorlib" || n!.StartsWith("System")), Is.True);
        Assert.That(typeof(MatchSettings).GetProperties().All(p => p.SetMethod == null), Is.True, "Settings are fixed for the match");
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchSettings((CommitmentMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchSettings(missedAnnouncementPenalty: (AnnouncementPenalty)99));
    }

    [Test]
    public void R001_ordered_deck_is_consumed_round_robin_then_drawn_from_the_front()
    {
        var ready = Fixture.Readied(4);
        var deck = Fixture.Deck(0);
        var state = Fixture.Accepted(RulesEngine.StartRound(ready, ready.Token, deck));
        for (int seat = 0; seat < 4; seat++)
            Assert.That(state.Round.GetHand(state.Players[seat]), Is.EqualTo(deck.Take(56).Where((_, i) => i % 4 == seat)));
        Assert.That(state.Round.Boneyard, Is.EqualTo(deck.Skip(56)));
    }
}
