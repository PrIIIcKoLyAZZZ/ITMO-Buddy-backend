namespace ITMO.Buddy.Infrastructure.Storage;

// MinIO 7 reads the upload with the Memory<byte> overload and no cancellation token.
// This view leaves the source open and checks its length without keeping a second buffer.
internal sealed class PhotoUploadStream(Stream source, long length, CancellationToken cancellationToken) : Stream
{
    private long _remaining = length;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _remaining == 0)
        {
            return 0;
        }

        // MinIO supplies the default token; use the upload token for the underlying read.
        var read = await source.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken);
        if (read == 0)
        {
            throw new IOException("Photo stream ended before the declared length.");
        }

        _remaining -= read;
        if (_remaining == 0 && !source.CanSeek &&
            await source.ReadAsync(new byte[1].AsMemory(), cancellationToken) != 0)
        {
            throw new IOException("Photo stream exceeds the declared length.");
        }

        return read;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
