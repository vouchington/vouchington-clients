using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.ReferralLinks;

public sealed partial class ReferralLinksViewModelTests
{
  [Fact]
  public async Task FetchValidationInfoAsyncReturnsReferralProgramRules()
  {
    var service = new RecordingReferralLinksService
    {
      ValidationInfoResponse = new ReferralProgramValidationInfoResponse(
          new ReferralProgramValidationInfo(
              "Paste an application URL.",
              ["https://example.com/apply"])),
    };
    var viewModel = new ReferralLinksViewModel(service);

    var validationInfo = await viewModel.FetchValidationInfoAsync(
        "program-1",
        TestContext.Current.CancellationToken);

    Assert.Equal("program-1", service.LastValidationInfoId);
    Assert.Equal("Paste an application URL.", validationInfo.UserHelpText);
    Assert.Equal("https://example.com/apply", validationInfo.ExampleUrls[0]);
  }

  [Fact]
  public async Task ManagementMutationsRefreshMineRows()
  {
    var service = new RecordingReferralLinksService
    {
      MineResponse = new ReferralLinksResponse([], new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.CreateAsync(
        "program-1",
        new Uri("https://example.com/referrals"),
        "  ",
        TestContext.Current.CancellationToken);
    Assert.NotNull(service.CreatedBody);
    Assert.Equal("program-1", service.CreatedBody.ReferralProgramId);
    Assert.Null(service.CreatedBody.Label);

    await viewModel.RenameAsync("link-1", "Renamed", TestContext.Current.CancellationToken);
    Assert.Equal("link-1", service.LastUpdatedId);
    Assert.Equal("Renamed", service.UpdatedBody?.Label);

    await viewModel.DeleteAsync("link-1", TestContext.Current.CancellationToken);
    Assert.Equal("link-1", service.LastDeletedId);

    await viewModel.SetActiveAsync("link-1", active: true, TestContext.Current.CancellationToken);
    Assert.Equal("link-1", service.LastActivatedId);

    await viewModel.SetActiveAsync("link-1", active: false, TestContext.Current.CancellationToken);

    Assert.Equal("link-1", service.LastDeactivatedId);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task LoadMineAsyncGroupsRowsByProgramName()
  {
    var now = DateTimeOffset.UtcNow;
    var service = new RecordingReferralLinksService
    {
      MineResponse = new ReferralLinksResponse(
          [
            new ReferralLink(
                "link-z",
                "user-1",
                "program-z",
                "url-z",
                "https://example.com/z",
                "Zeta link",
                null,
                null,
                now,
                now,
                "Zeta Program",
                "zeta-program"),
            new ReferralLink(
                "link-a",
                "user-1",
                "program-a",
                "url-a",
                "https://example.com/a",
                "Alpha link",
                null,
                null,
                now,
                now,
                "Alpha Program",
                "alpha-program"),
            new ReferralLink(
                "link-a2",
                "user-1",
                "program-a",
                "url-a2",
                "https://example.com/a2",
                "Alpha second",
                null,
                null,
                now,
                now,
                "Alpha Program",
                "alpha-program"),
          ],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["link-a", "link-a2", "link-z"], viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task LoadMoreMineAsyncUsesStoredCursorAndAppendsRows()
  {
    var service = new RecordingReferralLinksService
    {
      MineResponses =
      [
        MineResponse("link-1", "https://example.com/one", new PageInfo("cursor-1", true, null)),
        MineResponse("link-2", "https://example.com/two", new PageInfo(null, false, null)),
      ],
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMoreLinks);

    await viewModel.LoadMoreMineAsync(TestContext.Current.CancellationToken);

    Assert.Equal("cursor-1", service.LastMineRequest?.After);
    Assert.False(viewModel.HasMoreLinks);
    Assert.Equal(["link-1", "link-2"], viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task FirstPageAnalyticsErrorsResetPagination()
  {
    var service = new RecordingReferralLinksService
    {
      ClickResponses =
      [
        ClickResponse("click-1", "https://example.com/one", new PageInfo("cursor-1", true, null)),
      ],
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMoreAnalytics);

    service.ThrowClicks = true;
    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.False(viewModel.HasMoreAnalytics);
    Assert.False(viewModel.HasMoreLinks);
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task StaleAnalyticsCompletionDoesNotRestoreAnalyticsCursor()
  {
    var deferredClicks = new TaskCompletionSource<ReferralClickLogResponse>();
    var service = new RecordingReferralLinksService
    {
      DeferredClickResponse = deferredClicks,
      MineResponse = MineResponse("link-1", "https://example.com/ref", new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    var analyticsTask = viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);
    await Task.Yield();
    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);

    deferredClicks.SetResult(ClickResponse(
        "click-1",
        "https://example.com/click",
        new PageInfo("stale-cursor", true, null)));
    await analyticsTask;

    Assert.False(viewModel.HasMoreAnalytics);
    Assert.Equal(["link-1"], viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task LoadMoreAnalyticsAsyncIsIgnoredDuringModeReload()
  {
    var deferredMine = new TaskCompletionSource<ReferralLinksResponse>();
    var service = new RecordingReferralLinksService
    {
      ClickResponses =
      [
        ClickResponse("click-1", "https://example.com/one", new PageInfo("cursor-1", true, null)),
        ClickResponse("click-2", "https://example.com/two", new PageInfo(null, false, null)),
      ],
      DeferredMineResponse = deferredMine,
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMoreAnalytics);

    var mineTask = viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    await Task.Yield();
    await viewModel.LoadMoreAnalyticsAsync(TestContext.Current.CancellationToken);
    deferredMine.SetResult(MineResponse("link-1", "https://example.com/ref", new PageInfo(null, false, null)));
    await mineTask;

    Assert.Null(service.LastClickRequest?.After);
    Assert.False(viewModel.HasMoreAnalytics);
    Assert.Equal(["link-1"], viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task ManagementMutationsHandleCancellationAndErrors()
  {
    var service = new RecordingReferralLinksService
    {
      MineResponse = new ReferralLinksResponse([], new PageInfo(null, false, null)),
      CancelMutation = true,
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.DeleteAsync("link-1", TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Idle, viewModel.State);

    service.CancelMutation = false;
    service.ThrowMutation = true;
    await viewModel.DeleteAsync("link-1", TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task ManagementMutationErrorsPreserveLoadedRows()
  {
    var service = new RecordingReferralLinksService
    {
      MineResponse = new ReferralLinksResponse(
          [new ReferralLink(
              "link-1",
              "user-1",
              "program-1",
              "url-1",
              "https://example.com/ref",
              "Current",
              DateTimeOffset.UtcNow,
              null,
              DateTimeOffset.UtcNow,
              DateTimeOffset.UtcNow,
              "Program",
              "program")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    service.ThrowMutation = true;
    await viewModel.DeleteAsync("link-1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Collection(viewModel.Items, item => Assert.Equal("link-1", item.Id));
  }

  [Fact]
  public async Task ManagementMutationErrorsUseApiResponseText()
  {
    var service = new RecordingReferralLinksService
    {
      MineResponse = new ReferralLinksResponse(
          [new ReferralLink(
              "link-1",
              "user-1",
              "program-1",
              "url-1",
              "https://example.com/ref",
              "Current",
              DateTimeOffset.UtcNow,
              null,
              DateTimeOffset.UtcNow,
              DateTimeOffset.UtcNow,
              "Program",
              "program")],
          new PageInfo(null, false, null)),
      MutationException = new VouchaApiException(
          HttpStatusCode.UnprocessableEntity,
          """{"user_error_text":"Use your referral URL from the partner dashboard."}"""),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(
        "program-1",
        new Uri("https://example.com/ref"),
        null,
        TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("Use your referral URL from the partner dashboard.", viewModel.ErrorMessage);
    Assert.Collection(viewModel.Items, item => Assert.Equal("link-1", item.Id));
  }
}
