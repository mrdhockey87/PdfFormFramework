using System.IO.Compression;

namespace PdfFormFramework.Services;

public static class PdfCompressionService
{
    public static string DecompressGzToTempPdf(string gzPath)
    {
        string pdfTemp = Path.Combine(FileSystem.CacheDirectory,
            Path.GetFileNameWithoutExtension(gzPath) + "_temp.pdf");

        using var gz = File.OpenRead(gzPath);
        using var gzip = new GZipStream(gz, CompressionMode.Decompress);
        using var output = File.Create(pdfTemp);
        gzip.CopyTo(output);
        return pdfTemp;
    }
}
