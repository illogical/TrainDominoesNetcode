using System;
namespace Assets.Scripts.Game.States
{
    public class PlayerActionPendingState : GameStateBase
    {
        public PlayerActionPendingState(GameStateContext context) : base(context) { }
        public override void EnterState() { ctx.Input.RetryClicked += RetryClicked; }
        public override void LeaveState() { ctx.Input.RetryClicked -= RetryClicked; }
        private void RetryClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Retry(); }
    }
}
