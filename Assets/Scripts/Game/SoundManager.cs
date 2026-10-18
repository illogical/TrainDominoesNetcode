using System;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Game
{
    /// <summary>
    /// Owns game audio playback. Testing is silent: Editor Play Mode (unless overridden in the Inspector),
    /// batchmode players (the CLI integration harness) and any player started with <see cref="MuteArgument"/>.
    /// Muting only suppresses presentation audio; selection, input and rules are unaffected.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        /// <summary>Command-line flag that mutes a player build, for manual testing of standalone builds.</summary>
        public const string MuteArgument = "--mute-audio";

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SoundCollection clickSounds;
        [Tooltip("Editor Play Mode is silent by default for testing. Enable to hear game sound in the Editor.")]
        [SerializeField] private bool playSoundInEditor;

        /// <summary>True when this session suppresses all game audio.</summary>
        public bool IsMuted => IsTestingSession(Application.isEditor, Application.isBatchMode, playSoundInEditor, Environment.GetCommandLineArgs());

        private float restoreVolume = 1f;
        private bool listenerMuted;

        /// <summary>The mute policy, separated from Unity state so it can be reasoned about and tested directly.</summary>
        public static bool IsTestingSession(bool isEditor, bool isBatchMode, bool playSoundInEditor, string[] arguments)
        {
            if (arguments != null && arguments.Contains(MuteArgument)) return true;
            if (isBatchMode) return true;
            return isEditor && !playSoundInEditor;
        }

        private void Awake()
        {
            if (!IsMuted) return;
            // Silence the listener too, so any future sound path is covered by the same policy.
            restoreVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            listenerMuted = true;
        }

        private void OnDestroy()
        {
            // The listener volume outlives the scene in the Editor; restore it when Play Mode ends.
            if (listenerMuted) AudioListener.volume = restoreVolume;
        }

        public void PlayRandomClickSound()
        {
            if (IsMuted) return;
            if (clickSounds == null || clickSounds.Sounds == null || !clickSounds.Sounds.Any())
            {
                Debug.LogError("No click sounds are available.");
                return;
            }

            int index = UnityEngine.Random.Range(0, clickSounds.Sounds.Count); // Max is exclusive for ints, so every clip can play.
            audioSource.clip = clickSounds.Sounds[index];
            audioSource.Play();
        }
    }
}
