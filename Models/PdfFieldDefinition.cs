namespace PdfFormFramework.Models;

public class PdfFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public PdfFieldType FieldType { get; set; } = PdfFieldType.Unknown;
    public Rect Bounds { get; set; }
    public string Value { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public Action<string>? OnValueChanged { get; set; }
}
