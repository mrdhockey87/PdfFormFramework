#if WINDOWS
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// WebView2 (WinUI 3)
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

// SkiaSharp placeholder renderer (swap with your PDF renderer)
using SkiaSharp;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
    
namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    // Entry: Prefer WebView2 print pipeline (works reliably in MAUI/WinUI); fallback to default viewer
    public static partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        // 1) Preferred: use WebView2 printing (native Edge viewer print UI)
        try
        {
            var ok = await PrintWithWebView2Async(filePath);
            if (ok) return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 print failed: {ex.Message}");
        }

        // 2) Last resort: open in default viewer
        try
        {
            await OpenInDefaultViewerAsync(filePath);
        }
        catch { /* ignore */ }
    }

    // Use the existing WebView2-based preview to show the system print UI
    private static async Task<bool> PrintWithWebView2Async(string filePath)
    {
        try
        {
            await ShowSystemPrintDialogAsync(filePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PrintWithWebView2Async failed: {ex.Message}");
            return false;
        }
    }

    // System print dialog via WebView2 (Edge PDF viewer)
    private static async Task ShowSystemPrintDialogAsync(string filePath)
    {
        var printCopyPath = await CreatePrintCopyAsync(filePath);

        var page = new ContentPage { Title = "Print Preview" };
        var mauiWebView = new WebView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var closeBtn = new Microsoft.Maui.Controls.Button
        {
            Text = "Close",
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(8)
        };

        Microsoft.Maui.Controls.Window? previewWindow = null;

        async Task CleanupAsync(WebView2? native)
        {
            try
            {
                if (native?.CoreWebView2 is not null)
                {
                    native.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "appassets", string.Empty, CoreWebView2HostResourceAccessKind.Allow);
                }
            }
            catch { /* ignore */ }

            try
            {
                if (File.Exists(printCopyPath))
                    File.Delete(printCopyPath);
            }
            catch { /* ignore */ }

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (previewWindow is not null && Microsoft.Maui.Controls.Application.Current is not null)
                    {
                        Microsoft.Maui.Controls.Application.Current.CloseWindow(previewWindow); // <-- use Application to close
                        previewWindow = null;
                    }
                });
            }
            catch { /* ignore */ }
        }

        closeBtn.Clicked += async (_, __) =>
        {
            var native = await MainThread.InvokeOnMainThreadAsync<WebView2?>(() =>
                mauiWebView.Handler?.PlatformView as WebView2);
            await CleanupAsync(native);
        };

        var layout = new Microsoft.Maui.Controls.Grid();
        layout.Add(mauiWebView);
        layout.Add(closeBtn);
        page.Content = layout;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            previewWindow = new Microsoft.Maui.Controls.Window(page) { Title = "Print Preview" };
            Microsoft.Maui.Controls.Application.Current?.OpenWindow(previewWindow);
        });

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (mauiWebView.Handler is null)
            {
                var tcs = new TaskCompletionSource<bool>();
                void HandlerChanged(object? s, EventArgs e)
                {
                    mauiWebView.HandlerChanged -= HandlerChanged;
                    tcs.TrySetResult(true);
                }
                mauiWebView.HandlerChanged += HandlerChanged;
                await tcs.Task;
            }

            var native = mauiWebView.Handler?.PlatformView as WebView2;
            if (native is null)
            {
                Debug.WriteLine("Native WebView2 not available.");
                await CleanupAsync(null);
                return;
            }

            try
            {
                if (native.CoreWebView2 is null)
                    await native.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnsureCoreWebView2Async failed: {ex.Message}");
                await CleanupAsync(native);
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(printCopyPath)!;
                native.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "appassets",
                    folder,
                    CoreWebView2HostResourceAccessKind.Allow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetVirtualHostNameToFolderMapping failed: {ex.Message}");
                await CleanupAsync(native);
                return;
            }

            async void OnNavCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                native.NavigationCompleted -= OnNavCompleted;

                if (!e.IsSuccess)
                {
                    Debug.WriteLine($"Navigation failed: {e.WebErrorStatus}");
                    await CleanupAsync(native);
                    return;
                }

                try
                {
                    native.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ShowPrintUI failed: {ex.Message}");
                    await CleanupAsync(native);
                }
            }

            native.NavigationCompleted += OnNavCompleted;

            string fileName = Path.GetFileName(printCopyPath);
            string url = $"https://appassets/{Uri.EscapeDataString(fileName)}";

            try
            {
                native.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex.Message}");
                await CleanupAsync(native);
            }
        });
    }

    private static async Task<string> CreatePrintCopyAsync(string sourcePath)
    {
        try
        {
            var safeName = Path.GetFileNameWithoutExtension(sourcePath);
            var dest = Path.Combine(FileSystem.CacheDirectory, $"print_{safeName}_{DateTime.UtcNow.Ticks}.pdf");
            using var input = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using var output = File.Create(dest);
            await input.CopyToAsync(output);
            return dest;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreatePrintCopyAsync failed: {ex.Message}");
            return sourcePath;
        }
    }

    private static Task OpenInDefaultViewerAsync(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenInDefaultViewerAsync failed: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}
#endif