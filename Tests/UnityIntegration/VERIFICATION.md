# Unity CLI integration verification — 2026-10-18

Verification uses Unity 6000.6.6f1 CLI only, with an isolated temporary project and actual macOS host/client player processes. No editor UI actions, saved scene changes, package upgrades, Lobby, or Relay were used.

The complete lifecycle production-script snapshot was copied with deletion of obsolete sources and byte-compared against the working project. Its aggregate SHA-256 is recorded in `production-source.sha256`; it covers sorted relative paths and contents under Assets/Scripts, including metadata. The temporary project's GameScene and packages retain the project's saved development settings. The harness injects only a deterministic rules-core authority; those development bypasses never govern core legality.

## Completed maximum-capacity checks

`python3 Tests/UnityIntegration/run_eight.py` passed with all eight actual players. Each received a nine-tile hand and nine train identities. The host submitted a permitted action through production client/transport boundaries. Disconnecting one participant suspended the host's view and disabled its actions. Creating a new match seated exactly the remaining seven participants; each remaining client received that seven-seat view. Evidence: `observed-eight-0.log` through `observed-eight-7.log`.

## Completed Immediate integration and presentation checks

`python3 Tests/UnityIntegration/run_players.py` passed from both actual players. They completed all thirteen rounds, projected round scores and match winners, then received a fresh match with cleared scores. The host disconnected; the remote client's board became unavailable. Initial Ready used actual generated button listeners. Setup and the first preselected placement used normal animation speed; its mesh ended within 0.001 units of the production layout position. Identical accepted-request replay and stale-token replay crossed the actual RPC boundary without changing revision. Legal Place/Draw/Pass and Announce commands progressed the complete match, with safe stale-command rejections retried from current projections.

After that real match, the host ran explicitly **presentation-only synthetic DTO probes**. Forty rapid updates exercised queue overflow, then every final placement endpoint matched the latest view. An in-flight draw was replaced by a new presentation generation with a one-tile hand; old meshes/callbacks did not overwrite its final layout. These probes do not establish rule legality or transport filtering.

Evidence: `observed-immediate-host.log` and `observed-immediate-client.log`. COUNTS lines count submitted command attempts; accepted outcomes have individual PASS lines.

## Completed Draft integration checks

`python3 Tests/UnityIntegration/run_players.py --draft` passed from both actual players. They completed thirteen rounds, final winners, fresh-match score reset, and remote disconnect input gating. Each peer successfully undid a placement and replayed it; both repeatedly sealed turns through EndTurn. Draft setup/first selection-placement, exact final transforms, identical/stale RPC replay, permitted announcements, and the same explicitly presentation-only overflow/cancellation probes all passed. Evidence: `observed-draft-host.log` and `observed-draft-client.log`.

## Final revision-gap hardening regression

After the complete lifecycle runs, root added one ClientSession behavior: a view revision gap invalidates incremental presentation and reconciles the same-round board. The full lifecycle snapshot hash remains `75b8d7276ee40e184928400227fdd280da11c08c83a42e2c91b329f6e14a8b3f` (`production-source.sha256`).

The final source was rebuilt with Unity CLI, then `python3 Tests/UnityIntegration/run_players.py --presentation-only` passed on both actual players. They completed ordinary real startup/Ready/deal, then an explicitly synthetic presentation probe moved the same tile from train 0 at revision 10 to train 1 at revision 12. Both peers invalidated the old presentation generation and placed the mesh within 0.001 units of train 1's expected position. This is presentation reconciliation proof, not a simulation of actual packet loss.

The final gap-regression source snapshot hash is `5d7165ba5a42c2fc41b8b0359f49f9dcb398292354fa13f2093f09da80133aeb` (`production-source-gap.sha256`). It was byte-compared to the working source after the build. The complete matches were not repeated after this one-branch hardening; the final build and focused regression are the validation for that delta. Evidence: `observed-presentation-host.log`, `observed-presentation-client.log`, and `observed-gap-build.log`.

## Harness corrections during development

An initial test expected fifteen tiles at two players; the correct selected core profile deals sixteen. A first greedy chain-deck generator could exhaust an endpoint; the adapter correctly retained the awaiting-deck state and logged the fixture supplier failure. The fixture now retries deterministic randomized trails. Concurrent Ready/Announce submissions correctly produce stale-position rejections; the harness now waits for outcomes and resubmits from the latest view. These were harness assumptions, not production fixes.

## Limits

Headless final-transform checks establish numerical endpoints and coroutine completion, not perceived visual quality. Initial Ready invokes the actual generated button listener; tile/train selection uses the production client API rather than mouse/raycast input. Packet loss, uncertain-delivery transport recovery, real service integration, deliberate joint-winner fixtures, and reconnect/resume remain outside these CLI smoke runs. Separate .NET source-linked suites cover additional boundary cases, but are not evidence for those untested real transport scenarios.

Valid challenge penalties and explicit private-draft information-leak assertions over real serialized transport are implemented but unverified by these smoke scenarios; the source-linked boundary suite provides separate coverage. Both supported penalty settings (DrawOneTile/None) are not both exercised here.

## 2026-10-18 refactor re-run

After the pre-Priority-4 refactor, all four scenarios (Immediate, Draft, eight-player, presentation-only) passed again from a fresh CLI build, and the filtered logs were identical. See `production-source-refactor.sha256` and the refactor section of `Assets/Docs/GAMEPLAY_INTEGRATION_VERIFICATION.md`.
