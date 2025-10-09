namespace PdfFormFramework.Services;

public static class TempFileService
{
    public static void Cleanup(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
