# Legacy runtime baseline harness

This harness belongs to the pre-integration source at `0eec4e56dd974d84a02d2b33f7aa198484f180e8`, not the integrated gameplay API. It uses the actual GameScene and GameSession RPCs in two macOS players over localhost UnityTransport. Never install its scripts in the working project's Assets.

The observed run is summarized in [the baseline report](../../Assets/Docs/STATE_002_RUNTIME_BASELINE.md). The filtered evidence log contains no service tokens or tile values. A successful player exit is not an assertion that every scenario passed.

To reproduce, export that commit's Assets, Packages, and ProjectSettings into a fresh temporary project. Copy `LegacyBaseline.cs` into the temporary Assets and `LegacyBuild.cs` into temporary Assets/Editor. A PackageCache symlink to the installed project's cache avoids package redownloads. The pinned Unity executable is `/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity`.

The tested temporary project is `/tmp/td-legacy-baseline`; output is `/tmp/td-legacy-build/Legacy.app`. Build:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath /tmp/td-legacy-baseline -executeMethod LegacyBuild.Build -logFile /tmp/td-legacy-build-elevated.log
```

Delete `/tmp/td-baseline-ready` before each run, then launch these in separate processes within a few seconds:

```sh
/tmp/td-legacy-build/Legacy.app/Contents/MacOS/TrainDominoesNetcode -batchmode -nographics --baseline-host -logFile /tmp/td-baseline-host.log
/tmp/td-legacy-build/Legacy.app/Contents/MacOS/TrainDominoesNetcode -batchmode -nographics -logFile /tmp/td-baseline-client.log
```

The harness disables SimpleMatchmaking before starting networking, configures `127.0.0.1:17897`, and submits actions directly through production RPCs. It does not test pointer/button dispatch. `-nographics` runs real MonoBehaviour/coroutine logic but does not establish visual quality or exact transform endpoints. Fixed waits are observation windows rather than animation completion assertions.

The client-local DominoTracker does not mirror its authoritative hand collection, so a logged client hand of zero is not a claim that no tiles were delivered. Host hand counts are authoritative. The harness's `individual-*` labels record intended scenarios; the report correctly classifies them as group-phase operations when the preceding group completion fails.

Unity needs permission for its Package Manager Unix socket and loopback UDP socket. In the recorded environment sandboxed startup failed with `listen EPERM`; the isolated build/run succeeded after sandbox escalation. No Lobby, Relay, Authentication, or live-service API was invoked.
