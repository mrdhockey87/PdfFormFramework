using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Advanced;
using System.Diagnostics;

namespace PdfFormFramework.Services;

/// <summary>
/// A direct PDF form filler that modifies the PDF at a lower level, bypassing AcroForm API issues
/// </summary>
public class DirectPdfFiller
{
    public static bool FillPdfFormDirectly(string sourcePdfPath, string outputPdfPath, Dictionary<string, string> values)
    {
        Debug.WriteLine($"Filling PDF form directly from {sourcePdfPath} to {outputPdfPath}");

        try
        {
            // First, copy the source file to ensure we're not modifying the original
            if (sourcePdfPath != outputPdfPath)
            {
                File.Copy(sourcePdfPath, outputPdfPath, true);
            }

            // Open the PDF in modify mode
            using var document = PdfReader.Open(outputPdfPath, PdfDocumentOpenMode.Modify);

            if (document.AcroForm == null)
            {
                Debug.WriteLine("No AcroForm found in PDF");
                return false;
            }

            // Store if we've made any changes
            bool madeChanges = false;

            // Get all form fields and their full paths
            Dictionary<string, PdfDictionary> allFields = FindAllFormFields(document);
            Debug.WriteLine($"Found {allFields.Count} fields in PDF");

            // Log all the field names to help with debugging
            foreach (var fieldName in allFields.Keys)
            {
                Debug.WriteLine($"Available PDF field: {fieldName}");
            }

            // Loop through our field values
            foreach (var kvp in values)
            {
                string fieldName = kvp.Key;
                string fieldValue = kvp.Value;

                // Skip empty field values (optional)
                if (string.IsNullOrEmpty(fieldValue))
                    continue;

                // Check if this field exists in the PDF - try multiple possible field name formats
                // Sometimes PDF fields have different naming conventions
                PdfDictionary? fieldDict = null;

                // Try exact match first
                if (allFields.TryGetValue(fieldName, out fieldDict))
                {
                    Debug.WriteLine($"Found field '{fieldName}' by exact match");
                }
                // Try case-insensitive match
                else
                {
                    var caseInsensitiveMatch = allFields.Keys.FirstOrDefault(k =>
                        k.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    if (caseInsensitiveMatch != null && allFields.TryGetValue(caseInsensitiveMatch, out fieldDict))
                    {
                        Debug.WriteLine($"Found field '{fieldName}' as '{caseInsensitiveMatch}' (case insensitive)");
                        fieldName = caseInsensitiveMatch; // Use the actual field name from the PDF
                    }
                }

                // If we found the field, try to set its value
                if (fieldDict != null)
                {
                    Debug.WriteLine($"Setting field '{fieldName}' to '{fieldValue}'");

                    try
                    {
                        // Get the field type
                        string? fieldType = fieldDict.Elements.GetName("/FT");

                        // For text fields
                        if (fieldType == "/Tx")
                        {
                            // Force direct value setting to ensure it's displayed
                            fieldDict.Elements["/V"] = new PdfString(fieldValue);

                            // Remove existing appearance dictionary to force regeneration
                            if (fieldDict.Elements.ContainsKey("/AP"))
                                fieldDict.Elements.Remove("/AP");

                            // Ensure field is editable (not read-only)
                            int flags = fieldDict.Elements.GetInteger("/Ff");
                            flags &= ~(1 << 0); // Clear read-only bit
                            fieldDict.Elements.SetInteger("/Ff", flags);

                            madeChanges = true;
                        }
                        else if (fieldType == "/Btn") // Button (checkbox/radio button)
                        {
                            // Normalize the value
                            string normalizedValue = fieldValue.Trim().ToLowerInvariant();

                            // Set appropriate value based on checkbox state
                            if (normalizedValue == "yes" || normalizedValue == "true" || normalizedValue == "on" || normalizedValue == "1")
                            {
                                fieldDict.Elements.SetName("/V", "/Yes");
                                // Also set the appearance state
                                if (fieldDict.Elements.ContainsKey("/AS"))
                                {
                                    fieldDict.Elements.SetName("/AS", "/Yes");
                                }
                            }
                            else
                            {
                                fieldDict.Elements.SetName("/V", "/Off");
                                // Also set the appearance state
                                if (fieldDict.Elements.ContainsKey("/AS"))
                                {
                                    fieldDict.Elements.SetName("/AS", "/Off");
                                }
                            }
                            madeChanges = true;
                        }
                        else if (fieldType == "/Ch") // Choice field (combo/list box)
                        {
                            // For combo boxes, use our specialized extension method
                            Debug.WriteLine($"Setting combo box field '{fieldName}' to '{fieldValue}'");
                            
                            try 
                            {
                                // Use our specialized method that handles both string value and index
                                bool success = fieldDict.SetComboValue(fieldValue);
                                
                                if (success)
                                {
                                    Debug.WriteLine($"Successfully set combo box value for '{fieldName}'");
                                    madeChanges = true;
                                }
                                else
                                {
                                    // If the specialized method fails, fall back to the simple approach
                                    Debug.WriteLine("Falling back to simple approach for combo box");
                                    fieldDict.Elements.SetString("/V", fieldValue);
                                    madeChanges = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error setting combo box value: {ex.Message}");
                                // Try alternative approach
                                try
                                {
                                    // First try to set it as an array with a single value
                                    PdfArray valueArray = new PdfArray(document);
                                    valueArray.Elements.Add(new PdfString(fieldValue));
                                    fieldDict.Elements["/V"] = valueArray;
                                    madeChanges = true;
                                }
                                catch 
                                {
                                    // If all else fails, try direct string
                                    fieldDict.Elements.SetString("/V", fieldValue);
                                    madeChanges = true;
                                }
                            }
                        }
                        else
                        {
                            // Unknown field type, try generic string approach
                            Debug.WriteLine($"Unknown field type '{fieldType}', using generic approach");
                            fieldDict.Elements.SetString("/V", fieldValue);
                            madeChanges = true;
                        }

                        // Set AP (Appearance) dictionary to ensure value is visible
                        if (!fieldDict.Elements.ContainsKey("/AP"))
                        {
                            Debug.WriteLine("No appearance dictionary, marking for appearance generation");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error setting value for field '{fieldName}': {ex.Message}");

                        // Try alternative approach for setting the value
                        try
                        {
                            Debug.WriteLine("Trying alternative value setting approach");
                            if (fieldDict.Elements.ContainsKey("/V"))
                            {
                                fieldDict.Elements.Remove("/V"); // Remove existing value first
                            }
                            fieldDict.Elements.Add("/V", new PdfString(fieldValue));
                            madeChanges = true;
                        }
                        catch (Exception ex2)
                        {
                            Debug.WriteLine($"Alternative approach failed: {ex2.Message}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"Field '{fieldName}' not found in PDF");
                }
            }

            if (madeChanges)
            {
                // Ensure form fields are set to regenerate their appearance
                // Try multiple approaches since PDFsharp versions may vary
                try
                {
                    Debug.WriteLine("Setting NeedAppearances flag");
                    if (document.AcroForm.Elements.ContainsKey("/NeedAppearances"))
                    {
                        document.AcroForm.Elements.Remove("/NeedAppearances");
                    }

                    // Try using PdfName
                    document.AcroForm.Elements.Add("/NeedAppearances", new PdfName("/True"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting NeedAppearances with PdfName: {ex.Message}");

                    // Try alternative method
                    try
                    {
                        document.AcroForm.Elements.SetBoolean("/NeedAppearances", true);
                    }
                    catch (Exception ex2)
                    {
                        Debug.WriteLine($"Error setting NeedAppearances with SetBoolean: {ex2.Message}");

                        // Try direct string
                        try
                        {
                            document.AcroForm.Elements.SetString("/NeedAppearances", "true");
                        }
                        catch
                        {
                            // Ignore if this also fails
                        }
                    }
                }

                // Save the modified PDF
                Debug.WriteLine($"Saving modified PDF to {outputPdfPath}");
                document.Save(outputPdfPath);
                Debug.WriteLine($"PDF saved with filled form fields to {outputPdfPath}");

                return true;
            }
            else
            {
                Debug.WriteLine("No changes made to PDF form");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error filling PDF form directly: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
            return false;
        }
    }

    private static Dictionary<string, PdfDictionary> FindAllFormFields(PdfDocument document)
    {
        var result = new Dictionary<string, PdfDictionary>();

        try
        {
            if (document.AcroForm == null)
                return result;

            // Get the AcroForm's Fields array
            PdfArray? fields = document.AcroForm.Elements.GetArray("/Fields");
            if (fields == null)
                return result;

            // Process all fields
            ProcessFieldsArray(document, fields, "", result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error finding form fields: {ex.Message}");
        }

        return result;
    }

    private static void ProcessFieldsArray(PdfDocument document, PdfArray fields, string parentName, Dictionary<string, PdfDictionary> result)
    {
        for (int i = 0; i < fields.Elements.Count; i++)
        {
            try
            {
                // Get field reference and resolve it
                var fieldRef = fields.Elements[i] as PdfReference;
                if (fieldRef == null)
                    continue;

                var fieldDict = fieldRef.Value as PdfDictionary;
                if (fieldDict == null)
                    continue;

                // Get field name
                string? fieldName = null;
                var nameObj = fieldDict.Elements.GetString("/T");
                if (nameObj != null)
                {
                    fieldName = nameObj;
                }

                // Build full field name with parent prefix
                string fullName = string.IsNullOrEmpty(parentName)
                    ? (fieldName ?? $"Field_{i}")
                    : string.IsNullOrEmpty(fieldName)
                        ? parentName
                        : $"{parentName}.{fieldName}";

                // Check if this field has children
                var kidsArray = fieldDict.Elements.GetArray("/Kids");
                if (kidsArray != null && kidsArray.Elements.Count > 0)
                {
                    // Process child fields recursively
                    ProcessFieldsArray(document, kidsArray, fullName, result);
                }
                else
                {
                    // This is a terminal field, add it to our results
                    result[fullName] = fieldDict;

                    // Some PDFs use a different naming convention without dots
                    // Add an alternative flat name to improve matching
                    if (fullName.Contains('.'))
                    {
                        string flatName = fullName.Split('.').Last();
                        if (!result.ContainsKey(flatName))
                        {
                            result[flatName] = fieldDict;
                        }
                    }

                    // Add a version without any dots or special characters
                    string normalizedName = new string(fullName.Where(c => char.IsLetterOrDigit(c)).ToArray());
                    if (!result.ContainsKey(normalizedName) && normalizedName != fullName)
                    {
                        result[normalizedName] = fieldDict;
                    }
                }

                // Also log field type to help with debugging
                string? fieldType = fieldDict.Elements.GetName("/FT");
                Debug.WriteLine($"Found field: {fullName}, Type: {fieldType ?? "unknown"}");

                // If it's a combo box, log its options
                if (fieldType == "/Ch")
                {
                    PdfArray? optionsArray = fieldDict.Elements.GetArray("/Opt");
                    if (optionsArray != null)
                    {
                        Debug.WriteLine($"Combo box {fullName} options:");
                        for (int j = 0; j < optionsArray.Elements.Count; j++)
                        {
                            if (optionsArray.Elements[j] is PdfString pdfStr)
                            {
                                Debug.WriteLine($"  Option {j}: {pdfStr.Value}");
                            }
                            else if (optionsArray.Elements[j] is PdfArray optionArray &&
                                    optionArray.Elements.Count > 0 &&
                                    optionArray.Elements[0] is PdfString optStr)
                            {
                                Debug.WriteLine($"  Option {j}: {optStr.Value}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing field at index {i}: {ex.Message}");
            }
        }
    }
}