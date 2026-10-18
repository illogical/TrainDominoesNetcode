using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class RegressionTests
{
    [Test]
    public void External_host_can_reconstruct_submitted_token_and_stale_revision_is_rejected()
    {
        var ctor = typeof(PositionToken).GetConstructor(new[] { typeof(string), typeof(int), typeof(long), typeof(long) });
        Assert.That(ctor, Is.Not.Null, "Transport adapters need a public token constructor");
        var state = Fixture.Position(new[] { Fixture.Tile(12, 5), Fixture.Tile(0, 0) });
        var token = state.Token;
        var received = new PositionToken(token.MatchId, token.RoundIndex, token.TurnNumber, token.Revision);
        state = Fixture.Accept(state, Fixture.A, new Place(received, Fixture.Tile(12, 5), TrainId.Shared));
        var rejected = RulesEngine.Apply(state, Fixture.B, new Draw(received));
        Assert.That(rejected.Error, Is.EqualTo(RuleError.StalePosition));
        Assert.That(rejected.State, Is.SameAs(state));
    }

    [Test]
    public void Empty_pile_does_not_end_round_before_later_passes_open_a_playable_private_train()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(6, 2) }, new[] { Fixture.Tile(0, 0) }, new[] { Fixture.Tile(1, 1) });
        // Partition the remaining multigraph into legal 12->6 and 12->2 private trains.
        // Its only odd vertices are 6 and 2, because the held 6-2 tile is excluded.
        var unused = state.Round.Deck.ToList();
        var trail = new List<int>();
        void Visit(int pip)
        {
            int i;
            while ((i = unused.FindIndex(id => DominoCatalog.Get(id).Low == pip || DominoCatalog.Get(id).High == pip)) >= 0)
            {
                int id = unused[i]; unused.RemoveAt(i);
                var tile = DominoCatalog.Get(id);
                Visit(tile.Low == pip ? tile.High : tile.Low);
                trail.Insert(0, id);
            }
        }
        Visit(6);
        Assert.That(unused, Is.Empty);
        int vertex = 6, split = 0;
        foreach (int id in trail)
        {
            var tile = DominoCatalog.Get(id);
            vertex = tile.Low == vertex ? tile.High : tile.Low;
            split++;
            if (vertex == 12) break;
        }
        Fixture.Train(state, TrainId.Personal(Fixture.B), trail.Take(split).Reverse().ToArray());
        Fixture.Train(state, TrainId.Personal(Fixture.C), trail.Skip(split).ToArray());
        Assert.That(state.Round.Boneyard, Is.Empty);
        state = Fixture.Accept(state, Fixture.A, new Pass(state.Token));
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.Playing), "B's upcoming pass will unlock A's 6-2");
        state = Fixture.Accept(state, Fixture.B, new Pass(state.Token));
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.Playing));
        state = Fixture.Accept(state, Fixture.C, new Pass(state.Token));
        Assert.That(RulesEngine.GetLegalPlacements(state, Fixture.A).Select(p => p.Train),
            Does.Contain(TrainId.Personal(Fixture.B)));
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(6, 2), TrainId.Personal(Fixture.B)));
        Assert.That(state.RoundResults.Single().Reason, Is.EqualTo(RoundEndReason.EmptyHand));
    }
}
