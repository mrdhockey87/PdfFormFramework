#if WINDOWS
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// WebView2 (WinUI 3)
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

// WinForms + GDI
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using GdiImage = System.Drawing.Image; // disambiguate Image

// SkiaSharp placeholder renderer (swap with your PDF renderer)
using SkiaSharp;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
    
namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    // In-memory pages for GDI printing
    private static readonly List<GdiImage> s_pages = new();
    private static int s_pageIndex;

    // Entry: Prefer WinForms/GDI pipeline; fallback to WebView2; then default viewer
    public static partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        // 1) Preferred: WinForms printer dialog + GDI print (no Acrobat/PrintManager)
        try
        {
            var ok = await PrintWithWinFormsAsync(filePath);
            if (ok) return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WinForms/GDI print failed: {ex.Message}");
        }

        // 2) Fallback: system print dialog via WebView2 (Edge PDF viewer)
        try
        {
            await ShowSystemPrintDialogAsync(filePath);
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 print fallback failed: {ex.Message}");
        }

        // 3) Last resort: open in default viewer
        try
        {
            await OpenInDefaultViewerAsync(filePath);
        }
        catch { /* ignore */ }
    }

    // WinForms print dialog + GDI print pipeline
    private static async Task<bool> PrintWithWinFormsAsync(string filePath)
    {
        s_pages.Clear();
        var rendered = await RenderPdfToBitmapsAsync(filePath);
        s_pages.AddRange(rendered);
        if (s_pages.Count == 0) return false;

        bool printed = false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            using var dlg = new PrintDialog
            {
                UseEXDialog = true,
                AllowSomePages = false,
                AllowSelection = false,
                ShowNetwork = true
            };

            using var doc = new PrintDocument();
            doc.DocumentName = Path.GetFileName(filePath);

            // Fill the whole page: no margins, origin at physical page
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.OriginAtMargins = false;

            doc.PrintPage += OnPrintPage;

            dlg.Document = doc;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                s_pageIndex = 0;
                try
                {
                    doc.Print();
                    printed = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Print failed: {ex.Message}");
                    printed = false;
                }
            }
        });

        foreach (var img in s_pages) img.Dispose();
        s_pages.Clear();

        return printed;
    }

    private static void OnPrintPage(object? sender, PrintPageEventArgs e)
    {
        if (e.Graphics is null) return;
        
        var img = s_pages[s_pageIndex];

        // Let GDI scale the bitmap to fit the printable area. Keeps units consistent.
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode   = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        e.Graphics.DrawImage(img, e.MarginBounds);

        s_pageIndex++;
        e.HasMorePages = s_pageIndex < s_pages.Count;
    }

    // Render PDF pages to GDI bitmaps using Windows.Data.Pdf (no Pdfium required)
    private static async Task<List<GdiImage>> RenderPdfToBitmapsAsync(string filePath)
    {
        var outputs = new List<GdiImage>();

        try
        {
            using var fs = File.OpenRead(filePath);
            IRandomAccessStream ras = fs.AsRandomAccessStream();

            PdfDocument pdf = await PdfDocument.LoadFromStreamAsync(ras);
            uint pageCount = pdf.PageCount;
            const double targetDpi = 300.0; // adjust as desired

            for (uint i = 0; i < pageCount; i++)
            {
                using PdfPage page = pdf.GetPage(i);

                // Convert page size from DIPs (1/96") to pixels at target DPI
                var sizeDip = page.Size;
                uint pxW = (uint)Math.Max(1, Math.Round(sizeDip.Width  * (targetDpi / 96.0)));
                uint pxH = (uint)Math.Max(1, Math.Round(sizeDip.Height * (targetDpi / 96.0)));

                using var pageStream = new InMemoryRandomAccessStream();
                var opts = new PdfPageRenderOptions
                {
                    DestinationWidth  = pxW,
                    DestinationHeight = pxH,
                    BackgroundColor   = Windows.UI.Color.FromArgb(255, 255, 255, 255) // white bg
                };
                await page.RenderToStreamAsync(pageStream, opts);
                pageStream.Seek(0);

                // Decode frame and re-encode as PNG
                var decoder = await BitmapDecoder.CreateAsync(pageStream);
                var softBmp = await decoder.GetSoftwareBitmapAsync();
                var converted = SoftwareBitmap.Convert(
                    softBmp,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                using var pngStream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);
                encoder.SetSoftwareBitmap(converted);
                await encoder.FlushAsync();
                pngStream.Seek(0);

                // GDI image from managed stream; clone to detach from stream
                using var managed = pngStream.AsStreamForRead();
                using var ms = new MemoryStream();
                await managed.CopyToAsync(ms);
                ms.Position = 0;

                using var img = GdiImage.FromStream(ms);
                var clone = new Bitmap(img);
                clone.SetResolution((float)targetDpi, (float)targetDpi); // helps scaling
                outputs.Add(clone);

                // Optional: write first page for debugging
                // if (i == 0) clone.Save(Path.Combine(FileSystem.CacheDirectory, "rendered_page1.png"));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RenderPdfToBitmapsAsync (Windows.Data.Pdf) failed: {ex.Message}");
        }

        return outputs;
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