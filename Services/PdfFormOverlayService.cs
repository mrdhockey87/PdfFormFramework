using Microsoft.Maui.Layouts;
using PdfFormFramework.Models;

namespace PdfFormFramework.Services;

public static class PdfFormOverlayService
{
    public static IEnumerable<View> CreateFieldViews(List<PdfFieldDefinition> fields, double pdfHeight)
    {
        foreach (var f in fields)
        {
            View control = f.FieldType switch
            {
                PdfFieldType.Text => new Entry
                {
                    Text = f.Value,
                    Placeholder = f.Name,
                    BackgroundColor = Colors.White,
                    TextColor = Colors.Black,
                    PlaceholderColor = Colors.Gray,
                    MinimumHeightRequest = 30,
                    Opacity = 0.9
                },
                PdfFieldType.MultiLineText => new Editor
                {
                    Text = f.Value,
                    AutoSize = EditorAutoSizeOption.TextChanges,
                    BackgroundColor = Colors.White,
                    TextColor = Colors.Black,
                    MinimumHeightRequest = 50,
                    Opacity = 0.9
                },
                PdfFieldType.CheckBox => new CheckBox
                {
                    IsChecked = f.Value.Equals("Yes", StringComparison.OrdinalIgnoreCase),
                    Color = Colors.Blue,
                    MinimumHeightRequest = 24,
                    MinimumWidthRequest = 24,
                    Scale = 1.2
                },
                PdfFieldType.ComboBox => new Picker
                {
                    ItemsSource = f.Options ?? [],
                    SelectedItem = f.Value,
                    BackgroundColor = Colors.White,
                    TextColor = Colors.Black,
                    MinimumHeightRequest = 30,
                    Opacity = 0.9
                },
                _ => new Label
                {
                    Text = f.Name,
                    BackgroundColor = Colors.Yellow.WithAlpha(0.5f),
                    TextColor = Colors.Black,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };

            // Set binding context
            control.BindingContext = f;

            // Set ZIndex to ensure control is on top of PDF
            control.ZIndex = 10;

            // Set up event handlers
            switch (control)
            {
                case Entry e:
                    e.TextChanged += (_, ev) => f.OnValueChanged?.Invoke(ev.NewTextValue);
                    break;
                case Editor e:
                    e.TextChanged += (_, ev) => f.OnValueChanged?.Invoke(ev.NewTextValue);
                    break;
                case CheckBox c:
                    c.CheckedChanged += (_, ev) => f.OnValueChanged?.Invoke(ev.Value ? "Yes" : "Off");
                    break;
                case Picker p:
                    p.SelectedIndexChanged += (_, ev) => f.OnValueChanged?.Invoke(p.SelectedItem?.ToString() ?? "");
                    break;
            }

            // Flip Y coordinate (PDF coords are from bottom-left, MAUI is from top-left)
            double y = pdfHeight - f.Bounds.Y - f.Bounds.Height;

            // Set bounds in the absolute layout
            AbsoluteLayout.SetLayoutBounds(control, new Rect(f.Bounds.X, y, f.Bounds.Width, f.Bounds.Height));
            AbsoluteLayout.SetLayoutFlags(control, AbsoluteLayoutFlags.None);

            // Return the control
            yield return control;
        }
    }
}