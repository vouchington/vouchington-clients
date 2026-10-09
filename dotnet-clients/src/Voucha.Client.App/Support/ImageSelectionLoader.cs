using System.Linq;
using Microsoft.Maui.Storage;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Support;

internal static class ImageSelectionLoader
{
  private const long MaxImageUploadBytes = 50L * 1024 * 1024;

  private static readonly HashSet<string> SupportedImageContentTypes = new(StringComparer.Ordinal)
  {
    "image/avif",
    "image/gif",
    "image/heic",
    "image/heif",
    "image/jpeg",
    "image/jpg",
    "image/png",
    "image/tiff",
    "image/webp",
  };

  public static async Task<IReadOnlyList<FileResult>?> PickImagesAsync()
  {
    var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
      {
        PickerTitle = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpEditorPickImages),
        FileTypes = FilePickerFileType.Images,
      });
    return results?.ToArray()!;
  }

  public static Task<ImageSelection> LoadAsync(FileResult file, CancellationToken cancellationToken = default) =>
      LoadAsync(file, file.OpenReadAsync, cancellationToken);

  internal static async Task<ImageSelection> LoadAsync(
      FileResult file, Func<Task<Stream>> openRead, CancellationToken cancellationToken = default)
  {
    var contentType = ResolveContentType(file);
    Stream? stream = await openRead().ConfigureAwait(false);

    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (stream.CanSeek)
      {
        if (stream.Length > MaxImageUploadBytes)
        {
          throw new InvalidOperationException(UiCopy.Localize(UiMessageKey.NativeDotnetCsharpImageTooLarge));
        }

        var selection = new ImageSelection(stream, contentType, stream.Length);
        stream = null;
        return selection;
      }

      var buffered = new MemoryStream();
      try
      {
        await CopyToBoundedMemoryStreamAsync(stream, buffered, cancellationToken).ConfigureAwait(false);
        buffered.Position = 0;
        return new ImageSelection(buffered, contentType, buffered.Length);
      }
      catch
      {
        await buffered.DisposeAsync().ConfigureAwait(false);
        throw;
      }
    }
    finally
    {
      if (stream is not null)
      {
        await stream.DisposeAsync().ConfigureAwait(false);
      }
    }
  }

  private static string ResolveContentType(FileResult file)
  {
    var contentType = file.ContentType;
    if (!string.IsNullOrWhiteSpace(contentType) && !string.Equals(contentType, "application/octet-stream", StringComparison.Ordinal))
    {
#pragma warning disable CA1308
      var normalizedContentType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
#pragma warning restore CA1308
      if (SupportedImageContentTypes.Contains(normalizedContentType))
      {
        return normalizedContentType;
      }
    }

#pragma warning disable CA1308
    return Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant() switch
#pragma warning restore CA1308
    {
      ".png" => "image/png",
      ".gif" => "image/gif",
      ".webp" => "image/webp",
      ".tif" or ".tiff" => "image/tiff",
      ".avif" => "image/avif",
      ".heic" => "image/heic",
      ".heif" => "image/heif",
      ".jpg" or ".jpeg" => "image/jpeg",
      _ => throw new InvalidOperationException(UiCopy.Localize(UiMessageKey.NativeDotnetCsharpUnsupportedImageFormat)),
    };
  }

  private static async Task CopyToBoundedMemoryStreamAsync(Stream source, Stream destination, CancellationToken cancellationToken)
  {
    var buffer = new byte[81920];
    long totalBytesRead = 0;
    while (true)
    {
      var bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
      if (bytesRead == 0) return;

      totalBytesRead += bytesRead;
      if (totalBytesRead > MaxImageUploadBytes)
      {
        throw new InvalidOperationException(UiCopy.Localize(UiMessageKey.NativeDotnetCsharpImageTooLarge));
      }

      await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
    }
  }
}

internal sealed class ImageSelection : IDisposable, IAsyncDisposable
{
  public ImageSelection(Stream content, string contentType, long contentLength)
  {
    Content = content ?? throw new ArgumentNullException(nameof(content));
    ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
    ContentLength = contentLength;
  }

  public Stream Content { get; }

  public string ContentType { get; }

  public long ContentLength { get; }

  public void Dispose()
  {
    Content.Dispose();
  }

  public ValueTask DisposeAsync()
  {
    return Content.DisposeAsync();
  }
}
