using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Game.States
{
    public class GameJoinedState : GameStateBase
    {
        public GameJoinedState(GameStateContext gameContext) : base(gameContext) { }

        public override string Name => nameof(GameJoinedState);

        public override void EnterState()
        {
            // enable the Draw and End Turn buttons. Via events or via the InputManager?
        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {

        }
    }
}
