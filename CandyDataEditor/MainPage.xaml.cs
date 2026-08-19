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
                    nativeWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                    await nativeWebView.EnsureCoreWebView2Async();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebView2 setup error: {ex.Message}");
                }
            }
        }
    }
}