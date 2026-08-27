using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class ReportIntegrityViewModelTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Fact]
  public async Task FiltersRenderActualEvidenceAndCanonicalEntityTargets()
  {
    var pending = ModerationIntegrityTestService.ReportFlag("pending", postId: "post-9");
    var resolved = ModerationIntegrityTestService.ReportFlag(
        "resolved", "dismissed", postId: null, userId: "user-8",
        resolvedAt: DateTimeOffset.Parse("2026-06-02T12:00:00Z"),
        resolvedById: "admin-4");
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (status, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage(
              status == IntegrityFlagStatus.Pending ? [pending] : [resolved])),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Null(viewModel.Items.Single().Entity.Route);
    Assert.Equal("Post ID post-9", UiLocalization.English.Resolve(viewModel.Items.Single().Entity.Label));
    Assert.Contains("reporter_user_ids", viewModel.Items.Single().Evidence);
    Assert.Contains("window_minutes: 30", viewModel.Items.Single().Evidence);

    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.Resolved, TestContext.Current.CancellationToken);
    Assert.Equal("/user/user-8", viewModel.Items.Single().Entity.Route);
    Assert.Equal("dismissed", viewModel.Items.Single().Flag.Resolution);
    Assert.Equal("admin-4", viewModel.Items.Single().Flag.ResolvedById);

    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);
    Assert.Equal(
        [IntegrityFlagStatus.Pending, IntegrityFlagStatus.Resolved, IntegrityFlagStatus.All],
        service.ReportFetches.Select(request => request.Status));
  }

  [Fact]
  public async Task RemainingEntityKindsStayCanonicalOrIdOnly()
  {
    var hostname = ModerationIntegrityTestService.ReportFlag(
        "domain-flag", postId: null, hostnameId: "domain-4");
    var rss = ModerationIntegrityTestService.ReportFlag("rss-flag", postId: null) with
    {
      RssFeedItemId = "rss-3",
    };
    var fallback = ModerationIntegrityTestService.ReportFlag("flag-only", postId: null);
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage([hostname, rss, fallback])),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/domain/domain-4", viewModel.Items[0].Entity.Route);
    Assert.Equal("RSS item ID rss-3", UiLocalization.English.Resolve(viewModel.Items[1].Entity.Label));
    Assert.Null(viewModel.Items[1].Entity.Route);
    Assert.Equal("Flag ID", UiLocalization.English.Resolve(viewModel.Items[2].Entity.Label));
  }

  [Fact]
  public async Task CursorPaginationDeduplicatesAndAllowsOneContinuation()
  {
    var continuation = new TaskCompletionSource<ReportIntegrityFlagsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, after, _) => after is null
          ? Task.FromResult(ModerationIntegrityTestService.ReportPage(
              [ModerationIntegrityTestService.ReportFlag("flag-1")], "cursor-1", true))
          : continuation.Task,
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var first = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    var duplicate = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.ReportFetches.Count);
    continuation.SetResult(ModerationIntegrityTestService.ReportPage(
        [
          ModerationIntegrityTestService.ReportFlag("flag-1"),
          ModerationIntegrityTestService.ReportFlag("flag-2"),
        ]));
    await Task.WhenAll(first, duplicate);

    Assert.Equal(["flag-1", "flag-2"], viewModel.Items.Select(row => row.Flag.Id));
    Assert.False(viewModel.HasMore);
    Assert.False(viewModel.IsLoadingMore);
  }

  [Fact]
  public async Task ContinuationFailurePreservesRowsAndCanRetry()
  {
    var fail = true;
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, after, _) => after is null
          ? Task.FromResult(ModerationIntegrityTestService.ReportPage(
              [ModerationIntegrityTestService.ReportFlag("flag-1")], "cursor-1", true))
          : fail
              ? Task.FromException<ReportIntegrityFlagsResponse>(
                  new HttpRequestException("continuation failed"))
              : Task.FromResult(ModerationIntegrityTestService.ReportPage(
                  [ModerationIntegrityTestService.ReportFlag("flag-2")])),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("flag-1", viewModel.Items.Single().Flag.Id);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("continuation failed", viewModel.ErrorMessage);

    fail = false;
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["flag-1", "flag-2"], viewModel.Items.Select(row => row.Flag.Id));
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task CancellationReturnsToStableInitialAndContinuationStates()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var initialService = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, token) =>
          Task.FromCanceled<ReportIntegrityFlagsResponse>(token),
    };
    var initial = new ReportIntegrityViewModel(initialService, Admin);

    await initial.LoadAsync(cancellation.Token);
    Assert.Equal(LoadState.Idle, initial.State);
    Assert.Empty(initial.Items);

    var continuationService = new ModerationIntegrityTestService
    {
      FetchReports = (_, after, token) => after is null
          ? Task.FromResult(ModerationIntegrityTestService.ReportPage(
              [ModerationIntegrityTestService.ReportFlag("flag-1")], "cursor-1", true))
          : Task.FromCanceled<ReportIntegrityFlagsResponse>(token),
    };
    var continuation = new ReportIntegrityViewModel(continuationService, Admin);
    await continuation.LoadAsync(TestContext.Current.CancellationToken);
    await continuation.LoadMoreAsync(cancellation.Token);

    Assert.Equal(LoadState.Loaded, continuation.State);
    Assert.Equal("flag-1", continuation.Items.Single().Flag.Id);
    Assert.True(continuation.CanLoadMore);
  }

  [Fact]
  public async Task StatusChangeRejectsStaleResponse()
  {
    var pending = new TaskCompletionSource<ReportIntegrityFlagsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (status, _, _) => status == IntegrityFlagStatus.Pending
          ? pending.Task
          : Task.FromResult(ModerationIntegrityTestService.ReportPage(
              [ModerationIntegrityTestService.ReportFlag(
                  "resolved", "dismissed", resolvedAt: DateTimeOffset.UtcNow)])),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);

    var oldLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.Resolved, TestContext.Current.CancellationToken);
    pending.SetResult(ModerationIntegrityTestService.ReportPage(
        [ModerationIntegrityTestService.ReportFlag("stale")]));
    await oldLoad;

    Assert.Equal("resolved", viewModel.Items.Single().Flag.Id);
  }

  [Theory]
  [InlineData(false, null)]
  [InlineData(true, "moderator")]
  [InlineData(true, "customer_support")]
  [InlineData(true, "member")]
  public async Task NonAdministratorsCannotLoadOrAct(bool authenticated, string? role)
  {
    var service = new ModerationIntegrityTestService();
    var roles = role is null ? Array.Empty<string>() : new[] { role };
    var viewModel = new ReportIntegrityViewModel(
        service, new NavigationViewer(authenticated, roles));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.DismissAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.PenalizeReportersAsync("flag-1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAuthorized);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityAdministratorMessage),
        viewModel.ErrorMessage);
    Assert.Empty(service.ReportFetches);
    Assert.Equal(0, service.ReportDismissals);
    Assert.Equal(0, service.ReportPenalties);
  }

  [Fact]
  public async Task ActionsAreMutuallyExclusiveAndUseAuthoritativeReturnedFlag()
  {
    var dismissal = new TaskCompletionSource<ReportIntegrityFlagResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var pending = ModerationIntegrityTestService.ReportFlag("flag-1");
    var authoritative = ModerationIntegrityTestService.ReportFlag(
        "flag-1", "dismissed", resolvedAt: DateTimeOffset.UtcNow,
        resolvedById: "admin-authoritative");
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage([pending])),
      DismissReport = (_, _) => dismissal.Task,
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);

    var dismiss = viewModel.DismissAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.PenalizeReportersAsync("flag-1", TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsActionInFlight("flag-1"));
    Assert.Equal(0, service.ReportPenalties);
    dismissal.SetResult(new ReportIntegrityFlagResponse(authoritative));
    await dismiss;

    Assert.Equal("dismissed", viewModel.Items.Single().Flag.Resolution);
    Assert.Equal("admin-authoritative", viewModel.Items.Single().Flag.ResolvedById);
    Assert.False(viewModel.CanDismiss("flag-1"));
  }

  [Theory]
  [InlineData(IntegrityFlagStatus.Pending, false)]
  [InlineData(IntegrityFlagStatus.Resolved, true)]
  [InlineData(IntegrityFlagStatus.All, true)]
  public async Task AuthoritativeResolutionEvictsOnlyThePendingRowAndPreservesOrder(
      IntegrityFlagStatus status,
      bool retainsResolved)
  {
    var flags = new[]
    {
      ModerationIntegrityTestService.ReportFlag("before"),
      ModerationIntegrityTestService.ReportFlag("target"),
      ModerationIntegrityTestService.ReportFlag("after"),
    };
    var authoritative = flags[1] with
    {
      Resolution = "dismissed",
      ResolvedAt = DateTimeOffset.UtcNow,
    };
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage(flags, "cursor-1", true)),
      DismissReport = (_, _) => Task.FromResult(new ReportIntegrityFlagResponse(authoritative)),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);

    if (status == IntegrityFlagStatus.Pending)
      await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    else
      await viewModel.SelectStatusAsync(status, TestContext.Current.CancellationToken);
    await viewModel.DismissAsync("target", TestContext.Current.CancellationToken);

    Assert.Equal(
        retainsResolved ? ["before", "target", "after"] : ["before", "after"],
        viewModel.Items.Select(row => row.Flag.Id));
    Assert.True(viewModel.HasMore);
    if (retainsResolved) Assert.Equal("dismissed", viewModel.Items[1].Flag.Resolution);
  }

  [Fact]
  public async Task FailedPenaltyCanRetryAndConfirmedResponseReplacesFlag()
  {
    var attempts = 0;
    var authoritative = ModerationIntegrityTestService.ReportFlag(
        "flag-1", "penalized", resolvedAt: DateTimeOffset.UtcNow,
        resolvedById: "admin-1");
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage(
              [ModerationIntegrityTestService.ReportFlag("flag-1")])),
      PenalizeReporters = (_, _) =>
      {
        attempts++;
        return attempts == 1
            ? Task.FromException<ReportIntegrityPenaltyResponse>(
                new VouchaApiException("penalty failed", null, HttpStatusCode.BadRequest))
            : Task.FromResult(new ReportIntegrityPenaltyResponse(
                authoritative, 2, [new("penalty-1", "reporter-1")]));
      },
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);

    await viewModel.PenalizeReportersAsync("flag-1", TestContext.Current.CancellationToken);
    Assert.Equal("penalty failed", viewModel.ActionError("flag-1"));
    Assert.True(viewModel.CanPenalizeReporters("flag-1"));
    await viewModel.PenalizeReportersAsync("flag-1", TestContext.Current.CancellationToken);

    Assert.Equal("penalized", viewModel.Items.Single().Flag.Resolution);
    Assert.Equal(2, viewModel.PenalizedUserCount("flag-1"));
    Assert.Null(viewModel.ActionError("flag-1"));
  }

  [Fact]
  public async Task CanceledMutationReconcilesTheExactFlagBeforeUnlocking()
  {
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage(
              [ModerationIntegrityTestService.ReportFlag("flag-1")])),
      DismissReport = (_, token) => Task.FromCanceled<ReportIntegrityFlagResponse>(token),
      FetchReport = (_, _) => Task.FromResult(new ReportIntegrityFlagResponse(
          ModerationIntegrityTestService.ReportFlag(
              "flag-1", "dismissed", resolvedAt: DateTimeOffset.UtcNow))),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await viewModel.DismissAsync("flag-1", cancellation.Token);

    Assert.False(viewModel.IsActionInFlight("flag-1"));
    Assert.False(viewModel.NeedsReconciliation("flag-1"));
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task FailedExactReloadCanReconcileByGetWithoutRepeatingDismissal()
  {
    var reloads = 0;
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.ReportPage(
          [ModerationIntegrityTestService.ReportFlag("flag-1")])),
      DismissReport = (_, _) => Task.FromException<ReportIntegrityFlagResponse>(
          new System.Text.Json.JsonException("invalid success body")),
      FetchReport = (_, _) => ++reloads == 1
          ? Task.FromException<ReportIntegrityFlagResponse>(
              new HttpRequestException("reload failed"))
          : Task.FromResult(new ReportIntegrityFlagResponse(
              ModerationIntegrityTestService.ReportFlag(
                  "flag-1", "dismissed", resolvedAt: DateTimeOffset.UtcNow))),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.DismissAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.DismissAsync("flag-1", TestContext.Current.CancellationToken);

    Assert.Equal(1, service.ReportDismissals);
    Assert.True(viewModel.NeedsReconciliation("flag-1"));
    Assert.False(viewModel.CanDismiss("flag-1"));

    await viewModel.ReconcileAsync("flag-1");

    Assert.Equal(2, reloads);
    Assert.Equal(1, service.ReportDismissals);
    Assert.False(viewModel.NeedsReconciliation("flag-1"));
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task ReviewResolveAndApplyCapabilitiesHaveSeparateGates()
  {
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.ReportPage(
          [ModerationIntegrityTestService.ReportFlag("flag-1")])),
    };
    var actions = new ReportIntegrityViewModel(service, Admin, capabilities:
        new IntegrityCapabilities(true, true, false, true, true));
    await actions.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(actions.CanDismiss("flag-1"));
    Assert.False(actions.CanPenalizeReporters("flag-1"));

    var noReview = new ReportIntegrityViewModel(service, Admin, capabilities:
        new IntegrityCapabilities(false, true, true, true, true));
    await noReview.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(noReview.IsAuthorized);
    Assert.Equal(LoadState.Error, noReview.State);
  }

  [Fact]
  public async Task EvidenceRecursivelyFormatsValuesAndRefreshesWithLocale()
  {
    var details = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
        """{"note":"quoted","ratio":12.5,"nested":{"value":3.25},"items":["x",2.5],"ok":true,"none":null}""")!;
    var flag = ModerationIntegrityTestService.ReportFlag("flag-1") with { Details = details };
    var service = new ModerationIntegrityTestService
    {
      FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage([flag])),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var viewModel = new ReportIntegrityViewModel(
        service, Admin, new UiLocalization(controller), controller);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    AssertEvidence(viewModel.Items.Single().Evidence, "12.5", "3.25", "2.5");
    foreach (var locale in new[] { "es", "fr", "pt" })
    {
      controller.ApplySavedLocale(locale);
      AssertEvidence(viewModel.Items.Single().Evidence, "12,5", "3,25", "2,5");
    }
  }

  private static void AssertEvidence(
      string evidence, string ratio, string nested, string arrayNumber)
  {
    Assert.Contains("note: quoted", evidence, StringComparison.Ordinal);
    Assert.DoesNotContain("\"quoted\"", evidence, StringComparison.Ordinal);
    Assert.Contains($"ratio: {ratio}", evidence, StringComparison.Ordinal);
    Assert.Contains($"nested: {{value: {nested}}}", evidence, StringComparison.Ordinal);
    Assert.Contains($"items: [x, {arrayNumber}]", evidence, StringComparison.Ordinal);
    Assert.Contains("ok: true", evidence, StringComparison.Ordinal);
    Assert.Contains("none: null", evidence, StringComparison.Ordinal);
  }

  private sealed class StubLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
