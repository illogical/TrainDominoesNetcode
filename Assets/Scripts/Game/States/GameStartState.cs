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
        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.DominoClicked += InputManager_DominoClicked;
            ctx.GameplayManager.InputManager.DrawButtonClicked += InputManager_DrawButtonClicked;
            ctx.GameplayManager.InputManager.EndTurnClicked += InputManager_EndTurnClicked;

            ctx.GameSession.PlaceEngineServerRpc();

            Debug.Log("GameStartedState.EnterState");
            //ctx.Player.CmdDealDominoes(12);    // TODO: wondering if GameplayManager should contain the logic for determining how many dominoes to deal to each player
        }

        public override void UpdateState()
        {
            //ctx.SwitchState(ctx.PlayerTurnStartedState);
        }

        public override void LeaveState()
        {

        }

        private void InputManager_DominoClicked(object sender, int dominoId)
        {
            ctx.GameplayManager.SelectDomino(dominoId);

            //if (!selectedDominoId.HasValue)
            //{
            //    // raise domino
            //    layoutManager.SelectDomino(meshManager.GetDominoMeshById(dominoId));
            //    selectedDominoId = dominoId;
            //}
            //else if (selectedDominoId == dominoId)
            //{
            //    // lower domino
            //    layoutManager.DeselectDomino(meshManager.GetDominoMeshById(dominoId));
            //    selectedDominoId = null;
            //}
            //else
            //{
            //    layoutManager.DeselectDomino(meshManager.GetDominoMeshById(selectedDominoId.Value));
            //    layoutManager.SelectDomino(meshManager.GetDominoMeshById(dominoId));
            //    selectedDominoId = dominoId;
            //}

            // TODO: add set of events for which type of domino was clicked (player domino, station domino, or engine domino)

            // currently only player dominoes are clickable

        }

        // TODO: how to prevent needing to repeat this across all states? It is different for first state vs subsequent states
        private void InputManager_DrawButtonClicked(object sender, EventArgs e)
        {
            ctx.GameSession.DrawInitialDominoesServerRpc();
        }

        private void InputManager_EndTurnClicked(object sender, EventArgs e)
        {
            ctx.GameSession.EndTurnServerRpc();
        }
    }
}
