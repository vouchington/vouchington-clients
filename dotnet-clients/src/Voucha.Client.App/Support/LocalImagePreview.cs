using Microsoft.Maui.Graphics.Platform;
namespace Voucha.Client.App.Support;

internal sealed record LocalImageSelection(byte[] Bytes, bool CanPreview);

internal static class LocalImagePreview
{
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
}
