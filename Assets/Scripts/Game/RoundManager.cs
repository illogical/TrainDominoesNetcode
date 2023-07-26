using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Models;
using UnityEngine;

namespace Assets.Scripts.Game
{
    public class RoundManager
    {
        private const int _maxRounds = 12;
        
        private List<Round> _rounds;
        private Dictionary<ulong, int> _playerScoreTotals;

        public RoundManager()
        {
            _rounds = new List<Round>();
            _playerScoreTotals = new Dictionary<ulong, int>();
            StartNewRound();
        }

        /// <summary>
        /// Used to skip rounds to test the game over logic
        /// </summary>
        /// <param name="roundCount"></param>
        public RoundManager(int roundCount)
        {
            _rounds = new List<Round>();
            _playerScoreTotals = new Dictionary<ulong, int>();
            
            if (roundCount > 12)
            {
                Debug.LogError("Error: Cannot skip more than 12 rounds");
                return;
            }
            
            for (int i = 0; i < roundCount; i++)
            {
                StartNewRound();
            }
        }

        public int GetRoundNumber() => _rounds.Count;
        public bool IsLastRound => _rounds.Count >= _maxRounds;
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

        public List<ulong> GetGameWinners()
        {
            var lowestScore = _playerScoreTotals.Values.Min(t => t);
            var winners = _playerScoreTotals
                .Where(t => t.Value == lowestScore)
                .Select(t => t.Key)
                .ToList();

            return winners;
        }
    }
}