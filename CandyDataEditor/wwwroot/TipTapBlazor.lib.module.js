// Blazor JavaScript Initializer
// See https://learn.microsoft.com/aspnet/core/blazor/fundamentals/startup#javascript-initializers

function loadCSS(url) {
    if (document.querySelector(`link[href="${url}"]`)) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = url;
    document.head.appendChild(link);
}

function ensureCSS() {
    loadCSS('_content/TipTapBlazor/css/tiptap-editor.css');
}

// .NET 6/7 (legacy Blazor WASM / Server)
export function beforeStart() { }
export function afterStarted() { ensureCSS(); }

// .NET 8+ (Blazor Web App)
export function beforeWebStart() { }
export function beforeServerStart() { }
export function afterWebStarted() { ensureCSS(); }
export function afterServerStarted() { ensureCSS(); }
