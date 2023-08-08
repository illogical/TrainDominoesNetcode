using System;
using System.Collections.Generic;

namespace Assets.Scripts.Models
{
    /// <summary>
    /// Serializable lists of domino IDs for player dominoes and a station's dominoes.
    /// </summary>
    [Serializable]
    public class PlayedDominoes
    {
        public int[] PlayerDominoIds { get; private set; }
        // ordered list of tracks and the dominoIds within them
        public Station Station { get; private set; }
        
        public PlayedDominoes(int[] playerDominoIds, Station station)
        {
            PlayerDominoIds = playerDominoIds;
            Station = station;
        }
    }
}
