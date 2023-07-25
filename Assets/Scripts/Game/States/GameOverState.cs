using System;

namespace Assets.Scripts.Game.States
{
    public class GameOverState : GameStateBase
    {
        public GameOverState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(GameOverState);

        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.ReadyButtonClicked += InputManager_ReadyButtonClicked;
            
            ShowGameOverUI();
        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {
            ctx.GameplayManager.InputManager.ReadyButtonClicked -= InputManager_ReadyButtonClicked;
            // TODO: button for players to start a new game
            // TODO: wait for all players to click button to start new game
            // TODO: Alert other players how many other players have requested to play again
            // TODO: if any players leave the game then alert other players (generically) to start over or exit
        }

        private void ShowGameOverUI()
        {
            ulong? winnerClientId = ctx.GameplayManager.TurnManager.GetRoundWinnerClientId();
            if (!winnerClientId.HasValue)
            {
                return;
            }
            
            // use the scores that each client has stored in their TurnManager
            ctx.GameplayManager.InputManager.SetRestartReadyButtonEnabled(true);
            var playerScores = ctx.GameplayManager.RoundManager.GetRoundScores();
            var playerTotals = ctx.GameplayManager.RoundManager.GetPlayerTotalScores();
            ctx.GameplayManager.GameIsOver(winnerClientId.Value, playerScores, playerTotals);
        }

        private void InputManager_ReadyButtonClicked(object sender, EventArgs e)
        {
            //ctx.SwitchState(new GameStartedState(ctx));
        }
        
    }
}