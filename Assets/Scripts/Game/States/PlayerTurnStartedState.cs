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
        public int? SelectedPlayerDominoId;

        public PlayerTurnStartedState(GameStateContext gameContext) : base(gameContext) { }

        // TODO: how do we know if this player has a track yet?

        public override string Name => nameof(PlayerTurnStartedState);
        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(true);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(true);

            SelectedPlayerDominoId = null;
            ctx.GameplayManager.DominoClicked += DominoClicked;
            ctx.GameplayManager.PlayerDominoSelected += SelectPlayerDomino;
        }

        public override void UpdateState() { }

        public override void LeaveState()
        {
            ctx.GameplayManager.DominoClicked -= DominoClicked;
            ctx.GameplayManager.PlayerDominoSelected -= SelectPlayerDomino;
        }

        private void DominoClicked(object sender, int dominoId)
        {
            //ctx.GameSession.CmdDominoClicked(dominoId);
        }

        private void SelectPlayerDomino(object sender, int dominoId)
        {
            //SelectedPlayerDominoId = dominoId;

            //ctx.GameplayManager.DominoTracker.SetSelectedDomino(SelectedPlayerDominoId.Value);
            //ctx.Player.CmdSelectPlayerDomino(SelectedPlayerDominoId.Value, null);

            //ctx.SwitchState(ctx.PlayerSelectedPlayerDominoState);
        }
    }
}
