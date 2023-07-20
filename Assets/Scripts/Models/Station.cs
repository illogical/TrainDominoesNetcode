using System;
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

        public Track AddTrackClone(Track track)
        {
            Track newTrack = new Track(track.PlayerId, track.HasTrain);
            newTrack.DominoIds.AddRange(track.DominoIds);

            return newTrack;
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

        public Track GetTrackByDominoId(int dominoId)
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i].DominoIds.Contains(dominoId))
                {
                    return Tracks[i];
                }
            }

            return null;
        }

        public int[] GetNewDominoesByComparingToStation(Station updatedStation)
        {
            List<int> newlyAddedDominoes = new List<int>();

            // TODO: currently there is a bug where this will iterate over newly-added tracks (I think)
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
                    if (addedDominoCount < 0)
                    {
                        throw new Exception($"addedDominoCount is negative. Why?");
                    }
                    
                    List<int> endDominoes = updatedTrack.DominoIds.GetRange(localCurrentTrack.DominoIds.Count,
                        addedDominoCount);
                    newlyAddedDominoes.AddRange(endDominoes);
                }
            }

            return newlyAddedDominoes.ToArray();
        }

        // TODO: CloneTracks is redundant
        public List<Track> CloneTracks()
        {
            var clonedTracks = new List<Track>();
            for (int trackIndex = 0; trackIndex < TrackCount(); trackIndex++)
            {
                // add the first domino to a fresh track
                clonedTracks.Add(new Track(Tracks[trackIndex].DominoIds[0], Tracks[trackIndex].PlayerId, Tracks[trackIndex].HasTrain));
                for (int dominoIndex = 1; dominoIndex < Tracks[trackIndex].DominoIds.Count; dominoIndex++)
                {
                    clonedTracks[trackIndex].AddDominoToTrack(Tracks[trackIndex].DominoIds[dominoIndex]);
                }
            }
            return clonedTracks;
        }
    }
}