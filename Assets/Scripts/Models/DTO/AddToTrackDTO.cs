using System;
using System.Collections.Generic;

namespace Assets.Scripts.Models.DTO
{
    [Serializable]
    public class AddToTrackDTO
    {
        public ulong ClientId { get; set; }
        public int SelectedDominoId { get; set; }
        public int TrackIndex { get; set; }
        public bool IsFlipped { get; set; }
        public int[] PlayerDominoes { get; set; }
        public Station PlayerTurnStation { get; set; }
    }
}