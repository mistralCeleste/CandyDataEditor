// wwwroot/js/spellcheck.js

export class Spellchecker {
    constructor() {
        this.globalGameWords = new Set();
        this.globalGameWordsArray = [];
        this.bucketByLength = new Map(); // Fast length-indexed lookup
    }

    getGlobalDictionaryWordCount() {
        return this.globalGameWordsArray.length;
    }

    // Set and synchronize the global dictionary memory instantly (< 10ms)
    setGlobalDictionary(customWordList) {
        if (!customWordList) return;

        const cleanList = customWordList.map(w => w.toLowerCase().trim()).filter(Boolean);
        this.globalGameWords = new Set(cleanList);
        this.globalGameWordsArray = Array.from(this.globalGameWords);

        // Partition dictionary into buckets by word length for instant filtering
        this.bucketByLength.clear();
        for (const word of this.globalGameWordsArray) {
            const len = word.length;
            if (!this.bucketByLength.has(len)) {
                this.bucketByLength.set(len, []);
            }
            this.bucketByLength.get(len).push(word);
        }

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

    // Fast suggestion finder with transposition support and compound splitting
    getSpellSuggestions(misspelledWord, maxSuggestions = 5) {
        if (!misspelledWord || this.globalGameWordsArray.length === 0) return [];

        const rawTarget = misspelledWord.trim();
        const target = rawTarget.toLowerCase();
        const targetLen = target.length;
        const candidates = new Set();

        // 1. Compound / Split Word Check (e.g., "rockface" -> "rock face")
        for (let i = 2; i <= targetLen - 2; i++) {
            const left = target.slice(0, i);
            const right = target.slice(i);
            if (this.globalGameWords.has(left) && this.globalGameWords.has(right)) {
                candidates.add(`${left} ${right}`);
            }
        }

        // 2. Fetch candidates with length difference <= 2 (or 1 for short words like "teh")
        const maxLenDelta = targetLen <= 3 ? 1 : 2;
        const firstChar = target[0];

        for (let len = targetLen - maxLenDelta; len <= targetLen + maxLenDelta; len++) {
            const bucket = this.bucketByLength.get(len);
            if (!bucket) continue;

            for (const word of bucket) {
                // Heuristic: matching first char OR short distance candidate
                if (word[0] === firstChar || targetLen <= 4) {
                    candidates.add(word);
                }
            }
        }

        // 3. Score and rank via Damerau-Levenshtein distance (swaps cost 1 edit)
        const scored = [];
        for (const candidate of candidates) {
            const dist = this._damerauLevenshtein(target, candidate.replace(' ', ''));
            // Allow distance of 1 for short words ("teh" -> "the"), distance of 2 for others
            const allowedDist = targetLen <= 3 ? 1 : 2;
            if (dist <= allowedDist) {
                scored.push({ word: candidate, dist });
            }
        }

        scored.sort((a, b) => a.dist - b.dist || Math.abs(a.word.length - targetLen) - Math.abs(b.word.length - targetLen));

        const results = [];
        const seen = new Set();

        for (const item of scored) {
            const casedWord = this._matchCase(rawTarget, item.word);
            const lowerCased = casedWord.toLowerCase();

            if (!seen.has(lowerCased)) {
                seen.add(lowerCased);
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

    // Fast Damerau-Levenshtein Distance (transposition aware: 'teh' -> 'the' = distance 1)
    _damerauLevenshtein(a, b) {
        if (a === b) return 0;
        if (!a) return b ? b.length : 0;
        if (!b) return a.length;

        const lenA = a.length;
        const lenB = b.length;
        const d = Array.from({ length: lenA + 1 }, () => new Array(lenB + 1).fill(0));

        for (let i = 0; i <= lenA; i++) d[i][0] = i;
        for (let j = 0; j <= lenB; j++) d[0][j] = j;

        for (let i = 1; i <= lenA; i++) {
            for (let j = 1; j <= lenB; j++) {
                const cost = a[i - 1] === b[j - 1] ? 0 : 1;
                d[i][j] = Math.min(
                    d[i - 1][j] + 1,      // Deletion
                    d[i][j - 1] + 1,      // Insertion
                    d[i - 1][j - 1] + cost // Substitution
                );

                // Adjacent Transposition check ('teh' -> 'the')
                if (i > 1 && j > 1 && a[i - 1] === b[j - 2] && a[i - 2] === b[j - 1]) {
                    d[i][j] = Math.min(d[i][j], d[i - 2][j - 2] + cost);
                }
            }
        }

        return d[lenA][lenB];
    }

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
}
