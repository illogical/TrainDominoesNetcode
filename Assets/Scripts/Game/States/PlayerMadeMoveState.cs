using System;

namespace Assets.Scripts.Game.States
{
    public class PlayerMadeMoveState : GameStateBase
    {
        public PlayerMadeMoveState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(PlayerMadeMoveState);
        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked += InputManager_EndTurnClicked;
            ctx.GameplayManager.PlayerTurnStarted += GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.PlayerTurnEnded += GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.AwaitTurn += GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerHasWonRound += GameplayManagerPlayerHasWonRound;
            ctx.GameplayManager.PlayerHasWonGame += GameplayManager_PlayerHasWonGame;
        
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(true);
        }

        public override void UpdateState()
        {
        
        }

        public override void LeaveState()
        {
            ctx.GameplayManager.InputManager.DominoClicked -= InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked -= InputManager_EndTurnClicked;
            ctx.GameplayManager.PlayerTurnStarted -= GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.PlayerTurnEnded -= GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.AwaitTurn -= GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerHasWonRound -= GameplayManagerPlayerHasWonRound;
            ctx.GameplayManager.PlayerHasWonGame -= GameplayManager_PlayerHasWonGame;
        }
        
        /// <summary>
        /// After the group turn, this is the first player's turn
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="clientId"></param>
        private void GameplayManager_PlayerTurnStarted(object sender, ulong clientId)
        {
            ctx.SwitchState(ctx.PlayerTurnStartedState);
        }
    
        private void InputManager_DominoClicked(object sender, int dominoId)
        {
            // TODO: only take action if this is the domino that was added to a track this turn
        
            // the server decides which type of domino was clicked
            //ctx.GameplayManager.SoundManager.PlayRandomClickSound();
            //ctx.GameSession.SelectDominoServerRpc(dominoId);
        }
    
        private void InputManager_EndTurnClicked(object sender, EventArgs e)
        {
            ctx.GameSession.EndTurnServerRpc();
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
        }
    
        private void GameplayManager_PlayerTurnEnded(object sender, EventArgs e)
        {
            // when a player ends their turn, why would it matter if a track had been laid?

            //ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
            ctx.SwitchState(ctx.PlayerAwaitingTurnState);
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
    }
}
