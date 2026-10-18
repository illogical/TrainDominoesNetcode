using System;
using System.Linq;
using TrainDominoesRulesEngine;
using Core = TrainDominoesRulesEngine.RulesEngine;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>
    /// The only place a host snapshot becomes a client payload. A recipient receives its own
    /// filtered view and only events whose audience is public or that recipient.
    /// </summary>
    public static class RecipientProjection
    {
        public static ProjectionPacket Build(MatchState state, PlayerId recipient, TransitionResult result, CommandOutcome outcome)
        {
            return new ProjectionPacket
            {
                View = PlayerViewDto.From(Core.GetPlayerView(state, recipient)),
                Outcome = outcome,
                Events = result == null ? Array.Empty<PresentationEventDto>() : EventsFor(result, recipient),
            };
        }

        /// <summary>Source indexes are assigned before filtering so peers can correlate shared events.</summary>
        public static PresentationEventDto[] EventsFor(TransitionResult result, PlayerId recipient)
        {
            return result.Events
                .Select((e, index) => new { e, index })
                .Where(p => !p.e.Audience.HasValue || p.e.Audience.Value == recipient)
                .Select(p => new PresentationEventDto
                {
                    SourceIndex = p.index,
                    Kind = p.e.Kind,
                    Player = p.e.Player?.Value ?? -1,
                    DominoId = p.e.DominoId ?? -1,
                })
                .ToArray();
        }
    }
}
