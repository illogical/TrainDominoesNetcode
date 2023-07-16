using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Assets.Scripts.Models
{
    public class Station
    {
        // a station has 8 tracks
        public List<Track> Tracks = new List<Track>(8); // tracks by track number

        public Station()
        {
        }

        /// <summary>
        /// Adds the first domino to a new track
        /// </summary>
        /// <param name="dominoId"></param>
        public Track AddTrack(int dominoId)
        {
            var track = new Track(dominoId);
            Tracks.Add(track);

            return track;
        }

        public int TrackCount()
        {
            return Tracks.Count;
        }

        /// <summary>
        /// Adds a domino to an existing track
        /// </summary>
        /// <param name="dominoId"></param>
        /// <param name="trackIndex"></param>
        public Track AddDominoToTrack(int dominoId, int trackIndex)
        {
            Tracks[trackIndex].AddDominoToTrack(dominoId);

            return Tracks[trackIndex];
        }

        [CanBeNull]
        public Track GetTrackByIndex(int trackIndex)
        {
            if (trackIndex >= Tracks.Count)
            {
                return null;
            }
            
            return Tracks[trackIndex];
        }

        public int? GetTrackIndexByDominoId(int dominoId)
        {
            foreach (var track in Tracks)
            {
                if (track.ContainsDomino(dominoId))
                {
                    return Tracks.IndexOf(track);
                }
            }

            return null;
        }

        public Track GetTrackByNetId(ulong clientId)
        {
            foreach (var track in Tracks)
            {
                if (track.PlayerId == clientId)
                {
                    return track;
                }
            }

            return null;
        }

        public List<List<int>> GetDominoIdByTracks()
        {
            var tracks = new List<List<int>>();

            foreach (var track in Tracks)
            {
                tracks.Add(track.DominoIds);
            }

            return tracks;
        }

        public int? GetTrackByDominoId(int dominoId)
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i].DominoIds.Contains(dominoId))
                {
                    return i;
                }
            }

            return null;
        }

        public int[] GetNewDominoesByComparingToStation(Station updatedStation)
        {
            List<int> newlyAddedDominoes = new List<int>();

            for (int i = 0; i < updatedStation.Tracks.Count; i++)
            {
                Track localCurrentTrack = this.GetTrackByIndex(i);
                Track updatedTrack = updatedStation.GetTrackByIndex(i);

                if (localCurrentTrack == null)
                {
                    // this is a new track
                    newlyAddedDominoes.AddRange(updatedTrack.DominoIds);
                }
                else if (localCurrentTrack.DominoIds.Count != updatedTrack.DominoIds.Count)
                {
                    // the track counts differ. Find which dominoes are unaccounted for.
                    int addedDominoCount = updatedTrack.DominoIds.Count - localCurrentTrack.DominoIds.Count;
                    List<int> endDominoes = updatedTrack.DominoIds.GetRange(localCurrentTrack.DominoIds.Count,
                        addedDominoCount);
                    newlyAddedDominoes.AddRange(endDominoes);
                    // for (int j = 0; j < updatedTrack.DominoIds.Count; j++)
                    // {
                    //     if (localCurrentTrack.DominoIds.Count >= j)
                    //     {
                    //         
                    //     }
                    // }
                }
            }

            return newlyAddedDominoes.ToArray();
        }
    }
}