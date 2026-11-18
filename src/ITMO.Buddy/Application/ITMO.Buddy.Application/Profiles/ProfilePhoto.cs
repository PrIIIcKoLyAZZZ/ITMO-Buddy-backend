using System.Buffers.Binary;
using Hjg.Pngcs;
using ITMO.Buddy.Application.Abstractions.Profiles;
using ITMO.Buddy.Application.Errors;
using JpegLibrary;

namespace ITMO.Buddy.Application.Profiles;

internal static class ProfilePhoto
{
    public static async Task<MemoryStream> ReadAsync(
        ProfilePhotoUpload upload,
        CancellationToken cancellationToken)
    {
        var content = new MemoryStream();
        try
        {
            var buffer = new byte[64 * 1024];
            while (true)
            {
                // Read one extra byte to detect excess content even when the stream cannot seek.
                var remaining = upload.Length - content.Length + 1;
                var read = await upload.Content.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (read >= remaining)
                {
                    throw SizeMismatch();
                }

                content.Write(buffer.AsSpan(0, read));
            }

            if (content.Length != upload.Length)
            {
                throw SizeMismatch();
            }

            content.Position = 0;
            return content;
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }

    // Compressed size is capped separately. Bound decoder work and expanded image buffers.
    private const int MaxDecodedSamples = 64 * 1024 * 1024;

    public static (string Extension, string ContentType) DetectFormat(
        MemoryStream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = content.GetBuffer().AsMemory(0, (int)content.Length);

        try
        {
            if (bytes.Span.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                ValidatePngContainer(bytes.Span);
                ValidatePngPixels(content, cancellationToken);
                return ("png", "image/png");
            }

            if (bytes.Span.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) &&
                bytes.Span.EndsWith(new byte[] { 0xFF, 0xD9 }))
            {
                ValidateJpeg(bytes, cancellationToken);
                return ("jpg", "image/jpeg");
            }
        }
        catch (Exception error) when (
            error is PngjException or InvalidDataException or IOException or
                NotSupportedException or InvalidOperationException or ArgumentException or
                IndexOutOfRangeException or ArithmeticException)
        {
            throw InvalidFormat();
        }
        finally
        {
            content.Position = 0;
        }

        throw InvalidFormat();
    }

    private static void ValidatePngContainer(ReadOnlySpan<byte> bytes)
    {
        // Inspect IHDR before the PNG reader allocates its scanline buffers.
        if (bytes.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != 13 ||
            !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw InvalidFormat();
        }

        ValidateDecodeBudget(
            BinaryPrimitives.ReadInt32BigEndian(bytes[16..]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[20..]),
            channels: 4);

        var chunks = bytes[8..];
        while (chunks.Length >= 12)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(chunks);
            if (size > chunks.Length - 12)
            {
                break;
            }

            var type = chunks.Slice(4, 4);
            chunks = chunks[(12 + (int)size)..];
            if (type.SequenceEqual("IEND"u8) && size == 0 && chunks.IsEmpty)
            {
                return;
            }

            // The PNG reader decodes the base image only. Reject additional frames
            // rather than store animation data that has not been decoded.
            if (type.SequenceEqual("IEND"u8) || type.SequenceEqual("acTL"u8) ||
                type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))
            {
                break;
            }
        }

        throw InvalidFormat();
    }

    private static void ValidatePngPixels(MemoryStream content, CancellationToken cancellationToken)
    {
        var reader = new PngReader(content) { ShouldCloseStream = false };
        try
        {
            for (var row = 0; row < reader.ImgInfo.Rows; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reader.ReadRowByte(row);
            }
        }
        finally
        {
            reader.End();
        }
    }

    private static void ValidateJpeg(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var decoder = new JpegDecoder();
        decoder.SetInput(bytes);
        if (decoder.Identify() != bytes.Length)
        {
            throw InvalidFormat();
        }

        ValidateDecodeBudget(decoder.Width, decoder.Height, decoder.NumberOfComponents);

        var output = new ValidationBlockWriter(cancellationToken);
        decoder.SetOutputWriter(output);
        decoder.Decode();
        if (!output.HasPixels)
        {
            throw InvalidFormat();
        }
    }

    private static void ValidateDecodeBudget(int width, int height, int channels)
    {
        if (width <= 0 || height <= 0 || channels <= 0)
        {
            throw InvalidFormat();
        }

        if ((long)width * height > MaxDecodedSamples / channels)
        {
            throw AppException.Validation("profile.photo.decode_limit", "The image is too large to decode.");
        }
    }

    private sealed class ValidationBlockWriter(CancellationToken cancellationToken) : JpegBlockOutputWriter
    {
        public bool HasPixels { get; private set; }

        public override void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasPixels = true;
        }
    }

    private static AppException InvalidFormat() =>
        AppException.Validation("profile.photo.invalid_format", "Photo must be a complete JPEG or non-animated PNG image.");

    private static AppException SizeMismatch() =>
        AppException.Validation("profile.photo.invalid_size", "The actual photo size does not match the declared size.");
}
