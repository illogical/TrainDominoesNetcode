﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Models
{
    [Serializable]
    public class Track
    {
        public ulong? PlayerId { get; set; }  // each player gets only 1 track. When null then anyone can use this track
        public bool HasTrain { get; set; }  // TODO: needs set to true when a player is unable to play a domino
        public List<int> DominoIds { get; set; }

        public Track(ulong? owner = null, bool hasTrain = false)
        {
            DominoIds = new List<int>();
            PlayerId = owner;
            HasTrain = hasTrain;
        }
        
        public Track(int dominoId, ulong? owner = null, bool hasTrain = false)
        {
            DominoIds = new List<int> { dominoId };

            PlayerId = owner;
            HasTrain = hasTrain;
        }

        public void AddDominoToTrack(int dominoId)
        {
            DominoIds.Add(dominoId);
        }

        public bool IsAvailable() => HasTrain == false && !PlayerId.HasValue;
        public bool ContainsDomino(int dominoId) => DominoIds.Contains(dominoId);
        public int GetEndDominoId() => DominoIds[DominoIds.Count - 1];
    }
}
