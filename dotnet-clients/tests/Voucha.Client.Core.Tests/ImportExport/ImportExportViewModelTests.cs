using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportExportViewModelTests
{
  [Fact]
  public void ValidatesEmptyOversizedAndOverFiveHundredInputs()
  {
    Assert.NotNull(ImportExportValidation.ValidateTopics([]));
    Assert.NotNull(ImportExportValidation.ValidateTopics(Enumerable.Repeat("topic", 501).ToArray()));
    Assert.NotNull(ImportExportValidation.Validate(new(SourceImportFormat.Urls, [])));
    Assert.NotNull(ImportExportValidation.Validate(new(
        SourceImportFormat.Csv,
        Text: new string('x', ImportExportValidation.MaximumBodyBytes))));
    var retrying = new ImportResult(null, "source", ImportResultStatus.Pending, "Retry scheduled", null, null);
    Assert.True(retrying.IsRetrying());
    Assert.Equal("Retrying", retrying.DisplayStatus);
  }

  [Fact]
  public void CsvValidationExcludesRecognizedExportHeader()
  {
    var fiveHundred = "url,title\n" + string.Join('\n', Enumerable.Range(1, 500).Select(i => $"https://example.test/{i},Feed {i}"));
    var fiveHundredOne = fiveHundred + "\nhttps://example.test/501,Feed 501";

    Assert.Null(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: fiveHundred)));
    Assert.Equal(
        "Import up to 500 sources at a time.",
        UiLocalization.English.Resolve(
            ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: fiveHundredOne))!.Value));
  }

  [Fact]
  public async Task TopicImportReturnsPartialRowOutcomesWithoutPolling()
  {
    var service = new FakeImportExportService
    {
      TopicResponse = new([Result("Travel", ImportResultStatus.Followed), Result("Bad", ImportResultStatus.Error)]),
    };
    using var model = TopicModel(service);
    model.InputText = "Travel\nBad";

    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, model.Results.Count);
    Assert.Equal(0, service.StatusCalls);
    Assert.True(model.HasPartialFailures);
  }

  [Fact]
  public async Task TopicImportCancellationSuppressesLateCompletion()
  {
    var service = new FakeImportExportService { PendingTopicImport = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = TopicModel(service);
    model.InputText = "Travel";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);

    model.CancelActiveOperations();
    service.PendingTopicImport!.SetResult(new([Result("Travel", ImportResultStatus.Followed)]));
    await import;

    Assert.True(service.TopicImportToken.IsCancellationRequested);
    Assert.Empty(model.Results);
    Assert.False(model.IsBusy);
  }

  [Fact]
  public async Task TopicImportLateFaultAfterCancellationIsSuppressed()
  {
    var service = new FakeImportExportService { PendingTopicImport = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = TopicModel(service);
    model.InputText = "Travel";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);
    model.CancelActiveOperations();
    service.PendingTopicImport!.SetException(new HttpRequestException("late topic failure"));

    await import;

    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task TopicExportCancellationSuppressesLateArtifact()
  {
    var service = new FakeImportExportService { PendingTopicExport = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = TopicModel(service);
    var export = model.ExportAsync(TestContext.Current.CancellationToken);

    model.CancelActiveOperations();
    service.PendingTopicExport!.SetResult(ImportExportTestDocuments.Create());
    await export;

    Assert.True(service.TopicExportToken.IsCancellationRequested);
    Assert.Null(model.ExportDocument);
    Assert.False(model.IsBusy);
  }

  [Fact]
  public async Task TopicExportLateFaultAfterCancellationIsSuppressed()
  {
    var service = new FakeImportExportService { PendingTopicExport = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = TopicModel(service);
    var export = model.ExportAsync(TestContext.Current.CancellationToken);
    model.CancelActiveOperations();
    service.PendingTopicExport!.SetException(new HttpRequestException("late export failure"));

    await export;

    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task SourceSubmitCancellationPreventsBatchAndPollingMutation()
  {
    var service = new FakeImportExportService { PendingSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);

    model.CancelActiveOperations();
    service.PendingSubmission!.SetResult(service.Submission);
    await import;

    Assert.True(service.SourceSubmitToken.IsCancellationRequested);
    Assert.Null(model.BatchId);
    Assert.Equal(0, service.StatusCalls);
  }

  [Fact]
  public async Task SourceSubmitLateFaultAfterCancellationIsSuppressed()
  {
    var service = new FakeImportExportService { PendingSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);
    model.CancelActiveOperations();
    service.PendingSubmission!.SetException(new HttpRequestException("late submit failure"));

    await import;

    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task SourceStatusLateFaultAfterCancellationIsSuppressed()
  {
    var service = new FakeImportExportService { PendingStatus = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);
    model.CancelActiveOperations();
    service.PendingStatus!.SetException(new HttpRequestException("late status failure"));

    await import;

    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task FailedTopicImportCanRetryWithPreservedInput()
  {
    var service = new FakeImportExportService();
    service.TopicResponses.Enqueue(new([Result("Travel", ImportResultStatus.Error)]));
    service.TopicResponses.Enqueue(new([Result("Travel", ImportResultStatus.Followed)]));
    using var model = TopicModel(service);
    model.InputText = "Travel";

    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Travel", model.InputText);
    Assert.True(model.Results[0].IsTerminalFailure());
    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.TopicImportCalls);
    Assert.Equal(ImportResultStatus.Followed, model.Results[0].Status);
  }

  [Fact]
  public async Task ConcurrentTopicImportCallsSubmitOnce()
  {
    var service = new FakeImportExportService { PendingTopicImport = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var model = TopicModel(service);
    model.InputText = "Travel";
    var first = model.ImportAsync(TestContext.Current.CancellationToken);
    var second = model.ImportAsync(TestContext.Current.CancellationToken);
    service.PendingTopicImport!.SetResult(new([Result("Travel", ImportResultStatus.Followed)]));

    await Task.WhenAll(first, second);

    Assert.Equal(1, service.TopicImportCalls);
  }

  [Fact]
  public void ExternalAdapterFailureOnlyMutatesCurrentLifecycle()
  {
    var service = new FakeImportExportService();
    using var model = TopicModel(service);
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();

    model.ReportExternalFailure(new IOException("picker failed"), TestContext.Current.CancellationToken);
    Assert.Equal("picker failed", model.ErrorMessage);
    model.ReportExternalFailure(new IOException("late picker failure"), cancelled.Token);

    Assert.Equal("picker failed", model.ErrorMessage);
  }

  [Fact]
  public async Task SourceImportPollsSequentiallyUntilTerminal()
  {
    var service = new FakeImportExportService();
    service.Statuses.Enqueue(Pending());
    service.Statuses.Enqueue(Terminal());
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";

    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.StatusCalls);
    Assert.Equal(2, model.Results.Count);
    Assert.False(model.IsMonitoring);
    Assert.True(model.Results[1].IsTerminalFailure());
    Assert.Equal("Failed", model.Results[1].DisplayStatus);
  }

  [Fact]
  public async Task StopAndResumeMonitorTheSameBatchWithoutResubmitting()
  {
    var service = new FakeImportExportService();
    service.Statuses.Enqueue(Pending());
    var enteredDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var model = SourceModel(service, async (_, token) =>
    {
      enteredDelay.SetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, token);
    });
    model.InputText = "https://example.test/feed.xml";
    var import = model.ImportAsync(TestContext.Current.CancellationToken);
    await enteredDelay.Task;

    model.StopMonitoring();
    await import;
    service.Statuses.Enqueue(Terminal());
    await model.ResumeMonitoringAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, service.SubmitCalls);
    Assert.Equal(FakeImportExportService.ImportId, model.BatchId);
    Assert.NotNull(model.Progress?.CompletedAt);
  }

  [Fact]
  public async Task StatusFailureCanRetryWithoutResubmitting()
  {
    var service = new FakeImportExportService { StatusFailure = new HttpRequestException("offline") };
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal("offline", model.ErrorMessage);

    service.StatusFailure = null;
    service.Statuses.Enqueue(Terminal());
    await model.TryStatusAgainAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, service.SubmitCalls);
    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task FailedNewSubmissionCannotRetryPreviousBatchStatus()
  {
    var service = new FakeImportExportService();
    service.Statuses.Enqueue(Pending());
    var enteredDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var model = SourceModel(service, async (_, token) =>
    {
      enteredDelay.SetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, token);
    });
    model.InputText = "https://example.test/first.xml";
    var firstImport = model.ImportAsync(TestContext.Current.CancellationToken);
    await enteredDelay.Task;
    model.StopMonitoring();
    await firstImport;
    Assert.True(model.CanResume);
    Assert.NotEmpty(model.Results);

    service.PendingSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingSubmission.SetException(new HttpRequestException("submit failed"));
    model.InputText = "https://example.test/second.xml";
    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal("submit failed", model.ErrorMessage);
    Assert.Null(model.BatchId);
    Assert.Null(model.Progress);
    Assert.Empty(model.Results);
    Assert.False(model.CanRetryStatus);
  }

  [Fact]
  public async Task RegressingResponsesAreIgnored()
  {
    var service = new FakeImportExportService { Submission = Submission(completed: 1, pending: 1) };
    service.Statuses.Enqueue(Status(completed: 0, failed: 0, pending: 2));
    service.Statuses.Enqueue(Terminal());
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";

    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, model.Progress?.CompletedRows);
    Assert.Equal(2, service.StatusCalls);
  }

  [Fact]
  public async Task MismatchedBatchTerminalSnapshotIsIgnored()
  {
    var service = new FakeImportExportService();
    service.Statuses.Enqueue(new(
        Summary(2, 0, 0, DateTimeOffset.UtcNow, "70000000-0000-7000-8000-000000000099"),
        [Result("stale", ImportResultStatus.Followed)]));
    service.Statuses.Enqueue(Terminal());
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";

    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.StatusCalls);
    Assert.DoesNotContain(model.Results, result => result.Input == "stale");
    Assert.Equal(FakeImportExportService.ImportId, model.Progress?.Id);
  }

  [Fact]
  public async Task ZeroPendingRowsWithoutCompletionDoesNotOfferResume()
  {
    var service = new FakeImportExportService();
    service.Statuses.Enqueue(Status(completed: 2, failed: 0, pending: 0));
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";

    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsMonitoring);
    Assert.False(model.CanResume);
    Assert.False(model.CanRetryStatus);
    Assert.True(model.Progress?.IsTerminal());
  }

  [Fact]
  public async Task ConcurrentImportTapDoesNotSubmitTwice()
  {
    var service = new FakeImportExportService
    {
      PendingSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously),
    };
    service.Statuses.Enqueue(Terminal());
    using var model = SourceModel(service, (_, _) => Task.CompletedTask);
    model.InputText = "https://example.test/feed.xml";
    var first = model.ImportAsync(TestContext.Current.CancellationToken);

    var second = model.ImportAsync(TestContext.Current.CancellationToken);
    service.PendingSubmission!.SetResult(service.Submission);
    await Task.WhenAll(first, second);

    Assert.Equal(1, service.SubmitCalls);
  }

  [Fact]
  public async Task ExportUsesRouteDerivedFilterAndPortableDocument()
  {
    var service = new FakeImportExportService();
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/podcasts/import-export"), service);
    model.SourceExportFormat = SourceExportFormat.Opml;

    await model.ExportAsync(TestContext.Current.CancellationToken);

    Assert.Equal("podcast", service.ExportedFeedType);
    Assert.Equal(SourceExportFormat.Opml, service.ExportedFormat);
    Assert.Equal("rss-feeds.opml", model.ExportDocument?.FileName);
  }

  [Theory]
  [InlineData(SourceExportFeedType.All, null)]
  [InlineData(SourceExportFeedType.Article, "article")]
  [InlineData(SourceExportFeedType.Podcast, "podcast")]
  [InlineData(SourceExportFeedType.Video, "video")]
  public async Task SourceExportPickerOverridesRouteDefault(
      SourceExportFeedType selectedType,
      string? expectedApiValue)
  {
    var service = new FakeImportExportService();
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/podcasts/import-export"), service)
    {
      SelectedSourceExportFeedType = selectedType,
    };

    await model.ExportAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expectedApiValue, service.ExportedFeedType);
  }

  private static ImportExportViewModel TopicModel(FakeImportExportService service) =>
      new(ImportExportRouteContext.FromPath("/my/topics/import-export"), service);

  private static ImportExportViewModel SourceModel(
      FakeImportExportService service,
      Func<TimeSpan, CancellationToken, Task> delay) =>
      new(ImportExportRouteContext.FromPath("/my/sources/import-export"), service, delay);

  private static ImportResult Result(string input, ImportResultStatus status) =>
      new(null, input, status, status == ImportResultStatus.Error ? "failed" : null, null, null);

  private static RssFeedImportSubmission Submission(int completed = 0, int pending = 2) =>
      new(Summary(completed, 0, pending), new Uri($"/api/v1/my/import/rss-feeds/{FakeImportExportService.ImportId}", UriKind.Relative));

  private static RssFeedImportStatus Pending() => Status(0, 0, 2);

  private static RssFeedImportStatus Terminal() => new(
      Summary(2, 0, 0, DateTimeOffset.UtcNow),
      [Result("one", ImportResultStatus.Followed), Result("two", ImportResultStatus.Error)]);

  private static RssFeedImportStatus Status(int completed, int failed, int pending) =>
      new(Summary(completed, failed, pending), [Result("one", ImportResultStatus.Pending)]);

  private static RssFeedImportSummary Summary(
      int completed,
      int failed,
      int pending,
      DateTimeOffset? completedAt = null,
      string id = FakeImportExportService.ImportId) =>
      new(id, 2, completed, failed, pending, completedAt, DateTimeOffset.UtcNow);

  private sealed class FakeImportExportService : IImportExportService
  {
    public const string ImportId = "70000000-0000-7000-8000-000000000001";
    public TopicImportResponse TopicResponse { get; set; } = new([]);
    public RssFeedImportSubmission Submission { get; set; } = ImportExportViewModelTests.Submission();
    public Queue<RssFeedImportStatus> Statuses { get; } = [];
    public Exception? StatusFailure { get; set; }
    public TaskCompletionSource<RssFeedImportSubmission>? PendingSubmission { get; set; }
    public TaskCompletionSource<TopicImportResponse>? PendingTopicImport { get; set; }
    public TaskCompletionSource<ExportDocument>? PendingTopicExport { get; set; }
    public TaskCompletionSource<RssFeedImportStatus>? PendingStatus { get; set; }
    public Queue<TopicImportResponse> TopicResponses { get; } = [];
    public int TopicImportCalls { get; private set; }
    public CancellationToken TopicImportToken { get; private set; }
    public CancellationToken TopicExportToken { get; private set; }
    public CancellationToken SourceSubmitToken { get; private set; }
    public int SubmitCalls { get; private set; }
    public int StatusCalls { get; private set; }
    public string? ExportedFeedType { get; private set; }
    public SourceExportFormat ExportedFormat { get; private set; }

    public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token)
    {
      TopicImportCalls++;
      TopicImportToken = token;
      return PendingTopicImport?.Task ?? Task.FromResult(TopicResponses.Count > 0 ? TopicResponses.Dequeue() : TopicResponse);
    }

    public Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken token)
    {
      SubmitCalls++;
      SourceSubmitToken = token;
      return PendingSubmission?.Task ?? Task.FromResult(Submission);
    }

    public Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, CancellationToken token)
    {
      StatusCalls++;
      if (StatusFailure is { } failure) throw failure;
      if (PendingStatus is not null) return PendingStatus.Task;
      return Task.FromResult(Statuses.Dequeue());
    }

    public Task<ExportDocument> ExportTopicsAsync(CancellationToken token)
    {
      TopicExportToken = token;
      return PendingTopicExport?.Task ??
          Task.FromResult(ImportExportTestDocuments.Create());
    }

    public Task<ExportDocument> ExportSourcesAsync(string? feedType, SourceExportFormat format, CancellationToken token)
    {
      ExportedFeedType = feedType;
      ExportedFormat = format;
      return Task.FromResult(ImportExportTestDocuments.Create("rss-feeds.opml", "text/xml", "<opml/>"));
    }
  }
}
