using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/SoundCollection", fileName = "SoundCollection")]
public class SoundCollection : ScriptableObject
{
    public List<AudioClip> Sounds;
}
