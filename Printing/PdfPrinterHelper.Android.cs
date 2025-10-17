#if ANDROID
using Android.Content;
using Android.Print;
using Android.OS;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    static public partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        try
        {
            var context = Platform.CurrentActivity ?? Platform.AppContext;
            var printManager = (PrintManager?)context.GetSystemService(Context.PrintService) ?? 
                              throw new InvalidOperationException("Printing not supported on this device.");

            // Create a PrintDocumentAdapter for an existing PDF file
            var printAdapter = new PrintFileDocumentAdapter(context, filePath);

            // Launch Android’s native print dialog
            printManager.Print("Print PDF", printAdapter, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Android printing failed: {ex.Message}");
            // Fallback to sharing (email option)
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Send PDF via email",
                File = new ShareFile(filePath)
            });
        }
    }

    private class PrintFileDocumentAdapter : PrintDocumentAdapter
    {
        private readonly Context _context;
        private readonly string _filePath;

        public PrintFileDocumentAdapter(Context context, string filePath)
        {
            _context = context;
            _filePath = filePath;
        }

        public override void OnLayout(PrintAttributes? oldAttributes, PrintAttributes? newAttributes,
            CancellationSignal? cancellationSignal, LayoutResultCallback? callback, Bundle? extras)
        {
            var info = new PrintDocumentInfo.Builder(System.IO.Path.GetFileName(_filePath))
                .SetContentType(PrintContentType.Document)
                .Build();

            callback?.OnLayoutFinished(info, true);
        }

        public override void OnWrite(PageRange[]? pages, ParcelFileDescriptor? destination,
            CancellationSignal? cancellationSignal, WriteResultCallback? callback)
        {
            try
            {
                // Simply copy the existing PDF bytes into Android’s destination stream
                using var input = System.IO.File.OpenRead(_filePath);
                using var output = new Java.IO.FileOutputStream(destination!.FileDescriptor);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, bytesRead);

                output.Flush();
                callback?.OnWriteFinished(new[] { PageRange.AllPages ?? new PageRange(0, int.MaxValue) });
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("PdfPrinter", $"Error printing PDF: {ex}");
                callback?.OnWriteFailed(ex.Message);
            }
        }
    }
}
#endif
