// wwwroot/js/spellcheck.js

export class Spellchecker {
    constructor() {
        this.globalGameWords = new Set();
        this.globalGameWordsArray = [];
    }

    getGlobalDictionaryWordCount() {

        return this.globalGameWordsArray.length;
    }

    // Set and synchronize the global dictionary memory
    setGlobalDictionary(customWordList) {
        if (!customWordList) return;

        const cleanList = customWordList.map(w => w.toLowerCase().trim());
        this.globalGameWords = new Set(cleanList);
        this.globalGameWordsArray = cleanList;

        // Trigger view refresh across active TipTap instances
        if (window.tiptapInstances) {
            Object.values(window.tiptapInstances).forEach(editor => {
                if (editor && editor.view) {
                    const { state, dispatch } = editor.view;
                    dispatch(state.tr.setMeta('dictionaryUpdated', Date.now()));
                }
            });
        }
    }

    // Fast O(1) set lookup for ProseMirror decorations
    isWordInDictionary(word) {
        if (!word) return true;
        return this.globalGameWords.has(word.toLowerCase().trim());
    }

    // Fast suggestion algorithm with Levenshtein distance
    getSpellSuggestions(misspelledWord, maxSuggestions = 5) {
        if (!misspelledWord || this.globalGameWordsArray.length === 0) return [];

        const rawTarget = misspelledWord.trim();
        const target = rawTarget.toLowerCase();
        const firstChar = target[0];
        const targetLen = target.length;

        const candidates = this.globalGameWordsArray.filter(w => {
            if (Math.abs(w.length - targetLen) > 3) return false;
            return w[0] === firstChar || this._fastLevenshtein(target, w) <= 2;
        });

        const scored = candidates.map(w => ({
            word: w,
            dist: this._fastLevenshtein(target, w)
        }));

        scored.sort((a, b) => a.dist - b.dist || Math.abs(a.word.length - targetLen) - Math.abs(b.word.length - targetLen));

        const results = [];
        const seen = new Set();

        for (const item of scored) {
            if (item.dist > 3) break;
            const casedWord = this._matchCase(rawTarget, item.word);
            if (!seen.has(casedWord.toLowerCase())) {
                seen.add(casedWord.toLowerCase());
                results.push(casedWord);
                if (results.length >= maxSuggestions) break;
            }
        }

        return results;
    }

    // Event listener handler for editor right-click menu
    handleContextMenu(event, container, elementId, dotnetHelper) {
        const target = event.target.closest('.custom-misspelled-word, .misspelled-word, [data-misspelled]');
        if (!target) return;

        event.preventDefault();
        event.stopPropagation();

        const word = target.innerText.replace(/[\u2028\u2029]/g, '').trim();
        const rect = container.getBoundingClientRect();

        const clickX = event.clientX - rect.left;
        const clickY = event.clientY - rect.top;

        const editor = window.tiptapInstances ? window.tiptapInstances[elementId] : null;
        if (editor && editor.view) {
            const pos = editor.view.posAtDOM(target, 0);

            if (pos !== null && pos !== undefined) {
                const from = pos;
                const to = pos + word.length;
                const suggestions = this.getSpellSuggestions(word, 5);

                dotnetHelper.invokeMethodAsync('OpenSpellcheckContextMenu',
                    word, suggestions, from, to, clickX, clickY);
            }
        }
    }

    // Internal helpers
    _matchCase(original, replacement) {
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

    _fastLevenshtein(a, b) {
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
}
