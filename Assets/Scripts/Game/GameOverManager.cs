using System.Collections.Generic;

namespace Assets.Scripts.Game
{
    public class GameOverManager
    {
        private GameOverUI _gameOverUI;
        
        public GameOverManager(GameOverUI gameOverUI)
        {
            _gameOverUI = gameOverUI;
        }

        // TODO: swap winnderClientId with player name
        public void GameIsOver(ulong winnerClientId, Dictionary<ulong, int> playerScores)
        {
            // the player who has 0 is the winner but we also know who just ended their turn and played their last domino

            _gameOverUI.Show(winnerClientId.ToString(), playerScores);

        }
    }
}