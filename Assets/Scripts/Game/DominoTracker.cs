using Assets.Scripts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Game
{
    public class DominoTracker
    {
        public int? SelectedDomino { get; private set; }
        public Dictionary<int, DominoEntity> AllDominoes = new Dictionary<int, DominoEntity>();
        public Station Station { get; private set; }

        private PlayerDominoes playerDominoes = new PlayerDominoes();
        private List<int> availableDominoes = new List<int>();
        private List<int> engineIndices = new List<int>();
        private int engineIndex = -1;

        private const int maxDots = 12;

        /// <summary>
        /// Creates 91 dominoes based upon 12 point max train domino set
        /// </summary>
        public void CreateDominoSet()
        {
            var index = 0;
            for (int i = 0; i < maxDots + 1; i++)
            {
                for (int j = i; j < maxDots + 1; j++)
                {
                    AllDominoes.Add(index, createDomino(i, j, index));
                    availableDominoes.Add(index);

                    if (i == j)
                    {
                        // track index for each double
                        engineIndices.Add(index);
                    }

                    index++;
                }
            }
        }

        public DominoEntity GetDominoByID(int dominoId) => AllDominoes[dominoId];
        public DominoEntity GetEngineDomino() => AllDominoes[engineIndices[engineIndex]];
        public int GetEngineDominoID() => engineIndices[engineIndex];
        public List<int> GetPlayerDominoes(ulong clientId) => playerDominoes.GetPlayerDominoes(clientId);
        public bool IsPlayerDomino(ulong clientId, int dominoId) => playerDominoes.Dominoes[clientId].Contains(dominoId);
        public bool IsEngine(int dominoId) => engineIndices[engineIndex] == dominoId;

        /// <summary>
        /// Used for picking up a player's dominoes at the start of a game
        /// </summary>
        /// <param name="count"></param>
        public int[] PickUpDominoes(ulong clientId, int count)
        {
            List<int> newDominoes = new List<int>();
            for (int i = 0; i < count; i++)
            {
                DominoEntity newDomino = GetDominoFromBonePile();
                newDominoes.Add(newDomino.ID);
                AddPlayerDomino(clientId, newDomino.ID);
            }
            return newDominoes.ToArray();
        }

        public int PickUpDomino(ulong clientId)
        {
            var newDomino = GetDominoFromBonePile();
            AddPlayerDomino(clientId, newDomino.ID);

            return newDomino.ID;
        }

        public void SetSelectedDomino(int dominoId)
        {
            SelectedDomino = dominoId;
        }

        public void AddPlayerDomino(ulong clientId, int dominoId)
        {
            playerDominoes.AddDomino(clientId, dominoId);
        }

        public void AddPlayerDominoes(ulong clientId, List<int> dominoIds)
        {
            playerDominoes.AddDominoes(clientId, dominoIds);
        }

        public DominoEntity GetDominoFromBonePile()
        {
            int randomDominoIndex = UnityEngine.Random.Range(0, availableDominoes.Count); // TODO: could do a single shuffle instead (better performance)
            int dominoID = availableDominoes[randomDominoIndex];
            var domino = AllDominoes[dominoID];

            availableDominoes.RemoveAt(randomDominoIndex);
            return domino;
        }

        public DominoEntity GetNextEngineAndCreateStation()
        {
            var engine = AllDominoes[engineIndices[++engineIndex]];
            availableDominoes.Remove(engine.ID);    // no longer available to pick up

            Station = new Station(engine);

            return engine;
        }

        //public Track AddToNewTrack(int dominoId) => station.AddTrack(dominoId);

        //public Track AddToTrack(int dominoId, int trackIndex) => station.AddDominoToTrack(dominoId, trackIndex);


        private DominoEntity createDomino(int topScore, int bottomScore, int index)
        {
            return new DominoEntity()
            {
                TopScore = topScore,
                BottomScore = bottomScore,
                ID = index
            };
        }
    }
}
