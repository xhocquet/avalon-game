---
name: gemini-chrome
description: Use Gemini through the local debug-enabled Chrome session. Trigger when the user wants Codex to open Gemini, switch to an existing Gemini tab, verify the Chrome remote connection, submit prompts in Gemini, generate images in the browser, inspect or download Gemini outputs, or operate on Gemini through the chrome-remote-control plugin and DevTools endpoint.
---

# Gemini Chrome

Use the local Chrome remote-control path first.

## Workflow

1. Check that `chrome-remote-control@personal` is enabled when browser control is needed.
2. Verify Chrome DevTools is reachable at `http://127.0.0.1:51495/json/version`.
3. Inspect `http://127.0.0.1:51495/json/list` for existing Gemini pages.
4. Prefer reusing an existing Gemini tab unless the task should be isolated from prior conversation state.
5. If a fresh chat is safer, open one with `http://127.0.0.1:51495/json/new?https://gemini.google.com/app`.
6. Prefer the Chrome remote MCP tools if they are available in the session.
7. If those MCP tools are not exposed, fall back to the DevTools HTTP endpoints for connection checks, page discovery, opening Gemini, and closing extra tabs.

## Prompting

- Inspect the current page before assuming selectors. Gemini UI details move.
- The composer a11y uid is found by taking a snapshot and grepping for `textbox` — look for `uid=X textbox "Enter a prompt for Gemini"`. Use `click` on that uid then `type_text` with `submitKey: "Enter"` to send.
- Do NOT use `fill` on the Gemini composer — it does not trigger generation. Always use `click` → `type_text`.
- When editing the composer directly, prefer text-only updates and input/change events. Avoid HTML assignment into the editor.
- After sending, the tab URL changes from `/app` to `/app/<id>` once Gemini accepts the request. Watch for that rather than waiting for generation text.

## Parallel Image Generation

- For multiple simultaneous generations: open one fresh tab per generation with `new_page`, submit each prompt in sequence (select page → take snapshot → grep for textbox uid → click → type_text), then wait for all tabs to finish before downloading.
- Snapshots are large and auto-saved to `~/.claude/gemini-snap.txt`. Skip full reads — grep the saved snapshot file for `textbox` or `Download` to get the uid you need directly.
- Tab selection pattern: `select_page` by page number, then `take_snapshot` (output is auto-saved), then grep the file.

## Image Generation

- For image requests, use a fresh Gemini tab unless the user explicitly wants the current thread reused.
- Prefix the prompt with `Generate an image:` so Gemini routes it to the image model immediately.
- Generation is complete when the tab URL has settled to `/app/<id>` and a `Download full size image` button is present in the snapshot.
- Do NOT use `wait_for` with long timeouts — ask the user to confirm images are done instead, then proceed.

## Downloading Generated Images

**Fastest path — click the built-in download button:**

1. `select_page` to the target tab.
2. `take_snapshot` — the output file path is printed in the result.
3. Grep the snapshot file for `Download`:
   ```
   grep -n "Download" ~/.claude/gemini-snap.txt
   ```
   This returns a line like `uid=X_Y button "Download full size image"`.
4. `click` that uid. The file downloads to `~/Downloads/` as `Gemini_Generated_Image_<hash>.png`.
5. Immediately rename it to something short and descriptive before doing anything else:
   ```powershell
   Rename-Item "$env:USERPROFILE\Downloads\Gemini_Generated_Image_<hash>.png" "gemini-<short-name>.png"
   ```
   Use a name that reflects the prompt or intended use (e.g. `gemini-draft-lobby.png`, `gemini-skinwalker-3.png`). This keeps downstream commands and `/inbound` calls readable.

Repeat for each tab. The button uid prefix changes per tab (matches the page's internal context number) but the label is always `"Download full size image"`.

**Do NOT:**
- Try to fetch blob URLs with `evaluate_script` + `fetch` — blob URLs expire and the fetch will fail.
- Use anchor click tricks to download lh3.googleusercontent.com URLs — the tab navigates away instead of saving.
- Use `wait_for` with multi-minute timeouts — it blocks and the user cannot interrupt cleanly.

## After Downloading

- Pass the renamed file paths directly to `/inbound` for compression and MDX placement.

## Cleanup

- Do not leave behind extra blank tabs or duplicate Gemini tabs.
- If you opened temporary Gemini tabs to probe or isolate a prompt, close them when finished and keep only the tab that matters for the result.
- Be explicit in the final reply about what tab was kept or closed when cleanup mattered.

## Chrome MCP Failure Protocol

**Stop immediately and report** if any of these happen — do not retry, do not work around:

- `list_pages` returns an error or empty result
- `take_snapshot` fails or returns garbage
- `select_page` returns an error
- Any MCP tool throws a connection error

When stopped, tell the user: "Chrome MCP is not responding. Restart the `chrome-remote-control@personal` MCP server and then re-run the command."

**Do not attempt more than 2 sequential MCP calls without a successful result.** If two calls in a row fail, stop.

The user will restart the MCP server themselves — spinning through retries wastes context and money.

## Notes

- Target `https://gemini.google.com/app`.
- Be explicit when the browser is reachable but MCP tool exposure is missing. Those are different failure modes.
- The reusable Chrome server adds small randomized action jitter. Keep that behavior for page-driving actions.
- Keep user-facing output short: report whether Chrome is reachable, whether Gemini is open, whether the prompt was submitted, and what page id or URL was selected.
