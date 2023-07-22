using System.Collections.Generic;
using Assets.Scripts.Models;

namespace Assets.Scripts.Game
{
    public class RoundManager
    {
        private List<Round> _rounds;
        private Dictionary<ulong, int> _playerScoreTotals;

        public RoundManager()
        {
            _rounds = new List<Round>();
            _playerScoreTotals = new Dictionary<ulong, int>();
            StartNewRound();
        }

        public int GetRoundNumber() => _rounds.Count;
        public Dictionary<ulong, int> GetRoundScores() => _rounds[_rounds.Count - 1].GetRoundScores();
        public Dictionary<ulong, int> GetPlayerTotalScores() => _playerScoreTotals;

        public void StartNewRound()
        {
            _rounds.Add(new Round());
        }

        public void EndRound(Dictionary<ulong, int> playerScores)
        {
            _rounds[_rounds.Count - 1].SetRoundScores(playerScores);
            UpdatePlayerTotalGameScores();
        }

        private Dictionary<ulong, int> UpdatePlayerTotalGameScores()
        {
            _playerScoreTotals.Clear();

            for (int i = 0; i < _rounds.Count; i++)
            {
                Round round = _rounds[i];
                var roundScores = round.GetRoundScores();
                foreach(ulong clientId in roundScores.Keys)
                {
                    _playerScoreTotals.TryAdd(clientId, 0);
                    _playerScoreTotals[clientId] += roundScores[clientId];
                }
            }
            
            return _playerScoreTotals;
        }
    }
}