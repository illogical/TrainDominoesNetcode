using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Game.States
{
    /// <summary>
    /// The player's turn just began. A domino has not been selected yet.
    /// </summary>
    public class PlayerTurnStartedState : GameStateBase
    {
        public PlayerTurnStartedState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(PlayerTurnStartedState); 
        
        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked += InputManager_DrawButtonClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked += InputManager_EndTurnClicked;

            ctx.GameplayManager.PlayerTurnStarted += GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.AwaitTurn += GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerTurnEnded += GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.PlayerHasWonRound += GameplayManagerPlayerHasWonRound;

            // cannot end turn until the dominoes are drawn
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(true);

            // this could also draw the initial dominoes however the animations are not smooth for the host player for some reason
            //ctx.GameSession.DrawInitialDominoesServerRpc();
        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {
            ctx.GameplayManager.InputManager.DominoClicked -= InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked -= InputManager_DrawButtonClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked -= InputManager_EndTurnClicked;

            ctx.GameplayManager.PlayerTurnStarted -= GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.AwaitTurn -= GameplayManager_AwaitTurn;
            ctx.GameplayManager.PlayerTurnEnded -= GameplayManager_PlayerTurnEnded;
            ctx.GameplayManager.PlayerHasWonRound -= GameplayManagerPlayerHasWonRound;
        }

        private void InputManager_DominoClicked(object sender, int dominoId)
        {
            // the server decides which type of domino was clicked
            ctx.GameSession.SelectDominoServerRpc(dominoId);
        }

        // TODO: how to prevent needing to repeat this across all states? It is different for first state vs subsequent states
        private void InputManager_DrawButtonClicked(object sender, EventArgs e)
        {
            ctx.GameSession.DrawDominoesServerRpc();
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(true);
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

        /// <summary>
        /// After the group turn, this is the first player's turn
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="clientId"></param>
        private void GameplayManager_PlayerTurnStarted(object sender, ulong clientId)
        {
            ctx.SwitchState(ctx.PlayerTurnStartedState);
        }

        private void GameplayManager_AwaitTurn(object sender, EventArgs e)
        {
            ctx.SwitchState(ctx.PlayerAwaitingTurnState);
        }
        
        private void GameplayManagerPlayerHasWonRound(object sender, ulong winnerClientId)
        {
            ctx.SwitchState(ctx.RoundOverState);
        }
    }
}
