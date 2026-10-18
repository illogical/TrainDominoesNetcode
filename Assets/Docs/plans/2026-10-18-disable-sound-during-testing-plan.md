---
artifact_contract: ce-unified-plan/v1
artifact_readiness: implemented
product_contract_source: ce-plan-bootstrap
execution: code
title: Mute Game Audio During Editor Testing - Plan
type: feat
date: 2026-10-18
---

# Mute Game Audio During Editor Testing - Plan

## Implementation outcome (2026-10-18)

Implemented in `SoundManager`. The plan's assumption that CLI `-batchmode -nographics` players are inaudible was wrong: the user heard click sounds during harness runs on macOS. The policy (`SoundManager.IsTestingSession`) was therefore widened:

- **Muted:** Editor Play Mode by default (the Inspector field `playSoundInEditor` overrides this), every batchmode player, and any player started with `--mute-audio`.
- **Audible:** normal player builds started without the flag.
- **Listener muted too:** when muted, `AudioListener.volume` is set to 0, so future sound paths follow the same policy. The previous volume is restored on destroy, so Editor Play Mode does not leave the listener silent.
- **No scene edit:** the new serialized field defaults to off, which already gives the intended Editor behavior.
- **Small fix:** `Random.Range(0, Count - 1)` never chose the last clip; it now uses `Count`.

Verification:

- `Tests/UnityIntegration/TestingAudioBatch.cs` passes 7/7 policy checks in a Unity batch Editor.
- All four CLI player scenarios assert `audio-muted-for-testing` (14 player logs), and the runners also pass `--mute-audio`.
- Not verified, because it needs audible or interactive checks the user asked to avoid: the audible click in a normal player build, and the Inspector override during real Editor Play Mode. Those paths are covered statically and by the policy matrix only.

## Goal Capsule

**Objective:** Unity Editor Play Mode testing runs without repetitive game sound, while normal player builds retain sound.

**Approach:** Gate the existing presentation audio at `SoundManager` with an Editor-only testing mute that does not change authoritative gameplay or the serialized audio assets.

## Product Contract

### Summary

The game currently plays a random click clip whenever local tile selection changes. Repeated selection during testing makes the sound disruptive. Add a predictable way for Editor testing sessions to run silently while preserving sound in player builds.

### Requirements

#### Testing behavior

- R1. Game audio is muted during Unity Editor Play Mode by default.
- R2. The existing audio path remains available in player builds without requiring a scene or asset migration.
- R3. Muting audio does not change selection, presentation, or authoritative rules behavior.
- R4. The mute can be overridden for a deliberate audio check during Editor Play Mode.

### Scope Boundaries

In scope: the `SoundManager` playback gate, its `GameScene` configuration, and focused verification of Editor and player-build behavior.

Out of scope: new sounds, volume controls, a player-facing settings screen, audio mixer work, and changes to click-selection frequency.

### Assumptions

“Testing sessions” means Unity Editor Play Mode. Standalone development builds remain audible unless a later requirement extends the mute policy.

## Planning Contract

### Key Technical Decisions

- KTD1. Put the mute gate in `SoundManager`, the existing owner of audio playback, so all callers follow one policy.
- KTD2. Use an Editor-only default mute with an Inspector override. This makes normal Editor testing silent and keeps player-build behavior independent of development scene configuration.
- KTD3. Keep the existing `PlayRandomClickSound` call and sound assets intact. Muting must suppress playback at the presentation boundary rather than changing gameplay events.

### Existing Patterns

`GameplayManager.SelectionChanged` is the current caller of `SoundManager.PlayRandomClickSound()`. `GameScene.unity` serializes the SoundManager, AudioSource, and click SoundCollection references. `SimpleMatchmaking.cs` already uses `UNITY_EDITOR` for Editor-specific behavior; follow the project's existing conditional-compilation style where needed.

### Risks and Dependencies

- Unity's Editor compilation and player compilation must both retain valid `SoundManager` code across the conditional boundary.
- The Inspector override must be clear enough that a tester can temporarily hear audio without editing code.
- The current source search found the click playback path. If implementation discovers additional playback calls, route them through the same manager policy.
- The mute policy does not cover standalone development builds; those builds can still play sound during testing.

## Implementation Units

### U1. Add the Editor testing mute to SoundManager

**Requirements:** R1, R2, R3, R4.

**Dependencies:** None.

**Files:** `Assets/Scripts/Game/SoundManager.cs`, `Assets/Scenes/GameScene.unity`.

**Approach:** Add an Inspector-visible setting for allowing sound during Editor Play Mode, defaulting to muted. Guard `PlayRandomClickSound` before reading the sound collection or starting the AudioSource. Keep the guard Editor-specific so player builds preserve current playback. Set and serialize the default on the SoundManager object in `GameScene.unity`; preserve its existing AudioSource and SoundCollection references.

**Test scenarios:**

- In Editor Play Mode with the default setting, select and reselect tiles; no click clip starts.
- In Editor Play Mode with the override enabled, select a tile; the existing click sound plays.
- In a player build, select a tile with the serialized default scene setting; the existing click sound plays.
- With sound muted and an empty or missing SoundCollection, selection remains functional and the muted path does not emit the existing missing-sound error.
- Confirm the Inspector setting survives scene save and reload, and existing serialized audio references remain assigned.

### U2. Verify the audio boundary in Unity

**Requirements:** R1–R4.

**Dependencies:** U1.

**Files:** `Tests/UnityIntegration/IntegrationRuntime.cs`, `Tests/UnityIntegration/README.md`, `Assets/Scripts/Game/SoundManager.cs`, `Assets/Scenes/GameScene.unity`.

**Approach:** Extend the existing temporary-player integration harness to exercise tile selection and confirm the serialized scene still starts its existing click sound in a player build. Verify the Editor-only mute and Inspector override with a focused Editor Play Mode smoke check. Do not add a separate Unity test assembly for this localized behavior.

**Test scenarios:**

- In Editor Play Mode with the default setting, repeated tile selection leaves the scene AudioSource stopped.
- In Editor Play Mode with the override enabled and one valid scene clip, tile selection starts playback.
- In the temporary-player integration harness, tile selection starts the existing scene click sound.
- Repeated selection remains responsive and does not change selection events or game state when playback is muted.

## Verification Contract

The implementing change should be checked in Unity Editor `6000.6.6f1`, pinned by `ProjectSettings/ProjectVersion.txt`. Compile the Editor and a player target, run the existing temporary-player integration harness with its updated selection/audio assertion, then perform the Editor Play Mode smoke scenarios in U1. Record whether the checks were static, Editor Play Mode, or player-build checks; a successful compile alone does not prove playback behavior.

Do not change installed package versions or unrelated scene settings. Preserve the SoundManager's serialized AudioSource and click SoundCollection references.

## Definition of Done

- Editor Play Mode is silent by default and can be deliberately unmuted in the Inspector.
- Player builds retain the current click playback behavior.
- Muting does not alter game input, selection, or rules state.
- Focused tests and the described Editor/player checks pass, with their scope recorded.
- The scene retains valid audio references and no abandoned implementation experiments remain.
