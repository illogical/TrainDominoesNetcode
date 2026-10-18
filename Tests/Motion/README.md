# Initial-deal timing checks

From `RulesEngine` (uses its pinned SDK):

```sh
dotnet run --project ../Tests/Motion/Motion.csproj
```

Links the actual production `AnimationHelper.cs` with minimal Unity type stubs. Checks deterministic schedules for 100 seeds and hand sizes 1–91, modest duration bounds, ordered departures, and no extension of the authored batch deadline. Checks null-curve smooth acceleration, monotonic travel and exact endpoints. Samples 16- and 91-tile parallel vertical deal paths at 240 Hz, including synthetic orthographic pan/zoom and rectangular footprints. Shared edges do not count as intersection; a 1e-6 world-unit tolerance covers single-precision edge roundoff.

`LayoutManager.DealMotionSeed` is a serialized presentation-only seed (default 1729). Deal duration varies from 92–100% of the authored duration; departure jitter is bounded to ±20% of one authored stagger and clamped inside the original batch. This does not use gameplay randomness or govern authoritative timing. Existing animation assets still supply their authored easing; absent curves use smoothstep.

These checks are numerical model evidence only: Unity coroutine scheduling, actual camera projection, rotated footprints, resting board tiles, draw/placement/undo paths, interruption and human visual acceptance remain separate checks. In particular, modest timing variation does not prevent general moving/resting collisions. No collision-free claim is made for the complete presentation.
