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

        public override void LeaveState()
        {

        }
    }
}
