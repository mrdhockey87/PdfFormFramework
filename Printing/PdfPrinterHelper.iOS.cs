#if IOS
using UIKit;
using Foundation;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    static public partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        try
        {
            var controller = UIPrintInteractionController.SharedPrintController;
            controller.PrintingItem = NSData.FromFile(filePath);
            controller.Present(true, null);
        }
        catch
        {
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Send PDF via email",
                File = new ShareFile(filePath)
            });
        }
    }
}
#endif