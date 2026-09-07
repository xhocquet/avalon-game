---
name: tighten-docs
description: Rewrite one section of a long-lived agent doc (AGENTS.md, CLAUDE.md, README) to read like code documentation — short, explicit, non-repetitive, no claims that rot. Use when asked to clean up, tighten, simplify, condense, or de-duplicate a doc section, or when a section has grown into prose that costs more to maintain than it teaches.
---

# Tighten Docs

Rewrite one named section at a time. Always draft first, edit only after approval.

## Procedure

1. Read the whole file, not just the target section — you need to know what the other sections already own.
2. Verify every factual claim you plan to keep against the code it describes. Grep the symbol; open the method. A claim you cannot confirm gets deleted, not hedged.
3. Post the full rewritten section in a fenced block, plus a short list of what you cut and why.
4. Wait. Expect two or three rounds of "still too long."
5. On approval, splice it in — keep the surrounding sections byte-identical, check for a doubled blank line at the seam.
6. Report the line range and anything orphaned (see Orphans).

## Length

The rewrite must be shorter than the original in word count. If it isn't, you summarized instead of cutting. Cut again.

A bullet is one fact. If it needs a second sentence to justify the first, the justification is usually the part to drop.

## What to cut

- **Repeated identifiers.** Name a symbol once per bullet. Five `HeroAsset`s in three lines means the sentence is circling.
- **Trailing rationale.** `so nothing downstream goes back through the faction`, `That is deliberate:`, `which is what lets…`. State the rule; the reason belongs only where a reader would otherwise break the rule.
- **Restatement.** If the code name says it, the doc doesn't have to.
- **Migration history.** `X is gone, its fields moved to Y` teaches nothing once X is unreachable.
- **Worked-example walkthroughs.** One clause naming the example beats a paragraph tracing it.
- **Emphasis the sentence already carries.** Bold on every third phrase reads as none.

## Claims that rot

Delete anything that dates itself:

- `today`, `not yet`, `so far`, `currently`
- counts and states of the data — `0 on every row today`, `most are empty`
- predictions about unwritten code
- blanket claims covering fields you didn't enumerate (`the rest is unitless`) — they are usually false when written and always false later

A field that exists but nothing reads gets a bare `unread`, not a story about when it will be wired.

## Structure

A field→destination mapping belongs in a table, not eight bullets. Columns: the field, what it lands on, a short note. Blank notes are fine.

Prose bullets carry the gotchas the table can't: invariants, ordering constraints, the failure mode of doing it the obvious way.

## Section boundaries

If another section owns a topic, cut it and leave a bare cross-reference — `see Gold`, not a one-line summary of Gold. A summary is a second copy that drifts.

Check the cross-reference resolves. If the target section doesn't actually contain the fact, add it there in the same change.

## Orphans

When a fact is worth keeping but doesn't belong in this section, don't silently drop it. In the draft's cut list, name it and propose a home. Move it once the user agrees.
