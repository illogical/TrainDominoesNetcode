using System;

namespace Assets.Scripts.Game.States
{
    // TODO: PregameState
    public class PregameState : GameStateBase
    {
        public PregameState(GameStateContext gameContext) : base(gameContext) { }

        public override void EnterState()
        {
            // TODO: await all players to be ready (eventually min of 2 players and max of 6 players (I think))

            ctx.GameSession.OnPlayerJoined += GameSession_OnPlayerJoined;
        }

        private void GameSession_OnPlayerJoined(object sender, EventArgs e)
        {
            using var trace = TraceExecution();
            // register the player with the server
            ctx.GameSession.PlayerJoinedServerRpc();

            ctx.SwitchState(c => new GameStartedState(c));
        }

        public override void UpdateState()
        {
            
        }

        public override void LeaveState()
        {
            ctx.GameSession.OnPlayerJoined -= GameSession_OnPlayerJoined;
        }


    }
}
