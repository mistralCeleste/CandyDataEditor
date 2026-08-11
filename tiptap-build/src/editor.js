import { Editor } from '@tiptap/core'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'
import CharacterCount from '@tiptap/extension-character-count'

export function createEditor(elementId, initialContent, dotnetRef) {
    const editor = new Editor({
        element: document.getElementById(elementId),
        content: initialContent,
        extensions: [
            StarterKit,
            Placeholder.configure({
                placeholder: 'Start typing…'
            }),
            CharacterCount
        ],
        onUpdate({ editor }) {
            const html = editor.getHTML()
            dotnetRef.invokeMethodAsync("OnContentChanged", html)
        }
    })

    return editor
}
