using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ImportExport;

public sealed class ApiImportExportService(VouchaApiClient apiClient) : IImportExportService
{
  public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token) =>
      apiClient.ImportTopicsAsync(names, token);

  public Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken token) =>
      (request ?? throw new ArgumentNullException(nameof(request))).Format switch
      {
        SourceImportFormat.Urls => apiClient.ImportRssFeedUrlsAsync(request.Urls ?? [], token),
        SourceImportFormat.Csv => apiClient.ImportRssFeedCsvAsync(request.Text ?? string.Empty, token),
        SourceImportFormat.Opml => apiClient.ImportRssFeedOpmlAsync(request.Text ?? string.Empty, token),
        _ => throw new ArgumentOutOfRangeException(nameof(request)),
      };

  public Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, CancellationToken token) =>
      apiClient.RssFeedImportStatusAsync(importId, token);

  public async Task<ExportDocument> ExportTopicsAsync(CancellationToken token)
  {
    var path = await apiClient.DownloadTopicsExportAsync(token).ConfigureAwait(false);
    return new("topics.json", "application/json", path);
  }

  public async Task<ExportDocument> ExportSourcesAsync(
      string? feedType,
      SourceExportFormat format,
      CancellationToken token)
  {
    var formatName = format == SourceExportFormat.Csv ? "csv" : "opml";
    var path = await apiClient.DownloadRssFeedsExportAsync(feedType, formatName, token).ConfigureAwait(false);
    var mediaType = format == SourceExportFormat.Csv ? "text/csv" : "text/xml";
    return new($"rss-feeds.{formatName}", mediaType, path);
  }
}
