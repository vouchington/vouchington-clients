using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
namespace Voucha.Client.App.Support;

internal sealed record LocalImageSelection(byte[] Bytes, bool CanPreview);

internal static class LocalImagePreview
{
  private const int MaxRetainedThumbnailBytes = 256 * 1024;

  public static async Task<LocalImageSelection> ReadSelectedBytesAsync(
      Stream content,
      CancellationToken cancellationToken = default)
  {
    using var buffer = new MemoryStream();
    await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
    var bytes = buffer.ToArray();
    try
    {
      using var decodeStream = new MemoryStream(bytes, writable: false);
      using var decoded = PlatformImage.FromStream(decodeStream);
      if (decoded is null) throw new InvalidDataException();
    }
    catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
    {
      return new LocalImageSelection(bytes, false);
    }

    return new LocalImageSelection(bytes, true);
  }

  public static ImageSource SourceFromBytes(byte[] bytes) =>
      ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));

  public static byte[]? RetainedThumbnailFromBytes(byte[] bytes)
  {
    try
    {
      using var decodeStream = new MemoryStream(bytes, writable: false);
      using var decoded = PlatformImage.FromStream(decodeStream);
      if (decoded is null) return null;
      var thumbnail = decoded.Downsize(128);
      try
      {
        var encoded = thumbnail.AsBytes(ImageFormat.Png);
        return encoded is { Length: > 0 and <= MaxRetainedThumbnailBytes } ? encoded : null;
      }
      finally
      {
        if (!ReferenceEquals(thumbnail, decoded)) thumbnail.Dispose();
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
    {
      return null;
    }
  }
}
