using UnityEngine;

namespace Assets.Scripts.Game.States
{
    public class GameStateContext
    {
        public GameSession GameSession { get; private set; }
        public GameplayManager GameplayManager { get; private set; }

        public PregameState PregameState;
        public GameStartedState GameStartedState;
        public PlayerTurnStartedState PlayerTurnStartedState;
        public PlayerAwaitingTurnState PlayerAwaitingTurnState;

        private GameStateBase currentState;

        public GameStateContext(GameSession gameSession, GameplayManager gameplayManager)
        {
            PregameState = new PregameState(this);
            GameStartedState = new GameStartedState(this);
            PlayerTurnStartedState = new PlayerTurnStartedState(this);
            PlayerAwaitingTurnState = new PlayerAwaitingTurnState(this);


            GameSession = gameSession;
            GameplayManager = gameplayManager;

            currentState = PregameState;
            currentState.EnterState();
        }

        public void Update()
        {
            currentState.UpdateState();
        }

        public void SwitchState(GameStateBase state)
        {
            currentState.LeaveState();
            currentState = state;

            LogStateChanged(state.Name);

            state.EnterState();
        }

        public void LogStateChanged(string stateName)
        {
            Debug.Log($"<b><color=green>STATE:</color> {stateName}</b>");
        }
    }
}
