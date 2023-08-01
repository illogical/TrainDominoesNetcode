namespace Assets.Scripts.Game.States
{
    public class PlayerAwaitingTurnState : EndTurnStateBase
    {
        public PlayerAwaitingTurnState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(PlayerAwaitingTurnState);

        public override void EnterState()
        {
            base.EnterState();
            ctx.GameplayManager.PlayerTurnStarted += GameplayManager_PlayerTurnStarted;
            
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
        }

        public override void LeaveState()
        {
            ctx.GameplayManager.PlayerTurnStarted -= GameplayManager_PlayerTurnStarted;
            base.LeaveState();
        }

        private void GameplayManager_PlayerTurnStarted(object sender, ulong clientId)
        {
            ctx.SwitchState(ctx.PlayerTurnStartedState);
        }
    }
}
