using Xunit;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportExportPaginationTests
{
  [Fact]
  public async Task OpaquePagesPreserveOrderAndReplaceOverlappingIds()
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1", ImportResultStatus.Pending)], "opaque+/=")));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1"), Row("2", ImportResultStatus.Error)])));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "opaque+/="], service.Cursors);
    Assert.Equal(["1", "2"], model.Results.Select(row => row.Id));
    Assert.Equal([ImportResultStatus.Followed, ImportResultStatus.Error], model.Results.Select(row => row.Status));
    Assert.False(model.CanResume);
  }

  [Fact]
  public async Task LaterPageFailureRetriesSameCursorWithoutResubmitting()
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "next")));
    service.Pages.Enqueue(() => throw new IOException("status unavailable"));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.ErrorMessage);
    Assert.True(model.CanRetryStatus);
    await model.TryStatusAgainAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "next", "next"], service.Cursors);
    Assert.Equal(1, service.Submissions);
    Assert.Equal(["1", "2"], model.Results.Select(row => row.Id));
    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task TerminalLaterPageRequiresFreshTerminalTraversal()
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1", ImportResultStatus.Pending)], "next", terminal: false)));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "final")));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "next", null, "final"], service.Cursors);
    Assert.All(model.Results, row => Assert.Equal(ImportResultStatus.Followed, row.Status));
  }

  [Fact]
  public async Task FailedFinalRefreshRetainsVisibleRowsAndRemainsRetryable()
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1", ImportResultStatus.Pending)], "first", terminal: false)));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "refresh")));
    service.Pages.Enqueue(() => throw new IOException("refresh unavailable"));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, model.Progress?.PendingRows);
    Assert.Equal([ImportResultStatus.Pending, ImportResultStatus.Followed], model.Results.Select(row => row.Status));
    Assert.True(model.CanRetryStatus);
    await model.TryStatusAgainAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "first", null, "refresh", "refresh"], service.Cursors);
    Assert.All(model.Results, row => Assert.Equal(ImportResultStatus.Followed, row.Status));
  }

  [Fact]
  public async Task NewTerminalBatchResetsPriorSnapshotCompletionBeforeLaterPageFailure()
  {
    var service = new PagesService { SubmissionTerminal = true };
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1"), Row("2")])));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "new-next")));
    service.Pages.Enqueue(() => throw new IOException("later page failed"));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2", ImportResultStatus.Error)])));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.False(model.CanResume);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.ErrorMessage);
    Assert.True(model.CanRetryStatus);
    await model.TryStatusAgainAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, null, "new-next", "new-next"], service.Cursors);
    Assert.Equal(2, service.Submissions);
    Assert.Equal([ImportResultStatus.Followed, ImportResultStatus.Error], model.Results.Select(row => row.Status));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("same")]
  public async Task InvalidContinuationFailsWithoutLooping(string? next)
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "same")));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")], next, hasNext: true)));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.ErrorMessage);
    Assert.True(model.CanRetryStatus);
    Assert.Equal(2, service.Cursors.Count);
  }

  [Fact]
  public async Task CancelledLaterPageCannotPublishOverReplacementBatch()
  {
    var service = new PagesService();
    var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var held = new TaskCompletionSource<RssFeedImportStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "next")));
    service.Pages.Enqueue(() => { requested.SetResult(); return held.Task; });
    using var model = Model(service);
    var oldImport = model.ImportAsync(TestContext.Current.CancellationToken);
    await requested.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    model.CancelActiveOperations();
    service.BatchId = "replacement";
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("new")], batch: "replacement")));
    await model.ImportAsync(TestContext.Current.CancellationToken);
    held.SetResult(Page([Row("2")]));
    await oldImport.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal("replacement", model.BatchId);
    Assert.Equal("new", Assert.Single(model.Results).Id);
    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task OnePagePerTickPreservesContinuationAcrossPause()
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "next")));
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    var tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/sources/import-export"), service, (_, token) => { tick.SetResult(); return release.Task.WaitAsync(token); });
    model.InputText = "https://example.test/feed";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);
    await tick.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal([null], service.Cursors);
    Assert.True(model.IsMonitoring);
    release.SetResult();
    await import.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal([null, "next"], service.Cursors);
    Assert.Equal(2, model.Results.Count);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task ForeignOrRegressingLaterPageDoesNotPublishMixedSnapshot(bool foreign)
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "next")));
    service.Pages.Enqueue(() => Task.FromResult(new RssFeedImportStatus(
        Summary(foreign ? "foreign" : "batch", false), [Row("2", ImportResultStatus.Error)])));
    using var model = Model(service);
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.ErrorMessage);
    Assert.Empty(model.Results);
    Assert.True(model.CanRetryStatus);
    Assert.Equal([null, "next"], service.Cursors);
  }

  [Theory]
  [InlineData("cursor")]
  [InlineData("foreign")]
  [InlineData("regression")]
  public async Task ClientPageValidationUsesLiveLocalizedCopyAndRetriesSameCursor(string failure)
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "next")));
    service.Pages.Enqueue(() => Task.FromResult(failure switch
    {
      "foreign" => Page([Row("2")], batch: "foreign"),
      "regression" => Page([Row("2")], terminal: false),
      _ => Page([Row("2")], hasNext: true),
    }));
    var retryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var retryPage = new TaskCompletionSource<RssFeedImportStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.Pages.Enqueue(() => { retryStarted.SetResult(); return retryPage.Task; });
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("es"), new InlineDispatcher());
    var localization = new UiLocalization(controller);
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/sources/import-export"), service,
        (_, _) => Task.CompletedTask, localization, controller)
    { InputText = "https://example.test/feed" };
    await model.ImportAsync(TestContext.Current.CancellationToken);
    var spanish = localization.Localize(UiMessageKey.NativeDotnetCsharpActionFailed);
    Assert.Equal(spanish, model.ErrorMessage);
    Assert.True(model.CanRetryStatus);
    List<string?> changed = [];
    model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
    controller.ApplySavedLocale("fr");
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetCsharpActionFailed), model.ErrorMessage);
    Assert.NotEqual(spanish, model.ErrorMessage);
    Assert.Contains(nameof(model.ErrorMessage), changed);
    changed.Clear();
    var retry = model.TryStatusAgainAsync(TestContext.Current.CancellationToken);
    await retryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Null(model.ErrorMessage);
    Assert.Single(changed, name => name == nameof(model.ErrorMessage));
    Assert.Contains(nameof(model.CanRetryStatus), changed);
    retryPage.SetResult(Page([Row("2")]));
    await retry.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal([null, "next", "next"], service.Cursors);
    Assert.Equal(["1", "2"], model.Results.Select(row => row.Id));
    Assert.Equal(1, service.Submissions);
    Assert.Null(model.ErrorMessage);
    Assert.False(model.CanRetryStatus);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnrelatedTransportOrServerFailureRetainsOriginalDetailAcrossLocaleChanges(bool server)
  {
    var service = new PagesService();
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("1")], "next")));
    Exception failure = server
        ? new VouchaApiException(System.Net.HttpStatusCode.BadGateway, "upstream response body")
        : new IOException("transport detail supplied by boundary");
    service.Pages.Enqueue(() => throw failure);
    service.Pages.Enqueue(() => Task.FromResult(Page([Row("2")])));
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("es"), new InlineDispatcher());
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/sources/import-export"), service,
        (_, _) => Task.CompletedTask, new UiLocalization(controller), controller)
    { InputText = "https://example.test/feed" };
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal(failure.Message, model.ErrorMessage);
    controller.ApplySavedLocale("fr");
    Assert.Equal(failure.Message, model.ErrorMessage);
    await model.TryStatusAgainAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "next", "next"], service.Cursors);
    Assert.Null(model.ErrorMessage);
  }

  private sealed class InlineDispatcher : IUiThreadDispatcher
  {
    public bool IsDispatchRequired => false;
    public void Dispatch(Action action) => action();
  }

  private static ImportExportViewModel Model(PagesService service) => new(
      ImportExportRouteContext.FromPath("/my/sources/import-export"), service, (_, _) => Task.CompletedTask)
  { InputText = "https://example.test/feed" };

  private static ImportResult Row(string id, ImportResultStatus status = ImportResultStatus.Followed) =>
      new(id, $"https://example.test/{id}", status, null, null, null);

  private static RssFeedImportStatus Page(
      IReadOnlyList<ImportResult> rows, string? cursor = null, bool terminal = true,
      bool? hasNext = null, string batch = "batch") =>
      new(Summary(batch, terminal), rows, new(cursor, hasNext ?? cursor is not null, null));

  private static RssFeedImportSummary Summary(string id, bool terminal) =>
      new(id, 2, terminal ? 2 : 0, 0, terminal ? 0 : 2,
          terminal ? DateTimeOffset.UnixEpoch : null, DateTimeOffset.UnixEpoch);

  private sealed class PagesService : IImportExportService
  {
    public bool SubmissionTerminal { get; set; }
    public string BatchId { get; set; } = "batch";
    public int Submissions { get; private set; }
    public List<string?> Cursors { get; } = [];
    public Queue<Func<Task<RssFeedImportStatus>>> Pages { get; } = [];
    public Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken token)
    {
      Submissions++;
      return Task.FromResult(new RssFeedImportSubmission(Summary(BatchId, SubmissionTerminal), new("/status", UriKind.Relative)));
    }
    public Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, string? after, CancellationToken token)
    {
      Cursors.Add(after);
      return Pages.Dequeue()();
    }
    public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token) =>
        throw new NotSupportedException();
    public Task<ExportDocument> ExportTopicsAsync(CancellationToken token) => throw new NotSupportedException();
    public Task<ExportDocument> ExportSourcesAsync(string? feedType, SourceExportFormat format, CancellationToken token) =>
        throw new NotSupportedException();
  }
}
