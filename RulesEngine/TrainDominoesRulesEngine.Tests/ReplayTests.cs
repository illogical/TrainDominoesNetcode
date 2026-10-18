using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class ReplayTests
{
    [TestCase(2, CommitmentMode.Immediate, 1307)]
    [TestCase(3, CommitmentMode.Immediate, 2345)]
    [TestCase(8, CommitmentMode.Immediate, 4567)]
    [TestCase(2, CommitmentMode.Draft, 1307)]
    [TestCase(3, CommitmentMode.Draft, 2345)]
    [TestCase(8, CommitmentMode.Draft, 4567)]
    public void Public_API_replay_finishes_thirteen_rounds_with_rotating_starters_and_is_reproducible(
        int players, CommitmentMode commitment, int seed)
    {
        var first = PlayMatch(players, commitment, seed);
        var replay = PlayMatch(players, commitment, seed);
        Assert.That(replay, Is.EqualTo(first), "Same ordered decks and commands must reproduce all outcomes");
    }

    private static string PlayMatch(int count, CommitmentMode mode, int seed)
    {
        var random = new Random(seed);
        var roster = Enumerable.Range(1, count).Select(n => new PlayerId(n * 10)).ToArray();
        int startingSeat = count - 1;
        var state = RulesEngine.CreateMatch("replay", roster, startingSeat, new MatchSettings(mode));
        var transcript = new List<string>();
        int steps = 0;
        for (int round = 0; round < 13; round++)
        {
            Assert.That(state.Phase, Is.EqualTo(round == 0 ? MatchPhase.AwaitingReady : MatchPhase.RoundOver));
            Assert.That(state.ReadyPlayers, Is.Empty);
            foreach (var p in roster)
            {
                Assert.That(RulesEngine.GetAllowedActions(state, p).CanReady, Is.True);
                state = Advance(state, p, new Ready(state.Token));
            }
            Assert.That(state.Phase, Is.EqualTo(MatchPhase.AwaitingDeck));
            // Randomness belongs to the host. A fixed seed supplies an explicit shuffled permutation.
            var deck = Fixture.Deck(round);
            for (int i = deck.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            state = Fixture.Accepted(RulesEngine.StartRound(state, state.Token, deck));
            Assert.Multiple(() =>
            {
                Assert.That(state.Round.Index, Is.EqualTo(round));
                Assert.That(DominoCatalog.Get(state.Round.EngineId).Low, Is.EqualTo(12 - round));
                Assert.That(state.Round.StarterSeat, Is.EqualTo((startingSeat + round) % count));
                Assert.That(state.Round.Turn.Actor, Is.EqualTo(roster[(startingSeat + round) % count]));
                Assert.That(state.Round.Turn.Number, Is.Zero);
                Assert.That(state.ReadyPlayers, Is.Empty);
                Assert.That(state.Round.Trains.Count, Is.EqualTo(count + 1));
            });
            while (state.Phase == MatchPhase.Playing)
            {
                Assert.That(++steps, Is.LessThan(20000), "A legal-action bot must make progress, never loop forever");
                var actor = state.Round.Turn.Actor;
                var placements = RulesEngine.GetLegalPlacements(state, actor);
                var actions = RulesEngine.GetAllowedActions(state, actor);
                GameCommand command;
                if (placements.Count > 0)
                {
                    var choice = placements[0];
                    command = new Place(state.Token, choice.DominoId, choice.Train);
                }
                else if (actions.CanEndTurn) command = new EndTurn(state.Token);
                else if (actions.CanDraw) command = new Draw(state.Token);
                else if (actions.CanPass) command = new Pass(state.Token);
                else throw new AssertionException("Playing state has no progress action: " + Fingerprint(state));
                state = Advance(state, actor, command);
                transcript.Add(Fingerprint(state));
            }
            Assert.That(state.RoundResults.Count, Is.EqualTo(round + 1));
            Assert.That(state.RoundResults.Select(r => r.RoundIndex), Is.EqualTo(Enumerable.Range(0, round + 1)));
            var result = state.RoundResults[round];
            foreach (var p in roster)
            {
                Assert.That(result.Scores[p], Is.EqualTo(state.Round.GetHand(p).Sum(id => DominoCatalog.Get(id).PipTotal)));
                Assert.That(state.Totals[p], Is.EqualTo(state.RoundResults.Sum(r => r.Scores[p])));
            }
            if (result.Reason == RoundEndReason.EmptyHand)
            {
                Assert.That(result.EndingPlayer.HasValue, Is.True);
                Assert.That(state.Round.GetHand(result.EndingPlayer!.Value), Is.Empty);
            }
            else
            {
                Assert.That(state.Round.Boneyard, Is.Empty);
                Assert.That(result.EndingPlayer, Is.Null);
            }
        }
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.MatchOver));
        int lowest = state.Totals.Values.Min();
        Assert.That(state.Result.Winners, Is.EqualTo(roster.Where(p => state.Totals[p] == lowest)));
        return string.Join("\n", transcript);
    }

    private static MatchState Advance(MatchState previous, PlayerId actor, GameCommand command)
    {
        var before = Fingerprint(previous);
        var result = RulesEngine.Apply(previous, actor, command);
        Assert.That(result.Error, Is.EqualTo(RuleError.None), command.GetType().Name + " must agree with legal-action queries");
        Assert.That(Fingerprint(previous), Is.EqualTo(before), "Accepted commands must preserve the prior snapshot");
        Fixture.AssertConserved(result.State);
        return result.State;
    }

    private static string Fingerprint(MatchState state)
    {
        var parts = new List<string> { state.Phase.ToString(), state.Revision.ToString(), string.Join(",", state.ReadyPlayers) };
        if (state.Round != null)
        {
            var r = state.Round;
            parts.Add($"{r.Index}/{r.StarterSeat}/{r.Turn.Actor}/{r.Turn.Number}/{r.Turn.Requirement}/{r.Turn.DrawAttemptUsed}/{r.Turn.RequiredDrawnTile}");
            foreach (var p in state.Players) parts.Add(p + ":" + string.Join(",", r.GetHand(p)));
            parts.Add("deck:" + string.Join(",", r.Boneyard));
            foreach (var train in r.Trains)
                parts.Add(train.Id + "/" + train.IsMarked + ":" + string.Join(",", train.Placements.Select(p => $"{p.DominoId}:{p.Inward}:{p.Outward}:{p.IsProvisional}")));
        }
        foreach (var r in state.RoundResults)
            parts.Add(r.RoundIndex + "/" + r.Reason + ":" + string.Join(",", state.Players.Select(p => r.Scores[p])));
        return string.Join("|", parts);
    }
}
