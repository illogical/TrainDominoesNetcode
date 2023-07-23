using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Game;
using Assets.Scripts.Game.States;
using UnityEngine;

public class RoundOverState : GameStateBase
{
    public RoundOverState(GameStateContext gameContext) : base(gameContext) { }
    public override string Name => nameof(RoundOverState);
    public override void EnterState()
    {
        ctx.GameplayManager.InputManager.ReadyButtonClicked += InputManager_ReaadyButtonClicked;
        ctx.GameplayManager.AllPlayersReadyForNextRound += GameplayManager_AllPlayersReadyForNextRound;
            
        ShowGameOverUI();
    }


    public override void UpdateState()
    {
        
    }

    public override void LeaveState()
    {
        ctx.GameplayManager.InputManager.ReadyButtonClicked -= InputManager_ReaadyButtonClicked;
        ctx.GameplayManager.AllPlayersReadyForNextRound -= GameplayManager_AllPlayersReadyForNextRound;
    }
    
    // TODO: make different game over UI from round end UI
    private void ShowGameOverUI()
    {
        ulong? winnerClientId = ctx.GameplayManager.TurnManager.GetGameWinnerClientId();
        if (!winnerClientId.HasValue)
        {
            return;
        }
            
        // use the scores that each client has stored in their TurnManager
        var playerScores = ctx.GameplayManager.RoundManager.GetRoundScores();
        var playerTotals = ctx.GameplayManager.RoundManager.GetPlayerTotalScores();
        ctx.GameplayManager.GameIsOver(winnerClientId.Value, playerScores, playerTotals);
    }
    
    private void InputManager_ReaadyButtonClicked(object sender, EventArgs e)
    {
        ctx.GameSession.PlayerReadyForNextRoundServerRpc();
    }
    
    private void GameplayManager_AllPlayersReadyForNextRound(object sender, EventArgs e)
    {
        ctx.GameplayManager.DominoTracker.Reset();
        ctx.GameplayManager.TurnManager.Reset();

        // TODO: Ideally animate dominoes leaving then destroy them
        // destroy all meshes
        ctx.GameplayManager.ClientResetForNextRound();
        
        ctx.SwitchState(new GameStartedState(ctx));
    }
}
