using Assets.Scripts.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
                Track newTrack = playerTurnStation.AddTrack(dominoId);
                if (Station.GetTrackByNetId(clientId) == null)
                {
                    // this is this player's first track
                    newTrack.PlayerId = clientId;
                }
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

        public ulong? CheckPlayerDominoesForWinner()
        {
            foreach (var playerDominoIds in playerDominoes.Dominoes)
            {
                if (playerDominoIds.Value.Count == 0)
                {
                    return playerDominoIds.Key;
                }
            }

            return null;
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
                    Station.AddTrackClone(turnStations[stationIndex].Tracks[trackIndex]);
                }
            }
        }

        /// <summary>
        /// Apply's a player's dominoes to the main station via adding a new track or adding to an existing track.
        /// </summary>
        /// <param name="trackClientId">ClientId of the player whose submitted dominoes are being added to the main station.</param>
        public void MergeTurnTrackIntoStation(ulong trackClientId)
        {
            var turnStation = _turnStations[trackClientId];
            // check if any dominoes had been added to any tracks. If so, add them to the main station's equivalent track
            for (int trackIndex = 0; trackIndex < Station.TrackCount(); trackIndex++)
            {
                Track turnStationTrack = turnStation.Tracks[trackIndex];
                Track currentLocalTrack = Station.Tracks[trackIndex];

                if (turnStationTrack.DominoIds.Count > currentLocalTrack.DominoIds.Count)
                {
                    int newDominoCount = turnStationTrack.DominoIds.Count -
                                         currentLocalTrack.DominoIds.Count;
                    Station.Tracks[trackIndex].DominoIds.AddRange(turnStationTrack.DominoIds.GetRange(currentLocalTrack.DominoIds.Count, newDominoCount));
                }
            }

            int addedTracksCount = turnStation.TrackCount() - Station.TrackCount(); // should never be more than 1 track added per turn
            if (addedTracksCount > 0)
            {
                // new track had been added
                if (addedTracksCount > 1)
                {
                    Debug.LogError("Unexpected error: More than 1 track is being added during a player's turn.");
                }

                Station.Tracks.AddRange(turnStation.Tracks.GetRange(Station.Tracks.Count(), addedTracksCount));
            }
        }

        /// <summary>
        /// Overwrites all other players' TurnStation tracks with a clone of the main station's tracks.
        /// </summary>
        public void SyncMainStationWithPlayerTurnStations()
        {
            foreach (ulong clientId in _turnStations.Keys)
            {
                _turnStations[clientId].Tracks = Station.CloneTracks();
            }
        }

        /// <summary>
        /// Used by the server to calculate the number of dots on all the remaining players' dominoes
        /// </summary>
        /// <returns>Score by player's clientId</returns>
        public Dictionary<ulong, int> SumPlayerScores()
        {
            var playerScores = new Dictionary<ulong, int>();

            foreach (var dominoesByPlayer in playerDominoes.Dominoes)
            {
                // each player
                int playerTotal = 0;
                ulong playerClientId = dominoesByPlayer.Key;
                
                for (int j = 0; j < dominoesByPlayer.Value.Count; j++)
                {
                    DominoEntity dominoInfo = GetDominoByID(dominoesByPlayer.Value[j]);
                    playerTotal += dominoInfo.TopScore + dominoInfo.BottomScore;
                }
                playerScores.Add(playerClientId, playerTotal);
            }

            return playerScores;
        }

        public void Reset()
        {
            SelectedDomino = null;
            Station = new Station();
            _turnStations.Clear();
            playerDominoes.Dominoes.Clear();
            
            AllDominoes.Clear();
            availableDominoes.Clear();
            engineIndices.Clear();
            CreateDominoSet();
        }
    }
}
