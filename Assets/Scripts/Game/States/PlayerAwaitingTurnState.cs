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

        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {
            throw new NotImplementedException();
        }
    }
}
