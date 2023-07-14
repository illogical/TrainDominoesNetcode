using System.Collections.Generic;
using System.Numerics;

namespace Assets.Scripts.Models
{
    public class TrackPositions
    {
        private Dictionary<int, Vector3> _trackPositions;

        public TrackPositions()
        {
            _trackPositions = new Dictionary<int, Vector3>();
        }
    }
}
