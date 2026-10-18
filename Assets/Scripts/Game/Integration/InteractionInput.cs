using System;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>Local intents only. Availability is presentation; the server validates every command.</summary>
    public interface IInteractionInput
    {
        event EventHandler<int> DominoClicked;
        event EventHandler<TrainIdDto> TrainChosen;
        event EventHandler DrawButtonClicked, PassClicked, UndoClicked, EndTurnClicked, AnnounceClicked, ReadyButtonClicked, NewGameButtonClicked, RetryClicked, ResumeClicked;
        event EventHandler<int> ChallengeClicked;
    }
}
