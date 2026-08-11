import { Editor, Node, Mark, Extension, mergeAttributes, markInputRule, markPasteRule, textblockTypeInputRule } from '@tiptap/core';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet } from '@tiptap/pm/view';
import { Slice, Fragment } from '@tiptap/pm/model';
import StarterKit from '@tiptap/starter-kit';
import * as TipTapMarkdown from 'tiptap-markdown';


const Markdown = TipTapMarkdown.Markdown || TipTapMarkdown.default || TipTapMarkdown;

// Active game words state set from Blazor
window.activeGameWords = new Set();

// --- Native ProseMirror Custom Spellchecker ---
const LIGATURE_REGEX = /\[[a-zA-Z0-9_-]+\]|->|<-|--/g;
// Unicode word regex supporting French accents & English terms
const WORD_REGEX = /[\p{L}0-9_']+/gu;

export const NativeCustomSpellchecker = Extension.create({
    name: 'nativeCustomSpellchecker',

    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: new PluginKey('nativeCustomSpellchecker'),
                props: {
                    decorations(state) {
                        const allowed = window.activeGameWords;
                        if (!allowed || allowed.size === 0) return DecorationSet.empty;

                        const decorations = [];

                        // ONLY scan text blocks in the current selection / viewport for speed
                        const { selection } = state;
                        const parentBlock = selection.$from.node(-1) || state.doc;

                        state.doc.descendants((node, pos) => {
                            if (!node.isText || !node.text) return;

                            // 1. Skip ligatures
                            const ligatureRanges = [];
                            let ligMatch;
                            LIGATURE_REGEX.lastIndex = 0;
                            while ((ligMatch = LIGATURE_REGEX.exec(node.text)) !== null) {
                                ligatureRanges.push({
                                    start: ligMatch.index,
                                    end: ligMatch.index + ligMatch[0].length
                                });
                            }

                            // 2. Scan words
                            WORD_REGEX.lastIndex = 0;
                            let match;

                            while ((match = WORD_REGEX.exec(node.text)) !== null) {
                                const word = match[0];
                                const wordStart = match.index;
                                const wordEnd = wordStart + word.length;

                                if (word.length <= 1 || !isNaN(word)) continue;

                                const isInsideLigature = ligatureRanges.some(
                                    range => wordStart >= range.start && wordEnd <= range.end
                                );
                                if (isInsideLigature) continue;

                                const lower = word.toLowerCase().trim();
                                if (!allowed.has(lower)) {
                                    decorations.push(
                                        Decoration.inline(pos + wordStart, pos + wordEnd, {
                                            class: 'custom-misspelled-word',
                                        })
                                    );
                                }
                            }
                        });

                        return DecorationSet.create(state.doc, decorations);
                    },
                },
            }),
        ];
    },
});


// 1. GAME ICON & LIGATURE DECORATION ([icon], ->, <-, --)
const GameIconDecoration = Extension.create({
    name: 'gameIconDecoration',
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: new PluginKey('gameIconDecoration'),
                props: {
                    decorations(state) {
                        const decorations = [];
                        const regex = /\[[a-zA-Z0-9_-]+\]|->|<-|--/g;

                        state.doc.descendants((node, pos) => {
                            if (node.isText && node.text) {
                                let match;
                                regex.lastIndex = 0;
                                while ((match = regex.exec(node.text)) !== null) {
                                    const start = pos + match.index;
                                    const end = start + match[0].length;
                                    decorations.push(
                                        Decoration.inline(start, end, {
                                            class: 'game-icon-ligature',
                                        })
                                    );
                                }
                            }
                        });
                        return DecorationSet.create(state.doc, decorations);
                    },
                },
            }),
        ];
    },
});

// 2. KEYWORD MARK (==keyword==)
export const KeywordMark = Mark.create({
    name: 'keyword',

    addOptions() {
        return {
            HTMLAttributes: {
                class: 'keyword-mark',
            },
        };
    },

    parseHTML() {
        return [
            { tag: 'span.keyword-mark' },
            { tag: 'mark' },
        ];
    },

    renderHTML({ HTMLAttributes }) {
        return ['span', mergeAttributes(this.options.HTMLAttributes, HTMLAttributes), 0];
    },

    // Registers editor.chain().focus().toggleKeyword() and setKeyword()
    addCommands() {
        return {
            setKeyword:
                () =>
                    ({ commands }) => {
                        return commands.setMark(this.name);
                    },
            toggleKeyword:
                () =>
                    ({ commands }) => {
                        return commands.toggleMark(this.name);
                    },
            unsetKeyword:
                () =>
                    ({ commands }) => {
                        return commands.unsetMark(this.name);
                    },
        };
    },

    addInputRules() {
        return [
            markInputRule({
                find: /(?:^|\s)(==(?!\s+==)((?:[^=]+))==(?!\s+==))$/,
                type: this.type,
            }),
        ];
    },

    addPasteRules() {
        return [
            markPasteRule({
                find: /(?:^|\s)(==(?!\s+==)((?:[^=]+))==(?!\s+==))/g,
                type: this.type,
            }),
        ];
    },
});

// 3. ACTION MENU NODES (@ Action and @@ Sub-Action)
export const ActionHeading = Node.create({
    name: 'actionHeading',
    group: 'block',
    content: 'inline*',
    defining: true,

    addAttributes() {
        return {
            level: { default: 1 },
        };
    },

    parseHTML() {
        return [
            { tag: 'div.action-menu-parent', attrs: { level: 1 } },
            { tag: 'div.action-menu-child', attrs: { level: 2 } },
        ];
    },

    renderHTML({ HTMLAttributes }) {
        const level = HTMLAttributes.level || 1;
        const className = level === 1 ? 'action-menu-parent' : 'action-menu-child';
        return ['div', mergeAttributes(HTMLAttributes, { class: className }), 0];
    },

    // Commands callable from JavaScript / Blazor Toolbar
    addCommands() {
        return {
            setActionHeading: (attributes) => ({ commands }) => {
                return commands.setNode(this.name, attributes);
            },
            toggleActionHeading: (attributes) => ({ commands }) => {
                return commands.toggleNode(this.name, 'paragraph', attributes);
            },
        };
    },

    addInputRules() {
        return [
            textblockTypeInputRule({
                find: /^@@\s$/,
                type: this.type,
                getAttributes: () => ({ level: 2 }),
            }),
            textblockTypeInputRule({
                find: /^@\s$/,
                type: this.type,
                getAttributes: () => ({ level: 1 }),
            }),
        ];
    },

    // Prosemirror Paste Plugin to catch pasted @ and @@ block lines
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: new PluginKey('actionHeadingPaste'),
                props: {
                    transformPasted(slice) {
                        // Transforms pasted text lines starting with @ or @@ into ActionHeading nodes
                        const newContent = [];

                        slice.content.forEach(node => {
                            if (node.isTextblock) {
                                const text = node.textContent;
                                if (text.startsWith('@@ ')) {
                                    const textWithoutPrefix = text.slice(3);
                                    newContent.push(
                                        node.type.schema.nodes.actionHeading.create(
                                            { level: 2 },
                                            node.type.schema.text(textWithoutPrefix)
                                        )
                                    );
                                    return;
                                } else if (text.startsWith('@ ')) {
                                    const textWithoutPrefix = text.slice(2);
                                    newContent.push(
                                        node.type.schema.nodes.actionHeading.create(
                                            { level: 1 },
                                            node.type.schema.text(textWithoutPrefix)
                                        )
                                    );
                                    return;
                                }
                            }
                            newContent.push(node);
                        });

                        return new Slice(Fragment.from(newContent), slice.openStart, slice.openEnd);
                    },
                },
            }),
        ];
    },

});

export const GameDictionary = Extension.create({
    name: 'gameDictionary',

    addOptions() {
        return {
            words: [],
        };
    },

    addProseMirrorPlugins() {
        const extension = this;

        return [
            new Plugin({
                key: new PluginKey('gameDictionary'),
                props: {
                    decorations(state) {
                        const words = extension.options.words;
                        if (!words || words.length === 0) return DecorationSet.empty;

                        const decorations = [];

                        // Filter valid words and escape regex special characters
                        const cleanWords = words
                            .filter(w => typeof w === 'string' && w.trim().length > 0)
                            .map(w => w.trim().replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));

                        if (cleanWords.length === 0) return DecorationSet.empty;

                        // Case-insensitive regex matching words
                        const regex = new RegExp(`\\b(${cleanWords.join('|')})\\b`, 'gi');

                        state.doc.descendants((node, pos) => {
                            if (node.isText && node.text) {
                                let match;
                                regex.lastIndex = 0;
                                while ((match = regex.exec(node.text)) !== null) {
                                    const start = pos + match.index;
                                    const end = start + match[0].length;

                                    // Render a DOM element node that explicitly breaks spellcheck inheritance
                                    decorations.push(
                                        Decoration.inline(start, end, {
                                            spellcheck: 'false',
                                            style: 'spellcheck: false !important;',
                                            class: 'custom-game-word'
                                        })
                                    );
                                }
                            }
                        });

                        return DecorationSet.create(state.doc, decorations);
                    },
                },
            }),
        ];
    },
});

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
            editor.chain().focus().insertContent(`[${value}]`).run();
            break;
    }
};

export function createEditor(elementId, initialContent, dotnetRef, initialDictionary) {
    const editor = new Editor({
        element: document.getElementById(elementId),
        content: initialContent,
        extensions: [
            StarterKit,
            Placeholder.configure({ placeholder: "Start typing…" }),
            CharacterCount,

            SpellcheckerExtension.configure({
                dictionary: initialDictionary,   // array of words
                underlineColor: '#ff0000',
                spellcheckOnLoad: true,

                onMisspelled: (word) => {
                    dotnetRef.invokeMethodAsync("OnMisspelledWord", word);
                },
            }),
        ],
        onUpdate({ editor }) {
            const html = editor.getHTML();
            const markdown = editor.storage.markdown.getMarkdown();
            dotnetRef.invokeMethodAsync("OnContentChanged", html, markdown);
        }
    });

    // Store dictionary so JSInterop can update it later
    editor.storage.spellcheckerDictionary = initialDictionary;

    return editor;
}


// Register on window.TipTap
window.TipTap = {
    Editor,
    StarterKit,
    Markdown,
    GameDictionary,
    GameIconDecoration,
    KeywordMark,
    ActionHeading,
    NativeCustomSpellchecker
};