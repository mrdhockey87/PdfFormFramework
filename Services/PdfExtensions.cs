using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using System.Diagnostics;

namespace PdfFormFramework.Services;

public static class PdfExtensions
{
    public static void SetBoolean(this PdfDictionary dictionary, string key, bool value)
    {
        try
        {
            dictionary.Elements.SetString(key, value ? "true" : "false");
        }
        catch
        {
            try
            {
                dictionary.Elements.SetName(key, value ? "/True" : "/False");
            }
            catch
            {
                dictionary.Elements.SetInteger(key, value ? 1 : 0);
            }
        }
    }

    // NEW: clear appearance on field and all kids to force regeneration
    public static void ClearAppearance(this PdfDictionary fieldDict)
    {
        try
        {
            if (fieldDict.Elements.ContainsKey("/AP"))
                fieldDict.Elements.Remove("/AP");

            var kids = fieldDict.Elements.GetArray("/Kids");
            if (kids != null)
            {
                for (int i = 0; i < kids.Elements.Count; i++)
                {
                    if (kids.Elements[i] is PdfReference kidRef && kidRef.Value is PdfDictionary kidDict)
                    {
                        if (kidDict.Elements.ContainsKey("/AP"))
                            kidDict.Elements.Remove("/AP");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing appearance: {ex.Message}");
        }
    }

    // Improved: handle export/display, set V, DV, I; clear AP
    public static bool SetComboValue(this PdfDictionary fieldDict, string value)
    {
        try
        {
            string normalizedInput = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalizedInput))
            {
                // clear selection
                fieldDict.Elements.Remove("/V");
                fieldDict.Elements.Remove("/DV");
                fieldDict.Elements.Remove("/I");
                fieldDict.ClearAppearance();
                return true;
            }

            PdfArray? optionsArray = fieldDict.Elements.GetArray("/Opt");
            if (optionsArray != null && optionsArray.Elements.Count > 0)
            {
                for (int i = 0; i < optionsArray.Elements.Count; i++)
                {
                    string? export = null;
                    string? display = null;

                    if (optionsArray.Elements[i] is PdfString pdfStr)
                    {
                        export = pdfStr.Value;
                        display = pdfStr.Value;
                    }
                    else if (optionsArray.Elements[i] is PdfArray optionArray)
                    {
                        if (optionArray.Elements.Count > 0 && optionArray.Elements[0] is PdfString exportStr)
                            export = exportStr.Value;
                        if (optionArray.Elements.Count > 1 && optionArray.Elements[1] is PdfString displayStr)
                            display = displayStr.Value ?? export;
                    }

                    bool matches = (!string.IsNullOrEmpty(export) && export.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
                                   || (!string.IsNullOrEmpty(display) && display.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase));

                    if (matches)
                    {
                        // Use export if available; otherwise fall back to display or input
                        var selected = export ?? display ?? normalizedInput;

                        // Ensure /I exists and set the index
                        if (!fieldDict.Elements.ContainsKey("/I"))
                            fieldDict.Elements["/I"] = new PdfArray(fieldDict.Owner);
                        var indexArray = fieldDict.Elements.GetArray("/I");
                        if (indexArray != null)
                        {
                            indexArray.Elements.Clear();
                            indexArray.Elements.Add(new PdfInteger(i));
                        }

                        // Set current and default values
                        fieldDict.Elements.SetString("/V", selected);
                        fieldDict.Elements.SetString("/DV", selected);

                        // Clear AP so viewer regenerates the selected text appearance
                        fieldDict.ClearAppearance();
                        return true;
                    }
                }
            }

            // No options or no match: still set value, default, and clear AP
            fieldDict.Elements.SetString("/V", normalizedInput);
            fieldDict.Elements.SetString("/DV", normalizedInput);
            fieldDict.Elements.Remove("/I");
            fieldDict.ClearAppearance();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting combo value: {ex.Message}");
            return false;
        }
    }
}