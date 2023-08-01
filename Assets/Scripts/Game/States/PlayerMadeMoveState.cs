using System;

namespace Assets.Scripts.Game.States
{
    public class PlayerMadeMoveState : EndTurnStateBase
    {
        public PlayerMadeMoveState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(PlayerMadeMoveState);
        public override void EnterState()
        {
            base.EnterState();
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked += InputManager_EndTurnClicked;

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
            
            base.LeaveState();
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

    }
}
