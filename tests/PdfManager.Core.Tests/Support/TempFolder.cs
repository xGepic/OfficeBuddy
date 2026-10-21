namespace PdfManager.Core.Tests.Support;

/// <summary>A fresh folder per test, deleted afterwards.</summary>
public sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "PdfManagerTests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public string File(string fileName) => System.IO.Path.Combine(Path, fileName);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
