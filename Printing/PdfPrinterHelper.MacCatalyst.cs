#if MACCATALYST
using Foundation;
using Microsoft.Maui.ApplicationModel;
using System.IO;
using UIKit;

namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    public static partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!UIPrintInteractionController.PrintingAvailable)
                return;

            var ctrl = UIPrintInteractionController.SharedPrintController;

            var info = UIPrintInfo.PrintInfo;
            info.OutputType = UIPrintInfoOutputType.General;
            info.JobName = Path.GetFileName(filePath);
            ctrl.PrintInfo = info;
            ctrl.PrintingItem = NSUrl.FromFilename(filePath);

            ctrl.Present(true, null);
        });
    }
}
#endif