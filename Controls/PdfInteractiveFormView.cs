using Maui.PDFView;
using PdfFormFramework.Models;
using PdfFormFramework.Services;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

namespace PdfFormFramework.Controls;

public class PdfInteractiveFormView<TModel> : ContentView where TModel : class, new()
{
    // Use a Grid to ensure proper layout
    readonly Grid _mainGrid = new();

    // Use a PdfView for displaying the PDF
    private PdfView _pdfView;

    private string? _tempPdf;
    private string? _filledPdfPath;  // path returned by the filling service
    private string? _viewPdfPath;    // unique view copy path (cache-buster)
    private string? _sourceGzPath;   // original .pdf.gz path used to derive default filename
    private System.Timers.Timer? _layoutTimer;
    private PdfFormFillingService? _formFillingService;

    public event EventHandler<string>? OnPrintRequest;

    public TModel? Model { get; set; }

    // Prefer the current view copy; fall back to service/temp
    public string? CurrentPdfPath => _viewPdfPath ?? _formFillingService?.GetPdfPath() ?? _tempPdf;

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
            _sourceGzPath = gzPath;

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
            _filledPdfPath = await _formFillingService.FillFormWithModelAsync(model);
            Debug.WriteLine($"PDF filled with model data, saved to: {_filledPdfPath}");

            // Store the model
            Model = model;

            // Update the PDF view
            if (!string.IsNullOrEmpty(_filledPdfPath) && File.Exists(_filledPdfPath))
            {
                await RecreateAndLoadPdfView(_filledPdfPath);
            }
            else
            {
                Debug.WriteLine($"ERROR: Filled PDF file does not exist at path: {_filledPdfPath}");
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
            // Clean up old view copy if any
            if (!string.IsNullOrEmpty(_viewPdfPath) && File.Exists(_viewPdfPath))
            {
                try { File.Delete(_viewPdfPath); } catch { /* ignore */ }
            }

            // Create a unique view copy for cache-busting
            var dir = Path.GetDirectoryName(pdfPath) ?? "";
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            _viewPdfPath = Path.Combine(dir, $"{baseName}_{DateTime.UtcNow.Ticks}_view.pdf");
            File.Copy(pdfPath, _viewPdfPath, true);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Complete control replacement for clean reload
                _mainGrid.Children.Clear();
                _pdfView = null;
                GC.Collect();

                // Create new PDF viewer
                _pdfView = new PdfView { BackgroundColor = Colors.White };
                _mainGrid.Add(_pdfView);

                // Load the PDF using the unique path to avoid caching old content
                _pdfView.Uri = _viewPdfPath;
            });

            // Delete the filled (source) PDF after first load so future loads regenerate fresh data
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); // give the viewer time to open the copy
                try
                {
                    if (!string.IsNullOrEmpty(_filledPdfPath) && File.Exists(_filledPdfPath))
                    {
                        File.Delete(_filledPdfPath);
                        Debug.WriteLine($"Deleted filled PDF: {_filledPdfPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting filled PDF: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PDF view recreation error: {ex.Message}");
        }
    }

    // Share/Print from the framework (cross-platform)
    public async Task<bool> PrintAsync()
    {
        try
        {
            var pdfPath = CurrentPdfPath;
            if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
                return false;

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Print PDF",
                File = new ShareFile(pdfPath)
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Print/share failed: {ex.Message}");
            // Fallback: open with default viewer
            try
            {
                var pdfPath = CurrentPdfPath;
                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    await Launcher.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(pdfPath),
                        Title = "Open PDF"
                    });
                    return true;
                }
            }
            catch { /* ignore */ }
            return false;
        }
    }

    // Back-compat: keep existing call; raise event and also try built-in PrintAsync
    public void PrintForm()
    {
        var path = CurrentPdfPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            OnPrintRequest?.Invoke(this, path);
            _ = PrintAsync();
        }
    }

    // Save As from the framework with default filename composition.
    // Provide a save picker via delegate; if null, falls back to AppDataDirectory.
    // Delegate signature: (defaultFileName, sourceStream, ct) => returns saved file path or null if cancelled.
    public async Task<string?> SaveAsAsync(
        Func<string, Stream, CancellationToken, Task<string?>>? saveWithPicker = null,
        string? formName = null,
        CancellationToken ct = default)
    {
        try
        {
            var src = CurrentPdfPath;
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
                return null;

            var defaultFileName = ComposeDefaultFileName(formName);
            await using var input = File.OpenRead(src);

            // Use provided picker if available
            if (saveWithPicker != null)
            {
                var pickedPath = await saveWithPicker(defaultFileName, input, ct);
                return pickedPath;
            }

            // Fallback: save to AppDataDirectory with the default filename
            var dest = Path.Combine(FileSystem.AppDataDirectory, defaultFileName);
            input.Position = 0;
            await using (var output = File.Create(dest))
                await input.CopyToAsync(output, ct);

            return dest;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving filled PDF: {ex.Message}");
            return null;
        }
    }

    private string ComposeDefaultFileName(string? formName)
    {
        string first = MakeSafe(GetModelString("FirstName"));
        string last = MakeSafe(GetModelString("LastName"));
        string form = MakeSafe(formName ?? GetModelString("FormName") ?? "Form");

        string originalPdfName = "document.pdf";
        if (!string.IsNullOrEmpty(_sourceGzPath))
        {
            var justName = Path.GetFileName(_sourceGzPath);
            if (justName.EndsWith(".pdf.gz", StringComparison.OrdinalIgnoreCase))
            {
                // remove only .gz
                originalPdfName = justName[..^3]; // keep the ".pdf"
            }
            else
            {
                // ensure .pdf extension
                originalPdfName = Path.ChangeExtension(justName, ".pdf");
            }
        }

        var parts = new[] { first, last, form, originalPdfName }.Where(p => !string.IsNullOrWhiteSpace(p));
        var fileName = string.Join("_", parts);
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            fileName += ".pdf";
        return fileName;
    }

    private static string MakeSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray());
        cleaned = string.Join("_", cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return cleaned;
    }

    private string? GetModelString(string propertyName)
    {
        try
        {
            if (Model is null) return null;
            var prop = typeof(TModel).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.CanRead == true)
            {
                var val = prop.GetValue(Model)?.ToString();
                return val;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    // Existing "Save model data" no-op
    public void SaveModelData()
    {
        Debug.WriteLine("Model data saved");
    }

    protected void ContentView_Unloaded(object? sender, EventArgs e)
    {
        // Clean up timer if it exists
        _layoutTimer?.Stop();
        _layoutTimer?.Dispose();
        _layoutTimer = null;

        // Clean up the temp file(s)
        void TryDelete(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        TryDelete(_tempPdf);
        TryDelete(_filledPdfPath);
        TryDelete(_viewPdfPath);

        _tempPdf = null;
        _filledPdfPath = null;
        _viewPdfPath = null;

        // Clean up event handlers
        Unloaded -= ContentView_Unloaded;
    }
}