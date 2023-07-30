using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Game
{
    public class SoundManager : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SoundCollection clickSounds;

        public void PlayRandomSound()
        {
            int index = Random.Range(0, clickSounds.Sounds.Count); // Get a random index
            audioSource.clip = clickSounds.Sounds[index]; // Set the clip to the random sound
            audioSource.Play(); // Play the sound
        }
    }
}