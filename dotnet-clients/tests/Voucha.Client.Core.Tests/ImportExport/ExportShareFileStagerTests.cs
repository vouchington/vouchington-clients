using Voucha.Client.Core.ImportExport;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ExportShareFileStagerTests : IDisposable
{
  private readonly string root = Path.Combine(
      Path.GetTempPath(), $"voucha-share-stager-{Guid.NewGuid():N}");

  [Fact]
  public async Task StagesStableFilenameAndRetainsSuccessfulShare()
  {
    var document = await CreateDocumentAsync("topics.json", "topic bytes");
    string? stagedPath = null;

    await ExportShareFileStager.StageAndUseAsync(
        root,
        document,
        async (path, token) =>
        {
          stagedPath = path;
          Assert.Equal("topics.json", Path.GetFileName(path));
          Assert.Equal("topic bytes", await File.ReadAllTextAsync(path, token));
        },
        TestContext.Current.CancellationToken);

    Assert.NotNull(stagedPath);
    Assert.True(Directory.Exists(Path.GetDirectoryName(stagedPath)));
  }

  [Fact]
  public async Task ConcurrentStagesUseDistinctDirectories()
  {
    var first = await CreateDocumentAsync("topics.json", "first");
    var second = await CreateDocumentAsync("topics.json", "second");
    var bothStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = 0;
    var paths = new List<string>();
    var gate = new object();

    Task UseAsync(string path, CancellationToken _)
    {
      lock (gate) paths.Add(path);
      if (Interlocked.Increment(ref started) == 2) bothStarted.SetResult(true);
      return bothStarted.Task;
    }

    await Task.WhenAll(
        ExportShareFileStager.StageAndUseAsync(
            root, first, UseAsync, TestContext.Current.CancellationToken),
        ExportShareFileStager.StageAndUseAsync(
            root, second, UseAsync, TestContext.Current.CancellationToken));

    Assert.Equal(2, paths.Count);
    Assert.All(paths, path => Assert.Equal("topics.json", Path.GetFileName(path)));
    Assert.NotEqual(Path.GetDirectoryName(paths[0]), Path.GetDirectoryName(paths[1]));
    Assert.All(paths, path => Assert.True(Directory.Exists(Path.GetDirectoryName(path))));
  }

  [Fact]
  public async Task NextShareCleansExpiredStagedDirectories()
  {
    var first = await CreateDocumentAsync("topics.json", "first");
    string? firstPath = null;
    await ExportShareFileStager.StageAndUseAsync(
        root,
        first,
        (path, _) =>
        {
          firstPath = path;
          return Task.CompletedTask;
        },
        TestContext.Current.CancellationToken);
    var firstDirectory = Path.GetDirectoryName(firstPath)!;
    Directory.SetLastWriteTimeUtc(
        firstDirectory, DateTime.UtcNow - ExportShareFileStager.StagedFileRetention - TimeSpan.FromHours(1));

    var second = await CreateDocumentAsync("topics.json", "second");
    string? secondPath = null;
    await ExportShareFileStager.StageAndUseAsync(
        root,
        second,
        (path, _) =>
        {
          secondPath = path;
          return Task.CompletedTask;
        },
        TestContext.Current.CancellationToken);

    Assert.False(Directory.Exists(firstDirectory));
    Assert.True(Directory.Exists(Path.GetDirectoryName(secondPath)));
  }

  [Fact]
  public async Task CleanupCannotMaskTheSharingFailure()
  {
    var document = await CreateDocumentAsync("topics.json", "topic bytes");
    var expected = new InvalidOperationException("share failed");
    string? stagedPath = null;

    var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        ExportShareFileStager.StageAndUseAsync(
            root,
            document,
            (path, _) =>
            {
              stagedPath = path;
              Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
              return Task.FromException(expected);
            },
            TestContext.Current.CancellationToken));

    Assert.Same(expected, actual);
    Assert.NotNull(stagedPath);
    Assert.False(Directory.Exists(Path.GetDirectoryName(stagedPath)));
  }

  [Fact]
  public async Task CancellationCleansTheStagedDirectory()
  {
    var document = await CreateDocumentAsync("topics.json", "topic bytes");
    using var cancellation = new CancellationTokenSource();
    string? stagedPath = null;

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        ExportShareFileStager.StageAndUseAsync(
            root,
            document,
            (path, token) =>
            {
              stagedPath = path;
              cancellation.Cancel();
              return Task.FromCanceled(token);
            },
            cancellation.Token));

    Assert.NotNull(stagedPath);
    Assert.False(Directory.Exists(Path.GetDirectoryName(stagedPath)));
  }

  public void Dispose()
  {
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
  }

  private async Task<ExportDocument> CreateDocumentAsync(string fileName, string contents)
  {
    Directory.CreateDirectory(root);
    var path = Path.Combine(root, $"source-{Guid.NewGuid():N}");
    await File.WriteAllTextAsync(path, contents, TestContext.Current.CancellationToken);
    return new(fileName, "application/json", path);
  }
}
