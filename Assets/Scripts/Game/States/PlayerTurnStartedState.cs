using System;

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
            // TODO: track the added domino 
            
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked += InputManager_DrawButtonClicked;            
            ctx.GameplayManager.PlayerAddedDomino += GameplayManager_PlayerAddedDomino;

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
            ctx.GameplayManager.PlayerAddedDomino -= GameplayManager_PlayerAddedDomino;
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
            ctx.SwitchState(ctx.PlayerMadeMoveState);
        }
    }
}
