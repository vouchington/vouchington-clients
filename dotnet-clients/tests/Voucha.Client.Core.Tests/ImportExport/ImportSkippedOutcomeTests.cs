using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportSkippedOutcomeTests
{
  [Theory]
  [InlineData("RELATION_LIMIT_REACHED", RssFeedImportErrorCode.RelationLimitReached)]
  [InlineData("ACCOUNT_SUSPENDED", RssFeedImportErrorCode.AccountSuspended)]
  public void RssErrorRowsDecodePublicCodes(string code, RssFeedImportErrorCode expected)
  {
    var json = $"{{\"input\":\"https://example.test/feed\",\"status\":\"error\",\"error_code\":\"{code}\"}}";
    var row = JsonSerializer.Deserialize<ImportResult>(json, VouchaApiJson.Options)!;
    Assert.Equal(expected, row.ErrorCode);
    Assert.Equal(ImportResultStatus.Error, row.Status);
    Assert.True(row.IsTerminalFailure());
    Assert.False(row.IsRetrying());
    var presentation = new ImportResultPresentation(row, UiLocalization.English);
    Assert.Equal(UiLocalization.English.Format(UiMessageKey.NativeSwiftImportExportFailed,
        ("error", UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed))), presentation.LocalizedDisplayStatus);
    Assert.DoesNotContain(code, presentation.LocalizedDisplayStatus, StringComparison.Ordinal);
  }

  [Fact]
  public void TopicSkippedRefusalDecodesWithoutClaimingSuccess()
  {
    const string json = "{\"results\":[{\"input\":\"Travel\",\"status\":\"skipped\",\"error_code\":\"RELATION_LIMIT_REACHED\"}]}";
    var response = JsonSerializer.Deserialize<TopicImportResponse>(json, VouchaApiJson.Options)!;
    var row = Assert.Single(response.Results);
    Assert.Equal(ImportResultStatus.Skipped, row.Status);
    Assert.Equal(RssFeedImportErrorCode.RelationLimitReached, row.ErrorCode);
    Assert.True(row.IsTerminalFailure());
  }

  [Theory]
  [InlineData(ImportResultStatus.Error)]
  [InlineData(ImportResultStatus.Skipped)]
  public void RelationRefusalsUseLocalizedFailureInsteadOfServerCopy(ImportResultStatus status)
  {
    var row = new ImportResult("row", "Input", status, "RELATION_LIMIT_REACHED", null, null,
        RssFeedImportErrorCode.RelationLimitReached);
    var presentation = new ImportResultPresentation(row, UiLocalization.English);
    Assert.Null(presentation.ExternalContentError);
    Assert.DoesNotContain("RELATION_LIMIT_REACHED", presentation.LocalizedDisplayStatus, StringComparison.Ordinal);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed),
        presentation.LocalizedDisplayStatus, StringComparison.Ordinal);
  }

  [Fact]
  public async Task TopicSkippedRowsArePartialFailuresAndNeverFollowed()
  {
    using var model = new ImportExportViewModel(ImportExportRouteContext.FromPath("/my/topics/import-export"), new OutcomeService());
    model.InputText = "Travel\nSkipped";
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasPartialFailures);
    Assert.Equal([ImportResultStatus.Followed, ImportResultStatus.Skipped], model.Results.Select(row => row.Status));
  }

  [Theory]
  [InlineData(0, 2, false)]
  [InlineData(1, 1, true)]
  public async Task RssSummaryUsesBatchCountsWithSkippedAlreadyIncludedInFailed(int completed, int failed, bool partial)
  {
    using var model = new ImportExportViewModel(ImportExportRouteContext.FromPath("/my/sources/import-export"),
        new OutcomeService(completed, failed));
    model.InputText = "https://example.test/feed";
    await model.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal(partial, model.HasPartialFailures);
    Assert.Equal(1, model.ProgressValue);
    Assert.Equal(1, model.Progress!.SkippedRows);
    Assert.Equal(failed, model.Progress.FailedRows);
    Assert.Single(model.Results); // A bounded page is not the batch summary.
    Assert.Equal(ImportResultStatus.Error, model.Results[0].Status);
    Assert.Equal(RssFeedImportErrorCode.RelationLimitReached, model.Results[0].ErrorCode);
    Assert.False(model.IsMonitoring);
    Assert.False(model.CanResume);
  }

  private sealed class OutcomeService(int completed = 1, int failed = 1) : IImportExportService
  {
    private static ImportResult Skipped => new("row-1", "Skipped", ImportResultStatus.Skipped, null, null, null,
        RssFeedImportErrorCode.RelationLimitReached);
    private RssFeedImportSummary Summary => new("batch", 2, completed, failed, 0,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, SkippedRows: 1);
    public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken cancellationToken) =>
        Task.FromResult(new TopicImportResponse([new(null, "Travel", ImportResultStatus.Followed, null, null, null), Skipped]));
    public Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new RssFeedImportSubmission(Summary, new("/status", UriKind.Relative)));
    public Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, string? after, CancellationToken cancellationToken) =>
        Task.FromResult(new RssFeedImportStatus(Summary, [Skipped with { Status = ImportResultStatus.Error }], new(null, false, null)));
    public Task<ExportDocument> ExportTopicsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ExportDocument> ExportSourcesAsync(string? feedType, SourceExportFormat format, CancellationToken cancellationToken) => throw new NotSupportedException();
  }
}
