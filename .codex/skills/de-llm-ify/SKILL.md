---
name: de-llm-ify
description: Critique writing that feels AI-generated and revise it toward sharper, more human prose without thinning detail. Use when text sounds generic, symmetrical, over-smoothed, over-explained, hedge-heavy, or padded with abstract filler, and the goal is to keep substance while removing the synthetic feel.
---

# De-LLM-ify

Read the draft like a skeptical human editor, not a helpful assistant.

Start by identifying what makes the writing feel synthetic. Be concrete. Point to the exact words, sentence habits, or structural habits causing the problem.

Common failure modes:

- symmetry: balanced triples, mirrored clauses, tidy rhythmic lists
- abstraction: ideas named instead of shown
- filler transitions: `that said`, `with that in mind`, `in order to`, `it is important to`
- hedge and mush: `somewhat`, `fairly`, `quite`, `tends to`, `can often`
- fake emphasis: adjectives doing the work of specifics
- over-explaining: repeating the point in slightly different wording
- generic competence voice: polished, neutral, bloodless phrasing
- predictable cadence: every sentence landing with the same length and weight

When revising:

1. keep the meaning
2. keep the useful detail
3. cut summary words before cutting concrete words
4. replace abstraction with nouns, verbs, and texture
5. vary sentence lengths when the draft is too even
6. allow a little edge, compression, or asymmetry when it improves the read
7. do not turn blunt prose into brand prose

Prefer edits like:

- `has a lot of visual variety` -> `packs the frame with clashing shapes`
- `feels more dynamic and engaging` -> name what changes on screen
- `provides the player with` -> `gives the player`
- `in order to` -> `to`

Avoid these failure modes in your own rewrite:

- replacing one generic phrase with another
- flattening the writer's tone into your house style
- deleting the specific detail that made the passage useful
- making everything shorter if the real problem is vagueness, not length

Default output shape:

1. `What feels AI-written`
2. `Line edits`
3. `Rewrite`

In `What feels AI-written`, give a short blunt diagnosis.

In `Line edits`, call out the biggest phrase-level problems.

In `Rewrite`, provide a cleaned version that keeps the original level of detail unless the user asks for a heavier cut.

If the draft is already fine, say so plainly. Do not invent problems to satisfy the skill.
