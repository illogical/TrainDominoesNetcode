using System;

namespace Assets.Scripts.Game.States
{
    /// <summary>Waits for authoritative setup and local engine/deal presentation. Host Retry requests authoritative setup without supplying a deck.</summary>
    public class RoundInitializingState : ObservingStateBase
    {
        public RoundInitializingState(GameStateContext context) : base(context) { }
        public override void EnterState() { base.EnterState(); ctx.Input.RetryClicked += RetryClicked; }
        public override void LeaveState() { ctx.Input.RetryClicked -= RetryClicked; base.LeaveState(); }
        private void RetryClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.RetrySetup(); }
    }
}
