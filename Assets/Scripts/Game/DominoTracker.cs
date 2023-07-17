using Assets.Scripts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Assets.Scripts.Game
{
    public class DominoTracker
    {
        public int? SelectedDomino { get; private set; }
        public Dictionary<int, DominoEntity> AllDominoes = new Dictionary<int, DominoEntity>();     // all clients should have this dictionary
        public Station Station { get; private set; }

        private Dictionary<ulong, Station> _turnStations;   // track a station for each player and reconcile when the player ends their turn

        private PlayerDominoes playerDominoes = new PlayerDominoes();
        private List<int> availableDominoes = new List<int>();
        private List<int> engineIndices = new List<int>();
        private int engineIndex = -1;

        private const int maxDots = 12;

        public DominoTracker()
        {
            _turnStations = new Dictionary<ulong, Station>();
        }

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

        public DominoEntity[] GetDominoesByIDs(int[] dominoIds)
        {
            var dominoes = new List<DominoEntity>();
            foreach(var dominoId in dominoIds)
            {
                dominoes.Add(AllDominoes[dominoId]);
            }

            return dominoes.ToArray();
        }

        public DominoEntity GetDominoByID(int dominoId) => AllDominoes[dominoId];
        public DominoEntity GetEngineDomino() => AllDominoes[engineIndices[engineIndex]];
        public int GetEngineDominoID() => engineIndices[engineIndex];
        public List<int> GetPlayerDominoes(ulong clientId) => playerDominoes.GetPlayerDominoes(clientId);
        public bool IsPlayerDomino(ulong clientId, int dominoId) => playerDominoes.Dominoes[clientId].Contains(dominoId);
        public bool IsEngine(int dominoId) => engineIndices[engineIndex] == dominoId;

        public Station GetTurnStationByClientId(ulong clientId)
        {
            if (!_turnStations.ContainsKey(clientId))
            {
                _turnStations.Add(clientId, new Station());
            }

            return _turnStations[clientId];
        }

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

        public void SetSelectedDomino(int? dominoId)
        {
            SelectedDomino = dominoId;
        }

        public void AddPlayerDomino(ulong clientId, int dominoId)
        {
            playerDominoes.AddDomino(clientId, dominoId);
        }

        public DominoEntity GetDominoFromBonePile()
        {
            int randomDominoIndex = UnityEngine.Random.Range(0, availableDominoes.Count); // TODO: could do a single shuffle instead (better performance)
            int dominoID = availableDominoes[randomDominoIndex];
            var domino = AllDominoes[dominoID];

            availableDominoes.RemoveAt(randomDominoIndex);
            return domino;
        }
        
        public int[] GetDominoesFromTurnStations()
        {
            List<int> newDominoIds = new List<int>();

            foreach (Station station in _turnStations.Values)
            {
                foreach (Track track in station.Tracks)
                {
                    newDominoIds.AddRange(track.DominoIds);
                }
            }
            return newDominoIds.ToArray();
        }

        public DominoEntity GetNextEngineAndCreateStation()
        {
            var engine = AllDominoes[engineIndices[++engineIndex]];
            availableDominoes.Remove(engine.ID);    // no longer available to pick up

            Station = new Station();

            return engine;
        }

        /// <summary>
        /// Move a domino from the player to the station
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="dominoId"></param>
        public void PlayDomino(ulong clientId, int dominoId, int trackIndex)
        {
            playerDominoes.RemoveDomino(clientId, dominoId);

            Station playerTurnStation = GetTurnStationByClientId(clientId);

            if(trackIndex >= playerTurnStation.Tracks.Count)
            {
                playerTurnStation.AddTrack(dominoId);
            }
            else
            {
                playerTurnStation.AddDominoToTrack(dominoId, trackIndex);
            }
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

        public void UpdateStationToPlayerTurnStation(ulong clientId)
        {
            // TODO: keep an eye out regarding whether I should clone each track or if reference works here
            Station = _turnStations[clientId];
        }

        public void MergeTurnTracksIntoStation()
        {
            int addedTracks = 0;
            
            // adds each TurnStation track to the main Station
            for (int stationIndex = 0; stationIndex < _turnStations.Count; stationIndex++)
            {
                var turnStations = _turnStations.Values.ToList();
                for (int trackIndex = 0; trackIndex < turnStations[stationIndex].TrackCount(); trackIndex++)
                {
                    for (int dominoIndex = 0; dominoIndex < turnStations[stationIndex].Tracks[trackIndex].DominoIds.Count; dominoIndex++)
                    {
                        if (dominoIndex == 0)
                        {
                            // add the track before adding the first domino to it
                            Station.AddTrack(turnStations[stationIndex].Tracks[trackIndex].DominoIds[dominoIndex]);
                            addedTracks++;
                            continue;
                        }
                        Station.AddDominoToTrack(turnStations[stationIndex].Tracks[trackIndex].DominoIds[dominoIndex], addedTracks - 1);
                    }
                }
            }
        }
    }
}
