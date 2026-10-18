using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Assets.Scripts.Game.Integration;

namespace Assets.Scripts.Game.States
{
    /// <summary>Owns local interaction states, not authoritative game rules.</summary>
    public class GameStateContext : IDisposable
    {
        public GameSession GameSession { get; }
        public GameplayManager GameplayManager { get; }
        public ClientSession Client { get; }
        public IInteractionInput Input { get; }
        public GameStateBase CurrentState { get; private set; }
        public GameStateDiagnostics Diagnostics { get; }

        private readonly Dictionary<Type, GameStateBase> states = new Dictionary<Type, GameStateBase>();
        private bool disposed;

        public GameStateContext(GameSession gameSession, GameplayManager gameplayManager,
            GameStateDiagnostics diagnostics = null)
        {
            GameSession = gameSession;
            GameplayManager = gameplayManager;
            Client = gameSession.Client;
            Input = gameplayManager.InputManager;
            Diagnostics = diagnostics ?? new GameStateDiagnostics(message => Debug.Log(message, gameSession));
            SwitchState(c => new PregameState(c), reason: "Initialize");
        }

        /// <summary>
        /// Creates a state on first use, then reuses it within this context. Constructors must
        /// have no subscription/entry side effects. Set reuse:false for a fresh, uncached entry.
        /// Switching to the current type still leaves and re-enters it.
        /// </summary>
        public void SwitchState<T>(Func<GameStateContext, T> factory, bool reuse = true,
            [CallerMemberName] string reason = null) where T : GameStateBase
        {
            ThrowIfDisposed();
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            if (!reuse || !states.TryGetValue(typeof(T), out var state))
            {
                state = factory(this);
                if (state == null || state.Context != this)
                    throw new InvalidOperationException("The state factory must return a state belonging to this context.");
                if (reuse) states.Add(typeof(T), state);
            }

            var previous = CurrentState;
            if (previous != null) Execute(previous, nameof(GameStateBase.LeaveState), previous.LeaveState);
            CurrentState = state;
            Diagnostics.RecordTransition(previous, state, reason);
            Execute(state, nameof(GameStateBase.EnterState), state.EnterState);
        }

        public void Update()
        {
            ThrowIfDisposed();
            Execute(CurrentState, nameof(GameStateBase.UpdateState), CurrentState.UpdateState);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var previous = CurrentState;
            CurrentState = null;
            try
            {
                if (previous != null) Execute(previous, nameof(GameStateBase.LeaveState), previous.LeaveState);
                Diagnostics.RecordTransition(previous, null, nameof(Dispose));
            }
            finally { states.Clear(); }
        }

        private void Execute(GameStateBase state, string operation, Action action)
        {
            using (Diagnostics.TraceExecution(state, operation))
            {
                try { action(); }
                catch (Exception exception)
                {
                    Diagnostics.RecordFault(state, operation, exception);
                    throw;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(GameStateContext));
        }
    }
}
