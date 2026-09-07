---
name: moodboard-ui-kit
description: >
  Extract a reusable UI/style kit from a mood board image and its source
  prompt, then render that kit inside repo content such as MDX/HTML style
  boards. Use when Codex needs to pull a palette from a mood board, derive
  tokens like outline weight/corner radius/surface treatment, add a compact
  visual board below an image, include generated asset samples, or write
  standalone prompts for another image model to generate matching assets.
---

# Moodboard UI Kit

Build a compact, reusable style board from an existing mood board and prompt pair. Keep the result grounded in the source image rather than inventing canon.

## Workflow

1. Locate the source block first.
   Prefer the document image and adjacent prompt text already in the repo.
   Read the surrounding section before editing so the board fits the existing structure.

2. Extract tokens from the image, not from generic taste.
   Pull:
   - primary palette colors
   - paper/background color
   - outline weight and darkness
   - corner radius range
   - shadow style
   - texture direction
   - pattern language
   - shape language

3. Keep the framing exploratory unless the repo already treats the style as final.
   Use labels like `extracted`, `exploratory`, or `generated sample` when the direction is still being tested.
   Do not harden brainstorm material into canon without an explicit user request.

4. Render the board near the source mood board.
   In MDX/GDD repos, place the board directly below the relevant mood board image.
   Match the current repo pattern: inline sections, no `details` wrapper by default, and prompt text shown openly below the board.
   Prefer editing shared CSS/components instead of stuffing large inline styles into one section.

5. Include real, reusable outputs.
   A strong board usually contains:
   - a palette section
   - a token section
   - a generated-assets section or at least one generated sample image
   - standalone prompts for another image model

## Output Rules

Follow these defaults unless the user asks for something else:

- Render palettes as real palette references, not scattered color boxes.
  Prefer a single palette container with stacked horizontal color rows and the hex code aligned inside each row.
  Do not add a separate `Palette` heading inside that block unless the user asks for it.
  Do not add color-name labels when the current layout is hex-only.
- Do not generate HTML preview widgets like chips, cards, mini-scenes, or pattern tiles unless the user explicitly asks for them.
- Use shared CSS classes for reusable styling.
- Prefer the existing open layout over collapsible containers.
- When generated assets exist on disk, show them in the board instead of only describing them.
- Keep copy short.
  The board should read like a design artifact, not a long essay.

## Prompt Writing For External Image Models

Write each asset prompt as a standalone prompt. Do not rely on a shared footer or modifier block that the user has to merge manually later.

Each prompt should usually contain:
- subject and asset type
- palette direction
- line quality
- fill/shading style
- texture guidance
- composition or isolation guidance
- output constraints
- negative constraints

Default rendering language for this project family:
- crisp cartoon line art
- solid flat fills
- cel-style separation between shapes
- medium dark outlines
- bright candy colors on warm paper
- no watercolor
- no gouache
- no soft painterly texture

If the user reports that generated assets feel too painterly, sharpen the prompt rather than changing the palette first.

Read [references/prompt-patterns.md](references/prompt-patterns.md) when you need reusable prompt skeletons or tuning phrases.

## Repo Editing Guidance

When applying this skill in this repo:
- Edit the MDX source first.
- Add presentation styles in shared CSS.
- Preserve the repo's distinction between brainstorm material and finalized canon.
- Keep the board visually useful but lightweight.
- Reuse the current `moodboard-kit` structure from Prompts 1 and 2 instead of inventing a new board layout.
- Keep prompt labels as plain inline text blocks, not dropdown summaries.
- Do not add synthetic preview/demo fragments to the board by default.

When adding generated image samples:
- use the actual repo asset path
- add an explicit alt text
- label the image as a generated sample or generated asset

## Checklist

Before finishing, verify that the board:
- clearly reflects the actual mood board image
- contains a usable palette and not just decorative swatches
- includes explicit token values where possible
- includes standalone external-model prompts
- avoids generic filler language
- fits the surrounding document structure
