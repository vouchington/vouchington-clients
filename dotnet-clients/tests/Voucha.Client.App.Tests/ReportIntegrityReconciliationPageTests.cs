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
public sealed class ReportIntegrityReconciliationPageTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Theory]
  [InlineData(false, "report-integrity-dismiss-flag-1")]
  [InlineData(true, "report-integrity-penalize-flag-1")]
  public async Task FailedAutomaticReloadRendersGetOnlyReconciliation(
      bool penalize,
      string actionId)
  {
    var service = new ReconciliationService(penalize);
    var viewModel = new ReportIntegrityViewModel(service, Admin);
    var page = Create(new ReportIntegrityPage(viewModel));
    await page.ReloadAsync();

    Find<Button>(page, actionId).SendClicked();
    if (penalize)
    {
      Assert.Equal(0, service.MutationCalls);
      Find<Button>(page, "report-integrity-penalize-confirm-flag-1").SendClicked();
    }
    await WaitUntilAsync(() => service.ExactFetches == 1 &&
        Descendants<Button>(page).Any(button =>
            button.AutomationId == "report-integrity-reconcile-flag-1"));

    var reconcile = Find<Button>(page, "report-integrity-reconcile-flag-1");
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityReconcile),
        reconcile.Text);
    reconcile.SendClicked();
    await WaitUntilAsync(() => service.ExactFetches == 2 &&
        !Descendants<Border>(page).Any(border =>
            border.AutomationId == "report-integrity-flag-flag-1"));

    Assert.Equal(1, service.MutationCalls);
    Assert.False(viewModel.NeedsReconciliation("flag-1"));
  }

  [Fact]
  public async Task PenaltyPromptCanCancelAndConfirmExactlyOnce()
  {
    var service = new ReconciliationService(penalize: true);
    var page = Create(new ReportIntegrityPage(new ReportIntegrityViewModel(service, Admin)));
    await page.ReloadAsync();

    Find<Button>(page, "report-integrity-penalize-flag-1").SendClicked();
    Assert.Equal(0, service.MutationCalls);
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityConfirmAction),
        Find<Button>(page, "report-integrity-penalize-confirm-flag-1").Text);
    var cancel = Find<Button>(page, "report-integrity-penalize-cancel-flag-1");
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeSwiftIntegrityCancel),
        cancel.Text);
    cancel.SendClicked();
    Assert.Equal(0, service.MutationCalls);
    Assert.DoesNotContain(Descendants<Button>(page), button =>
        button.AutomationId == "report-integrity-penalize-confirm-flag-1");

    Find<Button>(page, "report-integrity-penalize-flag-1").SendClicked();
    var confirm = Find<Button>(page, "report-integrity-penalize-confirm-flag-1");
    confirm.SendClicked();
    confirm.SendClicked();
    await WaitUntilAsync(() => service.ExactFetches == 1);

    Assert.Equal(1, service.MutationCalls);
    Assert.False(Find<Button>(page, "report-integrity-penalize-flag-1").IsEnabled);
  }

  [Fact]
  public async Task CapabilityAndAuthorizationDenialsNeverPromptOrPost()
  {
    var capabilityService = new ReconciliationService(penalize: true);
    var capabilityViewModel = new ReportIntegrityViewModel(
        capabilityService, Admin, capabilities: new IntegrityCapabilities(true, true, false, true, true));
    var capabilityPage = Create(new ReportIntegrityPage(capabilityViewModel));
    await capabilityPage.ReloadAsync();

    var deniedPenalty = Find<Button>(capabilityPage, "report-integrity-penalize-flag-1");
    Assert.False(deniedPenalty.IsEnabled);
    deniedPenalty.SendClicked();
    Assert.Equal(0, capabilityService.MutationCalls);
    Assert.DoesNotContain(Descendants<Button>(capabilityPage), button =>
        button.AutomationId == "report-integrity-penalize-confirm-flag-1");

    var authorizationService = new ReconciliationService(penalize: true);
    var authorizationPage = Create(new ReportIntegrityPage(new ReportIntegrityViewModel(
        authorizationService, new NavigationViewer(true, ["member"]))));
    await authorizationPage.ReloadAsync();

    Assert.Equal(0, authorizationService.ListFetches);
    Assert.DoesNotContain(Descendants<Button>(authorizationPage), button =>
        button.AutomationId?.StartsWith("report-integrity-penalize-", StringComparison.Ordinal) == true);
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

  private sealed class ReconciliationService(bool penalize) : IModerationIntegrityService
  {
    private readonly ReportIntegrityFlag pending = Flag();
    public int ExactFetches { get; private set; }
    public int ListFetches { get; private set; }
    public int MutationCalls { get; private set; }

    public Task<ReportIntegrityFlagsResponse> FetchReportFlagsAsync(
        IntegrityFlagStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default)
    {
      ListFetches++;
      return Task.FromResult(new ReportIntegrityFlagsResponse(
          [pending], new PageInfo(null, false, pending.Id)));
    }

    public Task<ReportIntegrityFlagResponse> DismissReportFlagAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      if (penalize) throw new InvalidOperationException("Unexpected dismissal.");
      MutationCalls++;
      return Task.FromException<ReportIntegrityFlagResponse>(
          new HttpRequestException("dismiss result uncertain"));
    }

    public Task<ReportIntegrityPenaltyResponse> PenalizeReportersAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      if (!penalize) throw new InvalidOperationException("Unexpected penalty.");
      MutationCalls++;
      return Task.FromException<ReportIntegrityPenaltyResponse>(
          new HttpRequestException("penalty result uncertain"));
    }

    public Task<ReportIntegrityFlagResponse> FetchReportFlagAsync(
        string flagId, CancellationToken cancellationToken = default)
    {
      ExactFetches++;
      return ExactFetches == 1
          ? Task.FromException<ReportIntegrityFlagResponse>(
              new HttpRequestException("reload failed"))
          : Task.FromResult(new ReportIntegrityFlagResponse(pending with
          {
            Resolution = penalize ? "penalized" : "dismissed",
            ResolvedAt = DateTimeOffset.UtcNow,
          }));
    }

    public Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltiesAsync(
        IntegrityPenaltyStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportAbusePenaltyResponse> FetchReportPenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<ReportAbusePenaltyResponse> RevokeReportPenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteIntegrityFlagsResponse> FetchVoteFlagsAsync(
        IntegrityFlagStatus status, string? after = null, int limit = 25,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<VoteIntegrityFlagResponse> ResolveVoteFlagAsync(
        string flagId, VoteIntegrityResolution resolution,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<VoteIntegrityFlagResponse> FetchVoteFlagAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteRingPenaltyAsync(
        string flagId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltiesAsync(
        IntegrityPenaltyStatus status, string? after = null, int limit = 25,
        string? sourceFlagId = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteWeightPenaltyResponse> FetchVotePenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<VoteWeightPenaltyResponse> RevokeVotePenaltyAsync(
        string penaltyId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private static ReportIntegrityFlag Flag() => new(
        "flag-1", "post-1", null, null, null, "mass_report_suspected", 5, 0.6,
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{}")!,
        null, null, null, DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
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
