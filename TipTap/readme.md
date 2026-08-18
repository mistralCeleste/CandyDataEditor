# TipTap bundle

This small TipTap bundle project contains the custom editor entrypoint and several ProseMirror / TipTap extensions used by the CandyDataEditor app.
The bundle is built into the MAUI app's `wwwroot` and consumed by the Blazor `TipTapEditor` component via `tiptap-interop.js`.

## Summary

### Development & build
1. From `TipTap/` run:
   - `npm install`
   - `npx esbuild tiptap-entry.js --bundle --outfile=../CandyDataEditor/wwwroot/js/tiptap-bundle.js`
2. The MAUI project already includes an MSBuild target that runs this automatically during build if needed.

### Useful files
- `tiptap-entry.js` — entrypoint that defines and exports the extensions and a `createEditor` helper.
- `package.json` — dependency list for TipTap and ProseMirror packages (not included here; use `npm install` to populate).
- `CandyDataEditor/wwwroot/js/tiptap-bundle.js` — generated artifact consumed by the MAUI Blazor code.

### Location
- Source `TipTap/tiptap-entry.js` to built output (project build step) `CandyDataEditor/wwwroot/js/tiptap-bundle.js`
- Build step in `CandyDataEditor.csproj` runs `npm install` (if needed) and `npx esbuild tiptap-entry.js --bundle --outfile=../CandyDataEditor/wwwroot/js/tiptap-bundle.js`

### Prerequisites
- Node (for local development and the MSBuild step that bundles the JS).
- `npm install` inside the `TipTap/` folder to populate dependencies before building.

### How the bundle is consumed
- The MAUI Blazor layer expects a global `window.TipTap` object that exposes the Editor constructor, StarterKit, and each custom extension.
- `CandyDataEditor/wwwroot/js/tiptap-interop.js` uses these exports to create TipTap instances and to wire Blazor JS interop callbacks.

### What this bundle registers / exports
- window.TipTap = { Editor, StarterKit, Markdown, GameDictionary, GameIconDecoration, KeywordMark, ActionHeading, TildeList, NativeCustomSpellchecker }
- `createEditor(elementId, initialContent, dotnetRef, initialDictionary)` — a helper factory used during development; the Blazor interop uses a similar init routine inside `tiptap-interop.js`.

---

## Custom extensions (what each one does)

### NativeCustomSpellchecker
A ProseMirror Plugin Extension that scans document text nodes and applies inline decorations to words not found in the active dictionary.

- Behavior:
  - Uses `WORD_REGEX` to find word tokens and skips ligature patterns (bracketed icons and special arrow patterns).
  - Looks up known words through `window.activeGameWords` or the `window.spellchecker` object.
  - Adds `Decoration.inline` for misspelled words with CSS class `custom-misspelled-word` and `data-misspelled="true"`.

Notes:
- Controlled by `window.DEBUG_SPELLCHECK` for optional debug logging.
- Integrates with the app-level spellchecker code which provides suggestions and context-menu interactions.

---

### GameIconDecoration
A Decoration extension (ProseMirror Plugin) that identifies and visually styles "game icon" ligatures in text.
  - Patterns:
  - Bracketed tokens like `[place]`, `[mob]`, `[defense]`, etc.
  - Arrow ligatures: `->`, `<-`, `--`
  - Behavior: Adds inline decorations with CSS class `game-icon-ligature` for matched ranges so CSS can provide ligature font rendering or replacement icons.

---

### KeywordMark
A Mark extension that highlights domain-specific "keywords" in text.
  - Syntax: `==keyword==`
  - Purpose: Inline mark for highlighting domain-specific "keywords".
  - Behavior:
    - Provides `toggleKeyword`, `setKeyword`, `unsetKeyword` commands.
    - Parses and serializes via `<span class="keyword-mark">...</span>` and input/paste rules to automatically convert `==...==` to the mark.
    - Renders with the `keyword-mark` class for styling.

---

### ActionHeading
A Block node extension that represents game "action" blocks as semantic block nodes (distinct from normal headings).
  - Syntax: Prefix lines with `@ ` (level 1) and `@@ ` (level 2)
  - Purpose: Represent game "action" blocks as semantic block nodes (distinct from normal headings).
  - Behavior:
    - Node has `level` attribute (1 or 2) and renders as `div.action-menu-parent` or `div.action-menu-child`.
    - Provides `toggleActionHeading` and `setActionHeading` commands for toolbar use.
    - Input rules convert `@ ` / `@@ ` typed at the start of a block into the action node.
    - Includes paste transform plugin: when pasting text that begins with `@ ` or `@@ `, it converts the pasted block into `actionHeading` nodes while stripping the raw prefix.

---

### TildeList
An Extension (input rule) that provides a convenience input rule to create a bullet list using `~` like the app's markdown convention.
  - Syntax: `~ ` at start of line
  - Purpose: Convenience input rule to create a bullet list using `~` like the app's markdown convention.
  - Behavior: When `~ ` is typed at start of a new line, it triggers the editor to wrap the block in a bullet list.

---

### GameDictionary
A Decoration extension (ProseMirror Plugin) that marks known game-specific words in the editor so they are excluded from spellcheck or rendered with special styling.
- Behavior:
  - Accepts an options `words: []` (an initial array) and searches text nodes for those words.
  - Creates decorations that set `spellcheck: false` and `class: custom-game-word` for matched words so browser/native spellcheck and the custom spell plugin can avoid flagging them.
  - Uses a case-insensitive, word-boundary regex built from the provided list; escapes special regex characters when building the pattern.

Integration notes and tips
- Dictionary hydration: The app loads dictionaries server-side (C# `GameDictionaryService`) and pushes them into JS via `setGlobalGameDictionary(...)` (see `tiptap-interop.js`). That triggers a refresh of plugin decorations across all TipTap instances.
- Editor lifecycle: Interop code stores editor instances in `window.tiptapInstances[elementId]`. Always call the provided destroy routine on component dispose to remove the instance.
- Commands and toolbar: The interop exposes `execTipTapCommand(elementId, commandName, value)` to toggle marks, insert icons, toggle action headings, undo/redo, and list toggles. These commands map to the custom extensions where appropriate.
- Paste & serialization: `ActionHeading` contains robust paste transforms so copying content into the editor preserves action blocks. Serialization to the project's custom markdown is handled at the interop layer (not in each extension): the bundle exposes the structural nodes and marks so serialization plugins can do a reliable round-trip.
