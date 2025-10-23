using PdfFormFramework.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PdfFormFramework.Services;

public class PdfFormFillingService
{
    private readonly PdfFieldService _fieldService;
    private string _outputPath;

    public PdfFormFillingService(string pdfPath)
    {
        _fieldService = new PdfFieldService(pdfPath);
        _outputPath = pdfPath;
    }

    public string GetPdfPath() => _outputPath;

    /// <summary>
    /// Fills a PDF form with data from a model object
    /// </summary>
    /// <typeparam name="TModel">Model type containing form data</typeparam>
    /// <param name="model">The data model</param>
    /// <returns>The path to the filled PDF file</returns>
    public async Task<string> FillFormWithModelAsync<TModel>(TModel model) where TModel : class, new()
    {
        Debug.WriteLine("Starting to fill PDF form with model data");

        // Extract field definitions
        var fields = _fieldService.ExtractFields();

        // Log fields found to help debug
        Debug.WriteLine($"Found {fields.Count} fields in the PDF form");
        foreach (var field in fields)
        {
            Debug.WriteLine($"Field: {field.Name}, Type: {field.FieldType}, Value: '{field.Value}'");

            // Log combo box options
            if (field.FieldType == PdfFieldType.ComboBox && field.Options != null)
            {
                Debug.WriteLine($"Combo box {field.Name} options: {string.Join(", ", field.Options)}");
            }
        }

        // Dump model properties
        Debug.WriteLine("Model properties:");
        foreach (var prop in typeof(TModel).GetProperties())
        {
            var value = prop.GetValue(model);
            Debug.WriteLine($"Property: {prop.Name}, Value: '{value}'");
        }

        // Create binding service
        var binding = new PdfDataBindingService<TModel>();

        // Apply model data to fields
        binding.FromModel(fields, model);

        // Log field values after binding
        Debug.WriteLine("Field values after binding:");
        foreach (var field in fields)
        {
            Debug.WriteLine($"Field: {field.Name}, Value: '{field.Value}'");
        }

        try
        {
            // Convert to dictionary for field service
            Dictionary<string, string> fieldValues = binding.ToDictionary(fields);
            Debug.WriteLine($"Created dictionary with {fieldValues.Count} values");

            // Create a new output path to ensure we're not reading and writing to the same file
            string originalPath = _fieldService.GetTempPath();
            _outputPath = Path.Combine(
                Path.GetDirectoryName(originalPath) ?? "",
                Path.GetFileNameWithoutExtension(originalPath) + "_filled.pdf");

            // Try to fill the form using the direct method
            bool success = DirectPdfFiller.FillPdfFormDirectly(originalPath, _outputPath, fieldValues);

            if (!success)
            {
                Debug.WriteLine("Direct PDF filling failed, trying fallback method");

                // If direct filling failed, try the original method
                // First copy the original PDF to ensure we're not reading and writing to the same file
                File.Copy(originalPath, _outputPath, true);

                // Then apply the values with the original method
                var outputFieldService = new PdfFieldService(_outputPath);
                outputFieldService.ApplyFieldValues(fieldValues);
            }

            Debug.WriteLine($"PDF filled successfully: {_outputPath}");

            // Ensure file is fully written
            await Task.Delay(500);

            // Return the path to the filled PDF
            return _outputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error filling PDF form: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");

            // Return the original path if there was an error
            return _fieldService.GetTempPath();
        }
    }

    /// <summary>
    /// Fills a PDF form with key-value data
    /// </summary>
    /// <param name="fieldValues">Dictionary of field names and values</param>
    /// <returns>The path to the filled PDF file</returns>
    public string FillFormWithData(Dictionary<string, string> fieldValues)
    {
        try
        {
            Debug.WriteLine($"Filling PDF form with {fieldValues.Count} values");

            // Create a new output path
            string originalPath = _fieldService.GetTempPath();
            _outputPath = Path.Combine(
                Path.GetDirectoryName(originalPath) ?? "",
                Path.GetFileNameWithoutExtension(originalPath) + "_filled.pdf");

            // Try to fill the form using the direct method
            bool success = DirectPdfFiller.FillPdfFormDirectly(originalPath, _outputPath, fieldValues);
            
            if (!success)
            {
                Debug.WriteLine("Direct PDF filling failed, trying fallback method");
                
                // If direct filling failed, try the original method
                // First copy the original PDF to ensure we're not reading and writing to the same file
                File.Copy(originalPath, _outputPath, true);
                
                // Then apply the values with the original method
                var outputFieldService = new PdfFieldService(_outputPath);
                outputFieldService.ApplyFieldValues(fieldValues);
            }

            Debug.WriteLine($"PDF filled successfully at {_outputPath}");

            return _outputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error filling form with data: {ex.Message}");

            // Return the original path if there was an error
            return _fieldService.GetTempPath();
        }
    }
}