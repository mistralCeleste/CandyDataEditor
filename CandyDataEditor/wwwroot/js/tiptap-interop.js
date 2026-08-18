// wwwroot/js/tiptap-interop.js
import { Spellchecker } from './spellcheck.js';

window.spellchecker = new Spellchecker();
window.tiptapInstances = window.tiptapInstances || {};

// Expose global dictionary hooks for Blazor JS interop
window.setGlobalGameDictionary = (wordList) => {
    if (window.spellchecker) {
        window.spellchecker.setGlobalDictionary(wordList);
    }

    const normalizedSet = new Set();
    if (Array.isArray(wordList)) {
        wordList.forEach(w => {
            if (w) normalizedSet.add(w.toString().toLowerCase().trim());
        });
    }
    window.activeGameWords = normalizedSet;

    // Force ProseMirror plugin re-decoration pass on all active editor instances
    if (window.tiptapInstances) {
        Object.values(window.tiptapInstances).forEach(editor => {
            if (editor && editor.view) {
                const tr = editor.state.tr.setMeta('spellcheckRefresh', Date.now());
                editor.view.dispatch(tr);
            }
        });
    }
};

window.getGlobalDictionaryWordCount = () => window.spellchecker.getGlobalDictionaryWordCount();
window.isWordInDictionary = (word) => window.spellchecker.isWordInDictionary(word);
window.getSpellSuggestions = (word, max) => window.spellchecker.getSpellSuggestions(word, max);

// Global Toolbar Commands
window.execTipTapCommand = function (elementId, commandName, value) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    switch (commandName) {
        case 'toggleList':
            editor.chain().focus().toggleBulletList().run();
            break;
        case 'keyword':
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

// Attach one-time global click & keydown listeners to dismiss the context menu
window.attachContextMenuDismissListener = function (dotnetRef) {
    const dismissHandler = (event) => {
        // Ignore clicks originating inside the context menu itself
        if (event.target.closest('.dropdown-menu')) {
            return;
        }

        // Notify Blazor to close menu
        if (dotnetRef) {
            dotnetRef.invokeMethodAsync('CloseSpellcheckContextMenu');
        }

        // Clean up listeners
        document.removeEventListener('mousedown', dismissHandler, true);
        document.removeEventListener('keydown', keydownHandler, true);
    };

    const keydownHandler = (event) => {
        // Close menu on Escape key
        if (event.key === 'Escape') {
            if (dotnetRef) {
                dotnetRef.invokeMethodAsync('CloseSpellcheckContextMenu');
            }
            document.removeEventListener('mousedown', dismissHandler, true);
            document.removeEventListener('keydown', keydownHandler, true);
        }
    };

    // Use capture phase (true) so clicks anywhere in the window trigger this before other stopPropagation calls
    setTimeout(() => {
        document.addEventListener('mousedown', dismissHandler, true);
        document.addEventListener('keydown', keydownHandler, true);
    }, 10);
};

window.initTipTap = function (elementId, initialContent, dotnetHelper) {
    const container = document.getElementById(elementId);
    if (!container) return;

    // Attach Context Menu Listener directly via Spellchecker class method
    container.addEventListener('contextmenu', (event) => {
        window.spellchecker.handleContextMenu(event, container, elementId, dotnetHelper);
    }, true);

    // Initialize TipTap Instance
    const editor = new window.TipTap.Editor({
        element: container,
        content: parseCustomMarkdownToHtml(initialContent),
        editorProps: {
            attributes: { spellcheck: 'false' }
        },
        extensions: [
            window.TipTap.StarterKit.configure({ heading: { levels: [1, 2, 3] } }),
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
        onBlur: () => {
            dotnetHelper.invokeMethodAsync('OnEditorBlurred');
        }
    });

    window.tiptapInstances[elementId] = editor;
};

// Word Replacement Handler called by TipTapEditor context menu
window.replaceTipTapRange = function (elementId, from, to, originalWord, newText) {
    const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
    if (!editor) return;

    const textToInsert = originalWord ? matchCase(originalWord, newText) : newText;

    editor.chain()
        .focus()
        .deleteRange({ from, to })
        .insertContentAt(from, textToInsert)
        .run();
};

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

function parseCustomMarkdownToHtml(markdown) {
    if (!markdown || !markdown.trim()) return '<p></p>';

    //let src = markdown.replace(/\r\n/g, '\n').trim();
    let src = markdown
        .replace(/\u2029/g, '\n')
        .replace(/\u2028/g, ' ')
        .replace(/\r\n/g, '\n')
        .trim();

    // 1. Process Inline Marks FIRST (Bold, Italic, Keyword)
    src = src.replace(/==([^=]+)==/g, '<span class="keyword-mark">$1</span>');
    src = src.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
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
