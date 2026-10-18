using System;

namespace Assets.Scripts.Game.States
{
    /// <summary>
    /// These are shared across more than one state (for a group turn vs. a regular turn)
    /// </summary>
    public abstract class EndTurnStateBase : GameStateBase
    {
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

        public override void LeaveState()
        {
            ctx.GameplayManager.AwaitTurn -= GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerTurnEnded -= GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.PlayerHasWonRound -= GameplayManagerPlayerHasWonRound;
            ctx.GameplayManager.PlayerHasWonGame -= GameplayManager_PlayerHasWonGame;
        }

        private void GameplayManager_AwaitTurn(object sender, EventArgs e)
        {
            using var trace = TraceExecution();
            ctx.SwitchState(c => new PlayerAwaitingTurnState(c));
        }
    
        private void GameplayManagerPlayerHasWonRound(object sender, ulong winnerClientId)
        {
            using var trace = TraceExecution();
            ctx.SwitchState(c => new RoundOverState(c));
        }
    
        private void GameplayManager_PlayerHasWonGame(object sender, ulong e)
        {
            using var trace = TraceExecution();
            ctx.SwitchState(c => new GameOverState(c));
        }
    
        private void GameplayManager_PlayerTurnEnded(object sender, EventArgs e)
        {
            using var trace = TraceExecution();
            // when a player ends their turn, why would it matter if a track had been laid?

            //ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
            ctx.SwitchState(c => new PlayerAwaitingTurnState(c));
        }
    }
}
