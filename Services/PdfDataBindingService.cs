using System.Reflection;
using PdfFormFramework.Models;
using System.Diagnostics;

namespace PdfFormFramework.Services;

public class PdfDataBindingService<TModel> where TModel : class, new()
{
    public TModel ToModel(List<PdfFieldDefinition> fields)
    {
        var model = new TModel();
        foreach (var f in fields)
        {
            try
            {
                // Try to find property with exact name match first
                var prop = typeof(TModel).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance);

                // If no match, try case-insensitive match
                if (prop == null)
                {
                    prop = typeof(TModel).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                }

                // If still no match, try with normalized name (no spaces, special chars)
                if (prop == null)
                {
                    var normalizedFieldName = NormalizeFieldName(f.Name);
                    var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    prop = properties.FirstOrDefault(p => NormalizeFieldName(p.Name).Equals(normalizedFieldName, StringComparison.OrdinalIgnoreCase));
                }

                if (prop != null && prop.CanWrite)
                {
                    Debug.WriteLine($"Binding field '{f.Name}' to property '{prop.Name}'");
                    object? val = ConvertValue(f.Value, prop.PropertyType);
                    prop.SetValue(model, val);
                }
                else
                {
                    Debug.WriteLine($"No matching property found for field '{f.Name}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error binding field '{f.Name}': {ex.Message}");
            }
        }
        return model;
    }

    public void FromModel(List<PdfFieldDefinition> fields, TModel model)
    {
        var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            try
            {
                // Try to find property with exact name match first
                var prop = typeof(TModel).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance);

                // If no match, try case-insensitive match
                if (prop == null)
                {
                    prop = typeof(TModel).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                }

                // If still no match, try with normalized name (no spaces, special chars)
                if (prop == null)
                {
                    var normalizedFieldName = NormalizeFieldName(f.Name);
                    prop = properties.FirstOrDefault(p => NormalizeFieldName(p.Name).Equals(normalizedFieldName, StringComparison.OrdinalIgnoreCase));
                }

                if (prop != null && prop.CanRead)
                {
                    var value = prop.GetValue(model);
                    string stringValue = GetStringValue(value, f.FieldType);
                    Debug.WriteLine($"Setting field '{f.Name}' from property '{prop.Name}' with value: '{stringValue}'");
                    f.Value = stringValue;
                }
                else
                {
                    Debug.WriteLine($"No matching property found for field '{f.Name}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error binding property to field '{f.Name}': {ex.Message}");
            }
        }
    }

    public Dictionary<string, string> ToDictionary(List<PdfFieldDefinition> fields)
    {
        var dict = fields.ToDictionary(f => f.Name, f => f.Value);
        Debug.WriteLine($"Created dictionary with {dict.Count} field values");
        return dict;
    }

    private string NormalizeFieldName(string name)
    {
        // Remove spaces, punctuation, and special characters
        var result = new string(name
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());
        return result;
    }

    private object? ConvertValue(string value, Type targetType)
    {
        try
        {
            if (string.IsNullOrEmpty(value))
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType == typeof(bool))
                return value.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("True", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("1");

            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }

    private string GetStringValue(object? value, PdfFieldType fieldType)
    {
        if (value == null)
            return "";

        if (value is bool boolValue)
        {
            return fieldType == PdfFieldType.CheckBox
                ? (boolValue ? "Yes" : "Off")
                : boolValue.ToString();
        }

        return value.ToString() ?? "";
    }
}