namespace Voucha.Client.Core.ImportExport;

public static class ExportShareFileStager
{
  internal static readonly TimeSpan StagedFileRetention = TimeSpan.FromDays(1);

  public static async Task StageAndUseAsync(
      string cacheDirectory,
      ExportDocument document,
      Func<string, CancellationToken, Task> useAsync,
      CancellationToken token)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
    ArgumentNullException.ThrowIfNull(document);
    ArgumentNullException.ThrowIfNull(useAsync);

    var exportsDirectory = Path.Combine(cacheDirectory, "exports");
    TryDeleteExpiredDirectories(exportsDirectory, DateTime.UtcNow - StagedFileRetention);
    var directory = Path.Combine(exportsDirectory, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, document.FileName);
    var shared = false;
    try
    {
      using var lease = document.AcquireLease();
      var source = File.OpenRead(lease.Document.FilePath);
      await using (source.ConfigureAwait(false))
      {
        var destination = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        await using (destination.ConfigureAwait(false))
        {
          await source.CopyToAsync(destination, token).ConfigureAwait(false);
        }
      }
      await useAsync(path, token).ConfigureAwait(false);
      shared = true;
    }
    finally
    {
      if (!shared) TryDeleteDirectory(directory);
    }
  }

  private static void TryDeleteExpiredDirectories(string exportsDirectory, DateTime cutoffUtc)
  {
    try
    {
      foreach (var directory in Directory.EnumerateDirectories(exportsDirectory))
      {
        if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
        if (Directory.GetLastWriteTimeUtc(directory) < cutoffUtc) TryDeleteDirectory(directory);
      }
    }
    catch (DirectoryNotFoundException)
    {
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
  }

  private static void TryDeleteDirectory(string directory)
  {
    try
    {
      Directory.Delete(directory, recursive: true);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
  }
}
