using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Game.States
{
    public abstract class GameStateBase
    {
        protected GameStateContext ctx;

        public GameStateBase(GameStateContext gameContext)
        {
            ctx = gameContext;
        }

        /// <summary>
        /// Used for logging state changes
        /// </summary>
        public abstract string Name { get; }
        /// <summary>
        /// Setup current state
        /// </summary>
        /// <param name="gameStateContext"></param>
        public abstract void EnterState();
        /// <summary>
        /// Main game loop during this state
        /// </summary>
        /// <param name="gameStateContext"></param>
        public abstract void UpdateState();
        /// <summary>
        /// Cleanup before switching to the next state
        /// </summary>
        /// <param name="gameStateContext"></param>
        public abstract void LeaveState();
    }
}
