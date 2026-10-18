---
type: rulebook
status: agreed-app-profile
version: 0.2
updated: 2026-10-17
---

# Train Dominoes rulebook — Puremco baseline

The user selected **Puremco rules**, replacing earlier choices based on uncertain recollection. This first iteration uses the **Puremco DominoRules web edition** as its primary reference and the University Games/Puremco printed sheet as corroboration. Where editions differ, identify the edition rather than silently mixing them. The standalone engine and Unity gameplay adapter now implement this profile. See [standalone verification](RULES_ENGINE_VERIFICATION.md) and [integration verification](GAMEPLAY_INTEGRATION_VERIFICATION.md) for their separately tested boundaries and limits.

**Naming:** Puremco is a brand/publisher of Mexican Train Dominoes, not a different game. Read [Mexican Train and Puremco: names and edition differences](MEXICAN_TRAIN_RULE_VARIANTS.md) for a comparison and prompts to recognize the family's version. Selecting this reference does not establish which edition the family historically played.

Read the [player-state review](PLAYER_STATE_REVIEW.md), [engine implementation plan](RULES_ENGINE_IMPLEMENTATION_PLAN.md), and [current implementation baseline](PROJECT_STATE.md). Rule IDs remain stable as this draft evolves.

## Remembering the trains

**Four players can have five trains: four personal trains and one shared Mexican Train.** The engine is separate. A train position may exist before any tile has been played there; an empty hub slot is not permission to invent another shared train.

| Train | Owner | Ordinary access |
| --- | --- | --- |
| Personal train | One player | Owner only while unmarked; everyone while marked |
| Mexican Train | Shared | Everyone; any player may start it on their turn |

A marker changes access to an existing personal train; it does not create a new train. An opponent starting an empty, marked train does not take ownership. Personal trains can alternate between private and public; the Mexican Train stays public. Use player count plus one train identities: five for four players, nine for eight. Physical layout may put the shared train beside the hub. [Puremco train/marker definitions](https://www.dominorules.com/mexican-train).

## Source and decision register

| Decision | Current selection | Origin / superseded assumption |
| --- | --- | --- |
| DEC-001 — Authority | Puremco web edition; printed Puremco sheet corroborates rules | Latest explicit user instruction replaces RNK as baseline |
| DEC-002 — Shared train | Exactly one shared Mexican Train, alongside one personal train per player | Explicit withdrawal of the earlier personal-only preference |
| DEC-003 — Opening | Sequential turns, one ordinary tile from the start, subject to doubles | Earlier simultaneous private opening chains are withdrawn from the selected profile; record them only as a possible future variant |
| DEC-004 — Doubles | Additional play may be on the double or another eligible train; unresolved doubles restrict following players | Earlier strict same-train coverage choice is superseded by Puremco |
| DEC-005 — Two doubles | Conditional two-double play with an in-hand third tile covering one | Do not substitute unlimited double chains from another edition |
| DEC-006 — Scope | Double-twelve, 2–8 players, thirteen engines, fixed seats/rotating starter | Earlier compatible choices retained; four-color art remains separate |
| DEC-007 — Deal table | Use explicit double-twelve counts on Puremco's web page | Earlier RNK table is superseded; printed sheet has an edition-specific table, not an automatic override |
| DEC-008 — App controls | User-reviewed 2026-10-17: Immediate placement by default; optional Draft/undo; readiness sets, joint winners, manual announcement/challenge, suspension and fresh-roster new matches | Application policies, not historical family rules; see R-015–R-018 |

The important correction is not just restoring a shared train: adopting Puremco also changes opening play and doubles. A memory or current code path does not override this register.

## Core play

The following is a concise, original paraphrase of the selected references, not a reproduction of either rule sheet.

### R-001 — Setup and dealing

Use 91 double-twelve tiles. Set aside the engine, shuffle the remainder, deal private hands, and retain the boneyard. Puremco's **web-edition** double-twelve deal counts are:

| Players | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Tiles each | 16 | 15 | 14 | 12 | 11 | 10 | 9 |

For four players this leaves 34 boneyard tiles after removing the engine. These numbers intentionally differ from RNK and some printed editions. [Puremco setup/table](https://www.dominorules.com/mexican-train).

### R-002 — Train identities

One personal position per player and one shared position exist for the round. No second Mexican Train is permitted. Starting another player's empty, marked position starts that player's personal train.

### R-003 — Matching and orientation

Match the exposed endpoint; an empty train matches the engine. Orient the new tile accordingly. Neither an inward pip nor a visually adjacent tile creates an extra attachment point. Doubles do not branch.

### R-004 — Turn and round order

Choose the first starter randomly. Turns and later round starters move clockwise. Engine values descend from twelve to blank: thirteen rounds. [Puremco round order](https://www.dominorules.com/mexican-train).

### R-005 — First turns

There is no special multi-tile opening phase. Players take turns from the beginning; ordinary turns play one tile, with the double exceptions below. Arranging a chain in your hand is planning, not playing that entire chain onto the board.

### R-006 — First-turn inability

Use the normal unable-to-play procedure, not a separate opening draw or chain-construction entitlement. An unstarted personal train does not prevent legal play elsewhere.

### R-007 — Turn completion

After a non-double ordinary play, the next player acts. A double needs its additional play or a legal failed-play outcome before the turn ends. Immediate play finishes automatically; optional Draft commitment is described in R-016.

### R-008 — An open double on a later turn

A double left exposed when its player's turn ends restricts subsequent play to satisfying that double, including on a normally private train. If unable, draw once, then cover or pass and mark your own train. Normal access resumes once the obligation is satisfied. There is no opening-double deferral or simultaneous-reveal queue.

### R-009 — Eligible trains

Ordinary destinations are your train, marked personal trains, and the shared Mexican Train. The starter may start the shared train immediately; it need not wait for a second turn or for everyone's personal train to begin. [Puremco first-turn and play rules, printed sheet page 4](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf).

### R-010 — Drawing and passing

Play when able. Otherwise draw one tile and use it if playable; if still unable, pass and mark your personal train. With an empty boneyard, pass directly when unable. A draw cannot be used strategically to avoid a playable hand tile. [Puremco unable-to-play rule, printed sheet page 4](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf).

### R-011 — Double follow-up and the two-double exception

A double requires an additional tile, either on that double **or another eligible train**. If no follow-up is available, draw and try; failing that, pass with your marker. If the double remains exposed, apply R-008 to following players.

Two doubles may be played on different eligible trains if a third tile already in hand covers one of them; do not draw for that third tile. The other double remains the next player's obligation. A final tile ends the round before further obligations. [Puremco double rules, printed sheet page 4](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf).

### R-012 — Markers and ownership

The owner removes their marker by playing on their own train. Playing elsewhere, or an opponent extending it, does not close it. The Mexican Train remains public. [Puremco marker removal](https://www.dominorules.com/mexican-train).

### R-013 — Round endings

End immediately when someone plays their final tile, even a double, or when the boneyard is empty and nobody can play. No first-turn grace period applies. Marker changes must be considered before deciding that play is blocked. [Puremco endings, printed sheet page 4](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf).

### R-014 — Scores and notification

Count unplayed pips as penalty points; lowest cumulative score wins. Blank pips are zero; do not import Chickenfoot's double-blank penalty. Players manually announce reaching one tile. The selected optional missed-announcement penalty and challenge window are defined in R-018; other optional infractions remain disabled. [Puremco scoring and optional penalties, printed sheet page 4](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf).

## Application conventions — reviewed project policies

The user reviewed these individually on 2026-10-17 and approved implementation of the standalone profile. They fill software gaps; do not present them as historical family rules or verbatim publisher requirements. Settings are fixed for a match; their GUI is a later task.

### R-015 — Match management

Use fixed seats with 2–8 participants. Readiness is a set: everyone must ready before initial play and each subsequent round, then the coordinator supplies the next validated shuffled deck. Score a round once. New Match starts with fresh readiness, engine schedule, hands, scores, results and request identity. Report all lowest-score players as joint winners; a future optional tiebreaker is deferred.

A disconnected participant suspends progression instead of silently changing the roster. The host may abandon a suspended match and create a fresh match with the remaining 2–8 participants; collect their readiness again. Do not drop seats from an active match. Connection detection and usable resynchronization belong to the host adapter. The adapter implements host-issued seat secrets, reclaim under a new transport ID, missing-seat feedback, Retry and host Resume once every fixed seat is connected. This preserves the roster; host migration, app-restart recovery and human review of recovery UX remain outside the recorded evidence.

### R-016 — Placement commitment and optional undo

`CommitmentMode.Immediate` is the default. Each accepted placement is public and irreversible; completing the current play requirement automatically advances the turn or ends the round. A permitted Pass explicitly ends an unsuccessful turn. Doubles still require their applicable follow-up. No End Turn or Undo is available in this mode.

The host may instead choose `CommitmentMode.Draft` before the match. Provisional placements may be undone last-in-first-out; selection itself remains local UI data. Only committed play advances the turn or ends a round. End Turn commits a completed draft; a legal Pass seals and commits an unsuccessful turn. Others cannot play during the actor's draft, although permitted between-turn announcements/challenges remain independent commands.

A draw seals earlier placements and clears their undo history because the player has learned a hidden tile. The drawn tile stays out of the boneyard permanently. Undoing a subsequent placement neither refreshes the draw allowance nor removes an obligation. A playable drawn tile must be replayed legally before End Turn. Show unsealed draft changes only to their actor; publish sealed placements when a draw makes them irreversible. A provisional final tile does not end a round until commitment; an immediate final tile ends it upon accepted placement.

### R-017 — Reviewed operational interpretations

Model drawing as one attempt for the **current play requirement**. If a drawn double is played and creates an additional-play requirement, an additional attempt is available when no follow-up exists. This operationalizes Puremco's extra-play instruction; the sheet does not spell out that combined scenario, so do not mislabel the boundary as a quotation.

In the two-double exception, the second placement is legal only if the held third-tile cover exists; after playing the second double the actor must cover one of the two, without drawing. Reject a third double and a second double that lacks the required in-hand cover. Check final-tile termination first. Under this bounded procedure, at most one double remains exposed when a turn completes; do not invent a generic multi-double queue from another edition.

### R-018 — Manual one-tile announcement and opponent challenge

Puremco's printed sheet requires notification at one remaining tile; its penalties are optional and offer several choices. The user selected a manual action with an always-available GUI button (implemented in the Unity action controls; permitted announcements/challenges remain usable during board motion). An unnecessary/repeated click returns harmless feedback: “You don't currently need to announce.” One remaining tile does not promise the player's next turn is their last.

An announcement can be accepted when the public, irreversible hand count is one, including between turns. Private draft previews do not create an announcement obligation. Going out within the turn ends the round first. If a completed turn leaves an unannounced one-tile hand, any opponent may challenge until the next player's first accepted play, draw or pass. An accepted announcement or one successful challenge closes the opportunity. Rejected actions do not expire it; host command order decides races. No timer or automatic missed-announcement punishment is used.

`AnnouncementPenalty.DrawOneTile` is the match-fixed default. A successful opponent challenge draws one tile for the offender when available, without consuming an ordinary draw allowance or requiring immediate play of that penalty tile. When the boneyard is empty, record the infraction and close the challenge without another penalty. `AnnouncementPenalty.None` records the infraction without drawing. Other penalties/settings UI are future work. These challenge/timing details are project policies; the publisher does not define this software protocol. [Printed notification and optional penalties](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf).

## Worked examples for the engine and state review

These scenarios are project-authored acceptance examples, not evidence that the current code passes them.

| Example | Input/action and expected outcome | Rules |
| --- | --- | --- |
| EX-001 | Four players: four personal identities plus one shared identity. A second shared train is rejected; marking a personal train still leaves five total | R-002, R-012 |
| EX-002 | Endpoint is 3 on an oriented 6–3. Reject 6–5 and accept 3–5 | R-003 |
| EX-003 | First player starts the Mexican Train with 12–5, then ends the turn. The next player may extend it even with an unstarted personal train | R-005–R-009 |
| EX-004 | A marks their train. B extends it; the marker stays. A extends it later; the marker is removed | R-009, R-012 |
| EX-005 | A plays 4–4 on their train, then a non-double on the shared train. That completes A's turn, but the next player must satisfy 4–4 | R-008, R-011 |
| EX-006 | A has a 4–4 and 6–6 for two eligible endpoints and an in-hand 4–2. Allow the two doubles followed by 4–2; the next player must cover 6–6. Without an in-hand covering third tile, reject the second double | R-011, R-017 |
| EX-007 | A plays their final tile, a double. Immediate mode ends the round on acceptance; Draft mode waits for commitment. No draw/coverage is required, even on A's first turn | R-013, R-016 |
| EX-008 | Empty boneyard: a pass opens a destination another player can use. Do not declare blocked; a later position with no legal action for anyone may end | R-010, R-012, R-013 |
| EX-009 | Final totals 90, 90, 110, 130 yield two winners under the selected app tie policy | R-014, R-015 |
| EX-010 | A can play a non-double, so Draw is illegal. Undo/re-entry must not permit that draw or renew a previously used attempt | R-010, R-016 |
| EX-011 | Pending double is on B's private train. A can cover it despite ordinary access restrictions, without transferring ownership or closing A's marked train | R-008, R-012 |
| EX-012 | After a normal no-play draw, the only playable tile is a double. Its follow-up is a separate requirement; a failed follow-up draw cannot be retried | R-010, R-011, R-017 |
| EX-013 | One public tile remains: allow a manual announcement. Repeated announcement has no effect. An opponent may instead challenge an omission before the next accepted play/draw/pass; draw one penalty tile if available, once | R-014, R-018 |

## Variations deliberately excluded

- Simultaneous opening chains, private opening submissions, and deferred opening-double resolution: earlier project proposal, no longer selected.
- Mandatory same-train follow-up immediately after every double: RNK-style rule, not the selected Puremco extra-play rule.
- Unlimited doubles or generic queues of several exposed doubles: other variants, not the selected conditional two-double procedure.
- Personal-only play, multiple shared trains, extra turns to build a whole opening chain, and special blank penalties: not selected.
- Chickenfoot branches: a separate game, not a type of shared Mexican Train.

[Pagat documents several alternate opening/double treatments](https://www.pagat.com/domino/star/mextrain.html); [RNK publishes strict double coverage and opening chains](https://rnkgaming.com/wp-content/uploads/2021/03/Mexican-Train-Rules.pdf). They explain why earlier recollections were plausible, but do not override the user's selected Puremco baseline.

## Review status and sources

The published baseline and R-015–R-018 application profile were reviewed and approved on 2026-10-17. The standalone engine and Unity gameplay/state integration are implemented. [Standalone verification](RULES_ENGINE_VERIFICATION.md) records core evidence; [integration verification](GAMEPLAY_INTEGRATION_VERIFICATION.md) records subsequent console, actual localhost, authorized live Lobby/Relay and synthetic presentation results. The [gameplay review roadmap](plans/2026-10-18-gameplay-gap-analysis-and-review-plan.md) tracks remaining startup, recovery, admission and visual acceptance work.

Research checked 2026-10-17:

- **S1 — Puremco / DominoRules, Mexican Train:** [web rules](https://www.dominorules.com/mexican-train). Selected edition; its explicit double-twelve deal table governs. Direct retrieval timed out; the full search-indexed rules text was available.
- **S2 — University Games / Puremco, Domino Game Rules, B010788 (05/16):** [publisher PDF](https://www.universitygames.com/_files/ugd/05f72c_230047a18d1c4372a9ec2dd61b1de8f0.pdf), Mexican Train section, especially page 4. Text inspected. Its deal-table extraction is flattened; this draft does not infer columns from it or claim its table matches S1.
- **S3 — John McLeod, Pagat:** [Mexican Train and variations](https://www.pagat.com/domino/star/mextrain.html), comparison only.
- **S4 — RNK Sales (2020):** [Mexican Train rules](https://rnkgaming.com/wp-content/uploads/2021/03/Mexican-Train-Rules.pdf), comparison only; superseded as the proposed baseline.

No source is claimed to settle all software controls. Describe verification by its recorded scope and date; a passing core suite or scripted match does not establish exhaustive multiplayer behavior or human acceptance of visual quality.
