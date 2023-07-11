using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Game.States
{
    public class TurnEndedState : GameStateBase     // TODO: debate if TurnEndedState is needed
    {
        public TurnEndedState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(TurnEndedState);

        public override void EnterState()
        { 
            // TODO: check if this was the first turn and if so register that this player is ready for the next turn
            // TODO: need to track if all players are ready for the next turn and if so, start the next turn
        }

        public override void UpdateState()
        {
            throw new NotImplementedException();
        }

        public override void LeaveState()
        {
            throw new NotImplementedException();
        }


    }
}
