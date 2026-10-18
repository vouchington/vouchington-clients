using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportExportSourceLimitTests
{
  [Theory]
  [InlineData(SourceImportFormat.Urls)]
  [InlineData(SourceImportFormat.Csv)]
  [InlineData(SourceImportFormat.Opml)]
  public async Task SubmitsEverySourceAboveTheTopicRowLimit(SourceImportFormat format)
  {
    var urls = Enumerable.Range(1, 501).Select(index => $"https://example.test/feed/{index}").ToArray();
    var input = format switch
    {
      SourceImportFormat.Urls => string.Join('\n', urls),
      SourceImportFormat.Csv => "url,title\n" + string.Join('\n', urls.Select(url => $"{url},Feed")),
      SourceImportFormat.Opml => "<opml><body>" + string.Concat(urls.Select(url => $"<outline xmlUrl=\"{url}\"/>")) + "</body></opml>",
      _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
    var service = new CapturingService();
    using var model = new ImportExportViewModel(
        ImportExportRouteContext.FromPath("/my/sources/import-export"), service);
    model.SourceImportFormat = format;
    model.InputText = input;

    await model.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Null(model.ErrorMessage);
    var request = Assert.IsType<SourceImportRequest>(service.Request);
    Assert.Equal(format, request.Format);
    if (format == SourceImportFormat.Urls) Assert.Equal(urls, request.Urls);
    else Assert.Equal(input, request.Text);
    Assert.Equal(1, service.Submissions);
  }

  [Theory]
  [InlineData(SourceImportFormat.Urls)]
  [InlineData(SourceImportFormat.Csv)]
  [InlineData(SourceImportFormat.Opml)]
  public void LargeSourceImportsStillRespectTheEncodedByteBound(SourceImportFormat format)
  {
    var url = "https://example.test/" + new string('x', ImportExportValidation.MaximumBodyBytes);
    var request = format switch
    {
      SourceImportFormat.Urls => new SourceImportRequest(format, [url]),
      SourceImportFormat.Csv => new SourceImportRequest(format, Text: $"url\n{url}"),
      SourceImportFormat.Opml => new SourceImportRequest(format, Text: $"<opml><outline xmlUrl=\"{url}\"/></opml>"),
      _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    var error = ImportExportValidation.Validate(request);

    Assert.Equal(UiMessageKey.NativeSwiftImportExportSourceFileTooLarge, error?.Key);
  }

  private sealed class CapturingService : IImportExportService
  {
    public SourceImportRequest? Request { get; private set; }
    public int Submissions { get; private set; }
    private static RssFeedImportSummary Summary => new(
        "70000000-0000-7000-8000-000000000001", 501, 501, 0, 0,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    public Task<RssFeedImportSubmission> SubmitSourcesAsync(SourceImportRequest request, CancellationToken token)
    {
      Request = request;
      Submissions++;
      return Task.FromResult(new RssFeedImportSubmission(Summary, new Uri("/status", UriKind.Relative)));
    }

    public Task<RssFeedImportStatus> GetSourceStatusAsync(string importId, string? after, CancellationToken token) =>
        Task.FromResult(new RssFeedImportStatus(Summary, []));

    public Task<TopicImportResponse> ImportTopicsAsync(IReadOnlyList<string> names, CancellationToken token) =>
        throw new NotSupportedException();

    public Task<ExportDocument> ExportTopicsAsync(CancellationToken token) => throw new NotSupportedException();

    public Task<ExportDocument> ExportSourcesAsync(string? feedType, SourceExportFormat format, CancellationToken token) =>
        throw new NotSupportedException();
  }
}
