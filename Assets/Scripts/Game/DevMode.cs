using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Game
{
    public class DevMode : MonoBehaviour
    {
        //[SerializeField] public bool FullLogging = true;
        [Range(1, 12)]
        [SerializeField] public int StartAtRound = 1;
        [FormerlySerializedAs("NumberOfDominoes")]
        [Range(1, 12)]
        [SerializeField] public int DominoStartCount = 12;
    }
}
