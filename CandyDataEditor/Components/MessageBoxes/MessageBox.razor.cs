using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.MessageBoxes
{

    public enum MessageBoxType
    {
        Alert,
        Confirm
    }

    public partial class MessageBox : ComponentBase
    {
        [Parameter] public bool IsOpen { get; set; }
        [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

        [Parameter] public string Title { get; set; } = "Notification";
        [Parameter] public string Message { get; set; } = string.Empty;
        [Parameter] public MessageBoxType Type { get; set; } = MessageBoxType.Alert;

        [Parameter] public string OkButtonText { get; set; } = "OK";
        [Parameter] public string CancelButtonText { get; set; } = "Cancel";

        [Parameter] public EventCallback<bool> OnResult { get; set; }

        // Matches Windows paths (D:\folder\file.txt), UNC network paths (\\server\share), or URLs (http/https)
        private static readonly Regex PathAndUrlRegex = new(
            @"(?:[a-zA-Z]:\\[^:<>""]+)|(?:\\\\[^\s<>""]+)|(?:https?://[^\s<>""]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task CloseAsync()
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            await OnResult.InvokeAsync(false);
        }

        private async Task ConfirmAsync()
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            await OnResult.InvokeAsync(true);
        }

        private async Task CancelAsync()
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            await OnResult.InvokeAsync(false);
        }

        private async Task HandleBackdropClick()
        {

            if (Type == MessageBoxType.Alert)
            {
                await CloseAsync();
            }
        }

        private RenderFragment RenderFormattedMessage() => builder =>
        {
            if (string.IsNullOrEmpty(Message)) return;

            var matches = PathAndUrlRegex.Matches(Message);
            int lastIndex = 0;
            int seq = 0;

            foreach (Match match in matches)
            {
                // Append plain text prior to match
                if (match.Index > lastIndex)
                {
                    builder.AddContent(seq++, Message.Substring(lastIndex, match.Index - lastIndex));
                }

                string matchedString = match.Value;

                if (matchedString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // Web URL
                    builder.OpenElement(seq++, "a");
                    builder.AddAttribute(seq++, "href", matchedString);
                    builder.AddAttribute(seq++, "target", "_blank");
                    builder.AddAttribute(seq++, "rel", "noopener noreferrer");
                    builder.AddAttribute(seq++, "class", "msgbox-link");
                    builder.AddContent(seq++, matchedString);
                    builder.CloseElement();
                }
                else
                {
                    // File or Directory Path
                    builder.OpenElement(seq++, "a");
                    builder.AddAttribute(seq++, "href", "javascript:void(0)");
                    builder.AddAttribute(seq++, "class", "msgbox-path-link");
                    builder.AddAttribute(seq++, "title", "Click to open path in Explorer");
                    builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => OpenPathInExplorer(matchedString)));
                    builder.AddContent(seq++, matchedString);
                    builder.CloseElement();
                }

                lastIndex = match.Index + match.Length;
            }

            // Append remaining text
            if (lastIndex < Message.Length)
            {
                builder.AddContent(seq++, Message.Substring(lastIndex));
            }
        };

        private void OpenPathInExplorer(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;

                string targetPath = path.Trim();

                // If it's a file, select it in Explorer; if it's a directory, open the directory
                if (File.Exists(targetPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{targetPath}\"");
                }
                else if (Directory.Exists(targetPath))
                {
                    Process.Start("explorer.exe", $"\"{targetPath}\"");
                }
                else
                {
                    // Fallback: Attempt opening parent directory if sub-file/folder is non-existent
                    string? parentDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        Process.Start("explorer.exe", $"\"{parentDir}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open path in File Explorer: {ex.Message}");
            }
        }
    }
}
