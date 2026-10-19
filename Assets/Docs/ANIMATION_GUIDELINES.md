# Domino animation guidelines

User direction, 2026-10-18: give grouped domino movement some life by considering slight differences in start times and/or arrival times. The hand zoom/reflow now uses an ordered wave. Consider this explicitly when adding future animations; it is not a request to replace all existing motion in one refactor.

## Timing and visual intent

- For a group of tiles, consider a spatially coherent stagger: left-to-right, center-outward, or another order that suits the action. Avoid making every tile start, scale and finish identically by default.
- Bound the **whole group's** timing spread. A 120 ms hand wave remains 120 ms for 2, 16 or 60 tiles; it is not 120 ms multiplied by the tile count.
- Start staggering, arrival staggering and modest duration variation are choices to evaluate per animation. Use predictable, seedable variation when randomness helps; never use unrelated timing jitter as a substitute for choreography.
- Keep easing monotonic where overshoot could make bodies cross. Preserve exact final poses, IDs, ordering, selection and readability. Decorative scale inflation and rotation wobble need their own justification and clearance.
- Reduced motion removes the hand wave and bounce and uses the existing short slide. Future effects must define an equivalent reduced-motion path.

## Ownership and separation

- One owner writes each transform at a time. Capture actual visible position/scale when retargeting; authority, cancellation, scene/round replacement and direct manipulation take priority over a cosmetic job.
- Remove each tile's own temporary visual offsets on cancellation. A staggered cohort may have different current positions/scales; do not assume every member has the same phase.
- Independent phases invalidate the old shared-phase separation argument. Check actual projected body footprints throughout the proposed trajectories, including scale/depth, selected lift, visible clipping and nearby resting geometry relevant to the action.
- Reduce the timing spread or choose a coordinated path when necessary. If only synchronized motion is safe, use it and record why. Already-invalid starting geometry may require an explicitly recorded recovery snap.
- Animation duration/completion must never decide legality, turn advancement, network freshness, challenge deadlines or command acceptance. Keep cosmetic time unscaled where appropriate and preserve permitted controls while motion runs.

## Review and evidence

Capture normal-speed clips and compare departure/arrival timing, separation and final poses. Numerical/preflight samples and rendered-frame checks are useful diagnostics, not a continuous-time collision proof or user aesthetic acceptance. Verify interruption cleanup and input/raycast behavior separately. Recheck final art and real devices when available.

Current hand implementation: `LayoutManager.HandResizeWaveSeconds` defaults to **120 ms** total and is capped at **200 ms**. `HandReflowSchedule.WaveDelay` distributes this over the stable hand order. The controller applies each tile's eased phase to movement, zoom and settling; projected-body preflight can shorten or disable the spread. [Current implementation evidence](../../Tests/UnityIntegration/HAND_WAVE_VERIFICATION.md).
