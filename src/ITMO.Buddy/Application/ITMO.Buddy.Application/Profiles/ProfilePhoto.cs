using ITMO.Buddy.Application.Abstractions.Profiles;
using ITMO.Buddy.Application.Errors;

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

    public static (string Extension, string ContentType) DetectFormat(MemoryStream content)
    {
        var bytes = content.GetBuffer().AsSpan(0, (int)content.Length);

        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ("png", "image/png");
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return ("jpg", "image/jpeg");
        }

        throw AppException.Validation("profile.photo.invalid_format", "Photo must be a JPEG or PNG file.");
    }

    private static AppException SizeMismatch() =>
        AppException.Validation("profile.photo.invalid_size", "The actual photo size does not match the declared size.");
}
