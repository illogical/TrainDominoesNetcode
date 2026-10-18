using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class LifecycleTests
{
    [Test]
    public void Readiness_is_a_set_and_duplicate_ready_is_an_eventless_no_op()
    {
        var state = RulesEngine.CreateMatch("ready", new[] { Fixture.A, Fixture.B, Fixture.C }, 1);
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.AwaitingReady));
        Assert.That(state.ReadyPlayers, Is.Empty);
        var old = state;
        state = Fixture.Accept(state, Fixture.B, new Ready(state.Token));
        Assert.That(old.ReadyPlayers, Is.Empty, "Accepted commands must not mutate the input snapshot");
        Assert.That(state.ReadyPlayers, Is.EqualTo(new[] { Fixture.B }));
        var duplicate = RulesEngine.Apply(state, Fixture.B, new Ready(state.Token));
        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Accepted, Is.True);
            Assert.That(duplicate.State, Is.SameAs(state));
            Assert.That(duplicate.Events, Is.Empty);
            Assert.That(RulesEngine.GetAllowedActions(state, Fixture.B).CanReady, Is.False);
        });
        state = Fixture.Accept(state, Fixture.C, new Ready(state.Token));
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.AwaitingReady));
        state = Fixture.Accept(state, Fixture.A, new Ready(state.Token));
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.AwaitingDeck));
        RejectSame(state, Fixture.A, new Ready(state.Token), RuleError.WrongPhase);
        state = Fixture.Accepted(RulesEngine.StartRound(state, state.Token, Fixture.Deck(0)));
        Assert.That(state.ReadyPlayers, Is.Empty);
        Assert.That(state.Round.Turn.Actor, Is.EqualTo(Fixture.B));
        RejectSame(state, Fixture.A, new Ready(state.Token), RuleError.WrongPhase);
    }

    [Test]
    public void Lifecycle_commands_validate_participant_phase_and_exact_position_before_mutation()
    {
        var state = RulesEngine.CreateMatch("lifecycle", new[] { Fixture.A, Fixture.B }, 0);
        var initial = state.Token;
        RejectSame(state, Fixture.C, new Ready(state.Token), RuleError.UnknownPlayer);
        RejectSame(state, Fixture.A, new Draw(state.Token), RuleError.WrongPhase);
        var premature = RulesEngine.StartRound(state, state.Token, Fixture.Deck(0));
        Assert.That(premature.Error, Is.EqualTo(RuleError.WrongPhase));
        Assert.That(premature.State, Is.SameAs(state));
        state = Fixture.Accept(state, Fixture.A, new Ready(state.Token));
        RejectSame(state, Fixture.B, new Ready(initial), RuleError.StalePosition);
        Assert.That(RulesEngine.StartRound(state, initial, Fixture.Deck(0)).Error, Is.EqualTo(RuleError.StalePosition));
        Assert.That(RulesEngine.SetSuspended(state, initial, true).Error, Is.EqualTo(RuleError.StalePosition));
        Assert.Throws<InvalidOperationException>(() => RulesEngine.CreateNewMatch(state, "replacement", state.Players, 0));
    }

    [Test]
    public void Suspension_preserves_position_and_blocks_actions_and_coordinator_decks_until_resumed()
    {
        var ready = Fixture.Readied(3);
        var suspendedReady = Fixture.Accepted(RulesEngine.SetSuspended(ready, ready.Token, true));
        var deck = RulesEngine.StartRound(suspendedReady, suspendedReady.Token, Fixture.Deck(0));
        Assert.That(deck.Error, Is.EqualTo(RuleError.Suspended));
        Assert.That(deck.State, Is.SameAs(suspendedReady));
        RejectSame(suspendedReady, Fixture.A, new Ready(suspendedReady.Token), RuleError.Suspended);
        var playing = Fixture.Start();
        var originalHand = playing.Round.GetHand(Fixture.A).ToArray();
        var suspended = Fixture.Accepted(RulesEngine.SetSuspended(playing, playing.Token, true));
        Assert.That(suspended.IsSuspended, Is.True);
        Assert.That(playing.IsSuspended, Is.False);
        var noOp = RulesEngine.SetSuspended(suspended, suspended.Token, true);
        Assert.That(noOp.State, Is.SameAs(suspended));
        Assert.That(noOp.Events, Is.Empty);
        foreach (var command in new GameCommand[] { new Draw(suspended.Token), new Pass(suspended.Token),
                     new EndTurn(suspended.Token), new Undo(suspended.Token), new Ready(suspended.Token),
                     new Place(suspended.Token, originalHand[0], TrainId.Shared),
                     new AnnounceOneTile(suspended.Token), new ChallengeMissedAnnouncement(suspended.Token, Fixture.B) })
            RejectSame(suspended, Fixture.A, command, RuleError.Suspended);
        foreach (var p in suspended.Players)
        {
            Assert.That(RulesEngine.GetLegalPlacements(suspended, p), Is.Empty);
            var actions = RulesEngine.GetAllowedActions(suspended, p);
            Assert.That(actions.CanDraw || actions.CanPass || actions.CanEndTurn || actions.CanUndo || actions.CanReady || actions.CanAnnounce,
                Is.False);
            Assert.That(actions.ChallengeTargets, Is.Empty);
        }
        var resumed = Fixture.Accepted(RulesEngine.SetSuspended(suspended, suspended.Token, false));
        Assert.Multiple(() =>
        {
            Assert.That(resumed.Round.GetHand(Fixture.A), Is.EqualTo(originalHand));
            Assert.That(resumed.Round.Turn.Number, Is.EqualTo(playing.Round.Turn.Number));
            Assert.That(resumed.Round.Turn.Actor, Is.EqualTo(playing.Round.Turn.Actor));
            Assert.That(resumed.ReadyPlayers, Is.EqualTo(playing.ReadyPlayers));
            Assert.That(resumed.RoundResults, Is.Empty);
        });
        Assert.That(RulesEngine.GetLegalPlacements(resumed, Fixture.A).Count,
            Is.EqualTo(RulesEngine.GetLegalPlacements(playing, Fixture.A).Count));
    }

    [Test]
    public void Fresh_match_with_changed_roster_resets_scores_readiness_and_position_identity()
    {
        var old = Fixture.Position(new[] { Fixture.Tile(0, 12) }, new[] { Fixture.Tile(0, 0) });
        old = Fixture.Accept(old, Fixture.A, new Place(old.Token, Fixture.Tile(0, 12), TrainId.Personal(Fixture.A)));
        old = Fixture.Accepted(RulesEngine.SetSuspended(old, old.Token, true));
        Assert.That(old.RoundResults, Has.Count.EqualTo(1));
        var fresh = RulesEngine.CreateNewMatch(old, "fresh", new[] { Fixture.B, Fixture.C }, 1);
        Assert.Multiple(() =>
        {
            Assert.That(fresh.Players, Is.EqualTo(new[] { Fixture.B, Fixture.C }));
            Assert.That(fresh.Phase, Is.EqualTo(MatchPhase.AwaitingReady));
            Assert.That(fresh.Round, Is.Null);
            Assert.That(fresh.Result, Is.Null);
            Assert.That(fresh.RoundResults, Is.Empty);
            Assert.That(fresh.ReadyPlayers, Is.Empty);
            Assert.That(fresh.Totals.Values, Is.All.Zero);
            Assert.That(fresh.IsSuspended, Is.False);
            Assert.That(old.IsSuspended, Is.True);
            Assert.That(old.RoundResults, Has.Count.EqualTo(1));
        });
        RejectSame(fresh, Fixture.B, new Ready(old.Token), RuleError.StalePosition);
        RejectSame(fresh, Fixture.A, new Ready(fresh.Token), RuleError.UnknownPlayer);
        Assert.Throws<ArgumentException>(() => RulesEngine.CreateNewMatch(old, old.Id, fresh.Players, 0));
        fresh = Fixture.Accept(fresh, Fixture.B, new Ready(fresh.Token));
        Assert.That(fresh.Phase, Is.EqualTo(MatchPhase.AwaitingReady));
        fresh = Fixture.Accept(fresh, Fixture.C, new Ready(fresh.Token));
        fresh = Fixture.Accepted(RulesEngine.StartRound(fresh, fresh.Token, Fixture.Deck(0)));
        Assert.That(fresh.Round.Turn.Actor, Is.EqualTo(Fixture.C));
        Assert.That(fresh.Round.Index, Is.Zero);
        Assert.That(fresh.Round.Trains, Has.Count.EqualTo(3));
    }

    [Test]
    public void Going_out_scores_blank_as_zero_once_and_requires_fresh_readiness_for_next_round()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(0, 12) }, new[] { Fixture.Tile(0, 0) });
        state.Round.Notices[Fixture.B].ChallengeOpen = true;
        var expectedC = state.Round.GetHand(Fixture.C).Sum(id => DominoCatalog.Get(id).PipTotal);
        var staleToken = state.Token;
        state = Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(0, 12), TrainId.Shared));
        Assert.Multiple(() =>
        {
            Assert.That(state.Phase, Is.EqualTo(MatchPhase.RoundOver));
            Assert.That(state.RoundResults, Has.Count.EqualTo(1));
            Assert.That(state.RoundResults[0].Reason, Is.EqualTo(RoundEndReason.EmptyHand));
            Assert.That(state.RoundResults[0].EndingPlayer, Is.EqualTo(Fixture.A));
            Assert.That(state.RoundResults[0].Scores[Fixture.A], Is.Zero);
            Assert.That(state.RoundResults[0].Scores[Fixture.B], Is.Zero);
            Assert.That(state.RoundResults[0].Scores[Fixture.C], Is.EqualTo(expectedC));
            Assert.That(state.ReadyPlayers, Is.Empty);
            Assert.That(state.Round.Announcements.Any(a => a.ChallengeOpen), Is.False);
        });
        RejectSame(state, Fixture.A, new Place(staleToken, Fixture.Tile(0, 12), TrainId.Shared), RuleError.StalePosition);
        RejectSame(state, Fixture.A, new Pass(state.Token), RuleError.WrongPhase);
        foreach (var p in state.Players) state = Fixture.Accept(state, p, new Ready(state.Token));
        Assert.That(state.Phase, Is.EqualTo(MatchPhase.AwaitingDeck));
        state = Fixture.Accepted(RulesEngine.StartRound(state, state.Token, Fixture.Deck(1)));
        Assert.Multiple(() =>
        {
            Assert.That(state.RoundResults, Has.Count.EqualTo(1));
            Assert.That(state.Round.Index, Is.EqualTo(1));
            Assert.That(state.Round.EngineId, Is.EqualTo(Fixture.Tile(11, 11)));
            Assert.That(state.Round.Turn.Actor, Is.EqualTo(Fixture.B));
            Assert.That(state.Round.Turn.Number, Is.Zero);
            Assert.That(state.Round.Turn.UndoCount, Is.Zero);
            Assert.That(state.Round.Turn.DrawAttemptUsed, Is.False);
            Assert.That(state.Round.Turn.OpenDoubles, Is.Empty);
            Assert.That(state.Round.Trains.All(t => !t.IsMarked && t.Placements.Count == 0), Is.True);
            Assert.That(state.Round.Announcements.All(a => !a.Announced && !a.ChallengeOpen), Is.True);
        });
    }

    [Test]
    public void Empty_boneyard_and_no_playable_hands_scores_a_blocked_round_after_pass()
    {
        var state = Fixture.Position(new[] { Fixture.Tile(0, 0) }, new[] { Fixture.Tile(1, 1) },
            new[] { Fixture.Tile(2, 2) });
        // A legal shared chain consumes every non-engine tile incident on twelve.
        Fixture.Train(state, TrainId.Shared,
            Fixture.Tile(12, 0), Fixture.Tile(0, 1), Fixture.Tile(1, 12),
            Fixture.Tile(12, 2), Fixture.Tile(2, 3), Fixture.Tile(3, 12),
            Fixture.Tile(12, 4), Fixture.Tile(4, 5), Fixture.Tile(5, 12),
            Fixture.Tile(12, 6), Fixture.Tile(6, 7), Fixture.Tile(7, 12),
            Fixture.Tile(12, 8), Fixture.Tile(8, 9), Fixture.Tile(9, 12),
            Fixture.Tile(12, 10), Fixture.Tile(10, 11), Fixture.Tile(11, 12));
        Fixture.FillDeck(state, emptyDeck: true);
        var expected = state.Players.ToDictionary(p => p,
            p => state.Round.GetHand(p).Sum(id => DominoCatalog.Get(id).PipTotal));
        Assert.That(RulesEngine.GetLegalPlacements(state, Fixture.A), Is.Empty);
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.A).CanPass, Is.True);
        var result = RulesEngine.Apply(state, Fixture.A, new Pass(state.Token));
        state = Fixture.Accepted(result);
        Assert.Multiple(() =>
        {
            Assert.That(state.Phase, Is.EqualTo(MatchPhase.RoundOver));
            Assert.That(state.Round.Trains.Single(t => t.Id == TrainId.Personal(Fixture.A)).IsMarked, Is.True);
            Assert.That(state.RoundResults, Has.Count.EqualTo(1));
            Assert.That(state.RoundResults[0].Reason, Is.EqualTo(RoundEndReason.Blocked));
            Assert.That(state.RoundResults[0].EndingPlayer, Is.Null);
            Assert.That(result.Events.Count(e => e.Kind == EventKind.RoundEnded), Is.EqualTo(1));
            Assert.That(state.RoundResults[0].Scores, Is.EquivalentTo(expected));
        });
        RejectSame(state, state.Round.Turn.Actor, new Pass(state.Token), RuleError.WrongPhase);
    }

    [Test]
    public void Thirteenth_round_scores_all_joint_lowest_totals_and_ends_the_match()
    {
        var state = Fixture.Start();
        state.Round.Index = 12;
        state.Round.Hands[Fixture.A] = new List<int> { Fixture.Tile(0, 1) };
        state.Round.Hands[Fixture.B] = new List<int> { Fixture.Tile(2, 3) };
        state.Round.Hands[Fixture.C] = new List<int> { Fixture.Tile(4, 5) };
        Fixture.FillDeck(state);
        for (int i = 0; i < 12; i++)
            state.Results.Add(new RoundResult(i, RoundEndReason.Blocked, null, new Dictionary<PlayerId, int>
                { [Fixture.A] = i == 0 ? 10 : 0, [Fixture.B] = i == 0 ? 5 : 0, [Fixture.C] = 10 }));
        var ended = RulesEngine.Apply(state, Fixture.A, new Place(state.Token, Fixture.Tile(0, 1), TrainId.Shared));
        state = Fixture.Accepted(ended);
        Assert.Multiple(() =>
        {
            Assert.That(state.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(state.RoundResults, Has.Count.EqualTo(13));
            Assert.That(state.Totals[Fixture.A], Is.EqualTo(10));
            Assert.That(state.Totals[Fixture.B], Is.EqualTo(10));
            Assert.That(state.Result.Winners, Is.EqualTo(new[] { Fixture.A, Fixture.B }));
            Assert.That(ended.Events.Count(e => e.Kind == EventKind.RoundEnded), Is.EqualTo(1));
            Assert.That(ended.Events.Count(e => e.Kind == EventKind.MatchEnded), Is.EqualTo(1));
        });
        RejectSame(state, Fixture.A, new Ready(state.Token), RuleError.WrongPhase);
        Assert.That(RulesEngine.StartRound(state, state.Token, Fixture.Deck(0)).Error, Is.EqualTo(RuleError.WrongPhase));
        var fresh = RulesEngine.CreateNewMatch(state, "after-final", state.Players, 0);
        Assert.That(fresh.RoundResults, Is.Empty);
        Assert.That(fresh.Result, Is.Null);
    }

    private static void RejectSame(MatchState state, PlayerId actor, GameCommand command, RuleError expected)
    {
        var result = RulesEngine.Apply(state, actor, command);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(result.Events, Is.Empty);
        });
    }
}
