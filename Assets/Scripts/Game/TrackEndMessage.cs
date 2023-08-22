using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TrackEndMessage : MonoBehaviour
{
    [SerializeField] private TextMeshPro trackEndText;

    public void SetText(string text) => trackEndText.SetText(text);
    public string GetText() => trackEndText.text;
}
