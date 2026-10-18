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
python3 Tests/UnityIntegration/run_players.py --challenge
python3 Tests/UnityIntegration/run_players.py --challenge --penalty-none
python3 Tests/UnityIntegration/run_players.py --reconnect
```

`--reconnect` (NET-004): after three actions, the client submits a move and drops its connection before the answer arrives. It reconnects through the Retry action under a new transport ID and must reclaim the same seat with a rotated secret. A forged claim for the other seat is sent over a real RPC and must be refused. The host must report the missing seat, then everyone back, and resume. The match then finishes.

`--live` (NET-003) connects through the real Unity Lobby + Relay path (`SimpleMatchmaking`) with separate anonymous profiles (`--ugs-profile td-host` and `td-client`). **It uses network services: run it only with the user's permission.**

The two `--challenge` modes (NET-005) play complete Immediate matches without announcing. The player who sees an open window challenges, after first ignoring one window to prove the next accepted action closes it. They check self and repeated challenge rejections, penalty application and allowance, and private penalty delivery. Every mode checks privacy invariants on each received view.

The builder writes `/tmp/td-integration-build/Integration.app`; the runner starts two actual player processes over `127.0.0.1:17898`. Do not run the two modes concurrently. The runner clears its own coordination files, applies a bounded deadline, terminates remaining child processes on failure, and writes filtered evidence logs only after both players pass. Unity requires access to its local Package Manager Unix socket and localhost UDP sockets, which may require sandbox escalation.

## Assertions and limits

The assertions cover recipient projection startup, profile-sized hands, three train identities, accepted commands, final selected-placement positions, identical-request replay, stale command rejection without mutation, thirteen-round results/winners, fresh-match score reset, and remote disconnect input suspension. Draft additionally exercises undo and end-turn through the same client APIs and real transport. Automated selection calls the production ClientSession API; pointer hit testing is not exercised. Initial Ready uses the actual generated Button.onClick listener, while tile/train actions use the production ClientSession selection API.

A complete smoke run is not exhaustive adapter coverage. Packet loss, uncertain delivery retry, actual Lobby/Relay, joint-win fixtures, detailed reconnect/resume, synthetic presentation failure, and visual polish require separate verification. No claim of those cases follows from passing this harness. Deterministic legal play chooses the first permitted placement, then draw/pass; it does not use development legality bypasses.

The eight-player harness runs on port 17899. It checks nine trains and nine tiles per hand at all eight recipients, a host action, participant disconnect suspension, disabled input, and a replacement match with the seven remaining seats. Readiness and normal commands may receive safe StalePosition rejections when participants act concurrently; the harness retries from the latest view after acknowledgment.

Valid challenge penalties and explicit private-draft information-leak assertions over real serialized transport are implemented but unverified by these smoke scenarios; the source-linked boundary suite provides separate coverage. Both supported penalty settings (DrawOneTile/None) are not both exercised here.

## Silent testing

Batchmode players are muted by `SoundManager` (a batchmode macOS player otherwise still plays sound), and the runners also pass `--mute-audio`. Every scenario asserts `audio-muted-for-testing`. To check the mute policy itself, copy `TestingAudioBatch.cs` into the temporary project's `Assets/Editor` and run `-executeMethod TestingAudioBatch.Run`.

## Wire contract batch check (NET-002)

`WireContractBatch.cs` runs in the Editor without a player build. Copy it with `Tests/GameplayIntegration/WorstCaseMatch.cs` into the temporary project's `Assets/Editor`, then run:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /tmp/td-legacy-baseline -executeMethod WireContractBatch.Run -wireOut /tmp/td-wire-contract -logFile /tmp/td-wire-contract.log
```

It exits non-zero on failure. It writes `wire-contract-report.txt`, which records the raw `JsonUtility` behavior, the codec assertions and measured sizes from 20 seeded matches each at 2, 4 and 8 players. It also writes `Fixtures/*.json`. Copy the fixtures to `Tests/GameplayIntegration/Fixtures/` when the payload shape changes; the console suite decodes them. The last report is `observed-wire-contract.txt`.

The focused `--presentation-only` mode performs regular real host/client startup and Ready, then runs a synthetic recipient-view regression: a tile visible on one train at revision 10 is moved to another train at revision 12. It checks generation invalidation and the final mesh position. Its logs and `production-source-gap.sha256` are separate from the earlier complete lifecycle runs.
