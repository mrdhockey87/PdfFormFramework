using Maui.PDFView;
using PdfFormFramework.Models;
using PdfFormFramework.Services;
using System.Timers;

namespace PdfFormFramework.Controls;

public class PdfInteractiveFormView<TModel> : ContentView where TModel : class, new()
{
    // Use a Grid to ensure proper layout
    readonly Grid _mainGrid = new();

    // Use a ScrollView for scrolling capability
    readonly ScrollView _scrollView = new();
    readonly AbsoluteLayout _layout = [];
    readonly PdfView _pdfView = new();

    List<PdfFieldDefinition> _fields = [];
    PdfFieldService? _fieldService;
    PdfDataBindingService<TModel> _binding = new();
    string? _tempPdf;
    private System.Timers.Timer? _layoutTimer;

    // Default PDF dimensions (A4 size in points)
    private const double DefaultPdfWidth = 595;
    private const double DefaultPdfHeight = 842;

    // Track the actual dimensions for scaling
    private double _pdfWidth = DefaultPdfWidth;
    private double _pdfHeight = DefaultPdfHeight;
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    // For debugging
    private bool _debugLayout = true;

    public event EventHandler<string>? OnPrintRequest;

    public TModel? Model { get; set; }

    public PdfInteractiveFormView()
    {
        Unloaded += ContentView_Unloaded;
        Loaded += ContentView_Loaded;

        // Set up main grid
        _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        // Configure PDF view to fill the absolute layout
        _pdfView.BackgroundColor = Colors.White;
        AbsoluteLayout.SetLayoutBounds(_pdfView, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_pdfView, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        _layout.Children.Add(_pdfView);

        // Set ZIndex for proper layering
        _pdfView.ZIndex = 0; // Ensure PDF is at the back
        _layout.ZIndex = 0;

        // Set background color for better visibility
        _layout.BackgroundColor = Colors.White;

        // Configure ScrollView to allow scrolling
        _scrollView.Content = _layout;
        _scrollView.Orientation = ScrollOrientation.Vertical;
        _scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Always;

        // Add ScrollView to the Grid
        _mainGrid.Add(_scrollView, 0, 0);

        // Set the content of this control
        Content = _mainGrid;

        // Add size change handler to update scaling when layout changes
        SizeChanged += OnSizeChanged;

        // Log initialization
        Console.WriteLine("PdfInteractiveFormView initialized");
    }

    private void ContentView_Loaded(object? sender, EventArgs e)
    {
        Console.WriteLine("PdfInteractiveFormView loaded");
        // When the control is loaded, make sure to update the layout
        UpdateLayoutSizing();
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        Console.WriteLine($"Size changed: {Width}x{Height}");
        // When control size changes, update the layout and scaling factors
        UpdateLayoutSizing();
    }

    private void UpdateLayoutSizing()
    {
        if (Width <= 0 || Height <= 0) return;

        // Set minimum height for the layout to ensure scrolling works
        _layout.HeightRequest = Math.Max(Height, _pdfHeight * _scaleY + 20);
        _layout.WidthRequest = Width;

        // Enable scrolling explicitly
        _scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        _scrollView.HorizontalScrollBarVisibility = ScrollBarVisibility.Never;

        // Calculate scaling factors to position overlays correctly
        _scaleX = Width / _pdfWidth;
        // Use same scale factor for Y to maintain aspect ratio
        _scaleY = _scaleX;

        Console.WriteLine($"Layout sizing updated: PDF={_pdfWidth}x{_pdfHeight}, View={Width}x{Height}, Scale={_scaleX}");

        // Update overlay positions with new scaling
        UpdateOverlayPositions();
    }

    private void UpdateOverlayPositions()
    {
        // Skip if no fields defined or service not initialized
        if (_fields.Count == 0 || _fieldService == null)
        {
            Console.WriteLine("No fields to overlay");
            return;
        }

        Console.WriteLine($"Updating overlay positions for {_fields.Count} fields");

        // Remove existing overlay controls (keeping only PDF view)
        for (int i = _layout.Children.Count - 1; i > 0; i--)
        {
            _layout.Children.RemoveAt(i);
        }

        // Re-create overlay controls with updated scaling
        foreach (var view in PdfFormOverlayService.CreateFieldViews(_fields, _pdfHeight))
        {
            // Get the current bounds
            var currentBounds = AbsoluteLayout.GetLayoutBounds(view);

            // Apply scaling to position correctly
            var newBounds = new Rect(
                currentBounds.X * _scaleX,
                currentBounds.Y * _scaleY,
                Math.Max(currentBounds.Width * _scaleX, 40), // Ensure minimum width
                Math.Max(currentBounds.Height * _scaleY, 30) // Ensure minimum height
            );

            // Add color to make controls more visible
            if (view is Entry entry)
            {
                entry.BackgroundColor = Colors.White;
                entry.TextColor = Colors.Black;
                entry.Opacity = 0.9;
            }
            else if (view is Editor editor)
            {
                editor.BackgroundColor = Colors.White;
                editor.TextColor = Colors.Black;
                editor.Opacity = 0.9;
            }
            else if (view is CheckBox checkBox)
            {
                checkBox.Color = Colors.Blue;
                checkBox.Scale = 1.2;
            }
            else if (view is Picker picker)
            {
                picker.BackgroundColor = Colors.White;
                picker.TextColor = Colors.Black;
                picker.Opacity = 0.9;
            }

            // Set ZIndex to ensure control is on top
            view.ZIndex = 10;

            Console.WriteLine($"Positioning control at {newBounds}");

            AbsoluteLayout.SetLayoutBounds(view, newBounds);
            _layout.Children.Add(view);
        }

        Console.WriteLine($"Added {_layout.Children.Count - 1} overlay controls");
    }

    public void LoadPdfGz(string gzPath, TModel? dataModel = null)
    {
        Console.WriteLine($"Loading PDF from {gzPath}");
        _tempPdf = PdfCompressionService.DecompressGzToTempPdf(gzPath);
        _fieldService = new PdfFieldService(_tempPdf);

        // Set the URI to load the PDF
        _pdfView.Uri = _tempPdf;
        Model = dataModel ?? new TModel();

        // Get dimensions from the PDF file using PdfSharp
        try
        {
            using var document = PdfSharp.Pdf.IO.PdfReader.Open(_tempPdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.ReadOnly);
            if (document.Pages.Count > 0)
            {
                var page = document.Pages[0];
                _pdfWidth = page.Width.Point;
                _pdfHeight = page.Height.Point;
                Console.WriteLine($"Got PDF dimensions: {_pdfWidth}x{_pdfHeight}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting PDF dimensions: {ex.Message}");
        }

        // Wait a moment for the PDF to render before building the form
        DelayedBuildForm();
    }

    private void DelayedBuildForm()
    {
        // Cancel any existing timer
        _layoutTimer?.Stop();
        _layoutTimer?.Dispose();

        Console.WriteLine("Starting delayed build form");

        // Create a new timer to delay form building
        _layoutTimer = new System.Timers.Timer(1000); // 1 second delay
        _layoutTimer.Elapsed += (s, e) =>
        {
            // Run on UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Console.WriteLine("Delayed build form running");
                _layoutTimer?.Stop();
                _layoutTimer?.Dispose();
                _layoutTimer = null;
                BuildForm();

                // Add a second update after a short delay for layout refresh
                MainThread.BeginInvokeOnMainThread(async () => {
                    await Task.Delay(500);
                    UpdateLayoutSizing();
                });
            });
        };
        _layoutTimer.AutoReset = false;
        _layoutTimer.Start();
    }

    void BuildForm()
    {
        if (_fieldService == null) return;

        Console.WriteLine("Building form");
        _fields = _fieldService.ExtractFields();
        _binding.FromModel(_fields, Model!);

        Console.WriteLine($"Extracted {_fields.Count} fields");

        // Register value change callbacks
        foreach (var f in _fields)
        {
            f.OnValueChanged = v => f.Value = v;
        }

        // Create and position overlay controls
        UpdateLayoutSizing();

        // Force refresh layout
        InvalidateLayout();
    }

    public void SaveModelData()
    {
        if (Model != null)
        {
            var updated = _binding.ToModel(_fields);
            foreach (var p in typeof(TModel).GetProperties())
                p.SetValue(Model, p.GetValue(updated));
        }
    }

    public void PrintForm()
    {
        _fieldService?.ApplyFieldValues(_binding.ToDictionary(_fields));
        if (_tempPdf != null)
            OnPrintRequest?.Invoke(this, _tempPdf);
    }

    protected void ContentView_Unloaded(object? sender, EventArgs e)
    {
        // Clean up timer if it exists
        _layoutTimer?.Stop();
        _layoutTimer?.Dispose();
        _layoutTimer = null;

        // Clean up the temp file
        if (_tempPdf != null) TempFileService.Cleanup(_tempPdf);

        // Clean up event handlers
        SizeChanged -= OnSizeChanged;
        Loaded -= ContentView_Loaded;
    }
}