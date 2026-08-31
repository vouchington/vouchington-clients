using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ImportExport;

public enum ImportExportOwner { Topics, Sources }
public enum SourceImportFormat { Urls, Csv, Opml }
public enum SourceExportFormat { Csv, Opml }
public enum SourceExportFeedType { All, Article, Podcast, Video }

public static class SourceExportFeedTypeExtensions
{
  public static string? ApiValue(this SourceExportFeedType feedType) => feedType switch
  {
    SourceExportFeedType.All => null,
    SourceExportFeedType.Article => "article",
    SourceExportFeedType.Podcast => "podcast",
    SourceExportFeedType.Video => "video",
    _ => throw new ArgumentOutOfRangeException(nameof(feedType), feedType, "Unknown source export feed type."),
  };

  public static SourceExportFeedType FromApiValue(string? value) => value switch
  {
    null => SourceExportFeedType.All,
    "article" => SourceExportFeedType.Article,
    "podcast" => SourceExportFeedType.Podcast,
    "video" => SourceExportFeedType.Video,
    _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown source export feed type."),
  };
}

public sealed record ImportExportRouteContext(
    string Path,
    ImportExportOwner Owner,
    string Title,
    string? InitialFeedType)
{
  public static ImportExportRouteContext FromPath(string path) => path switch
  {
    "/my/topics/import-export" => new(path, ImportExportOwner.Topics, "Topics", null),
    "/my/news-sources/import-export" => new(path, ImportExportOwner.Sources, "News Sources", "article"),
    "/my/podcasts/import-export" => new(path, ImportExportOwner.Sources, "Podcasts", "podcast"),
    "/my/channels/import-export" => new(path, ImportExportOwner.Sources, "Channels", "video"),
    "/my/sources/import-export" => new(path, ImportExportOwner.Sources, "Sources", null),
    _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown import/export route."),
  };
}

public sealed record SourceImportRequest(
    SourceImportFormat Format,
    IReadOnlyList<string>? Urls = null,
    string? Text = null);

public sealed record ExportDocument(string FileName, string MediaType, string FilePath) : IDisposable
{
  private readonly object lifecycleLock = new();
  private int activeLeases;
  private bool disposalRequested;

  internal ExportDocumentLease AcquireLease()
  {
    ExportDocumentLease? lease = null;
    var acquired = false;
    try
    {
      lock (lifecycleLock)
      {
        ObjectDisposedException.ThrowIf(disposalRequested, this);
        activeLeases++;
        acquired = true;
      }
      lease = new ExportDocumentLease(this);
      return lease;
    }
    finally
    {
      if (acquired && lease is null) ReleaseLease();
    }
  }

  public void Dispose()
  {
    var delete = false;
    lock (lifecycleLock)
    {
      disposalRequested = true;
      delete = activeLeases == 0;
    }
    if (delete) File.Delete(FilePath);
  }

  private void ReleaseLease()
  {
    var delete = false;
    lock (lifecycleLock)
    {
      activeLeases--;
      delete = disposalRequested && activeLeases == 0;
    }
    if (delete) File.Delete(FilePath);
  }

}

internal sealed class ExportDocumentLease : IDisposable
{
  private ExportDocument? document;

  internal ExportDocumentLease(ExportDocument document) => this.document = document;

  public ExportDocument Document
  {
    get
    {
      ObjectDisposedException.ThrowIf(document is null, this);
      return document;
    }

  }

  public void Dispose() => Interlocked.Exchange(ref document, null)?.ReleaseLease();
}

public static class ImportResultStateExtensions
{
  public static bool IsRetrying(this ImportResult result) =>
      (result ?? throw new ArgumentNullException(nameof(result))).Status == ImportResultStatus.Pending &&
      !string.IsNullOrWhiteSpace(result.Error);

  public static bool IsTerminalFailure(this ImportResult result) =>
      (result ?? throw new ArgumentNullException(nameof(result))).Status == ImportResultStatus.Error;
}

public static class ImportProgressState
{
  public static bool IsTerminal(this RssFeedImportSummary summary) =>
      (summary ?? throw new ArgumentNullException(nameof(summary))).CompletedAt is not null ||
      summary.PendingRows == 0;
}
