using System;
using System.Collections.Generic;

namespace Assets.Scripts.Models.DTO
{
    [Serializable]
    public class TurnEndDTO
    {
        public Station MainStation { get; private set;  }
        public Dictionary<int, bool> DominoFlipInfo { get; private set; }
        public int[] AddedDominoes { get; private set; }
        public bool GroupTurn { get; private set;  }

        public TurnEndDTO(Station mainStation, Dictionary<int, bool> dominoFlipStates, int[] addedDominoes, bool groupTurn)
        {
            MainStation = mainStation;
            DominoFlipInfo = dominoFlipStates;
            AddedDominoes = addedDominoes;
            GroupTurn = groupTurn;
        }
    }
}