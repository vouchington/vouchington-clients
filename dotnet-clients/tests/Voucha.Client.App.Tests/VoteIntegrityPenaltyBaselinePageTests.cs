using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class VoteIntegrityPenaltyBaselinePageTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Theory]
  [InlineData(BaselineScenario.NoNewRow, true)]
  [InlineData(BaselineScenario.NewCommittedRow, false)]
  public async Task ReconciliationComparesPenaltyRowsCreatedAfterBaseline(
      BaselineScenario scenario,
      bool permitsRetry)
  {
    var service = new BaselineService(scenario);
    var page = Create(new VoteIntegrityPage(new VoteIntegrityViewModel(service, Admin)));
    await page.ReloadAsync();

    ConfirmPenalty(page);
    await WaitUntilAsync(() => service.PostCalls == 1 &&
        Descendants<Button>(page).Any(button =>
            button.AutomationId == "vote-integrity-reconcile-flag-1"));
    Find<Button>(page, "vote-integrity-reconcile-flag-1").SendClicked();
    await WaitUntilAsync(() => !Descendants<Button>(page).Any(button =>
        button.AutomationId == "vote-integrity-reconcile-flag-1"));

    Assert.Equal(permitsRetry ? 1 : 0, service.FlagGets);
    Assert.Equal(permitsRetry, Find<Button>(page, "vote-integrity-penalty-flag-1").IsEnabled);
    if (permitsRetry)
    {
      ConfirmPenalty(page);
      await WaitUntilAsync(() => service.PostCalls == 2);
    }
    else
    {
      Find<Button>(page, "vote-integrity-penalty-flag-1").SendClicked();
      Assert.Equal(1, service.PostCalls);
    }
  }

  [Fact]
  public async Task ConcurrentlyResolvedFlagIsRemovedAfterNoNewPenalty()
  {
    var service = new BaselineService(BaselineScenario.ConcurrentlyResolved);
    var page = Create(new VoteIntegrityPage(new VoteIntegrityViewModel(service, Admin)));
    await page.ReloadAsync();

    ConfirmPenalty(page);
    await WaitUntilAsync(() => Descendants<Button>(page).Any(button =>
        button.AutomationId == "vote-integrity-reconcile-flag-1"));
    Find<Button>(page, "vote-integrity-reconcile-flag-1").SendClicked();
    await WaitUntilAsync(() => !Descendants<Button>(page).Any(button =>
        button.AutomationId == "vote-integrity-penalty-flag-1"));

    Assert.Equal(1, service.PostCalls);
    Assert.Equal(1, service.FlagGets);
  }

  [Fact]
  public async Task FlagGetFailureKeepsRenderedActionReconcilable()
  {
    var service = new BaselineService(BaselineScenario.FlagGetFailure);
    var page = Create(new VoteIntegrityPage(new VoteIntegrityViewModel(service, Admin)));
    await page.ReloadAsync();

    ConfirmPenalty(page);
    await WaitUntilAsync(() => Descendants<Button>(page).Any(button =>
        button.AutomationId == "vote-integrity-reconcile-flag-1"));
    Find<Button>(page, "vote-integrity-reconcile-flag-1").SendClicked();
    await WaitUntilAsync(() => service.FlagGets == 1 &&
        Find<Button>(page, "vote-integrity-reconcile-flag-1").IsEnabled);

    Assert.Equal(1, service.PostCalls);
    Assert.True(Find<Button>(page, "vote-integrity-reconcile-flag-1").IsEnabled);
    Assert.False(Find<Button>(page, "vote-integrity-penalty-flag-1").IsEnabled);
  }

  [Theory]
  [InlineData(BaselineScenario.Failure)]
  [InlineData(BaselineScenario.ScopeMismatch)]
  public async Task InvalidBaselineFailsClosedBeforePost(BaselineScenario scenario)
  {
    var service = new BaselineService(scenario);
    var page = Create(new VoteIntegrityPage(new VoteIntegrityViewModel(service, Admin)));
    await page.ReloadAsync();

    ConfirmPenalty(page);
    await WaitUntilAsync(() => Descendants<Label>(page).Any(label =>
        label.Text?.Contains("baseline", StringComparison.OrdinalIgnoreCase) == true ||
        label.Text?.Contains("scope", StringComparison.OrdinalIgnoreCase) == true));

    Assert.Equal(0, service.PostCalls);
    Assert.True(Find<Button>(page, "vote-integrity-penalty-flag-1").IsEnabled);
    Assert.DoesNotContain(Descendants<Button>(page), button =>
        button.AutomationId == "vote-integrity-reconcile-flag-1");
  }

  private static void ConfirmPenalty(Page page)
  {
    Find<Button>(page, "vote-integrity-penalty-flag-1").SendClicked();
    Find<Button>(page, "vote-integrity-penalty-confirm-flag-1").SendClicked();
  }

  private static T Create<T>(T page) where T : Page
  {
    UiCopy.UseLocalization(UiLocalization.English);
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
      Application.Current!.Resources[key.Value] = UiLocalization.English.Localize(key);
    return page;
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

  public enum BaselineScenario
  {
    NoNewRow,
    NewCommittedRow,
    Failure,
    ScopeMismatch,
    ConcurrentlyResolved,
    FlagGetFailure,
  }

  private sealed class BaselineService(BaselineScenario scenario) : IModerationIntegrityService
  {
    private readonly VoteIntegrityFlag flag = Flag();
    private readonly VoteWeightPenalty historical = Penalty("old-revoked", revoked: true);
    public int PostCalls { get; private set; }
    public int FlagGets { get; private set; }
    private int snapshots;

    public Task<VoteIntegrityFlagsResponse> FetchVoteFlagsAsync(
        IntegrityFlagStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default) => Task.FromResult(
        new VoteIntegrityFlagsResponse([flag], new PageInfo(null, false, flag.Id)));

    public Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltiesAsync(
        IntegrityPenaltyStatus status, string? after = null, int limit = 25,
        string? sourceFlagId = null, CancellationToken cancellationToken = default)
    {
      snapshots++;
      if (scenario == BaselineScenario.Failure && snapshots == 1)
        return Task.FromException<VoteIntegrityPenaltiesResponse>(
            new HttpRequestException("baseline unavailable"));
      var scope = scenario == BaselineScenario.ScopeMismatch && snapshots == 1
          ? "different-flag"
          : sourceFlagId!;
      var rows = scenario == BaselineScenario.NewCommittedRow && snapshots >= 2
          ? new[] { historical, Penalty("new-active", revoked: false) }
          : [historical];
      return Task.FromResult(new VoteIntegrityPenaltiesResponse(
          rows, new PageInfo(null, false, rows[^1].Id), new("flag", scope)));
    }

    public Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteRingPenaltyAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      PostCalls++;
      return PostCalls == 1
          ? Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
              new HttpRequestException("result uncertain"))
          : Task.FromResult(new VoteIntegrityPenaltyApplicationResponse(2));
    }

    public Task<VoteIntegrityFlagResponse> ResolveVoteFlagAsync(
        string flagId, VoteIntegrityResolution resolution,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<VoteIntegrityFlagResponse> FetchVoteFlagAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      FlagGets++;
      if (scenario == BaselineScenario.FlagGetFailure)
        return Task.FromException<VoteIntegrityFlagResponse>(
            new HttpRequestException("flag unavailable"));
      var authoritative = scenario == BaselineScenario.ConcurrentlyResolved
          ? flag with
          {
            Resolution = "dismissed",
            ResolvedAt = DateTimeOffset.UtcNow,
            ResolvedById = "admin-2",
          }
          : flag;
      return Task.FromResult(new VoteIntegrityFlagResponse(authoritative));
    }
    public Task<ReportIntegrityFlagsResponse> FetchReportFlagsAsync(
        IntegrityFlagStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportIntegrityFlagResponse> DismissReportFlagAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<ReportIntegrityFlagResponse> FetchReportFlagAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<ReportIntegrityPenaltyResponse> PenalizeReportersAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltiesAsync(
        IntegrityPenaltyStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportAbusePenaltyResponse> FetchReportPenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<ReportAbusePenaltyResponse> RevokeReportPenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteWeightPenaltyResponse> FetchVotePenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteWeightPenaltyResponse> RevokeVotePenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private static VoteIntegrityFlag Flag() => new(
        "flag-1", "post-1", null, null, null, null, null, "velocity_spike",
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{}")!,
        null, null, null, DateTimeOffset.UtcNow);
    private static VoteWeightPenalty Penalty(string id, bool revoked) => new(
        id, "user-1", 0.2, "voting_ring", "flag-1", "admin-1",
        revoked ? DateTimeOffset.UtcNow : null, revoked ? "admin-2" : null,
        DateTimeOffset.UtcNow);
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
}
