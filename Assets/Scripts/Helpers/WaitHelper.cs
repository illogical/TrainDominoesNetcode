using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Helpers
{
    internal class WaitHelper
    {
        private Dictionary<float, WaitForSeconds> _waits;

        public WaitHelper()
        {
            _waits = new Dictionary<float, WaitForSeconds>();
        }

        public WaitForSeconds GetWait(float seconds)
        {
            if (!_waits.ContainsKey(seconds))
                _waits.Add(seconds, new WaitForSeconds(seconds));
            
            return _waits[seconds];
        }
    }
}