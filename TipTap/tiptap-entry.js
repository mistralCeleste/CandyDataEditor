import { Editor, Node, Mark, Extension, mergeAttributes, markInputRule, markPasteRule, textblockTypeInputRule, wrappingInputRule } from '@tiptap/core';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet } from '@tiptap/pm/view';
import { Slice, Fragment } from '@tiptap/pm/model';
import StarterKit from '@tiptap/starter-kit';
import * as TipTapMarkdown from 'tiptap-markdown';

const MarkdownExtension = TipTapMarkdown.Markdown || TipTapMarkdown.default || TipTapMarkdown;

// --- Native ProseMirror Custom Spellchecker ---
window.DEBUG_SPELLCHECK = true;
const LIGATURE_REGEX = /\[[a-zA-Z0-9_-]+\]|->|<-|--/g;
const WORD_REGEX = /[\p{L}0-9_']+/gu;

export const NativeCustomSpellchecker = Extension.create({
    name: 'nativeCustomSpellchecker',
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: new PluginKey('nativeCustomSpellchecker'),
                props: {
                    decorations(state) {
                        const spellchecker = window.spellchecker;
                        let allowedSet = (spellchecker && spellchecker.globalGameWords instanceof Set)
                            ? spellchecker.globalGameWords
                            : window.activeGameWords;

                        const allowedSize = allowedSet ? allowedSet.size : 0;
                        if (!allowedSet || allowedSize === 0) return DecorationSet.empty;

                        const decorations = [];

                        state.doc.descendants((node, pos) => {
                            if (!node.isText || !node.text) return;

                            const ligatureRanges = [];
                            let ligMatch;
                            LIGATURE_REGEX.lastIndex = 0;
                            while ((ligMatch = LIGATURE_REGEX.exec(node.text)) !== null) {
                                ligatureRanges.push({
                                    start: ligMatch.index,
                                    end: ligMatch.index + ligMatch[0].length
                                });
                            }

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

                                const lower = word.replace(/[\u200B-\u200D\uFEFF]/g, '').toLowerCase().trim();
                                const isKnown = allowedSet.has(lower);

                                if (!isKnown) {
                                    decorations.push(
                                        Decoration.inline(pos + wordStart, pos + wordEnd, {
                                            class: 'custom-misspelled-word',
                                            'data-misspelled': 'true'
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

// 1. GAME ICON & LIGATURE DECORATION
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
                                        Decoration.inline(start, end, { class: 'game-icon-ligature' })
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
        return { HTMLAttributes: { class: 'keyword-mark' } };
    },
    parseHTML() {
        return [{ tag: 'span.keyword-mark' }, { tag: 'mark' }];
    },
    renderHTML({ HTMLAttributes }) {
        return ['span', mergeAttributes(this.options.HTMLAttributes, HTMLAttributes), 0];
    },
    addCommands() {
        return {
            setKeyword: () => ({ commands }) => commands.setMark(this.name),
            toggleKeyword: () => ({ commands }) => commands.toggleMark(this.name),
            unsetKeyword: () => ({ commands }) => commands.unsetMark(this.name),
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
                find: /(?:==)([^=]+)(?:==)/g,
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
        return { level: { default: 1 } };
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
    addCommands() {
        return {
            setActionHeading: (attributes) => ({ commands }) => commands.setNode(this.name, attributes),
            toggleActionHeading: (attributes) => ({ commands }) => commands.toggleNode(this.name, 'paragraph', attributes),
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
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: new PluginKey('actionHeadingPaste'),
                props: {
                    transformPasted(slice, view) {
                        const schema = view.state.schema;
                        if (!schema.nodes.actionHeading) return slice;

                        const newContent = [];
                        slice.content.forEach((node) => {
                            if (node.isTextblock) {
                                const text = node.textContent;
                                let level = 0;
                                let prefixLength = 0;

                                if (text.startsWith('@@ ')) {
                                    level = 2;
                                    prefixLength = 3;
                                } else if (text.startsWith('@ ')) {
                                    level = 1;
                                    prefixLength = 2;
                                }

                                if (level > 0) {
                                    let contentFragment = Fragment.empty;
                                    if (node.content && node.content.size > 0) {
                                        const children = [];
                                        let charsToCut = prefixLength;

                                        node.content.forEach((child) => {
                                            if (charsToCut > 0 && child.isText) {
                                                const newText = child.text.slice(charsToCut);
                                                charsToCut = 0;
                                                if (newText.length > 0) {
                                                    children.push(child.withText(newText));
                                                }
                                            } else {
                                                children.push(child);
                                            }
                                        });
                                        contentFragment = Fragment.from(children);
                                    }

                                    newContent.push(
                                        schema.nodes.actionHeading.create(
                                            { level: level },
                                            contentFragment
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

// Standard Dash/Asterisk List Extension (Replaces TildeList)
export const DashList = Extension.create({
    name: 'dashList',
    addInputRules() {
        return [
            wrappingInputRule({
                find: /^[-*]\s$/,
                type: this.editor.schema.nodes.bulletList,
            }),
        ];
    },
});

// Custom Markdown Paste Handler to transform raw pasted text directly to TipTap Nodes
export const RawMarkdownPasteHandler = Extension.create({
    name: 'rawMarkdownPasteHandler',
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: new PluginKey('rawMarkdownPasteHandler'),
                props: {
                    transformPastedText(text) {
                        // Unescape headers, lists, and formatting pasted from other engines
                        return text
                            .replace(/\\([#\-*@_>])/g, '$1')
                            .replace(/\\\[([a-zA-Z0-9_-]+)\\\]/g, '[$1]');
                    }
                }
            })
        ];
    }
});

export function createEditor(elementId, initialContent, dotnetRef, initialDictionary) {
    const editor = new Editor({
        element: document.getElementById(elementId),
        content: initialContent,
        extensions: [
            StarterKit,
            MarkdownExtension.configure({
                html: true,
                transformPastedText: true,
                transformCopiedText: true,
            }),
            KeywordMark,
            ActionHeading,
            DashList,
            RawMarkdownPasteHandler,
            GameIconDecoration,
            NativeCustomSpellchecker,
        ],
        onUpdate({ editor }) {
            const html = editor.getHTML();
            const markdown = editor.storage.markdown.getMarkdown();
            dotnetRef.invokeMethodAsync("OnContentChanged", html, markdown);
        }
    });

    editor.storage.spellcheckerDictionary = initialDictionary;
    return editor;
}

// Window Exports
window.TipTap = {
    Editor,
    StarterKit,
    Markdown: MarkdownExtension,
    KeywordMark,
    ActionHeading,
    DashList,
    RawMarkdownPasteHandler,
    GameIconDecoration,
    NativeCustomSpellchecker
};
