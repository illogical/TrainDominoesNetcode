namespace Assets.Scripts.Game.States
{
    /// <summary>Observes another actor, but still allows manual announcement and challenge.</summary>
    public class PlayerAwaitingTurnState : ObservingStateBase
    {
        public PlayerAwaitingTurnState(GameStateContext context) : base(context) { }
    }
}
