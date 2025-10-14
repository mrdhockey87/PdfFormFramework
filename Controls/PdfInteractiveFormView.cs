using Maui.PDFView;
using PdfFormFramework.Models;
using PdfFormFramework.Services;
using System.Timers;
using System.Diagnostics;

namespace PdfFormFramework.Controls;

public class PdfInteractiveFormView<TModel> : ContentView where TModel : class, new()
{
    // Use a Grid to ensure proper layout
    readonly Grid _mainGrid = new();

    // Use a PdfView for displaying the PDF
    private PdfView _pdfView;

    private string? _tempPdf;
    private System.Timers.Timer? _layoutTimer;
    private PdfFormFillingService? _formFillingService;

    public event EventHandler<string>? OnPrintRequest;

    public TModel? Model { get; set; }

    public PdfInteractiveFormView()
    {
        Unloaded += ContentView_Unloaded;

        // Initialize PDF view
        _pdfView = new PdfView
        {
            BackgroundColor = Colors.White
        };

        // Set up main grid
        _mainGrid.Add(_pdfView, 0, 0);

        // Set the content of this control
        Content = _mainGrid;

        Debug.WriteLine("PdfInteractiveFormView initialized");
    }

    public async Task LoadPdfGz(string gzPath, TModel? dataModel = null)
    {
        Debug.WriteLine($"Loading PDF from {gzPath}");

        try
        {
            // Decompress the PDF
            _tempPdf = PdfCompressionService.DecompressGzToTempPdf(gzPath);
            Debug.WriteLine($"Decompressed PDF saved to: {_tempPdf}");

            // Initialize the form filling service
            _formFillingService = new PdfFormFillingService(_tempPdf);

            // Set model or create a new one
            Model = dataModel ?? new TModel();

            // Fill the PDF with model data before displaying
            if (Model != null)
            {
                await FillFormWithModelAsync(Model);
                return; // FillFormWithModelAsync already handles displaying the PDF
            }

            // Set the URI to load the filled PDF
            string pdfPath = _formFillingService.GetPdfPath();
            Debug.WriteLine($"Loading PDF from path: {pdfPath}");

            if (File.Exists(pdfPath))
            {
                await RecreateAndLoadPdfView(pdfPath);
            }
            else
            {
                Debug.WriteLine($"ERROR: PDF file does not exist at path: {pdfPath}");
            }

            Debug.WriteLine("PDF loaded and form filled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading PDF: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
        }
    }

    public async Task FillFormWithModelAsync(TModel model)
    {
        if (_formFillingService == null)
        {
            Debug.WriteLine("Form filling service is null");
            return;
        }

        Debug.WriteLine("Filling form with model data");

        try
        {
            // Fill the form with model data
            string filledPdfPath = await _formFillingService.FillFormWithModelAsync(model);
            Debug.WriteLine($"PDF filled with model data, saved to: {filledPdfPath}");

            // Store the model
            Model = model;

            // Update the PDF view
            if (File.Exists(filledPdfPath))
            {
                await RecreateAndLoadPdfView(filledPdfPath);
            }
            else
            {
                Debug.WriteLine($"ERROR: Filled PDF file does not exist at path: {filledPdfPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error filling form: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
        }
    }

    private async Task RecreateAndLoadPdfView(string pdfPath)
    {
        try
        {
            // Create a separate copy for viewing
            string viewCopy = Path.Combine(
                Path.GetDirectoryName(pdfPath) ?? "",
                Path.GetFileNameWithoutExtension(pdfPath) + "_view.pdf");
            File.Copy(pdfPath, viewCopy, true);

            await MainThread.InvokeOnMainThreadAsync(() => {
                // Complete control replacement for clean reload
                _mainGrid.Children.Clear();
                _pdfView = null;
                GC.Collect();

                // Create new PDF viewer
                _pdfView = new PdfView { BackgroundColor = Colors.White };
                _mainGrid.Add(_pdfView);

                // Load the PDF
                _pdfView.Uri = viewCopy;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PDF view recreation error: {ex.Message}");
        }
    }
    public void SaveModelData()
    {
        // Since we're using the native PDF form, we don't need to save data from UI controls
        // The data is already in the model, and the PDF has been filled with that data
        Debug.WriteLine("Model data saved");
    }

    public void PrintForm()
    {
        string? pdfPath = _formFillingService?.GetPdfPath() ?? _tempPdf;
        if (pdfPath != null && File.Exists(pdfPath))
        {
            Debug.WriteLine($"Printing PDF from: {pdfPath}");
            OnPrintRequest?.Invoke(this, pdfPath);
        }
        else
        {
            Debug.WriteLine("No PDF file available for printing");
        }
    }

    protected void ContentView_Unloaded(object? sender, EventArgs e)
    {
        // Clean up timer if it exists
        _layoutTimer?.Stop();
        _layoutTimer?.Dispose();
        _layoutTimer = null;

        // Clean up the temp file
        if (_tempPdf != null)
        {
            try
            {
                TempFileService.Cleanup(_tempPdf);
                Debug.WriteLine($"Cleaned up temp file: {_tempPdf}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up temp file: {ex.Message}");
            }
        }

        // Clean up event handlers
        Unloaded -= ContentView_Unloaded;
    }
}