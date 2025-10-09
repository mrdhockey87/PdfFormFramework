using PdfSharp.Fonts;
using System.IO;
using System.Reflection;

namespace PdfFormFramework.Services;

public class SystemFontResolver : IFontResolver
{
    private static readonly Dictionary<string, byte[]> _fontCache = new();

    // Define CourierPrime as the default fallback font family
    private const string FallbackFontFamily = "CourierPrime";

    // Define paths to CourierPrime font files in Resources/Fonts
    private const string RegularFontFile = "CourierPrime-Regular.ttf";
    private const string BoldFontFile = "CourierPrime-Bold.ttf";
    private const string ItalicFontFile = "CourierPrime-Italic.ttf";
    private const string BoldItalicFontFile = "CourierPrime-BoldItalic.ttf";

    // Map font names to file names
    private static readonly Dictionary<string, string> _fontNameToFileMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // CourierPrime fonts - our primary fonts
        { "courierprime", RegularFontFile },
        { "courierprimeb", BoldFontFile },
        { "courierprimei", ItalicFontFile },
        { "courierprimebi", BoldItalicFontFile },
        
        // Map standard Courier New to CourierPrime
        { "courier new", RegularFontFile },
        { "courier newb", BoldFontFile },
        { "courier newi", ItalicFontFile },
        { "courier newbi", BoldItalicFontFile }
    };

    public byte[] GetFont(string faceName)
    {
        // Check cache first
        if (_fontCache.TryGetValue(faceName, out byte[]? data))
        {
            return data;
        }

        try
        {
            string normalizedFaceName = faceName.ToLower().Replace(" ", "");
            string fontFileName;

            // Determine which font file to use
            if (_fontNameToFileMap.TryGetValue(normalizedFaceName, out string? mappedFileName))
            {
                fontFileName = mappedFileName;
            }
            else
            {
                // Default to regular CourierPrime or determine style based on suffix
                if (normalizedFaceName.EndsWith("bi"))
                    fontFileName = BoldItalicFontFile;
                else if (normalizedFaceName.EndsWith("b"))
                    fontFileName = BoldFontFile;
                else if (normalizedFaceName.EndsWith("i"))
                    fontFileName = ItalicFontFile;
                else
                    fontFileName = RegularFontFile;

                Console.WriteLine($"Font '{faceName}' not found, using CourierPrime variant: {fontFileName}");
            }

            // Try app's Resources/Fonts directory first
            string appFontsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts");
            string fontPath = Path.Combine(appFontsPath, fontFileName);

            if (File.Exists(fontPath))
            {
                data = File.ReadAllBytes(fontPath);
            }
            else
            {
                // Try system fonts directory
                string systemFontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                string systemFontPath = Path.Combine(systemFontsPath, fontFileName);

                if (File.Exists(systemFontPath))
                {
                    data = File.ReadAllBytes(systemFontPath);
                }
                else
                {
                    // Try to load from embedded resources
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = $"PdfFormFramework.Resources.Fonts.{fontFileName}";

                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);
                        data = memoryStream.ToArray();
                    }
                    else
                    {
                        // Emergency fallback - try to find any TTF in system fonts
                        var anySystemFont = Directory.GetFiles(systemFontsPath, "*.ttf").FirstOrDefault();
                        if (anySystemFont != null)
                        {
                            Console.WriteLine($"Emergency fallback to system font: {Path.GetFileName(anySystemFont)}");
                            data = File.ReadAllBytes(anySystemFont);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Cannot find font {fontFileName} or any fallback.");
                        }
                    }
                }
            }

            // Cache for future use
            _fontCache[faceName] = data;
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resolving font '{faceName}': {ex.Message}");
            throw;
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Always use CourierPrime as the base family
        string fontName = FallbackFontFamily.ToLower().Replace(" ", "");

        // Add appropriate style suffix
        if (isBold && isItalic)
            fontName += "bi";
        else if (isBold)
            fontName += "b";
        else if (isItalic)
            fontName += "i";

        return new FontResolverInfo(fontName);
    }
}