---
name: inbound
description: Process asset files for the project GDD workflow. Use when the user provides file paths and context for new images or other visual assets that need to be (1) renamed to a simpler stable filename and copied into the correct `docs/assets/...` folder, (2) moved to `trash/` from their source location, and (3) added to the right MDX section. Conversion to webp happens automatically at build/dev time via the predev/prebuild script.
---

# Inbound

Process files provided by the user in a fixed three-step order.

The user will pass one or more file paths and describe what the files are for. Use that context to decide where the assets belong and where they should appear in the MDX.

## Workflow

1. Rename them to simpler stable names and copy into the correct `docs/assets/...` location.
2. Move ALL original source files to `trash/` — including any that were not added to the MDX.
3. Update the relevant MDX file to reference the new files in the right section.

> **Note:** Conversion to webp is handled automatically. The `predev`/`prebuild` script (`scripts/convert-assets.mjs`) scans `docs/assets/` for non-webp images, converts them to webp at quality 82, updates MDX references, and moves the originals to `trash/`. Copy files in whatever format they arrive — no manual conversion needed.

## Step 1: Rename And Copy To The Right Asset Folder

Use the user's context to decide the destination folder under `docs/assets/`.

General rules:

- Reuse an existing nearby asset folder when one already fits the content.
- Keep names sequential if the destination set already uses numbered concept art files.
- Favor short stable names that reflect the local pattern, not the generator's original filename.
- Rename the file as part of the copy step. Do not keep the generator's original filename in `docs/assets/...` unless the user explicitly wants that.
- Copy into `docs/assets/...` using the simplified name.

Typical examples in this repo:

- world references: `docs/assets/world/...`
- style-guide references: `docs/assets/style-guide/...`
- character references: `docs/assets/characters/...`
- UI references: `docs/assets/ui/...`
- items: `docs/assets/items/...`
- textures: `docs/assets/textures/...`

### UI asset naming (`docs/assets/ui/`)

Every file in `docs/assets/ui/` must end with a `-N` variant suffix starting at `-1`.

- No unsuffixed base names. `banner-1.png` is wrong; `banner-1-1.png` is correct.
- The original concept PNG is always `-1`.
- Generated `webp` iterations start at `-2`.
- If a group already has `webp` files numbered from `-1`, shift them up before inserting the new `-1`.

```
bone-border-1.png    ✓  first in group
bone-border-2.webp   ✓  second in group
bone-border.png      ✗  not allowed
```

When adding to an existing group, continue the sequence after the inserted concept PNG.

## Step 2: Move ALL Original Source Files To Trash

After the renamed files exist in `docs/assets/...`, move every original source file to `trash/` at the project root — including files that were not selected for the MDX.

All concept art should be preserved, never deleted. The user keeps everything.

- Always use `mv` to `trash/` — never `rm`, `Remove-Item`, or any permanent deletion.
- Move ALL provided source files, not just the ones that were added to the MDX.
- This applies to all source files regardless of where they came from: `Downloads/`, a temp folder, another project directory, anywhere.
- Only skip the move if the user explicitly asks to keep the file in place.
- Make sure the MDX points at the repo-local asset before moving the source.

## Step 3: Update The MDX

Edit the relevant MDX file after the asset files are in place. The user may pass a file path with `@` to indicate which section to edit (e.g. `@docs/sections/07-menu-flow.mdx`), or describe the section in plain language.

- Search for the section the user described.
- Follow the local MDX pattern already used there, such as `Figure`, `TwoColumn`, plain `img`, or an existing grid.
- Insert the new references with repo-local paths like `/docs/assets/...`.
- Keep the surrounding text accurate and minimal.
- Do not invent new canon, lore, or art-direction conclusions just to justify the images.

Prefer changing the existing section rather than creating a new one unless the user asked for a new section.

## Verification

After edits:

- Confirm the new asset paths exist.
- Confirm the MDX points at the copied files, not the original source paths.
- Confirm the final asset names are the simplified repo-local names, not the source generator names.
- Confirm ALL original source files were moved to trash/, including any not added to the MDX.
