#if MACCATALYST
using Foundation;
using ImageIO;
using UIKit;
#endif

namespace Voucha.Client.App.Support;

internal sealed record LocalImageSelection(byte[] Bytes, byte[]? PreviewBytes)
{
  public bool CanPreview => PreviewBytes is not null;
}

internal static class LocalImagePreview
{
#if MACCATALYST
  private const int MaxThumbnailPixels = 128;
#endif
  private const int MaxRetainedThumbnailBytes = 256 * 1024;

  public static async Task<LocalImageSelection> ReadSelectedBytesAsync(
      Stream content,
      CancellationToken cancellationToken = default)
  {
    using var buffer = new MemoryStream();
    await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
    var bytes = buffer.ToArray();
    var thumbnail = await RetainedThumbnailFromBytesAsync(bytes, cancellationToken).ConfigureAwait(false);
    return new LocalImageSelection(bytes, thumbnail);
  }

  public static ImageSource SourceFromBytes(byte[] bytes) =>
      ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));

  public static Task<byte[]?> RetainedThumbnailFromBytesAsync(
      byte[] bytes, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(bytes);
    cancellationToken.ThrowIfCancellationRequested();
#if WINDOWS
    return WindowsLocalImagePreviewDecoder.RetainedThumbnailFromBytesAsync(bytes, cancellationToken);
#else
    return Task.FromResult(RetainedNativeThumbnail(bytes, cancellationToken));
#endif
  }

#if !WINDOWS
  private static byte[]? RetainedNativeThumbnail(byte[] bytes, CancellationToken cancellationToken)
  {
    try
    {
#if MACCATALYST
      using var data = NSData.FromArray(bytes);
      using var source = CGImageSource.FromData(data, new CGImageOptions { ShouldCache = false });
      if (source is null || source.ImageCount == 0) return null;
      using var thumbnail = source.CreateThumbnail(0, new CGImageThumbnailOptions
      {
        CreateThumbnailFromImageAlways = true,
        CreateThumbnailWithTransform = true,
        MaxPixelSize = MaxThumbnailPixels,
        ShouldCache = false,
      });
      if (thumbnail is null) return null;
      using var image = UIImage.FromImage(thumbnail);
      using var encoded = image.AsPNG();
      var result = encoded?.ToArray();
#else
      // Portable tests have no image decoder; product builds use the native bounded paths.
      byte[]? result = null;
#endif
      cancellationToken.ThrowIfCancellationRequested();
      return Bounded(result);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
    {
      return null;
    }
  }
#endif

  private static byte[]? Bounded(byte[]? bytes) =>
      bytes is { Length: > 0 and <= MaxRetainedThumbnailBytes } ? bytes : null;
}
