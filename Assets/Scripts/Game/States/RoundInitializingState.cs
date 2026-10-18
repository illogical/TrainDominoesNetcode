namespace Assets.Scripts.Game.States
{
    /// <summary>Waits for authoritative setup and local engine/deal presentation. It sends no setup RPC.</summary>
    public class RoundInitializingState : ObservingStateBase
    {
        public RoundInitializingState(GameStateContext context) : base(context) { }
    }
}
