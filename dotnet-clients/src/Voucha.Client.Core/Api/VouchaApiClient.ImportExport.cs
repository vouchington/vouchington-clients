namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token = default) =>
      SendAsync<TopicImportResponse>(VouchaApiEndpoints.ImportTopics(names), token);

  public Task<string> DownloadTopicsExportAsync(CancellationToken token = default) =>
      DownloadToTemporaryFileAsync(VouchaApiEndpoints.ExportTopicsDownload(), "topics.json", token);

  public Task<RssFeedImportSubmission> ImportRssFeedUrlsAsync(IReadOnlyList<string> urls, CancellationToken token = default) =>
      SendAsync<RssFeedImportSubmission>(VouchaApiEndpoints.ImportRssFeedUrls(urls), token);

  public Task<RssFeedImportSubmission> ImportRssFeedCsvAsync(string csv, CancellationToken token = default) =>
      SendAsync<RssFeedImportSubmission>(VouchaApiEndpoints.ImportRssFeedCsv(csv), token);

  public Task<RssFeedImportSubmission> ImportRssFeedOpmlAsync(string opml, CancellationToken token = default) =>
      SendAsync<RssFeedImportSubmission>(VouchaApiEndpoints.ImportRssFeedOpml(opml), token);

  public Task<RssFeedImportStatus> RssFeedImportStatusAsync(string importId, CancellationToken token = default) =>
      SendAsync<RssFeedImportStatus>(VouchaApiEndpoints.RssFeedImportStatus(importId), token);

  public Task<string> DownloadRssFeedsExportAsync(string? feedType, string format, CancellationToken token = default) =>
      DownloadToTemporaryFileAsync(VouchaApiEndpoints.ExportRssFeeds(feedType, format), $"rss-feeds.{format}", token);
}
