using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class ReportIntegrityDetailTests
{
  [Fact]
  public async Task DetailReadsReporterIdsWithoutReplacingSummaryQueue()
  {
    var summary = JsonSerializer.Deserialize<ReportIntegrityFlagsResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.default"), VouchaApiJson.Options)!;
    var detail = JsonSerializer.Deserialize<ReportIntegrityFlagResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.flag"), VouchaApiJson.Options)!;
    var reads = new List<string>();
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(summary),
      FetchReport = (id, _) =>
      {
        reads.Add(id);
        return Task.FromResult(detail);
      },
    };
    using var viewModel = new ReportIntegrityViewModel(service, new NavigationViewer(true, ["administrator"]));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Empty(reads);
    Assert.DoesNotContain(viewModel.Items, row => row.Evidence.Contains("reporter_user_ids", StringComparison.Ordinal));

    var opened = await viewModel.FetchDetailAsync(detail.Flag.Id, TestContext.Current.CancellationToken);

    Assert.Equal([detail.Flag.Id], reads);
    Assert.Contains("reporter-1", opened.Evidence);
    Assert.Contains("reporter-2", opened.Evidence);
    Assert.DoesNotContain(viewModel.Items, row => row.Evidence.Contains("reporter_user_ids", StringComparison.Ordinal));
  }

  [Fact]
  public async Task UnauthorizedDetailDoesNotReadService()
  {
    using var viewModel = new ReportIntegrityViewModel(new ModerationIntegrityTestService(),
        new NavigationViewer(true, []));
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        viewModel.FetchDetailAsync("flag-1", TestContext.Current.CancellationToken));
  }
}
