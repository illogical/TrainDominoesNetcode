using TMPro;
using UnityEngine;

namespace Assets.Scripts.Game
{
    public class TrackEndMessage : MonoBehaviour
    {
        [SerializeField] private TextMeshPro trackEndText;

        public void SetText(string text) => trackEndText.SetText(text);
        public string GetText() => trackEndText.text;
    }
}
