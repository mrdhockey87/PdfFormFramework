#if MACCATALYST
using AppKit;
using Foundation;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using UIKit;

namespace PdfFormFramework.Printing;
   
public partial class PdfPrinterHelper
{
    static public partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        try
        {
            var pdfData = NSData.FromFile(filePath);
            var printController = UIPrintInteractionController.SharedPrintController;
            if (printController == null)
                throw new InvalidOperationException("Printing not supported on this device.");

            printController.PrintingItem = pdfData; // Pass PDF directly
            printController.ShowsNumberOfCopies = true;
            printController.ShowsPaperSelectionForLoadedPapers = true;

            // Present print dialog modally (works on both iPad & MacCatalyst)
            printController.Present(true, (controller, completed, error) =>
            {
                if (error != null)
                    Console.WriteLine($"Print error: {error.LocalizedDescription}");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Printing failed: {ex.Message}");
            // Fallback to sharing (email option)
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Send PDF via email",
                File = new ShareFile(filePath)
            });
        }
    }
}
#endif
