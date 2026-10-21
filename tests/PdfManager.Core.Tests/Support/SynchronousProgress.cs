namespace PdfManager.Core.Tests.Support;

/// <summary>Reports straight away, unlike <see cref="Progress{T}"/>, which posts to a thread pool.</summary>
public sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
