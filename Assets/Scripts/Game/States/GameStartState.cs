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

        public override void EnterState()
        {
            ctx.GameSession.PlaceEngineServerRpc();

            ctx.SwitchState(c => new PlayerTurnStartedState(c));
        }

        public override void LeaveState()
        {

        }

    }
}
