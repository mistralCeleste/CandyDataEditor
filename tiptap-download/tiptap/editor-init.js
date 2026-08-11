window.createTiptapEditor = function (elementId, initialContent) {
    const editor = new window.tiptap.Editor({
        element: document.getElementById(elementId),
        content: initialContent,
        extensions: [
            window.tiptapStarterKit.StarterKit,
            window.tiptapExtensionPlaceholder.Placeholder.configure({
                placeholder: 'Start typing…',
            }),
            window.tiptapExtensionCharacterCount.CharacterCount,
        ],
    });

    return editor;
};
