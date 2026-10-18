using System;
using System.Runtime.CompilerServices;

namespace Assets.Scripts.Game.States
{
    public abstract class GameStateBase
    {
        protected readonly GameStateContext ctx;
        internal GameStateContext Context => ctx;

        public GameStateBase(GameStateContext gameContext)
        {
            ctx = gameContext ?? throw new ArgumentNullException(nameof(gameContext));
        }

        /// <summary>
        /// Used for logging state changes
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>Use a using scope in handlers to trace work even when no transition occurs.</summary>
        protected IDisposable TraceExecution([CallerMemberName] string operation = null)
            => ctx.Diagnostics.TraceExecution(this, operation);
        /// <summary>
        /// Setup current state
        /// </summary>
        /// <param name="gameStateContext"></param>
        public abstract void EnterState();

        /// <summary>
        /// Optional main game loop during this state
        /// </summary>
        /// <param name="gameStateContext"></param>
        public virtual void UpdateState()
        {
            
        }
        /// <summary>
        /// Cleanup before switching to the next state
        /// </summary>
        /// <param name="gameStateContext"></param>
        public abstract void LeaveState();
    }
}
