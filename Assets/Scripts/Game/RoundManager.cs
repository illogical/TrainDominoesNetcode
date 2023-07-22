using System.Collections.Generic;
using Assets.Scripts.Models;

namespace Assets.Scripts.Game
{
    public class RoundManager
    {
        private List<Round> _rounds;

        public RoundManager()
        {
            _rounds = new List<Round>();
            StartNewRound();
        }

        public int GetRoundNumber() => _rounds.Count;
        public Dictionary<ulong, int> GetRoundScores() => _rounds[_rounds.Count - 1].GetRoundScores();

        public void StartNewRound()
        {
            _rounds.Add(new Round());
        }

        public void EndRound(Dictionary<ulong, int> playerScores)
        {
            _rounds[_rounds.Count - 1].SetRoundScores(playerScores);
        }

        public Dictionary<ulong, int> UpdatePlayerTotalGameScores()
        {
            var playerTotals = new Dictionary<ulong, int>();

            for (int i = 0; i < _rounds.Count; i++)
            {
                Round round = _rounds[i];
                var roundScores = round.GetRoundScores();
                foreach(ulong clientId in roundScores.Keys)
                {
                    playerTotals.TryAdd(clientId, 0);
                    playerTotals[clientId] += roundScores[clientId];
                }
            }
            
            return playerTotals;
        }
    }
}