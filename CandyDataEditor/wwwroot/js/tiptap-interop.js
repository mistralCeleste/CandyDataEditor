// wwwroot/js/tiptap-interop.js

// Global Toolbar Commands
window.execTipTapCommand = function (elementId, commandName, value) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    switch (commandName) {
        case 'keyword':
            // Toggle mark specifically on inline selection
            editor.chain().focus().toggleMark('keyword').run();
            break;
        case 'bold':
            editor.chain().focus().toggleBold().run();
            break;
        case 'italic':
            editor.chain().focus().toggleItalic().run();
            break;
        case 'actionParent':
            editor.chain().focus().toggleActionHeading({ level: 1 }).run();
            break;
        case 'actionChild':
            editor.chain().focus().toggleActionHeading({ level: 2 }).run();
            break;
        case 'undo':
            editor.chain().focus().undo().run();
            break;
        case 'redo':
            editor.chain().focus().redo().run();
            break;
        case 'insertIcon':
            if (!value) return;

            // If the value is already an arrow or dash (e.g. "->" or "--"), insert directly.
            // Otherwise, wrap named tags in brackets (e.g. "heroes" -> "[heroes]")
            const textToInsert = (value === '->' || value === '<-' || value === '--' || value.startsWith('['))
                ? value
                : `[${value}]`;

            editor.chain().focus().insertContent(textToInsert).run();
            break;
    }
};

window.tiptapInstances = window.tiptapInstances || {};

window.initTipTap = function (elementId, initialContent, dotnetHelper) {
    const container = document.getElementById(elementId);
    if (!container) return;

    // Attach Right-Click Context Menu Listener for Misspelled Words
    container.addEventListener('contextmenu', (event) => {
        const target = event.target.closest('.custom-misspelled-word');
        if (target) {
            event.preventDefault();
            event.stopPropagation();

            const word = target.innerText.trim();
            const rect = container.getBoundingClientRect();

            // Calculate relative offset inside wrapper
            const clickX = event.clientX - rect.left;
            const clickY = event.clientY - rect.top;

            const editor = window.tiptapInstances[elementId];
            if (editor && editor.view) {
                // Find exact DOM position of the misspelled span inside ProseMirror
                const pos = editor.view.posAtDOM(target, 0);

                if (pos !== null && pos !== undefined) {
                    const from = pos;
                    const to = pos + word.length;

                    dotnetHelper.invokeMethodAsync('OpenSpellcheckContextMenu',
                        word, from, to, clickX, clickY);
                }
            }
        }
    });

    // Close context menu when clicking anywhere outside
    window.addEventListener('click', (event) => {
        // If the click is outside any open spellcheck menu, tell Blazor to close it
        if (!event.target.closest('.spellcheck-context-menu')) {
            dotnetHelper.invokeMethodAsync('CloseSpellcheckContextMenu');
        }
    });

    const editor = new window.TipTap.Editor({
        element: container,
        content: parseCustomMarkdownToHtml(initialContent),
        extensions: [
            window.TipTap.StarterKit,
            window.TipTap.Markdown,
            window.TipTap.KeywordMark,
            window.TipTap.ActionHeading,
            window.TipTap.GameIconDecoration,
            window.TipTap.NativeCustomSpellchecker,
        ],
        onUpdate: ({ editor }) => {
            const html = editor.getHTML();
            const markdown = serializeDocumentToCustomMarkdown(editor);
            dotnetHelper.invokeMethodAsync('OnContentChanged', html, markdown);
        },
    });

    if (!window.tiptapInstances) {
        window.tiptapInstances = {};
    }
    window.tiptapInstances[elementId] = editor;
};

// Helper to match replacement word capitalization to original word
function matchCase(original, replacement) {
    if (!original || !replacement) return replacement;

    // ALL UPPERCASE (e.g. "PENINSULLA" -> "PENINSULA")
    if (original === original.toUpperCase() && original.length > 1) {
        return replacement.toUpperCase();
    }

    // Title / Capitalized First Letter (e.g. "Peninsulla" -> "Peninsula")
    const firstChar = original.charAt(0);
    if (firstChar === firstChar.toUpperCase() && firstChar !== firstChar.toLowerCase()) {
        return replacement.charAt(0).toUpperCase() + replacement.slice(1);
    }

    // Default lowercase (e.g. "peninsulla" -> "peninsula")
    return replacement.toLowerCase();
}

// JS Helper to accurately replace the word range in TipTap
window.replaceTipTapRange = function (elementId, from, to, originalWord, newText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    // Match original casing (e.g., Peninsulla -> Peninsula)
    const casedText = matchCase(originalWord, newText);

    // Execute replacement transaction over exact range
    editor.chain()
        .focus()
        .deleteRange({ from, to })
        .insertContentAt(from, casedText)
        .run();
};

// JS Helper to replace word on suggestion click
window.replaceTipTapRange = function (elementId, from, to, newText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    editor.chain().focus().insertContentAt({ from, to }, newText).run();
};

// 1. Maintain global Set for O(1) checks
window.activeGameWords = new Set();
// Maintain array for fast suggestion searches
window.activeGameWordsArray = [];

window.updateGameDictionary = function (elementId, customWordList) {
    if (!customWordList) return;

    // Fast batch load into Set
    const cleanList = customWordList.map(w => w.toLowerCase().trim());
    window.activeGameWords = new Set(cleanList);
    window.activeGameWordsArray = cleanList;

    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor || !editor.view) return;

    // Trigger ProseMirror decoration refresh
    const { state, dispatch } = editor.view;
    dispatch(state.tr.setMeta('dictionaryUpdated', Date.now()));
};

// 2. ULTRA-FAST JS Levenshtein Suggestion Engine (Runs in 2ms)
window.getSpellSuggestions = function (misspelledWord, maxSuggestions = 5) {
    if (!misspelledWord || window.activeGameWordsArray.length === 0) return [];

    const rawTarget = misspelledWord.trim();
    const target = rawTarget.toLowerCase();
    const firstChar = target[0];
    const targetLen = target.length;

    // Filter candidates by length and first character
    const candidates = window.activeGameWordsArray.filter(w => {
        if (Math.abs(w.length - targetLen) > 3) return false;
        return w[0] === firstChar || fastLevenshtein(target, w) <= 2;
    });

    // Score candidates by Levenshtein distance
    const scored = candidates.map(w => ({
        word: w,
        dist: fastLevenshtein(target, w)
    }));

    scored.sort((a, b) => a.dist - b.dist || Math.abs(a.word.length - targetLen) - Math.abs(b.word.length - targetLen));

    // Return top unique suggestions WITH MATCHED CASE
    const results = [];
    const seen = new Set();

    for (const item of scored) {
        if (item.dist > 3) break;

        // Apply case matching to suggestion string
        const casedWord = matchCase(rawTarget, item.word);

        if (!seen.has(casedWord.toLowerCase())) {
            seen.add(casedWord.toLowerCase());
            results.push(casedWord);
            if (results.length >= maxSuggestions) break;
        }
    }

    return results;
};

// Fast Levenshtein implementation in pure JS
function fastLevenshtein(a, b) {
    if (a === b) return 0;
    if (a.length === 0) return b.length;
    if (b.length === 0) return a.length;

    const matrix = [];
    for (let i = 0; i <= b.length; i++) matrix[i] = [i];

    for (let j = 0; j <= a.length; j++) matrix[0][j] = j;

    for (let i = 1; i <= b.length; i++) {
        for (let j = 1; j <= a.length; j++) {
            if (b.charAt(i - 1) === a.charAt(j - 1)) {
                matrix[i][j] = matrix[i - 1][j - 1];
            } else {
                matrix[i][j] = Math.min(
                    matrix[i - 1][j - 1] + 1,
                    Math.min(matrix[i][j - 1] + 1, matrix[i - 1][j] + 1)
                );
            }
        }
    }

    return matrix[b.length][a.length];
}

// 1. Converts Raw Markdown (from SQLite) -> HTML elements for TipTap on initial load
function parseCustomMarkdownToHtml(md) {
    if (!md) return '';

    let html = md;

    // Convert ==keyword== to <span class="keyword-mark">keyword</span>
    html = html.replace(/==([^=]+)==/g, '<span class="keyword-mark">$1</span>');

    // Convert lines starting with @@ to child action node
    html = html.replace(/^@@\s+(.*$)/gim, '<div class="action-menu-child">$1</div>');

    // Convert lines starting with @ to parent action node
    html = html.replace(/^@\s+(.*$)/gim, '<div class="action-menu-parent">$1</div>');

    return html;
}

// 2. Converts TipTap Editor State -> Clean Raw Game Markdown
function serializeDocumentToCustomMarkdown(editor) {
    if (!editor.storage || !editor.storage.markdown) return '';

    // Get base markdown export from tiptap-markdown
    let markdown = editor.storage.markdown.getMarkdown();

    // 1. Convert Level 1 Action Divs (with any attributes) to "@ ActionText"
    // Matches: <div level="1" class="action-menu-parent">Text</div> or similar
    markdown = markdown.replace(/<div[^>]*class="action-menu-parent"[^>]*>([\s\S]*?)<\/div>/gi, (match, p1) => {
        const cleanText = p1.replace(/<p>/gi, '').replace(/<\/p>/gi, '').trim();
        return `@ ${cleanText}`;
    });

    // 2. Convert Level 2 Action Divs (with any attributes) to "@@ ActionText"
    markdown = markdown.replace(/<div[^>]*class="action-menu-child"[^>]*>([\s\S]*?)<\/div>/gi, (match, p1) => {
        const cleanText = p1.replace(/<p>/gi, '').replace(/<\/p>/gi, '').trim();
        return `@@ ${cleanText}`;
    });

    // 3. Convert <span class="keyword-mark">word</span> back to ==word==
    markdown = markdown.replace(/<span[^>]*class="keyword-mark"[^>]*>([\s\S]*?)<\/span>/gi, '==$1==');

    // 4. Unescape ligature brackets: \[mob\] -> [mob]
    markdown = markdown.replace(/\\\[([a-zA-Z0-9]+)\\\]/g, '[$1]');

    return markdown.trim();
}

window.getTipTapSelectedText = function (elementId) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return '';

    const { from, to } = editor.state.selection;
    if (from === to) return ''; // No selection

    // Get plain text inside selection and strip surrounding brackets or spaces if present
    let selectedText = editor.state.doc.textBetween(from, to, ' ');
    return selectedText.replace(/^\[|\]$/g, '').trim();
};

window.getTipTapMarkdown = function (elementId) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return '';

    return serializeDocumentToCustomMarkdown(editor);
};

// Gets the current HTML directly from TipTap instance
window.getTipTapHtml = function (elementId) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return '';

    return editor.getHTML();
};

window.setTipTapContentFromMarkdown = function (elementId, markdownText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    // Use TipTap's internal markdown extension command if available, or set content with markdown emit
    if (editor.commands.setContent) {
        // Passing emitUpdate = false prevents firing onUpdate back to Blazor
        editor.commands.setContent(markdownText || '', false, {
            parseOptions: { preserveWhitespace: 'full' }
        });
    }
};

// Sets TipTap content when user edits raw HTML text
window.setTipTapContentFromHtml = function (elementId, htmlText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    editor.commands.setContent(htmlText || '', false);
};

window.destroyTipTap = function (elementId) {
    if (window.tiptapInstances && window.tiptapInstances[elementId]) {
        try {
            window.tiptapInstances[elementId].destroy();
        } catch (e) { /* ignore */ }
        delete window.tiptapInstances[elementId];
    }
};