#if WINDOWS
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Voucha.Client.App.Support;

internal static class WindowsLocalImagePreviewDecoder
{
  internal const int MaxThumbnailPixels = 128;
  internal const int MaxRetainedThumbnailBytes = 256 * 1024;

  internal static async Task<byte[]?> RetainedThumbnailFromBytesAsync(
      byte[] bytes, CancellationToken cancellationToken)
  {
    try
    {
      using var input = new MemoryStream(bytes, writable: false);
      using var randomInput = input.AsRandomAccessStream();
      var decoder = await BitmapDecoder.CreateAsync(randomInput);
      if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0) return null;
      var scale = Math.Min(1d, (double)MaxThumbnailPixels / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
      var transform = new BitmapTransform
      {
        ScaledWidth = Math.Max(1u, (uint)(decoder.PixelWidth * scale)),
        ScaledHeight = Math.Max(1u, (uint)(decoder.PixelHeight * scale)),
      };
      using var thumbnail = await decoder.GetSoftwareBitmapAsync(
          BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
          transform, ExifOrientationMode.RespectExifOrientation,
          ColorManagementMode.ColorManageToSRgb);
      using var output = new InMemoryRandomAccessStream();
      var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
      encoder.SetSoftwareBitmap(thumbnail);
      await encoder.FlushAsync();
      if (output.Size > MaxRetainedThumbnailBytes) return null;
      output.Seek(0);
      using var resultStream = output.AsStreamForRead();
      using var buffer = new MemoryStream();
      await resultStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
      var result = buffer.ToArray();
      cancellationToken.ThrowIfCancellationRequested();
      return result is { Length: > 0 and <= MaxRetainedThumbnailBytes } ? result : null;
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
}
#endif
