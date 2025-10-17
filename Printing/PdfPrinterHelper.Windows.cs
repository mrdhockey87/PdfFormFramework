#if WINDOWS
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls; // for Page, NavigationPage, Shell, TabbedPage
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration; // <-- use WinRT to enumerate printers

namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool EnumPrinters(
        int flags, string name, int level, IntPtr pPrinterEnum,
        int cbBuf, out int pcbNeeded, out int pcReturned);

    private const int PRINTER_ENUM_LOCAL = 0x00000002;
    private const int PRINTER_ENUM_CONNECTIONS = 0x00000004;

    public static partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        bool hasPrinter = HasAnyPrinterAsync();

        if (hasPrinter)
        {
            // Try to invoke the default app's print for this PDF
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "print",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Print verb failed: {ex.Message}");

                // Fallback: open in default handler so user can print manually
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    return;
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"Open fallback failed: {ex2.Message}");
                    // Continue to email prompt below
                }
            }
        }

        // No printers found or printing failed: ask user to email
        bool sendEmail = false;
        try
        {
            var page = GetActivePage();
            if (page != null)
            {
                sendEmail = await MainThread.InvokeOnMainThreadAsync(() =>
                    page.DisplayAlert(
                        "No printer available",
                        "No printers were found. Would you like to email the form instead?",
                        "Email", "Cancel"));
            }
            else
            {
                sendEmail = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Prompt failed: {ex.Message}");
            sendEmail = true;
        }

        if (sendEmail)
        {
            try
            {
                var message = new EmailMessage
                {
                    Subject = "Completed Form",
                    Body = "Please find the completed form attached."
                };
                message.Attachments.Add(new EmailAttachment(filePath));
                await Email.Default.ComposeAsync(message);
            }
            catch (FeatureNotSupportedException)
            {
                // Email not supported: fall back to share UI
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Share PDF",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Email composition failed: {ex.Message}");
            }
        }
    }

    private static bool HasAnyPrinterAsync()
    {
        try
        {

            int flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
            EnumPrinters(flags, "", 2, IntPtr.Zero, 0, out int cbNeeded, out _);
            return cbNeeded > 0;
        }
        catch
        {
            return false;
        }
    }

    // Resolve the active page without using obsolete Application.Current.MainPage
    private static Page? GetActivePage()
    {
        var window = Application.Current?.Windows?.FirstOrDefault();
        var page = window?.Page;
        return GetTopPage(page);
    }

    private static Page? GetTopPage(Page? root)
    {
        if (root == null) return null;

        if (root.Navigation?.ModalStack?.Count > 0)
            return root.Navigation.ModalStack.Last();

        return root switch
        {
            NavigationPage nav => GetTopPage(nav.CurrentPage),
            TabbedPage tab => GetTopPage(tab.CurrentPage),
            Shell shell => GetTopPage(shell.CurrentPage),
            _ => root
        };
    }
}
#endif