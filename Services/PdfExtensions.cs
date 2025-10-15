using PdfSharp.Pdf;
using System.Diagnostics;

namespace PdfFormFramework.Services;

/// <summary>
/// Extensions and helper classes for PDF operations
/// </summary>
public static class PdfExtensions
{
    /// <summary>
    /// Sets a boolean value in the PDF dictionary
    /// </summary>
    public static void SetBoolean(this PdfDictionary dictionary, string key, bool value)
    {
        try
        {
            // Try to directly set the value using a string
            dictionary.Elements.SetString(key, value ? "true" : "false");
        }
        catch
        {
            // If that fails, try using a name
            try
            {
                dictionary.Elements.SetName(key, value ? "/True" : "/False");
            }
            catch
            {
                // As a last resort, try using an integer (1 for true, 0 for false)
                dictionary.Elements.SetInteger(key, value ? 1 : 0);
            }
        }
    }
    
    /// <summary>
    /// Sets a combo box value in the PDF dictionary using the most effective method
    /// </summary>
    public static bool SetComboValue(this PdfDictionary fieldDict, string value)
    {
        try
        {
            // Get the options array if it exists
            PdfArray? optionsArray = fieldDict.Elements.GetArray("/Opt");
            
            if (optionsArray != null && optionsArray.Elements.Count > 0)
            {
                // First try to find the index of the value in the options array
                for (int i = 0; i < optionsArray.Elements.Count; i++)
                {
                    string? optionValue = null;
                    
                    // Extract the option value
                    if (optionsArray.Elements[i] is PdfString pdfStr)
                    {
                        optionValue = pdfStr.Value;
                    }
                    else if (optionsArray.Elements[i] is PdfArray optionArray && 
                             optionArray.Elements.Count > 0 &&
                             optionArray.Elements[0] is PdfString optStr)
                    {
                        optionValue = optStr.Value;
                    }
                    
                    // Check if this option matches our value (case insensitive)
                    if (optionValue != null && 
                        optionValue.Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found a match - set both the value and the index
                        
                        // First, ensure /I exists as an array
                        if (!fieldDict.Elements.ContainsKey("/I"))
                        {
                            fieldDict.Elements["/I"] = new PdfArray(fieldDict.Owner);
                        }
                        
                        // Clear any existing selection
                        var indexArray = fieldDict.Elements.GetArray("/I");
                        if (indexArray != null)
                        {
                            indexArray.Elements.Clear();
                            indexArray.Elements.Add(new PdfInteger(i));
                        }
                        
                        // Set the value - use the exact case from the options array
                        fieldDict.Elements.SetString("/V", optionValue);
                        
                        return true;
                    }
                }
            }
            
            // If we get here, no match was found or there were no options
            // Just set the value directly
            fieldDict.Elements.SetString("/V", value);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting combo value: {ex.Message}");
            return false;
        }
    }
}