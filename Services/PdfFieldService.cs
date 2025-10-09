using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Advanced;
using PdfFormFramework.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfFormFramework.Services;

public class PdfFieldService
{
    private readonly string _tempFile;
    public string GetTempPath() => _tempFile;
    public PdfFieldService(string pdfPath)
    {
        _tempFile = pdfPath;
    }

    public List<PdfFieldDefinition> ExtractFields()
    {
        var list = new List<PdfFieldDefinition>();

        try
        {
            // Use Import mode which is documented and supported
            using var doc = PdfReader.Open(_tempFile, PdfDocumentOpenMode.Import);

            if (doc.AcroForm == null)
                return list;

            // Log the total number of fields found    
            Console.WriteLine($"Found {doc.AcroForm.Fields.Count} top-level fields in the form");

            // Use a safer approach to process fields
            SafeProcessFields(doc.AcroForm.Fields, list);

            // Log the results
            Console.WriteLine($"Successfully extracted {list.Count} fields in total");
            foreach (var field in list)
            {
                Console.WriteLine($"Field: {field.Name}, Type: {field.FieldType}, Value: {field.Value}");
                if (field.Options != null && field.Options.Count > 0)
                {
                    Console.WriteLine($"  Options: {string.Join(", ", field.Options)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting fields: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return list;
    }

    private void SafeProcessFields(PdfAcroField.PdfAcroFieldCollection fields, List<PdfFieldDefinition> results)
    {
        if (fields == null)
            return;

        // Safely iterate through the collection
        for (int i = 0; i < fields.Count; i++)
        {
            try
            {
                // Get the field safely, skipping references that can't be resolved
                PdfAcroField? field = null;
                try
                {
                    field = fields[i];
                }
                catch (InvalidCastException)
                {
                    // This handles the "Unable to cast PdfReference to PdfAcroField" error
                    Console.WriteLine($"Skipping field at index {i} due to reference resolution issue");
                    continue;
                }

                // Skip null or unnamed fields
                if (field == null || string.IsNullOrEmpty(field.Name))
                    continue;
                bool hasChildFields = false;
                try
                {
                    // FIXED: Explicitly check if Fields is null before checking Count
                    // Some valid fields may have a null Fields property if they don't have child fields
                    if(field.Fields != null)
                    {
                        if(field.Fields.Count > 0)
                        {
                            hasChildFields = true;
                        }
                    }
                }   
                catch (Exception ex)
                {
                    if (ex is NullReferenceException)
                    {
                        hasChildFields = false;
                        // Ignore NullReferenceException
                    }
                    else
                    {
                        //Need this to catch the exception and continue
                        Console.WriteLine($"Error checking child fields for {field.Name}: {ex.Message}");
                    }
                }
                // If it has child fields, process them recursively
                if (hasChildFields)
                {
                    SafeProcessFields(field.Fields, results);
                    continue;
                }

                // Process this field
                var def = new PdfFieldDefinition
                {
                    Name = field.Name,
                    Value = field.Value?.ToString() ?? string.Empty
                };

                // Determine field type
                if (field is PdfTextField textField)
                {
                    def.FieldType = textField.MultiLine ?
                        PdfFieldType.MultiLineText : PdfFieldType.Text;
                }
                else if (field is PdfCheckBoxField)
                {
                    def.FieldType = PdfFieldType.CheckBox;
                }
                else if (field is PdfComboBoxField comboField)
                {
                    def.FieldType = PdfFieldType.ComboBox;
                    def.Options = GetComboOptions(comboField);
                }
                else if (field is PdfRadioButtonField)
                {
                    def.FieldType = PdfFieldType.RadioButton;
                }
                else
                {
                    // Try to determine type from dictionary
                    var fieldType = field.Elements.GetName("/FT");
                    if (fieldType == "/Tx")
                    {
                        // Check for multiline flag (bit 13)
                        int flags = GetFieldFlags(field);
                        def.FieldType = ((flags & 0x1000) != 0) ?
                            PdfFieldType.MultiLineText : PdfFieldType.Text;
                    }
                    else if (fieldType == "/Btn")
                    {
                        // Check radio button flag (bit 16)
                        int flags = GetFieldFlags(field);
                        def.FieldType = ((flags & 0x8000) != 0) ?
                            PdfFieldType.RadioButton : PdfFieldType.CheckBox;
                    }
                    else if (fieldType == "/Ch")
                    {
                        def.FieldType = PdfFieldType.ComboBox;
                        def.Options = GetComboOptionsFromDict(field);
                    }
                    else
                    {
                        def.FieldType = PdfFieldType.Unknown;
                    }
                }

                // Get field bounds/rectangle
                try
                {
                    var rect = field.Elements.GetRectangle("/Rect");
                    if (rect != null)
                    {
                        def.Bounds = new Rect(rect.X1, rect.Y1, rect.Width, rect.Height);
                    }
                    else
                    {
                        def.Bounds = new Rect(0, 0, 100, 20);
                    }
                }
                catch
                {
                    def.Bounds = new Rect(0, 0, 100, 20);
                }

                results.Add(def);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing field at index {i}: {ex.Message}");
            }
        }
    }

    // Helper method to safely extract field flags
    private int GetFieldFlags(PdfAcroField field)
    {
        int flags = 0;
        try
        {
            var flagsObj = field.Elements.GetValue("/Ff");
            if (flagsObj is PdfInteger pdfInt)
            {
                flags = pdfInt.Value;
            }
            else if (flagsObj is PdfReference flagRef && flagRef.Value != null)
            {
                // Don't use pattern matching or direct casting - use type check and reflection instead
                var refValue = flagRef.Value;
                // Extract flags using reflection which works regardless of the exact type
                flags = GetFlagsUsingReflection(refValue);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting field flags: {ex.Message}");
        }
        return flags;
    }

    // Helper method for reflection-based approach
    private int GetFlagsUsingReflection(PdfObject obj)
    {
        try
        {
            // Try to get the type name first
            var typeName = obj.GetType().Name;

            // If it's a PdfInteger or has similar name, get its Value
            if (typeName.Contains("Integer") || typeName.Equals("PdfInteger"))
            {
                var propInfo = obj.GetType().GetProperty("Value");
                if (propInfo != null)
                {
                    var val = propInfo.GetValue(obj);
                    if (val != null)
                        return Convert.ToInt32(val);
                }
            }

            // Check for numeric value in the object's string representation
            var strValue = obj.ToString();
            if (int.TryParse(strValue, out int result))
                return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in reflection-based flag extraction: {ex.Message}");
        }

        return 0;
    }

    // Rest of the class remains the same...
    private void ProcessFields(PdfAcroField.PdfAcroFieldCollection fields, List<PdfFieldDefinition> results)
    {
        // Keep existing method for backward compatibility
        if (fields == null)
            return;

        foreach (PdfAcroField field in fields)
        {
            try
            {
                // Skip null or unnamed fields
                if (field == null || string.IsNullOrEmpty(field.Name))
                    continue;

                // If it has child fields, process them recursively
                if (field.Fields != null && field.Fields.Count > 0)
                {
                    ProcessFields(field.Fields, results);
                    continue;
                }

                // Process this field
                var def = new PdfFieldDefinition
                {
                    Name = field.Name,
                    Value = field.Value?.ToString() ?? string.Empty
                };

                // Determine field type
                if (field is PdfTextField textField)
                {
                    def.FieldType = textField.MultiLine ?
                        PdfFieldType.MultiLineText : PdfFieldType.Text;
                }
                else if (field is PdfCheckBoxField)
                {
                    def.FieldType = PdfFieldType.CheckBox;
                }
                else if (field is PdfComboBoxField comboField)
                {
                    def.FieldType = PdfFieldType.ComboBox;
                    def.Options = GetComboOptions(comboField);
                }
                else if (field is PdfRadioButtonField)
                {
                    def.FieldType = PdfFieldType.RadioButton;
                }
                else
                {
                    // Try to determine type from dictionary
                    var fieldType = field.Elements.GetName("/FT");
                    if (fieldType == "/Tx")
                    {
                        // Check for multiline flag (bit 13)
                        int flags = GetFieldFlags(field);
                        def.FieldType = ((flags & 0x1000) != 0) ?
                            PdfFieldType.MultiLineText : PdfFieldType.Text;
                    }
                    else if (fieldType == "/Btn")
                    {
                        // Check radio button flag (bit 16)
                        int flags = GetFieldFlags(field);
                        def.FieldType = ((flags & 0x8000) != 0) ?
                            PdfFieldType.RadioButton : PdfFieldType.CheckBox;
                    }
                    else if (fieldType == "/Ch")
                    {
                        def.FieldType = PdfFieldType.ComboBox;
                        def.Options = GetComboOptionsFromDict(field);
                    }
                    else
                    {
                        def.FieldType = PdfFieldType.Unknown;
                    }
                }

                // Get field bounds/rectangle
                try
                {
                    var rect = field.Elements.GetRectangle("/Rect");
                    if (rect != null)
                    {
                        def.Bounds = new Rect(rect.X1, rect.Y1, rect.Width, rect.Height);
                    }
                    else
                    {
                        def.Bounds = new Rect(0, 0, 100, 20);
                    }
                }
                catch
                {
                    def.Bounds = new Rect(0, 0, 100, 20);
                }

                results.Add(def);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing field: {ex.Message}");
            }
        }
    }

    private List<string> GetComboOptions(PdfComboBoxField field)
    {
        List<string> options = [];

        try
        {
            // Try to use GetSelectedValue or similar methods first
            if (field.Elements.ContainsKey("/Opt"))
            {
                var optArray = field.Elements.GetArray("/Opt");
                if (optArray != null)
                {
                    foreach (var item in optArray)
                    {
                        if (item is PdfString str)
                        {
                            options.Add(str.Value);
                        }
                        else if (item is PdfArray arr && arr.Elements.Count > 0)
                        {
                            // Usually [0] is export value, [1] is display value
                            var displayItem = arr.Elements.Count > 1 ? arr.Elements[1] : arr.Elements[0];
                            if (displayItem is PdfString displayStr)
                            {
                                options.Add(displayStr.Value);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting combo options: {ex.Message}");
        }

        return options;
    }

    private List<string> GetComboOptionsFromDict(PdfAcroField field)
    {
        List<string> options = [];

        try
        {
            if (field.Elements.ContainsKey("/Opt"))
            {
                var optArray = field.Elements.GetArray("/Opt");
                if (optArray != null)
                {
                    foreach (var item in optArray)
                    {
                        if (item is PdfString str)
                        {
                            options.Add(str.Value);
                        }
                        else if (item is PdfArray arr && arr.Elements.Count > 0)
                        {
                            // Usually [0] is export value, [1] is display value
                            var displayItem = arr.Elements.Count > 1 ? arr.Elements[1] : arr.Elements[0];
                            if (displayItem is PdfString displayStr)
                            {
                                options.Add(displayStr.Value);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting combo options from dictionary: {ex.Message}");
        }

        return options;
    }

    public void ApplyFieldValues(Dictionary<string, string> values)
    {
        try
        {
            // Open in Modify mode to allow changes
            using var doc = PdfReader.Open(_tempFile, PdfDocumentOpenMode.Modify);

            if (doc.AcroForm == null)
                return;

            // Apply values to fields, recursively if needed
            ApplyValuesToFields(doc.AcroForm.Fields, values);

            // Save the changes
            doc.Save(_tempFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying field values: {ex.Message}");
        }
    }

    private void ApplyValuesToFields(PdfAcroField.PdfAcroFieldCollection fields, Dictionary<string, string> values)
    {
        if (fields == null)
            return;

        foreach (PdfAcroField field in fields)
        {
            try
            {
                // Skip null or unnamed fields
                if (field == null || string.IsNullOrEmpty(field.Name))
                    continue;

                bool hasChildFields = false;
                try
                {
                    // FIXED: Explicitly check if Fields is null before checking Count
                    // Some valid fields may have a null Fields property if they don't have child fields
                    if (field.Fields != null)
                    {
                        if (field.Fields.Count > 0)
                        {
                            hasChildFields = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is NullReferenceException)
                    {
                        hasChildFields = false;
                        // Ignore NullReferenceException
                    }
                    else
                    {
                        //Need this to catch the exception and continue
                        Console.WriteLine($"Error checking child fields for {field.Name}: {ex.Message}");
                    }
                }
                // Process child fields if any
                if (hasChildFields)
                {
                    ApplyValuesToFields(field.Fields, values);
                    continue;
                }

                // Check if we have a value for this field
                if (!values.TryGetValue(field.Name, out string? value) || value == null)
                    continue;

                // Apply value based on field type
                if (field is PdfTextField textField)
                {
                    textField.Value = new PdfString(value);
                }
                else if (field is PdfCheckBoxField checkBox)
                {
                    bool isChecked = value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                    value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                    value.Equals("1", StringComparison.OrdinalIgnoreCase);
                    checkBox.Checked = isChecked;
                }
                else if (field is PdfComboBoxField comboBox)
                {
                    var options = GetComboOptions(comboBox);
                    int index = options.FindIndex(o => o.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        comboBox.Value = new PdfString(value);
                    }
                    else
                    {
                        // If the value is not in options, try to set it directly
                        comboBox.Value = new PdfString(value);
                    }
                }
                else if (field is PdfRadioButtonField radioButton)
                {
                    // Try to set by index if the value is numeric
                    if (int.TryParse(value, out int index))
                    {
                        radioButton.SelectedIndex = index;
                    }
                    else
                    {
                        // Otherwise try to set by name
                        field.Elements.SetName("/V", $"/{value}");
                    }
                }
                else
                {
                    // For other field types or as a fallback, set value directly
                    field.Value = new PdfString(value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying value to field {field.Name}: {ex.Message}");
            }
        }
    }

    // Keep these methods for backward compatibility
    private PdfFieldType DetermineFieldType(PdfAcroField field)
    {
        if (field is PdfTextField textField)
            return textField.MultiLine ? PdfFieldType.MultiLineText : PdfFieldType.Text;
        else if (field is PdfCheckBoxField)
            return PdfFieldType.CheckBox;
        else if (field is PdfComboBoxField)
            return PdfFieldType.ComboBox;
        else if (field is PdfRadioButtonField)
            return PdfFieldType.RadioButton;

        return PdfFieldType.Unknown;
    }

    public static List<string> FixComboBoxItems(PdfAcroField field)
    {
        if (field is PdfComboBoxField comboField)
        {
            var service = new PdfFieldService("");
            return service.GetComboOptions(comboField);
        }
        return [];
    }
}