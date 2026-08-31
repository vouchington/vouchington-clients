using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportExportExportLifecycleTests
{
  [Fact]
  public async Task ExportDoesNotReplaceResumedMonitoringOperation()
  {
    var service = new LifecycleService();
    var enteredDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/sources/import-export"),
        service,
        async (_, token) =>
        {
          enteredDelay.SetResult();
          await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
    model.InputText = "https://example.test/feed.xml";
    var initialImport = model.ImportAsync(TestContext.Current.CancellationToken);
    await enteredDelay.Task;
    model.StopMonitoring();
    await initialImport;

    service.PendingStatus = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var resumedMonitoring = model.ResumeMonitoringAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsMonitoring);
    Assert.False(model.CanExport);

    await model.ExportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, service.ExportCalls);
    Assert.False(service.StatusToken.IsCancellationRequested);
    Assert.True(model.IsMonitoring);
    service.PendingStatus.SetResult(LifecycleService.TerminalStatus());
    await resumedMonitoring;
  }

  [Fact]
  public async Task FailedExportRetryClearsPreviousShareArtifact()
  {
    var service = new LifecycleService();
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/topics/import-export"), service);

    await model.ExportAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.ExportDocument);
    service.ExportFailure = new HttpRequestException("export failed");

    await model.ExportAsync(TestContext.Current.CancellationToken);

    Assert.Null(model.ExportDocument);
    Assert.False(model.HasExportDocument);
    Assert.Equal("export failed", model.ErrorMessage);
  }

  [Fact]
  public async Task DisposeRemovesRetainedExportArtifact()
  {
    var service = new LifecycleService();
    var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/topics/import-export"), service);

    await model.ExportAsync(TestContext.Current.CancellationToken);
    var path = Assert.IsType<ExportDocument>(model.ExportDocument).FilePath;
    Assert.True(File.Exists(path));

    model.Dispose();

    Assert.False(File.Exists(path));
    Assert.Null(model.ExportDocument);
    Assert.False(model.HasExportDocument);
  }

  private sealed class LifecycleService : IImportExportService
  {
    private const string ImportId = "70000000-0000-7000-8000-000000000001";
    public TaskCompletionSource<RssFeedImportStatus>? PendingStatus { get; set; }
    public CancellationToken StatusToken { get; private set; }
    public Exception? ExportFailure { get; set; }
    public int ExportCalls { get; private set; }

    public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token) =>
        Task.FromResult(new TopicImportResponse([]));

    public Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken token) =>
        Task.FromResult(new RssFeedImportSubmission(Summary(0, 0, 1), new Uri($"/status/{ImportId}", UriKind.Relative)));

    public Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, CancellationToken token)
    {
      StatusToken = token;
      return PendingStatus?.Task ?? Task.FromResult(new RssFeedImportStatus(Summary(0, 0, 1), []));
    }

    public Task<ExportDocument> ExportTopicsAsync(CancellationToken token) => Export();

    public Task<ExportDocument> ExportSourcesAsync(
        string? feedType,
        SourceExportFormat format,
        CancellationToken token) => Export();

    private Task<ExportDocument> Export()
    {
      ExportCalls++;
      return ExportFailure is { } failure
          ? Task.FromException<ExportDocument>(failure)
          : Task.FromResult(ImportExportTestDocuments.Create());
    }

    public static RssFeedImportStatus TerminalStatus() =>
        new(Summary(1, 0, 0, DateTimeOffset.UtcNow), []);

    private static RssFeedImportSummary Summary(
        int completed,
        int failed,
        int pending,
        DateTimeOffset? completedAt = null) =>
        new(ImportId, 1, completed, failed, pending, completedAt, DateTimeOffset.UtcNow);
  }
}
