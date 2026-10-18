using System;
using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class DomainTests
{
    [Test]
    public void Catalog_preserves_legacy_min_then_max_ids_and_has_all_91_unique_tiles()
    {
        int index = 0;
        for (int low = 0; low <= 12; low++)
        for (int high = low; high <= 12; high++)
        {
            var tile = DominoCatalog.Get(index);
            Assert.That((tile.Id, tile.Low, tile.High), Is.EqualTo((index, low, high)));
            Assert.That(DominoCatalog.Id(high, low), Is.EqualTo(index++));
        }
        Assert.That(DominoCatalog.All.Count, Is.EqualTo(91));
        Assert.That(DominoCatalog.Get(0).PipTotal, Is.Zero);
    }

    [TestCase(2, 16)] [TestCase(3, 15)] [TestCase(4, 14)] [TestCase(5, 12)]
    [TestCase(6, 11)] [TestCase(7, 10)] [TestCase(8, 9)]
    public void R001_setup_deals_exact_web_edition_counts(int players, int handSize)
    {
        var state = Fixture.Start(players);
        Assert.That(state.Round.Trains.Count, Is.EqualTo(players + 1));
        Assert.That(state.Round.Trains.Count(t => t.Id.IsShared), Is.EqualTo(1));
        foreach (var p in state.Players) Assert.That(state.Round.GetHand(p).Count, Is.EqualTo(handSize));
        Assert.That(state.Round.Boneyard.Count, Is.EqualTo(90 - players * handSize));
        Fixture.AssertConserved(state);
    }

    [Test]
    public void Setup_rejects_duplicate_missing_foreign_engine_and_out_of_range_decks_atomically()
    {
        var ready = Fixture.Readied(4);
        var deck = Fixture.Deck(0);
        foreach (var invalid in new[] { deck.Take(89).ToArray(), deck.Append(deck[0]).ToArray(),
                     deck.Select((id, i) => i == 0 ? deck[1] : id).ToArray(),
                     deck.Select((id, i) => i == 0 ? 91 : id).ToArray(),
                     deck.Select((id, i) => i == 0 ? 90 : id).ToArray() })
        {
            var result = RulesEngine.StartRound(ready, ready.Token, invalid);
            Assert.That(result.Error, Is.EqualTo(RuleError.InvalidDeck));
            Assert.That(result.State, Is.SameAs(ready));
        }
    }

    [Test]
    public void Setup_rejects_bad_rosters_and_starting_seats()
    {
        Assert.Throws<ArgumentException>(() => RulesEngine.CreateMatch("m", new[] { new PlayerId(1) }, 0));
        Assert.Throws<ArgumentException>(() => RulesEngine.CreateMatch("m", new[] { new PlayerId(1), new PlayerId(1) }, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RulesEngine.CreateMatch("m", new[] { new PlayerId(1), new PlayerId(2) }, 2));
    }
}

internal static class Fixture
{
    internal static readonly PlayerId A = new(10), B = new(20), C = new(30);
    internal static int Tile(int a, int b) => DominoCatalog.Id(a, b);
    internal static int[] Deck(int round) => DominoCatalog.All.Where(t => t.Id != DominoCatalog.EngineForRound(round)).Select(t => t.Id).ToArray();
    internal static MatchState Readied(int count, MatchSettings? settings = null)
    {
        var state = RulesEngine.CreateMatch("match", Enumerable.Range(1, count).Select(i => new PlayerId(i * 10)), 0, settings);
        foreach (var p in state.Players) state = Accept(state, p, new Ready(state.Token));
        return state;
    }
    internal static MatchState Start(int count = 3, MatchSettings? settings = null)
    {
        var state = Readied(count, settings);
        return Accepted(RulesEngine.StartRound(state, state.Token, Deck(0)));
    }
    internal static MatchState Accept(MatchState state, PlayerId actor, GameCommand command) => Accepted(RulesEngine.Apply(state, actor, command));
    internal static MatchState Accepted(TransitionResult result)
    {
        Assert.That(result.Error, Is.EqualTo(RuleError.None));
        AssertConserved(result.State);
        return result.State;
    }
    // Full legal tile partition, but a synthetic mid-round position. Engine is 12-12.
    internal static MatchState Position(int[] a, int[]? b = null, int[]? c = null,
        CommitmentMode mode = CommitmentMode.Immediate, bool emptyDeck = false)
    {
        var state = Start(3, new MatchSettings(mode));
        var round = state.Round;
        round.Hands[A] = a.ToList();
        round.Hands[B] = (b ?? new[] { Tile(1, 2), Tile(2, 3) }).ToList();
        round.Hands[C] = (c ?? new[] { Tile(3, 4), Tile(4, 5) }).ToList();
        FillDeck(state, emptyDeck);
        return state;
    }
    internal static void FillDeck(MatchState state, bool emptyDeck = false)
    {
        var r = state.Round;
        var used = r.Hands.Values.SelectMany(h => h).Concat(r.Tracks.SelectMany(t => t.Tiles.Select(p => p.DominoId))).Append(r.EngineId).ToArray();
        Assert.That(used.Distinct().Count(), Is.EqualTo(used.Length), "Fixture must not duplicate tiles");
        var remaining = DominoCatalog.All.Select(t => t.Id).Except(used).ToList();
        r.Deck = emptyDeck ? new() : remaining;
        if (emptyDeck) r.Hands[C].AddRange(remaining);
        AssertConserved(state);
    }
    internal static void Train(MatchState state, TrainId train, params int[] chain)
    {
        int endpoint = 12;
        var t = state.Round.Tracks.Single(t => t.Id == train);
        foreach (var id in chain)
        {
            var d = DominoCatalog.Get(id);
            Assert.That(d.Low == endpoint || d.High == endpoint, Is.True, "Fixture chain must match");
            int next = d.Low == endpoint ? d.High : d.Low;
            t.Tiles.Add(new Placement(id, endpoint, next, train.Owner ?? A, false));
            endpoint = next;
        }
        FillDeck(state);
    }
    internal static void AssertConserved(MatchState state)
    {
        if (state.Round == null) return;
        var r = state.Round;
        var all = r.Hands.Values.SelectMany(h => h).Concat(r.Boneyard)
            .Concat(r.Trains.SelectMany(t => t.Placements.Select(p => p.DominoId))).Append(r.EngineId).OrderBy(i => i).ToArray();
        Assert.That(all, Is.EqualTo(Enumerable.Range(0, 91)));
    }
}
