using System;
namespace Assets.Scripts.Game.States
{
    public class RoundOverState : GameStateBase
    {
        public RoundOverState(GameStateContext context) : base(context) { }
        public override void EnterState() { ctx.Input.ReadyButtonClicked += ReadyClicked; }
        public override void LeaveState() { ctx.Input.ReadyButtonClicked -= ReadyClicked; }
        private void ReadyClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Ready(); }
    }
}
