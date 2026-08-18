# CandyDataEditor

A .NET MAUI (.NET 8) app that provides a Blazor-based rich editor for game data stored in SQLite.
This README focuses on service registration, the database-path event, and how the TipTap editor, spellcheck initializer, and ribbon UI integrate.


## Summary

- `SqliteDataService` is the central DB service — use `SetDatabasePathAsync` and listen to `OnDatabasePathChanged`.
- `GameDictionaryService` manages dictionary files; `SpellcheckInitializer` hydrates JS with `setGlobalGameDictionary`.
- `TipTapEditor.razor` relies on `tiptap-interop.js` for lifecycle, commands, spellcheck integration, and content serialization; use the `TipTapEditor` component directly in pages and rely on its `Value`/`MarkdownValue` bindings for persistence.

---

## Key services (DI)

Services are registered in `MauiProgram.cs`:

- `GameDictionaryService` — loads and syncs dictionary files from `wwwroot/dictionaries` into an AppData folder; exposes `SyncAndLoadAllDictionariesAsync()` and `AddWordToDictionaryFileAsync(...)`.
- `SqliteDataService` — the main database service that opens/closes/queries the SQLite DB.
  - Exposes `SetDatabasePathAsync(string path)`, `CloseDatabaseAsync()`, and many query helpers.
  - Raises the `OnDatabasePathChanged` event whenever the active DB path changes (including when closed).

---

## Database path event: `OnDatabasePathChanged`

### Declaration
`public event Func<string, Task>? OnDatabasePathChanged;` (in `SqliteDataService`)

### Fired in
  - `SetDatabasePathAsync(string path)` — invoked after changing the path and adding to recents.
  - `CloseDatabaseAsync()` — invoked with `string.Empty`.

### Typical usage

Components (for example `MainLayout.razor`) subscribe to this event to refresh the UI when the active DB changes:

```csharp
// MainLayout.razor (concept)
protected override async Task OnInitializedAsync()
{
    DbService.OnDatabasePathChanged += HandleDatabaseChanged;
    await RefreshDatabaseObjects();
}

public void Dispose()
{
    DbService.OnDatabasePathChanged -= HandleDatabaseChanged;
}
```
  - `HandleDatabaseChanged(string newPath)` should clear or reload object lists and navigate as needed. The project already follows this pattern in `MainLayout.razor`.

---

## TipTap editor integration

### Files of interest
- `wwwroot/js/tiptap-interop.js` — JS interop glue used by the Blazor editor.
- `Components/TipTapEditor.razor` — the Blazor wrapper component that the UI uses.

### How they fit together
`TipTapEditor.razor` creates a .NET-to-JS callback (`DotNetObjectReference`) and calls:
This calls `window.initTipTap` in `tiptap-interop.js`.

### `tiptap-interop.js` responsibilities
- Creates and stores TipTap instances in `window.tiptapInstances[elementId]`.
- Attaches contextmenu handling to integrate the custom spellchecker.
- Provides JS functions that Blazor calls:
  - `initTipTap(elementId, initialContent, dotnetHelper)`
  - `execTipTapCommand(elementId, commandName, value)` — used by toolbar buttons.
  - `getTipTapMarkdown(elementId)` and `getTipTapHtml(elementId)` — used when switching views.
  - `setTipTapContentFromMarkdown(elementId, markdownText)` / `setTipTapContentFromHtml(...)`
  - `replaceTipTapRange(elementId, from, to,originalWord, newText)` — used when a spellcheck replacement occurs.
  - `destroyTipTap(elementId)` — cleanup.
- Exposes global spell/dictionary helpers:
  - `setGlobalGameDictionary(wordList)` — used to hydrate JS spellchecker memory and refresh TipTap plugin decorations.

### TipTap <-> Blazor lifecycle
`TipTapEditor.razor` wires JSInvokablemethods:
- `OnContentChanged(html, markdown)` — invoked by the `onUpdate` handler in `initTipTap`.
- `OpenSpellcheckContextMenu(...)` — invoked by the spellcheck plugin when a right-click occurs; this shows a Blazor context menu for suggestions.
- `OnEditorBlurred()` — invoked by `onBlur`.
- On dispose, `TipTapEditor` calls `destroyTipTap` to remove the editor instance.

---

## SpellcheckInitializer

### File:
`Components/SpellcheckInitializer.razor` — included in `MainLayout.razor`.

### What it does
- On first render it calls `DictionaryService.SyncAndLoadAllDictionariesAsync()` to load all dictionaries from disk (and seed from `wwwroot/dictionaries` if necessary).
- It then calls the JS hook to hydrate global JS memory:
- This makes word lookups and suggestions available to the TipTap spellchecker plugin (the plugin uses `window.activeGameWords` and `window.spellchecker`).

### Usage
- The project includes `<SpellcheckInitializer />` in `MainLayout.razor` to ensure dictionaries are loaded once at startup for the Blazor session. No additional wiring required.

---

## RibbonMenuBar component

### File
`Components/RibbonMenuBar.razor`.

### Responsibilities
- Provides the top ribbon UI for opening, saving, exporting, and settings.
- Injects `SqliteDataService` and `GameDictionaryService` (via DI).
- Calls `await DbService.SetDatabasePathAsync(selectedFilePath);`
- This triggers `SqliteDataService.OnDatabasePathChanged` so any subscriber (like `MainLayout`) updates immediately.
- Exposes dictionary UI (lists dictionary files, counts, add/remove words) and calls `DictionaryService.AddWordToDictionaryFileAsync(...)`. After updates, it calls:
- so the TipTap spellchecker is refreshed.

### Subscription pattern:
- UI components that need to react to DB changes should subscribe to `DbService.OnDatabasePathChanged` and unsubscribe in `Dispose()` (see `MainLayout.razor`).

---

## Where JS lives and build notes
- `wwwroot/js/tiptap-interop.js` — TipTap interop; imports `spellcheck.js`.
- The `CandyDataEditor.csproj` defines a build step `BuildTipTapBundle` that builds the TipTap bundle found under `../TipTap` to `wwwroot/js/tiptap-bundle.js`. Keep Node/npm available when building TipTap assets.

### Quick troubleshooting

- If spellcheck suggestions don't appear after adding words, ensure `setGlobalGameDictionary(...)` is called (SpellcheckInitializer and RibbonMenuBar do this).
- If multiple editors are used, each TipTap instance is tracked in `window.tiptapInstances[elementId]`.
- Always dispose editors (the component calls `destroyTipTap`) to avoid memory leaks.


