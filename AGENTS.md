# Coding assistance for TrainDominoesNetcode

## Start here

Read [PROJECT_STATE.md](Assets/Docs/PROJECT_STATE.md) for the dated implementation baseline and [TASKS.md](TASKS.md) for priorities and completion criteria. Recheck source and scene settings before relying on the baseline. The project is a Unity multiplayer train-dominoes prototype targeting 2–8 players with a double-twelve set.

The user selected the [Puremco rulebook baseline](Assets/Docs/TRAIN_DOMINOES_RULEBOOK.md). Read its edition/decision register, the [player-state mapping](Assets/Docs/PLAYER_STATE_REVIEW.md), and the [rules-engine plan](Assets/Docs/RULES_ENGINE_IMPLEMENTATION_PLAN.md). The plan is not implemented. The latest Puremco instruction supersedes earlier personal-only, simultaneous-opening, RNK-deal-table, and strict-cover proposals. Use one personal train per player plus exactly one shared Mexican Train, sequential play from the first turn, and Puremco's extra-play/two-double rules.

“Player states” means `Assets/Scripts/Game/States`. They currently coordinate local input/events/UI; server-side rule decisions remain spread across `GameSession` and managers. Do not confuse local interaction states with authoritative game conditions.

## Rules and state work

- Start with STATE-001. The first matrix exists in `Assets/Docs/PLAYER_STATE_REVIEW.md`; review it and the rulebook's proposed application policies before implementing state changes. Baseline selection does not imply every app-policy detail has been reviewed.
- Keep known implementation behavior, intended rules, and proposals distinct. Do not assume generic train-dominoes rules apply without checking the agreed variant.
- Keep the server authoritative. UI button availability is not rule validation; validate acting player, action legality, and repeated/stale commands at the authoritative boundary.
- Follow the rules-engine plan's source-based assembly boundary and external test project using the same core sources. Planned targets are .NET Standard 2.1 / supported C# 9 for core and .NET 10 / NUnit for external tests; no projects or harness exist yet. Avoid manually copied DLLs and duplicated rule implementations.
- Keep Unity, Netcode, input, rendering, animation, and transport serialization outside the rules core. Make randomness controllable for reproducible tests. Extract in small tested slices, preserving IDs and payload compatibility unless migration is explicitly in scope.
- Balance state event subscriptions on entry/exit. Review group-turn, individual-turn, undo, and reset behavior together when changing shared state infrastructure.

## Unity and repository care

- Use the editor version in `ProjectSettings/ProjectVersion.txt` and installed package versions in `Packages/manifest.json`; do not upgrade packages incidentally.
- Preserve asset GUIDs, `.meta` files, serialized field references, and scene/prefab wiring. Use Unity-aware moves for assets; do not regenerate metadata for existing assets.
- Avoid generated project/cache edits (`Library`, `Temp`, `Logs`, generated `.csproj`/solution files) and third-party source edits unless explicitly required by the task. Preserve unrelated user changes.
- The inspected scene enables `IgnoreDominoComparisons` and starts at round 2. Recheck settings before verification; development bypasses are not intended rules. Do not silently change saved settings merely to make a test pass.
- Final four-color domino asset integration is late priority after rule/state work. Preserve the user's art and keep presentation ownership/color separate from rule decisions.

## Verification and handoff

Use focused core unit tests for rule behavior and Unity/host-client checks for adapters and transitions once those harnesses exist. No project-owned automated test suite was found in the initial inspection; do not invent a working command or infer correctness from the installed Test Framework package.

Report commands/checks actually performed, their outcomes, and untested scenarios. Distinguish static inspection, local play verification, and live-service verification. Update TASKS.md only when completion criteria are met, and update the overview/state matrix when behavior changes. Keep documentation-only tasks free of gameplay, scene, asset, and package changes.
