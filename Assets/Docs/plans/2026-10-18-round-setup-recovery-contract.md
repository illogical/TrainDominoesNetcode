# Round setup recovery contract (GAME-002)

Date: 2026-10-18. Focused implementation contract for the gameplay-gap roadmap.

The existing `Ready` command has a second adapter-owned lifecycle meaning only in AwaitingDeck: retry the pending setup. The coordinator accepts this only from the configured host transport, with an exact current position token, while not suspended. No new rule condition or state class is introduced. Existing request fingerprints/outcome caches make duplicate retries replay their outcome without calling the supplier twice. A new deliberate retry uses a fresh request identity. Stale, foreign, suspended and ordinary-phase requests retain existing rejection behavior.

A supplier exception or invalid deck leaves the immutable match snapshot, readiness, score ledger and position untouched. A corrected supplier passes through Core.StartRound validation and starts the pending round once. Resume retains its existing automatic attempt when resuming AwaitingDeck. No player-supplied deck is accepted over transport.

The existing Retry button is shown to the host in AwaitingDeck; RoundInitializing subscribes/unsubscribes it and sends Ready through ClientSession's correlated request slot. Participants receive a generic setup-failure message through the existing feedback RPC, including on synchronization. Supplier details remain in host diagnostics. Repeated failures are actionable; success clears the correlated rejection and changes phase normally. There are no wire schema/version changes.

Verification must distinguish source-linked coordinator/state checks, real localhost failure/recovery fixtures, and user-visible review. The first does not substitute for the latter two.
