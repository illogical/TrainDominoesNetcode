using System.Linq;
using NUnit.Framework;

namespace TrainDominoesRulesEngine.Tests;

[TestFixture]
public class AnnouncementTests
{
    private static MatchState OneTileTurn(CommitmentMode mode = CommitmentMode.Immediate)
    {
        var state = Fixture.Position(new[] { Fixture.Tile(12, 5), Fixture.Tile(0, 0) }, mode: mode);
        return Fixture.Accept(state, Fixture.A, new Place(state.Token, Fixture.Tile(12, 5), TrainId.Shared));
    }

    [Test]
    public void Manual_announcement_is_available_between_turns_once_and_closes_challenge()
    {
        var state = OneTileTurn();
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.B).ChallengeTargets, Does.Contain(Fixture.A));
        var result = RulesEngine.Apply(state, Fixture.A, new AnnounceOneTile(state.Token));
        state = Fixture.Accepted(result);
        Assert.That(result.Events.Single().Kind, Is.EqualTo(EventKind.OneTileAnnounced));
        Assert.That(RulesEngine.Apply(state, Fixture.A, new AnnounceOneTile(state.Token)).Error, Is.EqualTo(RuleError.AnnouncementNotRequired));
        Assert.That(RulesEngine.Apply(state, Fixture.B, new ChallengeMissedAnnouncement(state.Token, Fixture.A)).Error, Is.EqualTo(RuleError.ChallengeNotAllowed));
        Assert.That(RulesEngine.Apply(state, Fixture.C, new AnnounceOneTile(state.Token)).Error, Is.EqualTo(RuleError.AnnouncementNotRequired));
    }

    [Test]
    public void Challenge_draws_one_private_penalty_tile_once_without_consuming_active_players_draw()
    {
        var state = OneTileTurn();
        int id = state.Round.Boneyard[0];
        var oldToken = state.Token;
        var result = RulesEngine.Apply(state, Fixture.C, new ChallengeMissedAnnouncement(state.Token, Fixture.A));
        state = Fixture.Accepted(result);
        Assert.That(state.Round.GetHand(Fixture.A).Count, Is.EqualTo(2));
        Assert.That(state.Round.Turn.Actor, Is.EqualTo(Fixture.B));
        Assert.That(state.Round.Turn.DrawAttemptUsed, Is.False);
        Assert.That(RulesEngine.GetEventsForPlayer(result, Fixture.A).Any(e => e.DominoId == id), Is.True);
        Assert.That(RulesEngine.GetEventsForPlayer(result, Fixture.B).Any(e => e.DominoId.HasValue), Is.False);
        Assert.That(RulesEngine.Apply(state, Fixture.C, new ChallengeMissedAnnouncement(oldToken, Fixture.A)).Error, Is.EqualTo(RuleError.StalePosition));
        Assert.That(RulesEngine.Apply(state, Fixture.B, new ChallengeMissedAnnouncement(state.Token, Fixture.A)).Error, Is.EqualTo(RuleError.ChallengeNotAllowed));
    }

    [Test]
    public void Rejected_action_preserves_window_valid_draw_expires_it_and_self_challenge_is_rejected()
    {
        var state = OneTileTurn();
        Assert.That(RulesEngine.Apply(state, Fixture.A, new ChallengeMissedAnnouncement(state.Token, Fixture.A)).Accepted, Is.False);
        var rejected = RulesEngine.Apply(state, Fixture.B, new Place(state.Token, Fixture.Tile(1, 2), TrainId.Shared));
        Assert.That(rejected.Accepted, Is.False);
        Assert.That(rejected.State, Is.SameAs(state));
        state = Fixture.Accept(state, Fixture.B, new Draw(state.Token));
        Assert.That(RulesEngine.Apply(state, Fixture.C, new ChallengeMissedAnnouncement(state.Token, Fixture.A)).Error, Is.EqualTo(RuleError.ChallengeNotAllowed));
    }

    [Test]
    public void Empty_boneyard_records_infraction_without_substitute_penalty()
    {
        var state = OneTileTurn();
        state.Round.Hands[Fixture.C].AddRange(state.Round.Deck);
        state.Round.Deck.Clear();
        var result = RulesEngine.Apply(state, Fixture.B, new ChallengeMissedAnnouncement(state.Token, Fixture.A));
        state = Fixture.Accepted(result);
        Assert.That(state.Round.GetHand(Fixture.A).Count, Is.EqualTo(1));
        Assert.That(result.Events.Select(e => e.Kind), Is.EqualTo(new[] { EventKind.MissedAnnouncement }));
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.B).ChallengeTargets, Is.Empty);
    }

    [Test]
    public void No_penalty_setting_records_infraction_without_drawing()
    {
        var state = OneTileTurn();
        state.Settings = new MatchSettings(CommitmentMode.Immediate, AnnouncementPenalty.None);
        int count = state.Round.Boneyard.Count;
        var result = RulesEngine.Apply(state, Fixture.B, new ChallengeMissedAnnouncement(state.Token, Fixture.A));
        state = Fixture.Accepted(result);
        Assert.That(state.Round.Boneyard.Count, Is.EqualTo(count));
        Assert.That(result.Events.Select(e => e.Kind), Is.EqualTo(new[] { EventKind.MissedAnnouncement }));
    }

    [Test]
    public void Private_draft_does_not_create_announcement_until_commit()
    {
        var state = OneTileTurn(CommitmentMode.Draft);
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.A).CanAnnounce, Is.False);
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.B).ChallengeTargets, Is.Empty);
        Assert.That(RulesEngine.Apply(state, Fixture.A, new AnnounceOneTile(state.Token)).Error, Is.EqualTo(RuleError.AnnouncementNotRequired));
        state = Fixture.Accept(state, Fixture.A, new EndTurn(state.Token));
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.A).CanAnnounce, Is.True);
        Assert.That(RulesEngine.GetAllowedActions(state, Fixture.C).ChallengeTargets, Does.Contain(Fixture.A));
    }
}
