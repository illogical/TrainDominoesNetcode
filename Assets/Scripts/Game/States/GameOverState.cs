using System;
namespace Assets.Scripts.Game.States
{
    public class GameOverState : GameStateBase
    {
        public GameOverState(GameStateContext context) : base(context) { }
        public override void EnterState() { ctx.Input.NewGameButtonClicked += NewGameClicked; }
        public override void LeaveState() { ctx.Input.NewGameButtonClicked -= NewGameClicked; }
        private void NewGameClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.NewMatch(); }
    }
}
