using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Game.States
{
    public class PregameState : GameStateBase
    {
        public PregameState(GameStateContext gameContext) : base(gameContext) { }

        public override string Name => nameof(GameStartedState);

        public override void EnterState()
        {
            // TODO: await all players to be ready
            Debug.Log("PregameState.EnterState");

            ctx.GameSession.OnPlayerJoined += GameSession_OnPlayerJoined;
        }

        private void GameSession_OnPlayerJoined(object sender, EventArgs e)
        {
            ctx.SwitchState(ctx.GameStartedState);
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
