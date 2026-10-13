using Voucha.Client.App.Support;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace Voucha.Client.WindowsImagePreview.Tests;

public sealed class WindowsLocalImagePreviewDecoderTests
{
  [Fact]
  public async Task RetainedThumbnailUsesWindowsDecoderAndBoundsDimensionsAndBytes()
  {
    var source = await CreatePngAsync(1024, 512);

    var preview = await WindowsLocalImagePreviewDecoder.RetainedThumbnailFromBytesAsync(
        source, TestContext.Current.CancellationToken);

    Assert.NotNull(preview);
    Assert.InRange(preview.Length, 1, WindowsLocalImagePreviewDecoder.MaxRetainedThumbnailBytes);
    using var encoded = new MemoryStream(preview, writable: false);
    using var randomAccess = encoded.AsRandomAccessStream();
    var decoder = await BitmapDecoder.CreateAsync(randomAccess);
    Assert.Equal(128u, decoder.PixelWidth);
    Assert.Equal(64u, decoder.PixelHeight);
  }

  [Fact]
  public async Task MalformedImageReturnsUnavailablePreview()
  {
    var preview = await WindowsLocalImagePreviewDecoder.RetainedThumbnailFromBytesAsync(
        [1, 2, 3, 4], TestContext.Current.CancellationToken);

    Assert.Null(preview);
  }

  [Fact]
  public async Task CancellationAtRetainedStreamCopyIsPreserved()
  {
    var source = await CreatePngAsync(1024, 512);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        WindowsLocalImagePreviewDecoder.RetainedThumbnailFromBytesAsync(source, cancellation.Token));
  }

  private static async Task<byte[]> CreatePngAsync(int width, int height)
  {
    using var bitmap = new SoftwareBitmap(
        BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
    using var stream = new InMemoryRandomAccessStream();
    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
    encoder.SetSoftwareBitmap(bitmap);
    await encoder.FlushAsync();
    stream.Seek(0);
    using var reader = new DataReader(stream.GetInputStreamAt(0));
    await reader.LoadAsync(checked((uint)stream.Size));
    var bytes = new byte[checked((int)stream.Size)];
    reader.ReadBytes(bytes);
    return bytes;
  }
}
