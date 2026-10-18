using System;
using Assets.Scripts.Game.Integration;
namespace Assets.Scripts.Game.States
{
    public class PlayerActingState : ObservingStateBase
    {
        public PlayerActingState(GameStateContext context) : base(context) { }
        public override void EnterState()
        {
            base.EnterState();
            ctx.Input.DominoClicked += TileClicked;
            ctx.Input.TrainChosen += TrainChosen;
            ctx.Input.DrawButtonClicked += DrawClicked;
            ctx.Input.PassClicked += PassClicked;
            ctx.Input.UndoClicked += UndoClicked;
            ctx.Input.EndTurnClicked += EndClicked;
        }
        public override void LeaveState()
        {
            ctx.Input.DominoClicked -= TileClicked;
            ctx.Input.TrainChosen -= TrainChosen;
            ctx.Input.DrawButtonClicked -= DrawClicked;
            ctx.Input.PassClicked -= PassClicked;
            ctx.Input.UndoClicked -= UndoClicked;
            ctx.Input.EndTurnClicked -= EndClicked;
            base.LeaveState();
        }
        private void TileClicked(object sender, int id) { using var trace = TraceExecution(); ctx.Client.ClickTile(id); }
        private void TrainChosen(object sender, TrainIdDto train) { using var trace = TraceExecution(); ctx.Client.ChooseTrain(train); }
        private void DrawClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Submit(CommandKind.Draw); }
        private void PassClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Submit(CommandKind.Pass); }
        private void UndoClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Submit(CommandKind.Undo); }
        private void EndClicked(object sender, EventArgs e) { using var trace = TraceExecution(); ctx.Client.Submit(CommandKind.EndTurn); }
    }
}
