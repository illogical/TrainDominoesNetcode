using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Game.States
{
    public class PlayerAwaitingTurnState : GameStateBase
    {
        public PlayerAwaitingTurnState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(PlayerAwaitingTurnState);

        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.SetDrawButtonEnabled(false);
            ctx.GameplayManager.InputManager.SetEndTurnButtonEnabled(false);

            ctx.GameplayManager.PlayerTurnStarted += GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.PlayerHasWonRound += GameplayManagerPlayerHasWonRound;
        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {
            ctx.GameplayManager.PlayerTurnStarted -= GameplayManager_PlayerTurnStarted;
            ctx.GameplayManager.PlayerHasWonRound -= GameplayManagerPlayerHasWonRound;
        }

        private void GameplayManager_PlayerTurnStarted(object sender, ulong clientId)
        {
            ctx.SwitchState(ctx.PlayerTurnStartedState);
        }
        
        private void GameplayManagerPlayerHasWonRound(object sender, ulong winnerClientId)
        {
            ctx.SwitchState(ctx.GameOverState);
        }
    }
}
