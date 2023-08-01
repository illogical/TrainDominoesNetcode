using System;
using UnityEngine;

namespace Assets.Scripts.Game.States
{
    /// <summary>
    /// The player's turn just began. A domino has not been selected yet.
    /// </summary>
    public class PlayerTurnStartedState : EndTurnStateBase
    {
        public PlayerTurnStartedState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(PlayerTurnStartedState);

        public override void EnterState()
        {
            base.EnterState();
            // TODO: track the added domino 
            
            ctx.GameplayManager.PlayerTurnStarted += GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked += InputManager_DrawButtonClicked; 
            ctx.GameplayManager.InputManager.EndTurnClicked += InputManager_EndTurnClicked;
            ctx.GameplayManager.PlayerAddedDomino += GameplayManager_PlayerAddedDomino;

            // cannot end turn until the dominoes are drawn
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(true);

            // this could also draw the initial dominoes however the animations are not smooth for the host player for some reason
            //ctx.GameSession.DrawInitialDominoesServerRpc();
        }

        public override void LeaveState()
        {
            ctx.GameplayManager.PlayerTurnStarted -= GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.InputManager.DominoClicked -= InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked -= InputManager_DrawButtonClicked;  
            ctx.GameplayManager.InputManager.EndTurnClicked -= InputManager_EndTurnClicked;
            ctx.GameplayManager.PlayerAddedDomino -= GameplayManager_PlayerAddedDomino;
            base.LeaveState();
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
            // the server decides which type of domino was clicked
            ctx.GameplayManager.SoundManager.PlayRandomClickSound();
            ctx.GameSession.SelectDominoServerRpc(dominoId);
        }

        // TODO: how to prevent needing to repeat this across all states? It is different for first state vs subsequent states
        private void InputManager_DrawButtonClicked(object sender, EventArgs e)
        {
            ctx.GameSession.DrawDominoesServerRpc();
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(true);
        }
        
        private void GameplayManager_PlayerAddedDomino(object sender, int selectedDominoId)
        {
            if (ctx.GameplayManager.TurnManager.IsGroupTurn) // IsGroupTurn was specifically sync'd across clients
            {
                Debug.Log("It is the group turn so carry on.");
                // during the group turn, allow the player to keep adding dominoes
                return;
            }
            ctx.SwitchState(ctx.PlayerMadeMoveState);
        }
        
        private void InputManager_EndTurnClicked(object sender, EventArgs e)
        {
            ctx.GameSession.EndTurnServerRpc(); // keep in mind this does not happen immediately
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
        }
    }
}
