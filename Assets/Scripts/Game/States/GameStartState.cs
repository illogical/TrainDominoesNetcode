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
            ctx.GameSession.PlaceEngineServerRpc();

            ctx.SwitchState(ctx.PlayerTurnStartedState);
        }

        public override void UpdateState()
        {
            
        }

        public override void LeaveState()
        {

        }

    }
}
