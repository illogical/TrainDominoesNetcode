using System;

namespace Assets.Scripts.Game.States
{
    public class RoundOverState : GameStateBase
    {
        public RoundOverState(GameStateContext gameContext) : base(gameContext) { }
        public override void EnterState()
        {
            ctx.GameplayManager.InputManager.ReadyButtonClicked += InputManager_ReaadyButtonClicked;
            ctx.GameplayManager.AllPlayersReadyForNextRound += GameplayManager_AllPlayersReadyForNextRound;
            
            ShowRoundOverUI();
        }

        public override void LeaveState()
        {
            ctx.GameplayManager.InputManager.ReadyButtonClicked -= InputManager_ReaadyButtonClicked;
            ctx.GameplayManager.AllPlayersReadyForNextRound -= GameplayManager_AllPlayersReadyForNextRound;
        }
    
        private void ShowRoundOverUI()
        {
            using var trace = TraceExecution();
            ulong? winnerClientId = ctx.GameplayManager.TurnManager.GetRoundWinnerClientId();
            if (!winnerClientId.HasValue)
            {
                return;
            }
            
            // use the scores that each client has stored in their TurnManager
            var playerScores = ctx.GameplayManager.RoundManager.GetRoundScores();
            var playerTotals = ctx.GameplayManager.RoundManager.GetPlayerTotalScores();
            ctx.GameplayManager.RoundIsOver(winnerClientId.Value, playerScores, playerTotals);
        }
    
        private void InputManager_ReaadyButtonClicked(object sender, EventArgs e)
        {
            using var trace = TraceExecution();
            ctx.GameSession.PlayerReadyForNextRoundServerRpc();
        }
    
        private void GameplayManager_AllPlayersReadyForNextRound(object sender, EventArgs e)
        {
            using var trace = TraceExecution();
            ctx.GameplayManager.DominoTracker.Reset();
            ctx.GameplayManager.TurnManager.ResetForNextRound();

            // TODO: Ideally animate dominoes leaving then destroy them
            // destroy all meshes
            ctx.GameplayManager.ClientResetForNextRound();
        
            ctx.SwitchState(c => new GameStartedState(c), reuse: false);
        }
    }
}
