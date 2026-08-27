using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ImportExport;

public interface IImportExportService
{
  Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token);
  Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken token);
  Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, CancellationToken token);
  Task<ExportDocument> ExportTopicsAsync(CancellationToken token);
  Task<ExportDocument> ExportSourcesAsync(string? feedType, SourceExportFormat format, CancellationToken token);
}
