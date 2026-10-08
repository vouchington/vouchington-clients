using System.Collections;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationIntegrityPageTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Fact]
  public async Task ReportPageRendersLoadingEvidenceEntityRouteAndActions()
  {
    var response = new TaskCompletionSource<ReportIntegrityFlagsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new PageService { Reports = (_, _, _) => response.Task };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    var page = Create(new ReportIntegrityPage(viewModel));

    var loading = page.ReloadAsync();
    await service.ReportStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(Find<ActivityIndicator>(page, "report-integrity-loading").IsRunning);
    response.SetResult(new ReportIntegrityFlagsResponse(
        [ReportFlag("report-1", userId: "user-1")],
        new PageInfo(null, false, "report-1")));
    await loading;

    var status = Find<Picker>(page, "report-integrity-status");
    Assert.Equal(["Pending", "Resolved", "All"], Assert.IsAssignableFrom<IEnumerable>(
        status.ItemsSource).Cast<string>());
    Assert.Contains(Descendants<Label>(page), label =>
        label.Text?.Contains("reporter_user_ids", StringComparison.Ordinal) == true);
    Assert.Equal(
        "/user/user-1",
        Find<Button>(page, "integrity-entity-user-1").CommandParameter);
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityPenalizeReportersAction),
        Find<Button>(page, "report-integrity-penalize-report-1").Text);
    Assert.True(Find<Button>(page, "report-integrity-dismiss-report-1").IsEnabled);
  }

  [Fact]
  public async Task ReportPageRendersAuthoritativePenaltyResultAndResolutionMetadata()
  {
    var pending = ReportFlag("report-1", userId: "user-1");
    var authoritative = ReportFlag(
        "report-1", userId: "user-1", resolution: "penalized",
        resolvedAt: DateTimeOffset.Parse("2026-06-02T12:00:00Z"),
        resolvedById: "admin-1");
    var service = new PageService
    {
      Reports = (_, _, _) => Task.FromResult(
          new ReportIntegrityFlagsResponse([pending], new PageInfo(null, false, pending.Id))),
      ReportPenalty = (_, _) => Task.FromResult(new ReportIntegrityPenaltyResponse(
          authoritative, 2, [new("penalty-1", "reporter-1")])),
    };
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);
    var page = Create(new ReportIntegrityPage(viewModel));
    await page.ReloadAsync();

    Find<Button>(page, "report-integrity-penalize-report-1").SendClicked();
    Assert.Equal(0, service.ReportPenaltyCalls);
    Find<Button>(page, "report-integrity-penalize-confirm-report-1").SendClicked();
    await WaitUntilAsync(() => service.ReportPenaltyCalls == 1 &&
        Descendants<Label>(page).Any(label => label.Text == UiLocalization.English.Format(
            UiMessageKey.NativeSwiftIntegrityPenalizedReporters, ("count", 2))));

    Assert.Contains(Descendants<Label>(page), label => label.Text == UiLocalization.English.Localize(
        UiMessageKey.NativeSwiftIntegrityPenalizedResolution));
    Assert.Contains(Descendants<Label>(page), label => label.Text == "admin-1");
    Assert.DoesNotContain(Descendants<Button>(page), button =>
        button.AutomationId?.StartsWith("report-integrity-dismiss-", StringComparison.Ordinal) == true);
  }

  [Fact]
  public async Task ReportPageRendersErrorAndEmptyStates()
  {
    var reportService = new PageService
    {
      Reports = (_, _, _) => Task.FromException<ReportIntegrityFlagsResponse>(
          new HttpRequestException("report queue unavailable")),
    };
    var reportPage = Create(new ReportIntegrityPage(
        new ReportIntegrityViewModel(reportService, Admin)));
    await reportPage.ReloadAsync();
    Assert.Contains(Descendants<Label>(reportPage), label =>
        label.Text == "report queue unavailable");

    var emptyService = new PageService
    {
      Reports = (_, _, _) => Task.FromResult(
          new ReportIntegrityFlagsResponse([], new PageInfo(null, false, null))),
    };
    var emptyPage = Create(new ReportIntegrityPage(
        new ReportIntegrityViewModel(emptyService, Admin)));
    await emptyPage.ReloadAsync();
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityNoFlagsMessage),
        Find<Label>(emptyPage, "report-integrity-empty").Text);
  }

  [Fact]
  public async Task VotePageRendersLoadingErrorAndEmptyStates()
  {
    var response = new TaskCompletionSource<VoteIntegrityFlagsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var loadingService = new PageService { Votes = (_, _, _) => response.Task };
    var loadingPage = Create(new VoteIntegrityPage(
        new VoteIntegrityViewModel(loadingService, Admin)));
    var loading = loadingPage.ReloadAsync();
    await loadingService.VoteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(Find<ActivityIndicator>(loadingPage, "vote-integrity-loading").IsRunning);
    response.SetResult(new VoteIntegrityFlagsResponse([], new PageInfo(null, false, null)));
    await loading;
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityNoFlagsMessage),
        Find<Label>(loadingPage, "vote-integrity-empty").Text);

    var errorService = new PageService
    {
      Votes = (_, _, _) => Task.FromException<VoteIntegrityFlagsResponse>(
          new HttpRequestException("vote queue unavailable")),
    };
    var errorPage = Create(new VoteIntegrityPage(
        new VoteIntegrityViewModel(errorService, Admin)));
    await errorPage.ReloadAsync();
    Assert.Contains(Descendants<Label>(errorPage), label =>
        label.Text == "vote queue unavailable");
  }

  [Fact]
  public async Task VotePageRendersEveryResolutionAndCountOnlyPenaltyKeepsFlagPending()
  {
    var pending = VoteFlag("vote-1", topicId: "topic-1");
    var service = new PageService
    {
      Votes = (_, _, _) => Task.FromResult(
          new VoteIntegrityFlagsResponse([pending], new PageInfo(null, false, pending.Id))),
      VotePenalty = (_, _) => Task.FromResult(new VoteIntegrityPenaltyApplicationResponse(pending, 3)),
      VoteResolution = (_, resolution, _) => Task.FromResult(new VoteIntegrityFlagResponse(
          VoteFlag(
              "vote-1", topicId: "topic-1",
              resolution: resolution.ToString().ToLowerInvariant(),
              resolvedAt: DateTimeOffset.UtcNow,
              resolvedById: "admin-2"))),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);
    var page = Create(new VoteIntegrityPage(viewModel));
    await page.ReloadAsync();

    Assert.Equal("/topic/topic-1", Find<Button>(page, "integrity-entity-topic-1").CommandParameter);
    Assert.True(Find<Button>(page, "vote-integrity-dismissed-vote-1").IsEnabled);
    Assert.True(Find<Button>(page, "vote-integrity-penalized-vote-1").IsEnabled);
    Assert.True(Find<Button>(page, "vote-integrity-suspended-vote-1").IsEnabled);
    Find<Button>(page, "vote-integrity-penalty-vote-1").SendClicked();
    Assert.Equal(0, service.VotePenaltyCalls);
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityConfirmAction),
        Find<Button>(page, "vote-integrity-penalty-confirm-vote-1").Text);
    Find<Button>(page, "vote-integrity-penalty-cancel-vote-1").SendClicked();
    Assert.Equal(0, service.VotePenaltyCalls);
    Assert.DoesNotContain(Descendants<Button>(page), button =>
        button.AutomationId == "vote-integrity-penalty-confirm-vote-1");
    Find<Button>(page, "vote-integrity-penalty-vote-1").SendClicked();
    Find<Button>(page, "vote-integrity-penalty-confirm-vote-1").SendClicked();
    await WaitUntilAsync(() => Descendants<Label>(page)
        .Any(label => label.Text == UiLocalization.English.Format(
            UiMessageKey.NativeSwiftIntegrityPenalizedUsers, ("count", 3))));
    Assert.True(Find<Button>(page, "vote-integrity-suspended-vote-1").IsEnabled);
    Assert.False(Find<Button>(page, "vote-integrity-penalty-vote-1").IsEnabled);

    Find<Button>(page, "vote-integrity-suspended-vote-1").SendClicked();
    await WaitUntilAsync(() => Descendants<Label>(page).Any(label =>
        label.Text == UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftIntegritySuspendedResolution)));
    Assert.DoesNotContain(Descendants<Button>(page), button =>
        button.AutomationId?.StartsWith("vote-integrity-dismissed-", StringComparison.Ordinal) == true);
  }

  [Fact]
  public async Task ReportPenaltyPageRendersAuditFieldsAndConfirmedRevocation()
  {
    var active = new ReportAbusePenalty(
        "penalty-1", "user-1", "mass_report_campaign", "flag-1", "admin-1",
        null, null, DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
    var revoked = active with
    {
      RevokedAt = DateTimeOffset.Parse("2026-06-02T12:00:00Z"),
      RevokedById = "admin-2",
    };
    var service = new PageService
    {
      ReportPenalties = (_, _, _) => Task.FromResult(new ReportIntegrityPenaltiesResponse(
          [active], new PageInfo(null, false, active.Id))),
      ReportRevoke = (_, _) => Task.FromResult(new ReportAbusePenaltyResponse(revoked)),
    };
    var page = Create(new ReportIntegrityPenaltiesPage(
        new ReportIntegrityPenaltyViewModel(service, Admin)));
    await page.ReloadAsync();

    Assert.Equal("/user/user-1", Find<Button>(page, "integrity-entity-user-1").CommandParameter);
    var revoke = Find<Button>(page, "report-integrity-penalties-revoke-penalty-1");
    revoke.SendClicked();
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityConfirmRevoke),
        Find<Button>(page, "report-integrity-penalties-revoke-penalty-1").Text);
    Find<Button>(page, "report-integrity-penalties-revoke-penalty-1").SendClicked();
    await WaitUntilAsync(() => !Descendants<Border>(page).Any(border =>
        border.AutomationId == "report-integrity-penalties-penalty-1"));
  }

  [Fact]
  public async Task UnknownResolutionRemainsVerbatim()
  {
    var resolved = ReportFlag(
        "report-unknown", resolution: "future_resolution",
        resolvedAt: DateTimeOffset.Parse("2026-06-02T12:00:00Z"));
    var service = new PageService
    {
      Reports = (_, _, _) => Task.FromResult(new ReportIntegrityFlagsResponse(
          [resolved], new PageInfo(null, false, resolved.Id))),
    };
    var page = Create(new ReportIntegrityPage(new ReportIntegrityViewModel(service, Admin)));

    await page.ReloadAsync();

    Assert.Contains(Descendants<Label>(page), label => label.Text == "future_resolution");
  }

  [Fact]
  public async Task LocaleRefreshRebuildsEveryStatusPickerAndPreservesSelection()
  {
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var localizationScope = UiCopy.PushLocalization(localization);
    var evidenceFlag = ReportFlag("report-evidence") with
    {
      Details = Details(
          """{"note":"quoted","ratio":12.5,"nested":{"value":3.25}}"""),
    };
    var service = new PageService
    {
      Reports = (_, _, _) => Task.FromResult(
          new ReportIntegrityFlagsResponse(
              [evidenceFlag], new PageInfo(null, false, evidenceFlag.Id))),
      Votes = (_, _, _) => Task.FromResult(
          new VoteIntegrityFlagsResponse([], new PageInfo(null, false, null))),
      ReportPenalties = (_, _, _) => Task.FromResult(
          new ReportIntegrityPenaltiesResponse([], new PageInfo(null, false, null))),
    };
    using var reports = new ReportIntegrityViewModel(service, Admin, localization, controller);
    using var votes = new VoteIntegrityViewModel(service, Admin, localization, controller);
    using var penalties = new ReportIntegrityPenaltyViewModel(service, Admin, localization, controller);
    var reportPage = Create(new ReportIntegrityPage(reports), localization);
    var votePage = Create(new VoteIntegrityPage(votes), localization);
    var penaltyPage = Create(new ReportIntegrityPenaltiesPage(penalties), localization);
    await reports.SelectStatusAsync(
        IntegrityFlagStatus.Resolved, TestContext.Current.CancellationToken);
    await votes.SelectStatusAsync(
        IntegrityFlagStatus.Resolved, TestContext.Current.CancellationToken);
    await penalties.SelectStatusAsync(
        IntegrityPenaltyStatus.Revoked, TestContext.Current.CancellationToken);

    AssertRenderedEvidence(reportPage, "12.5", "3.25");

    foreach (var locale in new[] { "es", "fr", "pt" })
    {
      controller.ApplySavedLocale(locale);
      AssertPicker(reportPage, "report-integrity-status", localization, true);
      AssertPicker(votePage, "vote-integrity-status", localization, true);
      AssertPicker(penaltyPage, "report-integrity-penalties-status", localization, false);
      AssertRenderedEvidence(reportPage, "12,5", "3,25");
    }
  }

  [Fact]
  public void LocalizationScopeRestoresAfterExceptionalExit()
  {
    UiCopy.UseLocalization(UiLocalization.English);
    using var controller = new UiLocaleController(new StubLanguageProvider("es"));
    Action exit = () =>
    {
      using var scope = UiCopy.PushLocalization(new UiLocalization(controller));
      throw new InvalidOperationException("expected test exit");
    };
    Assert.Throws<InvalidOperationException>(exit);
    Assert.Equal("Cancel", UiCopy.Localize(UiMessageKey.CommonCancel));
  }

  private static T Create<T>(T page, IUiLocalization? localization = null) where T : Page
  {
    localization ??= UiLocalization.English;
    UiCopy.UseLocalization(localization);
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      Application.Current!.Resources[key.Value] = localization.Localize(key);
    return page;
  }

  private static void AssertPicker(
      Page page, string id, IUiLocalization localization, bool flags)
  {
    var picker = Find<Picker>(page, id);
    var expected = flags
        ? new[]
        {
          localization.Localize(UiMessageKey.NativeSwiftIntegrityPending),
          localization.Localize(UiMessageKey.NativeSwiftIntegrityResolved),
          localization.Localize(UiMessageKey.NativeSwiftIntegrityAll),
        }
        : new[]
        {
          localization.Localize(UiMessageKey.NativeSwiftIntegrityActive),
          localization.Localize(UiMessageKey.NativeSwiftIntegrityRevoked),
          localization.Localize(UiMessageKey.NativeSwiftIntegrityAll),
        };
    Assert.Equal(expected, Assert.IsAssignableFrom<IEnumerable>(picker.ItemsSource).Cast<string>());
    Assert.Equal(1, picker.SelectedIndex);
  }

  private static void AssertRenderedEvidence(Page page, string ratio, string nested)
  {
    var evidence = Assert.Single(Descendants<Label>(page), label =>
        label.Text?.Contains("note: quoted", StringComparison.Ordinal) == true).Text!;
    Assert.DoesNotContain("\"quoted\"", evidence, StringComparison.Ordinal);
    Assert.Contains($"ratio: {ratio}", evidence, StringComparison.Ordinal);
    Assert.Contains($"nested: {{value: {nested}}}", evidence, StringComparison.Ordinal);
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++)
      await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }

  private static ReportIntegrityFlag ReportFlag(
      string id,
      string? userId = null,
      string? resolution = null,
      DateTimeOffset? resolvedAt = null,
      string? resolvedById = null) =>
      new(
          id, userId is null ? "post-1" : null, userId, null, null,
          "mass_report_suspected", 5, 0.6,
          Details("""{"reporter_user_ids":["reporter-1"],"window_minutes":30}"""),
          resolvedAt, resolvedById, resolution, DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

  private static VoteIntegrityFlag VoteFlag(
      string id,
      string? topicId = null,
      string? resolution = null,
      DateTimeOffset? resolvedAt = null,
      string? resolvedById = null) =>
      new(
          id, topicId is null ? "post-1" : null, topicId, null, null, null, null,
          "velocity_spike", Details("""{"vote_count":20}"""),
          resolvedAt, resolvedById, resolution, DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

  private static IReadOnlyDictionary<string, JsonElement> Details(string json) =>
      JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ??
          throw new InvalidOperationException("Test details did not decode.");

  private sealed class PageService : IModerationIntegrityService
  {
    public Func<IntegrityFlagStatus, string?, CancellationToken, Task<ReportIntegrityFlagsResponse>>
        Reports { get; init; } = (_, _, _) => throw new InvalidOperationException();
    public Func<IntegrityFlagStatus, string?, CancellationToken, Task<VoteIntegrityFlagsResponse>>
        Votes { get; init; } = (_, _, _) => throw new InvalidOperationException();
    public Func<string, CancellationToken, Task<ReportIntegrityPenaltyResponse>>
        ReportPenalty { get; init; } = (_, _) => throw new InvalidOperationException();
    public Func<string, VoteIntegrityResolution, CancellationToken, Task<VoteIntegrityFlagResponse>>
        VoteResolution { get; init; } = (_, _, _) => throw new InvalidOperationException();
    public Func<string, CancellationToken, Task<VoteIntegrityPenaltyApplicationResponse>>
        VotePenalty { get; init; } = (_, _) => throw new InvalidOperationException();
    public Func<IntegrityPenaltyStatus, string?, CancellationToken, Task<ReportIntegrityPenaltiesResponse>>
        ReportPenalties { get; init; } = (_, _, _) => throw new InvalidOperationException();
    public Func<string, CancellationToken, Task<ReportAbusePenaltyResponse>>
        ReportRevoke { get; init; } = (_, _) => throw new InvalidOperationException();
    public TaskCompletionSource ReportStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource VoteStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int ReportPenaltyCalls { get; private set; }
    public int VotePenaltyCalls { get; private set; }

    public Task<ReportIntegrityFlagsResponse> FetchReportFlagsAsync(
        IntegrityFlagStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default)
    {
      ReportStarted.TrySetResult();
      return Reports(status, after, cancellationToken);
    }
    public Task<ReportIntegrityFlagResponse> DismissReportFlagAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
    public Task<ReportIntegrityFlagResponse> FetchReportFlagAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
    public Task<ReportIntegrityPenaltyResponse> PenalizeReportersAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      ReportPenaltyCalls++;
      return ReportPenalty(flagId, cancellationToken);
    }
    public Task<VoteIntegrityFlagsResponse> FetchVoteFlagsAsync(
        IntegrityFlagStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default)
    {
      VoteStarted.TrySetResult();
      return Votes(status, after, cancellationToken);
    }
    public Task<VoteIntegrityFlagResponse> ResolveVoteFlagAsync(
        string flagId, VoteIntegrityResolution resolution,
        CancellationToken cancellationToken = default) =>
        VoteResolution(flagId, resolution, cancellationToken);
    public Task<VoteIntegrityFlagResponse> FetchVoteFlagAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
    public Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteRingPenaltyAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      VotePenaltyCalls++;
      return VotePenalty(flagId, cancellationToken);
    }
    public Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltiesAsync(
        IntegrityPenaltyStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default) =>
        ReportPenalties(status, after, cancellationToken);
    public Task<ReportAbusePenaltyResponse> FetchReportPenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
    public Task<ReportAbusePenaltyResponse> RevokeReportPenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        ReportRevoke(penaltyId, cancellationToken);
    public Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltiesAsync(
        IntegrityPenaltyStatus status, string? after = null, int limit = 25,
        string? sourceFlagId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VoteIntegrityPenaltiesResponse(
            [], new PageInfo(null, false, null), new("flag", sourceFlagId)));
    public Task<VoteWeightPenaltyResponse> FetchVotePenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
    public Task<VoteWeightPenaltyResponse> RevokeVotePenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class StubLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
