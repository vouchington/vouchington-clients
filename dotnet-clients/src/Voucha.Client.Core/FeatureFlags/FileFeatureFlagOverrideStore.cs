using System.Text.Json;

namespace Voucha.Client.Core.FeatureFlags;

public sealed class FileFeatureFlagOverrideStore(string path) : IFeatureFlagOverrideStore, IDisposable
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private readonly SemaphoreSlim gate = new(1, 1);

  public async Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (!File.Exists(path)) return new Dictionary<string, bool>(StringComparer.Ordinal);
      try
      {
        using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new Dictionary<string, bool>(StringComparer.Ordinal);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
      {
        return new Dictionary<string, bool>(StringComparer.Ordinal);
      }
    }
    finally
    {
      gate.Release();
    }
  }

  public async Task SaveAsync(IReadOnlyDictionary<string, bool> values, CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
      using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        await JsonSerializer.SerializeAsync(stream, values, JsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
      }
      File.Move(tempPath, path, overwrite: true);
    }
    finally
    {
      try
      {
        TryDeleteTemporaryFile(tempPath);
      }
      finally
      {
        gate.Release();
      }
    }
  }

  private static void TryDeleteTemporaryFile(string tempPath)
  {
    try
    {
      if (File.Exists(tempPath)) File.Delete(tempPath);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
    }
  }

  public void Dispose() => gate.Dispose();
}
