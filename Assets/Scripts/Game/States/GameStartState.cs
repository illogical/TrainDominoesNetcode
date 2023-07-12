using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Game.States
{
    public class GameStartedState : GameStateBase
    {
        public GameStartedState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(GameStartedState);

        private int? selectedDominoId;

        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked += InputManager_DrawButtonClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked += InputManager_EndTurnClicked;

            ctx.GameplayManager.PlayerTurnStarted += GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.AwaitTurn += GameplayManager_AwaitTurn;

            ctx.GameSession.PlaceEngineServerRpc();

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

        }

        private void InputManager_DominoClicked(object sender, int dominoId)
        {
            // the server decides which type of domino was clicked
            ctx.GameSession.SelectDominoServerRpc(dominoId);
        }

        // TODO: how to prevent needing to repeat this across all states? It is different for first state vs subsequent states
        private void InputManager_DrawButtonClicked(object sender, EventArgs e)
        {
            ctx.GameSession.DrawInitialDominoesServerRpc();
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(true);
        }

        private void InputManager_EndTurnClicked(object sender, EventArgs e)
        {
            ctx.GameSession.EndFirstTurnServerRpc();
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);
        }

        private void GameplayManager_PlayerTurnStarted(object sender, EventArgs e)
        {
            ctx.SwitchState(ctx.PlayerTurnStartedState);
        }

        private void GameplayManager_AwaitTurn(object sender, EventArgs e)
        {
            ctx.SwitchState(ctx.PlayerAwaitingTurnState);
        }
    }
}
