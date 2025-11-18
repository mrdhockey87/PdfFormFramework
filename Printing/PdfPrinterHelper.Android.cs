#if ANDROID
using Android.Content;
using Android.OS;
using Android.Print;
using Microsoft.Maui.ApplicationModel;
using System.IO;
using SIO = System.IO;      // alias .NET IO
using JIO = Java.IO;        // alias Java IO

namespace PdfFormFramework.Printing;

public partial class PdfPrinterHelper
{
    public static partial async Task PlatformPrintOrEmailAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !SIO.File.Exists(filePath))
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity;
            if (activity is null) return;

            var printManager = (PrintManager)activity.GetSystemService(Context.PrintService)!;
            var adapter = new PdfFilePrintDocumentAdapter(activity, filePath);
            var jobName = SIO.Path.GetFileName(filePath);

            printManager.Print(jobName, adapter, null);
        });
    }

    private sealed class PdfFilePrintDocumentAdapter : PrintDocumentAdapter
    {
        private readonly Context _context;
        private readonly string _filePath;

        public PdfFilePrintDocumentAdapter(Context context, string filePath)
        {
            _context = context;
            _filePath = filePath;
        }

        public override void OnLayout(PrintAttributes? oldAttributes, PrintAttributes? newAttributes,
            CancellationSignal? cancellationSignal, PrintDocumentAdapter.LayoutResultCallback? callback, Bundle? extras)
        {
            if (cancellationSignal?.IsCanceled == true)
            {
                callback?.OnLayoutCancelled();
                return;
            }

            var info = new PrintDocumentInfo.Builder(SIO.Path.GetFileName(_filePath))
                .SetContentType(PrintContentType.Document)
                .SetPageCount(PrintDocumentInfo.PageCountUnknown)
                .Build();

            callback?.OnLayoutFinished(info, true);
        }

        public override void OnWrite(PageRange[]? pages, ParcelFileDescriptor? destination,
                                     CancellationSignal? cancellationSignal, WriteResultCallback? callback)
        {
            try
            {
                if (destination?.FileDescriptor == null)
                {
                    callback?.OnWriteFailed("Destination file descriptor is null.");
                    return;
                }

                using var input = new JIO.FileInputStream(_filePath);                  // Java.IO in
                using var output = new JIO.FileOutputStream(destination.FileDescriptor); // Java.IO out

                var inputChannel = input.Channel;
                var outputChannel = output.Channel;

                if (inputChannel is null || outputChannel is null)
                {
                    callback?.OnWriteFailed("Unable to access file channels for printing.");
                    return;
                }

                _ = inputChannel.TransferTo(0, inputChannel.Size(), outputChannel);

                callback?.OnWriteFinished(pages: [PageRange.AllPages!]);
            }   
            catch (System.Exception ex)
            {
                callback?.OnWriteFailed(ex.Message);
            }
            finally
            {
                try { destination?.Close(); } catch { /* ignore */ }
            }
        }
    }
}
#endif