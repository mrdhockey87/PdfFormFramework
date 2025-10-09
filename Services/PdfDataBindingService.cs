using System.Reflection;
using PdfFormFramework.Models;

namespace PdfFormFramework.Services;

public class PdfDataBindingService<TModel> where TModel : class, new()
{
    public TModel ToModel(List<PdfFieldDefinition> fields)
    {
        var model = new TModel();
        foreach (var f in fields)
        {
            var prop = typeof(TModel).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                object? val = Convert.ChangeType(f.Value, prop.PropertyType);
                prop.SetValue(model, val);
            }
        }
        return model;
    }

    public void FromModel(List<PdfFieldDefinition> fields, TModel model)
    {
        foreach (var f in fields)
        {
            var prop = typeof(TModel).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanRead)
            {
                f.Value = prop.GetValue(model)?.ToString() ?? "";
            }
        }
    }

    public Dictionary<string, string> ToDictionary(List<PdfFieldDefinition> fields) =>
        fields.ToDictionary(f => f.Name, f => f.Value);
}
