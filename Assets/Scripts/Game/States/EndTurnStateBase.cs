using System;

namespace Assets.Scripts.Game.States
{
    /// <summary>
    /// These are shared across more than one state (for a group turn vs. a regular turn)
    /// </summary>
    public abstract class EndTurnStateBase : GameStateBase
    {
        public override string Name => nameof(EndTurnStateBase);
        public EndTurnStateBase(GameStateContext gameContext) : base(gameContext)
        {
        }


        public override void EnterState()
        {
            ctx.GameplayManager.AwaitTurn += GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerTurnEnded += GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.PlayerHasWonRound += GameplayManagerPlayerHasWonRound;
            ctx.GameplayManager.PlayerHasWonGame += GameplayManager_PlayerHasWonGame;
        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {
            ctx.GameplayManager.AwaitTurn -= GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerTurnEnded -= GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.PlayerHasWonRound -= GameplayManagerPlayerHasWonRound;
            ctx.GameplayManager.PlayerHasWonGame -= GameplayManager_PlayerHasWonGame;
        }

        private void GameplayManager_AwaitTurn(object sender, EventArgs e)
        {
            ctx.SwitchState(ctx.PlayerAwaitingTurnState);
        }
    
        private void GameplayManagerPlayerHasWonRound(object sender, ulong winnerClientId)
        {
            ctx.SwitchState(ctx.RoundOverState);
        }
    
        private void GameplayManager_PlayerHasWonGame(object sender, ulong e)
        {
            ctx.SwitchState(ctx.GameOverState);
        }
    
        private void GameplayManager_PlayerTurnEnded(object sender, EventArgs e)
        {
            // when a player ends their turn, why would it matter if a track had been laid?

            //ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
            ctx.SwitchState(ctx.PlayerAwaitingTurnState);
        }
    }
}
