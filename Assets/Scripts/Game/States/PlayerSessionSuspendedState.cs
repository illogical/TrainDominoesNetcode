using System;
namespace Assets.Scripts.Game.States
{
    public class PlayerSessionSuspendedState : GameStateBase
    {
        public PlayerSessionSuspendedState(GameStateContext context) : base(context) { }
        public override void EnterState()
        {
            ctx.Input.ResumeClicked += ResumeClicked;
            ctx.Input.NewGameButtonClicked += NewGameClicked;
            ctx.Input.RetryClicked += SyncClicked;
        }
        public override void LeaveState()
        {
            ctx.Input.ResumeClicked -= ResumeClicked;
            ctx.Input.NewGameButtonClicked -= NewGameClicked;
            ctx.Input.RetryClicked -= SyncClicked;
        }
        private void ResumeClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Resume(); }
        private void NewGameClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.NewMatch(); }
        private void SyncClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Synchronize(); }
    }
}
