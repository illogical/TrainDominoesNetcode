using System.Collections.Generic;
using Assets.Scripts.Game;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the testing-audio mute policy without Play Mode. Copy into a temporary project's Assets/Editor.
/// Run: Unity -batchmode -nographics -projectPath &lt;temp&gt; -executeMethod TestingAudioBatch.Run -logFile &lt;log&gt;
/// </summary>
public static class TestingAudioBatch
{
    public static void Run()
    {
        var failures = new List<string>();
        void Check(bool condition, string name) { Debug.Log((condition ? "[TestingAudio] PASS " : "[TestingAudio] FAIL ") + name); if (!condition) failures.Add(name); }
        string[] none = new string[0], flag = { "Player", SoundManager.MuteArgument };

        Check(SoundManager.IsTestingSession(true, false, false, none), "Editor Play Mode is muted by default");
        Check(!SoundManager.IsTestingSession(true, false, true, none), "Inspector override lets the Editor play sound");
        Check(SoundManager.IsTestingSession(true, false, true, flag), "--mute-audio wins over the Inspector override");
        Check(SoundManager.IsTestingSession(false, true, false, none), "batchmode players (CLI harness) are muted");
        Check(!SoundManager.IsTestingSession(false, false, false, none), "normal player builds keep sound");
        Check(SoundManager.IsTestingSession(false, false, false, flag), "--mute-audio mutes a normal player build");

        // A real component in this (batchmode Editor) session reports muted and is safe to call.
        var probe = new GameObject("audio-probe").AddComponent<SoundManager>();
        Check(probe.IsMuted, "a SoundManager in this batch Editor session is muted");
        probe.PlayRandomClickSound(); // Muted path returns before touching the (missing) clips: no error, no exception.
        Object.DestroyImmediate(probe.gameObject);

        Debug.Log($"[TestingAudio] {6 + 1 - failures.Count}/7 passed");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }
}
