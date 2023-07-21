namespace Assets.Scripts.Game.States
{
    public class GameOverState : GameStateBase
    {
        public GameOverState(GameStateContext gameContext) : base(gameContext) { }
        public override string Name => nameof(GameOverState);
        
        public override void EnterState()
        {
            // TODO: calculate scores
            // TODO: announce the winner
            // TODO: display UI for starting a new game, starting over, or exiting
        }

        public override void UpdateState()
        {

        }

        public override void LeaveState()
        {
            // TODO: button for players to start a new game
            // TODO: wait for all players to click button to start new game
            // TODO: Alert other players how many other players have requested to play again
            // TODO: if any players leave the game then alert other players (generically) to start over or exit
        }
    }
}