---
type: rulebook
status: draft
version: 0.1
updated: 2026-10-17
---

# Train Dominoes: first rulebook

This draft specifies a Mexican Train game with a simultaneous opening adaptation. It is a researched design, not a description of everything the current code enforces. The user approved creating a first iteration and reviewing differences afterward. Their family's historical rules have not been identified.

Read alongside the [player-state review](PLAYER_STATE_REVIEW.md), [engine plan](RULES_ENGINE_IMPLEMENTATION_PLAN.md), and [current implementation baseline](PROJECT_STATE.md). Rule IDs below remain stable as the draft evolves.

## Remembering personal, public, and shared trains

**Four players have four personal trains and at most one shared Mexican Train: five trains in total.** Some may not have been started yet. The center engine is not an additional train. An eight-slot hub does not grant permission to create eight trains in a four-player game.

| Train | Owner | Who can normally play there? |
| --- | --- | --- |
| Your personal train | You | You |
| Another player's personal train | That player | Its owner, or anyone while its marker is displayed |
| The shared Mexican Train | No individual player | Everyone once opening has finished; any player may start it |

Marking a personal train **opens an existing train**; it does not create another one. Starting the Mexican Train creates the one shared train, not an unlimited supply of public trains. An opponent starting your empty, marked personal train does not become its owner. These distinctions follow the [RNK train definitions](https://rnkgaming.com/wp-content/uploads/2021/03/Mexican-Train-Rules.pdf) and [Puremco marker rules](https://www.dominorules.com/mexican-train).

The preceding plan's title, “Personal-Trains Variant,” and exclusion of the Mexican Train are superseded for this **recommended first draft**. The user reconsidered that choice after remembering public trains and asked to stay close to published play. Including the shared train is the assistant's resulting recommendation, not a claim that the user separately confirmed that exact correction. Review DEC-001 before implementing train structure.

## Reference editions and decision history

There is no single universally followed Mexican Train ruleset. Use RNK as the principal reference for opening chains and strict double coverage; use the other sources to identify variations and fill explicitly marked gaps. Do not silently combine contradictory editions.

| Decision | Origin and current disposition | Relationship to published play |
| --- | --- | --- |
| DEC-001 — Shared train | Earlier explicit “personal trains only” selection reconsidered in the latest message. Recommend one shared train in this draft; needs review | Omitting it would be a house variant; it is included in all three main references |
| DEC-002 — Opening | User accepted simultaneous private drafts, followed by simultaneous reveal and locked submissions | Opening chains are published; simultaneity/privacy/submission protocol are project adaptations |
| DEC-003 — Seats | User chose fixed seats with rotating starter. Recommend random initial starter | Rotating starter is published; random selection replaces RNK's tile-draw procedure |
| DEC-004 — Player count | User chose 2–8; four colored art sets remain a separate, later deliverable | Compatible with the reference range; requires up to nine trains including shared |
| DEC-005 — Opening draw | User accepted one attempt when unable to start, then continuation of the chain if successful | Opening draw exists in RNK; continuation after that draw is our explicit interpretation. Pagat instead has no opening draw |
| DEC-006 — Doubles | User chose strict coverage and accepted the recommendation of one draw per required play | Strict coverage follows RNK; Puremco permits a follow-up elsewhere. The repeated-draw boundary is made explicit here |
| DEC-007 — Rounds | User chose thirteen engines, including 0–0 | Follows RNK/Pagat; current code's twelve-round limit differs |
| DEC-008 — Remaining defaults | Recommended hand sizes, opening-double ordering, tie handling, and software undo/commit policies below remain reviewable | Do not describe them as independently confirmed family rules |

### How other games/editions differ

| Reference | Important distinction |
| --- | --- |
| RNK Mexican Train | Opening chains, strict double coverage, and the deal table used here |
| Puremco Mexican Train | Individual opening plays; a double's follow-up may be on another eligible train |
| Pagat Mexican Train description | Opening chains without an opening draw; deferred opening doubles; different deal sizes and permissive double follow-ups |
| Chickenfoot | A separate game where doubles require branching responses, rather than the single continuation used here |

The [Puremco description](https://www.dominorules.com/mexican-train), [Pagat variations](https://www.pagat.com/domino/star/mextrain.html), and [University Games sheet covering Mexican Train and Chickenfoot separately](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf) support these distinctions. This project is not implementing Chickenfoot, branching spinners, or an arbitrary-variant editor.

## Rules for this draft

### R-001 — Equipment and deal

Use a double-twelve set, one marker per player, and a boneyard. Select the round's engine before shuffling the rest. Recommended RNK deal sizes:

| Players | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Tiles each | 16 | 16 | 15 | 14 | 12 | 10 | 9 |

These are edition-specific, not universal. RNK's table groups two and three players together. Hands and unrevealed draws are private. [RNK setup](https://rnkgaming.com/wp-content/uploads/2021/03/Mexican-Train-Rules.pdf).

### R-002 — Train identity and capacity

Allocate one personal train to each seat and one shared train, including empty train positions. Never allocate additional shared trains. Capacity is player count plus one: five for four players, nine for eight. This is a consequence of the train definitions, independent of the visual hub.

### R-003 — Matching

An empty train starts with the engine value. Later placements match the train's **exposed endpoint**, not either pip on its last tile. The tile may be oriented either way to achieve that match. Doubles do not branch. [Pagat matching description](https://www.pagat.com/domino/star/mextrain.html).

### R-004 — Seats and rounds

Keep seats fixed. Choose the initial starter randomly, then advance that starting seat once per round. Play all engines from 12–12 down to 0–0. Submission speed never determines turn order. Random selection is a project convenience; the rotation/schedule follow the reference pattern. [RNK round progression](https://rnkgaming.com/wp-content/uploads/2021/03/Mexican-Train-Rules.pdf).

### R-005 — Opening construction

Each player privately builds any nonempty legal chain on their own train. They may stop before exhausting every extension; no longest-chain search is required. Nobody plays on other trains or starts the shared train during opening. Simultaneous editing and private visibility are DEC-002 adaptations to published opening-chain play.

### R-006 — Opening draw and failure

Only a player unable to start from their original hand may draw one extra tile. If it starts the train, it must be used and the player may continue the chain without further draws. Otherwise submit an empty, marked-open train. A player cannot voluntarily submit an empty train while holding a starter. Drawing to extend an already started opening chain is forbidden. This explicitly resolves the opening-draw interpretation in DEC-005.

### R-007 — Submit and reveal

Submission validates and locks that player's draft. Reveal the opening board only when everyone has submitted. The server serializes draw requests against one boneyard; draw-request order does not change seat order. Do not reveal another player's tiles through intermediate events. These are software rules for the simultaneous adaptation.

### R-008 — Opening doubles

Internal doubles are covered by the next tile in the chain. A terminal double does not interrupt another player's opening. After reveal, queue terminal doubles in seat order starting at the round's starter. Ordinary turns resolve that queue before unrestricted play. Deferral follows [Pagat's opening-double rule](https://www.pagat.com/domino/star/mextrain.html); seat ordering replaces chronological order because our openings are simultaneous.

### R-009 — Ordinary destinations

On a normal turn, an eligible destination is your personal train, another player's marked train, or the shared Mexican Train. The shared train may be started after opening, even if your personal train is unstarted. It stays public. A pending double instead restricts play to its endpoint regardless of ordinary ownership access. [Puremco eligible trains](https://www.dominorules.com/mexican-train).

### R-010 — Draw or pass

If a legal placement exists for the current obligation, play rather than draw or pass. Otherwise draw one tile if available; use it if legal, else pass. With an empty boneyard, pass directly if unable to play. No strategic drawing or repeated fishing is allowed. [Puremco general rules](https://www.dominorules.com/mexican-train).

### R-011 — Strict doubles and draw opportunities

Cover a newly played double on that same train. If no covering tile is in hand, attempt one draw for this new obligation, then cover or pass. An uncovered double survives into subsequent turns. Covering a queued double uses that player's ordinary play; the next player addresses the next queued double. This follows RNK's strict coverage, with DEC-006 clarifying draw opportunities.

Track draws by obligation: drawing a playable 4–4 may create a separate cover attempt, even if that makes two draws in one turn. A failed attempt cannot be retried by undoing or re-entering a local state. A last-tile double is handled by R-013.

### R-012 — Markers

A legal pass opens the player's train. Only its owner playing there closes it; playing elsewhere, or an opponent extending it, leaves its marker in place. Covering a double does not transfer ownership. The shared train never closes. [Puremco marker behavior](https://www.dominorules.com/mexican-train).

### R-013 — Round termination

After opening, a committed last-tile play ends the round, including a last-tile double. During opening, finish all submissions before ending on any empty hand. If no one goes out, end a blocked round when the boneyard is empty and no player has a legal move under current access and obligations. Re-evaluate after marker changes; an opened train may unlock a move. [Pagat round ending](https://www.pagat.com/domino/star/mextrain.html).

### R-014 — Scores and ties

Record each hand's remaining pip sum as penalty points and add it once to that player's match total. Blanks have value zero; no special double-blank penalty is adopted. After the thirteenth round, lowest total wins. Joint winners on tied totals are a project default, rather than arbitrarily selecting the first player. [Pagat scoring](https://www.pagat.com/domino/star/mextrain.html).

### R-015 — Readiness and lifecycle

Software policy: start with a fixed ready roster; count each player's readiness once. After a scored round, all remaining roster members must be ready before initializing the next. A new match resets scores, rounds, obligations, drafts, readiness, and markers. Disconnects suspend progression pending recovery/abandonment; v1 does not silently remove a player or substitute a bot. Transport recovery UI is separate work.

### R-016 — Preview, undo, and commitment

Software policy: selection is local UI data. Server-validated placements remain provisional until End Turn/Submit, allowing last-in-first-out undo within the current draft. An opening submission is final. Ordinary End Turn is legal only after satisfying the current play/cover requirement or reaching a legal pass condition.

Drawing is an irreversible information boundary. It seals preceding placements and clears their undo history; later placements can be revised without returning the drawn tile to the boneyard or renewing an allowance. Example: after sealing a provisional double to draw its cover, that double can no longer be undone. If undo removes a required drawn tile from the board, it remains in hand and must be replayed legally before ending the turn. A provisional final tile does not end a round until committed, and no other player starts meanwhile. These controls are app-specific adaptations, not table-game requirements.

## Worked examples for review and tests

| Example | Situation and expected outcome | Rules |
| --- | --- | --- |
| EX-001 | Four personal trains plus a shared train already exist. A player cannot create a second shared train; opening a personal train still leaves five total | R-002, R-012 |
| EX-002 | A train ends in oriented 6–3. A 6–5 cannot attach; a 3–5 can. Matching the hidden/inward 6 would be a bug | R-003 |
| EX-003 | With engine 12, a player has no 12. Their opening draw is 12–5; they may use it and continue with 5–8. They cannot draw again | R-005–R-007 |
| EX-004 | A marks their train. B extends it; A's marker stays. A later extends their own train and removes it | R-009, R-012 |
| EX-005 | A draws 4–4 as their only legal normal play, then has no 4 to cover. One cover draw is allowed. If it fails, A passes and the next player must address 4–4 | R-010, R-011 |
| EX-006 | Two opening chains end in doubles. All players finish opening before coverage begins; queue order uses seats, not submission timestamps | R-007, R-008 |
| EX-007 | A plays their final tile, a double, and commits. End the round without demanding a cover | R-013, R-016 |
| EX-008 | Boneyard is empty. A pass opens a destination B can use: the round is not blocked. End only when updated legal-move evaluation finds none | R-010, R-012, R-013 |
| EX-009 | Final totals A=90, B=90, C=110, D=130 produce two match winners, A and B | R-014 |
| EX-010 | A submits first in opening. A cannot edit afterward and does not thereby become the first individual player | R-004, R-007 |
| EX-011 | A covers a double on B's private train. The obligation permits access, but does not open B's train or close A's marked train | R-009, R-011, R-012 |

Examples are project-authored acceptance scenarios, not quotations or demonstrations that the current game passes them.

## Review queue

- **OPEN-001:** Confirm the recommended restoration of exactly one shared Mexican Train after the latest clarification. Do not implement the earlier personal-only assumption.
- **OPEN-002:** Review simultaneous opening, continuation after an opening draw, and seat-ordered opening doubles as a package of explicit adaptations.
- **OPEN-003:** Confirm edition-specific hand sizes and the recommended joint-win/undo policies. Alternatives should be deliberate amendments with updated examples.

The user has authorized a first draft and iteration, not asserted that every recommendation reproduces their family's play. Keep STATE-001 open until this review is resolved. No gameplay changes are made by this file.

## Sources and attribution

Research checked 2026-10-17. Rules are paraphrased and project decisions are original specifications; no source is reproduced as a complete rule sheet.

- **S1 — RNK Sales, Mexican Train Rules (2020):** [manufacturer PDF](https://rnkgaming.com/wp-content/uploads/2021/03/Mexican-Train-Rules.pdf). Main edition reference; includes its deal table, opening-chain structure, and strict double coverage.
- **S2 — Puremco / DominoRules, Mexican Train:** [rules page](https://www.dominorules.com/mexican-train). Eligible trains, marker behavior, and comparison edition. Search-indexed text was available; direct page retrieval timed out during research.
- **S3 — John McLeod, Pagat, Mexican Train:** [rules and variations](https://www.pagat.com/domino/star/mextrain.html). Opening deferral, end conditions, scoring, and explicit published alternatives.
- **S4 — University Games, Domino Game Rules, B010788 (05/16):** [publisher PDF](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf). Distinguishes Mexican Train and Chickenfoot. PDF text inspected; do not infer deal-table columns from its flattened extraction.

Sources do not settle undocumented software behavior. R-005–R-008 and R-015–R-016 intentionally specify those adaptations for review.
