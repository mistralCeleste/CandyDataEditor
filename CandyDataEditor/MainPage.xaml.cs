using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace CandyDataEditor
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
        }

        private async void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
        {
            // Safely check for Windows at runtime
            if (OperatingSystem.IsWindows() && e.WebView is not null)
            {
                // Use dynamic to bypass cross-platform compilation type-checking
                dynamic nativeWebView = e.WebView;

                try
                {
                    // 1. Enable native right-click context menu for spellcheck
                    nativeWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

                    // 2. Ensure CoreWebView2 is initialized
                    await nativeWebView.EnsureCoreWebView2Async();

                    // 3. Register custom game dictionary words with native spellchecker
                    var customWords = new[] {
                        "allfoes", "card10clubs", "warblade", "aegis", "d2020", "repeat2"
                    };

                    foreach (var word in customWords)
                    {
                        await nativeWebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.navigator.spellcheck?.addWord?.('{word}')"
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebView2 setup error: {ex.Message}");
                }
            }
        }
    }
}