# Project tasks

Start here: [project state and player-state inventory](Assets/Docs/PROJECT_STATE.md). Follow [coding guidance](AGENTS.md).

Use stable IDs when discussing work. Check a task only when its completion criteria are met; record evidence or a linked decision/test report beside it. Completed documentation does not mean the documented gameplay has been verified. Priorities below are intentional: agree on rules and states before adding more states, and integrate final art late.

## Completed baseline

- [x] **DOC-001 — Record project purpose, architecture, player states, gaps, and development history.** Evidence: `Assets/Docs/PROJECT_STATE.md`; static source/scene/history inspection on 2026-10-17, not runtime verification.
- [x] **DOC-002 — Establish coding guidance and a prioritized checklist.** Evidence: `AGENTS.md` and this file. Only documentation was created.

## Priority 1: fresh-session player-state and rules review

- [ ] **STATE-001 — Agree on the rules and existing-versus-required state matrix.** Depends on DOC-001. Read the overview, all player states, and corresponding GameSession RPC paths. Confirm the intended rules variant and player limits; cover dealing/readiness, group construction and submission, normal turns, selection, draw/pass, doubles, train access, undo/commit, round endings, scoring/ties, and restart/disconnect behavior. For each phase record authoritative world conditions, allowed actions, entry/exit triggers, local versus server responsibilities, current coverage, and required changes. **Done when:** the matrix and open-rule decisions are saved in `Assets/Docs/PLAYER_STATE_REVIEW.md`, the user agrees to the proposed behavior, and each phase is classified as existing state, needed state, rule data, or derived UI condition. Do not add states during this task.
- [ ] **STATE-002 — Establish the runtime baseline for the agreed review.** Depends on STATE-001. Use an appropriate local host/client setup and explicitly record development settings, including matching bypass and starting round. Cover startup/deal, group submission, individual placement/undo/draw/end, scores, and next-round entry. **Done when:** a reproducible checklist records passes, failures, and untested scenarios, with regressions separated from intended rule changes. Document required setup; live Lobby/Relay testing is not a prerequisite if local transport covers the case.

Fresh-session starting prompt:

> Read AGENTS.md, TASKS.md, and Assets/Docs/PROJECT_STATE.md. Work on STATE-001 only. Inspect Assets/Scripts/Game/States and the corresponding RPC/manager logic. Help establish the intended rules, then document existing and required phases with allowed actions, expected world conditions, transitions, and authority. Do not implement new states or extract rules until the matrix is agreed. Identify which conditions need states and which belong in rule data or UI permissions.

## Priority 2: rules isolation and tests

- [ ] **RULES-001 — Decide and document the pure C# assembly/test boundary.** Depends on STATE-001. Prefer source-based Unity `.asmdef` integration and an external test project referencing the same sources, without copied DLLs or duplicated implementations. Inspect Unity compatibility settings before selecting the target framework, language level, and test framework. Define command/result boundaries, identity mapping, deterministic randomness, and the adapter's responsibilities. **Done when:** a saved architecture decision names the source/test locations, dependency direction, supported targets, runnable test approach, and first extraction slice; the core has no Unity/Netcode/UI/animation dependencies by design.
- [ ] **RULES-002 — Add focused characterization tests and a runnable harness.** Depends on RULES-001 and STATE-002. Start with the selected extraction slice, using current behavior evidence; label known defects and intended changes explicitly. Keep Unity-specific adapter checks in Unity tests where necessary. **Done when:** documented commands run real assertions for the slice, test setup is reproducible, and tests distinguish legacy behavior from agreed rule requirements.
- [ ] **RULES-003 — Extract the first rules slice and connect its server adapter.** Depends on RULES-002. Move a bounded piece of logic without a wholesale rewrite. Keep server authority, stable identity, and current payload compatibility; isolate random dealing behind a controllable boundary where relevant. **Done when:** external core tests and relevant Unity adapter checks pass, with evidence that presentation and networking consume the core rather than duplicate the extracted rules.
- [ ] **RULES-004 — Extract remaining agreed rules incrementally.** Depends on RULES-003. Create smaller linked tasks for placement/orientation, train access, turn/draw obligations, provisional moves/undo/commit, round progression, and scoring as needed by the approved matrix. **Done when:** approved rule behavior has independent tests and a single authoritative implementation; known deviations are resolved or explicitly tracked.

## Priority 3: complete and refactor player behavior

- [ ] **STATE-003 — Implement the approved state changes against rule outcomes.** Depends on STATE-001 and the relevant RULES-003/004 slices. Create linked tasks per approved change. Cover group-versus-normal flow, drawn/selected conditions, pending/rejected actions, doubles where applicable, undo, and submission locks through the agreed state/data split. **Done when:** the matrix matches implementation, allowed actions derive from authoritative conditions, enter/leave subscriptions remain balanced, and focused state/adapter tests plus local smoke checks cover changed transitions.
- [ ] **STATE-004 — Complete round and game lifecycle coordination.** Depends on STATE-003 and relevant rules slices. Address readiness deduplication, round resets, engine progression, scores/ties, complete new-game reset, and agreed disconnect behavior. **Done when:** host and clients agree after round/game transitions and repeated readiness/restart actions cannot advance lifecycle twice.

## Priority 4: multiplayer validation and follow-up maintenance

- [ ] **NET-001 — Validate authoritative commands and client consistency.** Depends on STATE-003 and STATE-004. Cover invalid/out-of-turn requests, repeated/stale draw or end-turn requests, invalid IDs, unauthorized undo, empty boneyard, group merge/capacity, and rejected-command UI recovery. Include multiple players and host-as-player behavior. **Done when:** tests/checklists prove rejected commands preserve rule state and accepted commands converge across peers; no remaining acceptance relies only on disabled buttons.
- [ ] **NET-002 — Plan and replace BinaryFormatter payload serialization.** Depends on RULES-001; schedule separately from extraction. Inventory DTO/JSON paths and choose explicit payload contracts and compatibility/migration behavior. **Done when:** replacement serialization round-trips required payloads, malformed inputs are covered, and relevant host/client flows pass without the old formatter path.
- [ ] **NET-003 — Review networking compatibility and startup paths after the Unity upgrade.** Depends on STATE-002; final regression coverage follows NET-001/002. Review current RPC usage, local transport, scene-enabled matchmaking, Relay setup, authentication, and failure/shutdown behavior against installed packages. **Done when:** supported entry paths and setup are documented, relevant scenarios are verified, and unsupported/legacy paths have an explicit disposition. Record local and live-service verification separately.

## Late priority: integrate final domino assets

- [ ] **ART-001 — Integrate four colored player domino sets.** Depends on RULES-004, STATE-004, NET-001, and availability of the user's assets. Replace placeholder models and labels as appropriate, preserving domino values/identity, player ownership/color, orientation, selection/click targeting, hand/track layout, and animations. **Done when:** all four sets display and behave correctly in relevant multiplayer views, including placement, flip, undo, and round reset; art does not change rule behavior or imply a new player limit.

## Maintaining this checklist

Keep completed IDs stable. Add small linked tasks when implementation reveals distinct work; do not silently expand an existing task. Update the project overview/state matrix when behavior changes, and attach actual validation evidence before checking completion boxes. Framework and rules choices above are pending tasks, not decisions already implemented.
