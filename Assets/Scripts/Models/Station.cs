using System.Collections.Generic;


namespace Assets.Scripts.Models
{
    public class Station
    {
        // a station has 8 tracks
        public List<Track> Tracks = new List<Track>(8);  // tracks by track number

        public DominoEntity Engine { get; private set; }

        public Station(DominoEntity engine)
        {
            Engine = engine;
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

        public Track GetTrackByIndex(int trackIndex)
        {
            return Tracks[trackIndex];
        }

        public int? GetTrackIndexByDominoId(int dominoId)
        {
            foreach(var track in Tracks)
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
    }
}
