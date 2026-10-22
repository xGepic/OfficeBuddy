using System.Buffers.Binary;

namespace OfficeBuddy.Core.Tests.Support;

/// <summary>Reads the pixel size of a PNG image from its header.</summary>
public static class PngSize
{
    public static (int Width, int Height) Read(byte[] png)
    {
        png.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }).ShouldBeTrue("Not a PNG image.");
        // The first chunk is IHDR: width and height as big-endian integers at bytes 16 and 20.
        return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
    }
}
