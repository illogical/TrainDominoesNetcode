# Actual Unity CLI integration checks

These are temporary-player integration checks, separate from the source-linked .NET suites. Do not copy the runtime harness into the working project's Assets: it replaces the authority with a reproducible deck and automatically plays a match.

The harness runs the actual GameScene, MonoBehaviours, Netcode-generated RPCs, UnityTransport localhost sockets, JsonUtility projections, state coordinator, mesh presenter, and animation coroutines. It disables SimpleMatchmaking before Start; no Lobby/Relay service is used. Both players first complete setup and the host's first selected placement at normal animation speed. Later lifecycle work uses `Time.timeScale=50`, which is explicitly logged. This verifies completion and final transforms, not perceived visual quality.

## Reproduce

Use Unity **6000.6.6f1** and an isolated copy of the project's Assets, Packages, and ProjectSettings. The observed run reused `/tmp/td-legacy-baseline` from the earlier baseline; its production scripts were replaced using `rsync --delete`, and both legacy harness scripts were removed. Reusing its Library cache avoids importing the whole project again. Main-project editor locks do not affect this isolated project.

1. Copy current `Assets/Scripts` plus their metadata into temporary `Assets/Scripts`, deleting obsolete sources there.
2. Copy `IntegrationRuntime.cs` into temporary Assets and `IntegrationBuild.cs` into temporary Assets/Editor.
3. Build and run:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath /tmp/td-legacy-baseline -executeMethod IntegrationBuild.Build -logFile /tmp/td-integration-build.log
python3 Tests/UnityIntegration/run_players.py
python3 Tests/UnityIntegration/run_players.py --draft
python3 Tests/UnityIntegration/run_eight.py
python3 Tests/UnityIntegration/run_players.py --presentation-only
```

The builder writes `/tmp/td-integration-build/Integration.app`; the runner starts two actual player processes over `127.0.0.1:17898`. Do not run the two modes concurrently. The runner clears its own coordination files, applies a bounded deadline, terminates remaining child processes on failure, and writes filtered evidence logs only after both players pass. Unity requires access to its local Package Manager Unix socket and localhost UDP sockets, which may require sandbox escalation.

## Assertions and limits

The assertions cover recipient projection startup, profile-sized hands, three train identities, accepted commands, final selected-placement positions, identical-request replay, stale command rejection without mutation, thirteen-round results/winners, fresh-match score reset, and remote disconnect input suspension. Draft additionally exercises undo and end-turn through the same client APIs and real transport. Automated selection calls the production ClientSession API; pointer hit testing is not exercised. Initial Ready uses the actual generated Button.onClick listener, while tile/train actions use the production ClientSession selection API.

A complete smoke run is not exhaustive adapter coverage. Packet loss, uncertain delivery retry, actual Lobby/Relay, joint-win fixtures, detailed reconnect/resume, synthetic presentation failure, and visual polish require separate verification. No claim of those cases follows from passing this harness. Deterministic legal play chooses the first permitted placement, then draw/pass; it does not use development legality bypasses.

The eight-player harness runs on port 17899. It checks nine trains and nine tiles per hand at all eight recipients, a host action, participant disconnect suspension, disabled input, and a replacement match with the seven remaining seats. Readiness and normal commands may receive safe StalePosition rejections when participants act concurrently; the harness retries from the latest view after acknowledgment.

Valid challenge penalties and explicit private-draft information-leak assertions over real serialized transport are implemented but unverified by these smoke scenarios; the source-linked boundary suite provides separate coverage. Both supported penalty settings (DrawOneTile/None) are not both exercised here.

The focused `--presentation-only` mode performs regular real host/client startup and Ready, then runs a synthetic recipient-view regression: a tile visible on one train at revision 10 is moved to another train at revision 12. It checks generation invalidation and the final mesh position. Its logs and `production-source-gap.sha256` are separate from the earlier complete lifecycle runs.
