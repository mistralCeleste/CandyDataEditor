// wwwroot/js/tiptap-interop.js

// Global Toolbar Commands
window.execTipTapCommand = function (elementId, commandName, value) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    switch (commandName) {
        case 'toggleList':
            // Toggles the highlighted selection into a bullet list
            editor.chain().focus().toggleBulletList().run();
            break;
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

    // Direct Clipboard Intercept for Pasted Markdown / Headings / Lists
    container.addEventListener('paste', (event) => {
        const clipboardData = event.clipboardData || window.clipboardData;
        if (!clipboardData) return;

        const pastedText = clipboardData.getData('text/plain');

        // Check if pasted content contains markdown markers (#, ##, ~, -, *)
        if (pastedText && /^(#+|\~|-|\*|\d+\.)\s/m.test(pastedText)) {
            event.preventDefault();
            event.stopPropagation();

            const editor = window.tiptapInstances[elementId];
            if (editor) {
                const parsedHtml = parseCustomMarkdownToHtml(pastedText);
                // Insert parsed HTML structure directly into active ProseMirror cursor position
                editor.commands.insertContent(parsedHtml);
            }
        }
    }, true); // Capture phase listener

    // Attach Right-Click Context Menu Listener ...
    container.addEventListener('contextmenu', (event) => {
        // ... (keep your existing contextmenu code here) ...
    });

    // Close context menu on outside click ...
    window.addEventListener('click', (event) => {
        // ... (keep your existing click code here) ...
    });

    const editor = new window.TipTap.Editor({
        element: container,
        content: parseCustomMarkdownToHtml(initialContent),
        extensions: [
            window.TipTap.StarterKit.configure({
                heading: { levels: [1, 2, 3] },
            }),
            window.TipTap.Markdown,
            window.TipTap.KeywordMark,
            window.TipTap.ActionHeading,
            window.TipTap.GameIconDecoration,
            window.TipTap.NativeCustomSpellchecker,
            window.TipTap.MultiColumn,
            window.TipTap.TildeList,
        ],
        onUpdate: ({ editor }) => {
            const html = editor.getHTML();
            const markdown = serializeDocumentToCustomMarkdown(editor);
            dotnetHelper.invokeMethodAsync('OnContentChanged', html, markdown);
        },
    });

    window.tiptapInstances[elementId] = editor;
};

// Helper to match replacement word capitalization to original word
function matchCase(original, replacement) {
    if (!original || !replacement) return replacement;

    if (original === original.toUpperCase() && original.length > 1) {
        return replacement.toUpperCase();
    }

    const firstChar = original.charAt(0);
    if (firstChar === firstChar.toUpperCase() && firstChar !== firstChar.toLowerCase()) {
        return replacement.charAt(0).toUpperCase() + replacement.slice(1);
    }

    return replacement.toLowerCase();
}

// Single consolidated JS Helper to accurately replace word range
window.replaceTipTapRange = function (elementId, from, to, originalWord, newText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    // Use originalWord for casing if provided, otherwise fallback to direct text
    const textToInsert = originalWord ? matchCase(originalWord, newText) : newText;

    editor.chain()
        .focus()
        .deleteRange({ from, to })
        .insertContentAt(from, textToInsert)
        .run();
};

window.activeGameWords = new Set();
window.activeGameWordsArray = [];

window.updateGameDictionary = function (elementId, customWordList) {
    if (!customWordList) return;

    const cleanList = customWordList.map(w => w.toLowerCase().trim());
    window.activeGameWords = new Set(cleanList);
    window.activeGameWordsArray = cleanList;

    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor || !editor.view) return;

    const { state, dispatch } = editor.view;
    dispatch(state.tr.setMeta('dictionaryUpdated', Date.now()));
};

window.getSpellSuggestions = function (misspelledWord, maxSuggestions = 5) {
    if (!misspelledWord || window.activeGameWordsArray.length === 0) return [];

    const rawTarget = misspelledWord.trim();
    const target = rawTarget.toLowerCase();
    const firstChar = target[0];
    const targetLen = target.length;

    const candidates = window.activeGameWordsArray.filter(w => {
        if (Math.abs(w.length - targetLen) > 3) return false;
        return w[0] === firstChar || fastLevenshtein(target, w) <= 2;
    });

    const scored = candidates.map(w => ({
        word: w,
        dist: fastLevenshtein(target, w)
    }));

    scored.sort((a, b) => a.dist - b.dist || Math.abs(a.word.length - targetLen) - Math.abs(b.word.length - targetLen));

    const results = [];
    const seen = new Set();

    for (const item of scored) {
        if (item.dist > 3) break;

        const casedWord = matchCase(rawTarget, item.word);

        if (!seen.has(casedWord.toLowerCase())) {
            seen.add(casedWord.toLowerCase());
            results.push(casedWord);
            if (results.length >= maxSuggestions) break;
        }
    }

    return results;
};

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

// wwwroot/js/tiptap-interop.js

function parseCustomMarkdownToHtml(markdown) {
    if (!markdown || !markdown.trim()) return '<p></p>';

    let src = markdown.replace(/\r\n/g, '\n').trim();

    // 1. Process Inline Marks FIRST (Bold, Italic, Keyword)
    // ==keyword== -> <span class="keyword-mark">keyword</span>
    src = src.replace(/==([^=]+)==/g, '<span class="keyword-mark">$1</span>');
    // **bold** -> <strong>bold</strong>
    src = src.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    // *italic* -> <em>italic</em>
    src = src.replace(/\*([^*]+)\*/g, '<em>$1</em>');

    // GUARD: If content was originally HTML, return immediately after inline mark replacement
    if (/^<[a-z][\s\S]*>/i.test(src)) {
        return src;
    }

    // 2. Headings (#, ##, ###)
    src = src.replace(/^### (.*$)/gim, '<h3>$1</h3>');
    src = src.replace(/^## (.*$)/gim, '<h2>$1</h2>');
    src = src.replace(/^# (.*$)/gim, '<h1>$1</h1>');

    // 3. Action Headings (@ and @@)
    src = src.replace(/^@@ (.*$)/gim, '<div class="action-menu-child">$1</div>');
    src = src.replace(/^@ (.*$)/gim, '<div class="action-menu-parent">$1</div>');

    // 4. Lists (~, -, *)
    src = src.replace(/^(~|-|\*)\s+(.*$)/gim, '<li><p>$2</p></li>');

    // Group consecutive list items into <ul>
    src = src.replace(/(<li><p>.*?<\/p><\/li>\n?)+/gs, '<ul>$&</ul>');

    const lines = src.split('\n');
    let result = [];

    for (let line of lines) {
        let trimmed = line.trim();
        if (!trimmed) continue;

        if (/^<h[1-6]|^<div|^<ul|^<li|^<\/ul>|^<\/li>/.test(trimmed)) {
            result.push(trimmed);
        } else {
            result.push(`<p>${trimmed}</p>`);
        }
    }

    return result.join('');
}

// Converts TipTap Editor State -> Clean Raw Game Markdown
function serializeDocumentToCustomMarkdown(editor) {
    if (!editor.storage || !editor.storage.markdown) return '';

    let markdown = editor.storage.markdown.getMarkdown();

    // 1. Convert Level 1 Action Divs
    markdown = markdown.replace(/<div[^>]*class="action-menu-parent"[^>]*>([\s\S]*?)<\/div>/gi, (match, p1) => {
        const cleanText = p1.replace(/<p>/gi, '').replace(/<\/p>/gi, '').trim();
        return `@ ${cleanText}`;
    });

    // 2. Convert Level 2 Action Divs
    markdown = markdown.replace(/<div[^>]*class="action-menu-child"[^>]*>([\s\S]*?)<\/div>/gi, (match, p1) => {
        const cleanText = p1.replace(/<p>/gi, '').replace(/<\/p>/gi, '').trim();
        return `@@ ${cleanText}`;
    });

    // 3. Convert <span class="keyword-mark">
    markdown = markdown.replace(/<span[^>]*class="keyword-mark"[^>]*>([\s\S]*?)<\/span>/gi, '==$1==');

    // 4. Unescape ligature brackets: \[mob\] -> [mob]
    markdown = markdown.replace(/\\\[([a-zA-Z0-9_-]+)\\\]/g, '[$1]');

    return markdown.trim();
}

window.getTipTapSelectedText = function (elementId) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return '';

    const { from, to } = editor.state.selection;
    if (from === to) return '';

    let selectedText = editor.state.doc.textBetween(from, to, ' ');
    return selectedText.replace(/^\[|\]$/g, '').trim();
};

window.getTipTapMarkdown = function (elementId) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return '';

    return serializeDocumentToCustomMarkdown(editor);
};

window.getTipTapHtml = function (elementId) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return '';

    return editor.getHTML();
};

window.setTipTapContentFromMarkdown = function (elementId, markdownText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    const parsedHtml = parseCustomMarkdownToHtml(markdownText);
    editor.commands.setContent(parsedHtml, false);
};

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
