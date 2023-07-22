using System.Collections.Generic;

namespace Assets.Scripts.Models
{
    public class Round
    {
        private Dictionary<ulong, int> _playerScores;

        public Round()
        {
            _playerScores = new Dictionary<ulong, int>();
        }

        public void SetRoundScores(Dictionary<ulong, int> playerScores) => _playerScores = playerScores;
        public Dictionary<ulong, int> GetRoundScores() => _playerScores;
    }
}