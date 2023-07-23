using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Game
{
    public class PlayerDominoes
    {
        public Dictionary<ulong, List<int>> Dominoes { get; set; }    // List of domino IDs for each player's NetId

        public PlayerDominoes()
        {
            Dominoes = new Dictionary<ulong, List<int>>();
        }

        public List<int> GetPlayerDominoes(ulong clientId)
        {
            if (!Dominoes.ContainsKey(clientId))
            {
                Dominoes.Add(clientId, new List<int>());
            }

            return Dominoes[clientId];
        }


        public void AddDomino(ulong netId, int dominoId)
        {
            if (!Dominoes.ContainsKey(netId))
            {
                Dominoes.Add(netId, new List<int>());
            }

            Dominoes[netId].Add(dominoId);
        }

        public void AddDominoes(ulong netId, List<int> dominoIds)
        {
            if (!Dominoes.ContainsKey(netId))
            {
                Dominoes.Add(netId, new List<int>());
            }

            Dominoes[netId].AddRange(dominoIds);
        }

        public void RemoveDomino(ulong netId, int dominoId)
        {
            if (!Dominoes.ContainsKey(netId))
            {
                Debug.LogError($"PlayerDominoes.RemoveDomino: clientId {netId} not found");
            }

            if (!Dominoes[netId].Contains(dominoId))
            {
                Debug.LogError($"PlayerDominoes.RemoveDomino: dominoId {dominoId} not found");
            }

            Dominoes[netId].Remove(dominoId);
        }
    }
}
