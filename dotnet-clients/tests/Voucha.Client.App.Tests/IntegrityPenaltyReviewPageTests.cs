using System.Net;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.ModerationIntegrity;
using Xunit;
using static Voucha.Client.Core.Tests.ModerationIntegrity.PenaltyReviewFixtures;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class IntegrityPenaltyReviewPageTests
{
  private static readonly ModerationIntegrityTestService Ledger = new()
  {
    FetchReportPenalties = (_, _, _) => Task.FromResult(
        new ReportIntegrityPenaltiesResponse([], new PageInfo(null, false, null))),
    FetchVotePenalties = (_, _, _, _) => Task.FromResult(
        new VoteIntegrityPenaltiesResponse([], new PageInfo(null, false, null), new("flag", null))),
  };

  [Fact]
  public async Task ReportPageRendersDueQueueMoreConfirmAndCommittedReviewDate()
  {
    Setup();
    var reviews = new PenaltyReviewTestService();
    reviews.ReportDue.Enqueue(() => Task.FromResult(ReportDue(".page-1")));
    reviews.ReportReviews.Enqueue(() => Task.FromResult(ReportReviewed()));
    var model = new ReportIntegrityPenaltyReviewViewModel(Ledger, reviews, Admin);
    var page = Create(new ReportIntegrityPenaltiesPage(
        new ReportIntegrityPenaltyViewModel(Ledger, Admin), model));
    await page.ReloadAsync();
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var id = model.Rows[0].Id;

    AssertLabel(page, UiMessageKey.ModerationPenaltyReviewsTitle);
    AssertLabel(page, UiMessageKey.ModerationPenaltyReviewsDescription);
    AssertLabel(page, UiMessageKey.ModerationPenaltyReviewsMore);
    AssertLabel(page, UiMessageKey.ModerationPenaltyReviewsDue);
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.ModerationPenaltyReviewsRefresh),
        Find<Button>(page, "report-integrity-penalty-reviews-refresh").Text);
    Assert.True(Find<Button>(page, "report-integrity-penalty-reviews-load-more").IsVisible);
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.ModerationPenaltyReviewsConfirm),
        Find<Button>(page, $"report-integrity-penalty-reviews-confirm-{id}").Text);
    Assert.Equal(1, VisualTreeElements.Descendants<Button>(page).Count(button =>
        button.AutomationId == $"report-integrity-penalty-reviews-revoke-{id}"));

    Find<Button>(page, $"report-integrity-penalty-reviews-confirm-{id}").SendClicked();
    await PageTestWait.WaitUntilAsync(() => !VisualTreeElements.Descendants<Button>(page).Any(button =>
        button.AutomationId == $"report-integrity-penalty-reviews-confirm-{id}"));

    Assert.Single(reviews.ReviewCalls);
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label =>
        label.Text == UiCopy.FormatDateTime(ReportReviewed().Penalty.ReviewDueAt!.Value));
  }

  [Fact]
  public async Task VotePageRendersEmptyStateAndRevokeRemovesDueRow()
  {
    Setup();
    var reviews = new PenaltyReviewTestService();
    reviews.VoteDue.Enqueue(() => Task.FromResult(VoteDue()));
    reviews.VoteDue.Enqueue(() => Task.FromResult(new VoteIntegrityPenaltiesResponse(
        [], new PageInfo(null, false, null), null)));
    var revoked = VoteReviewed().Penalty with { RevokedAt = DateTimeOffset.Parse("2026-10-24T12:00:00Z") };
    var service = new ModerationIntegrityTestService
    {
      FetchVotePenalties = Ledger.FetchVotePenalties,
      RevokeVotePenalty = (_, _) => Task.FromResult(new VoteWeightPenaltyResponse(revoked)),
    };
    var model = new VoteIntegrityPenaltyReviewViewModel(service, reviews, Admin);
    var page = Create(new VoteIntegrityPenaltiesPage(
        new VoteIntegrityPenaltyViewModel(service, Admin), model));
    await page.ReloadAsync();
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var id = model.Rows[0].Id;

    Find<Button>(page, $"vote-integrity-penalty-reviews-revoke-{id}").SendClicked();
    await PageTestWait.WaitUntilAsync(() => model.IsEmpty);
    AssertLabel(page, UiMessageKey.ModerationPenaltyReviewsEmpty);
    Assert.False(Find<Button>(page, "vote-integrity-penalty-reviews-load-more").IsVisible);
  }

  [Fact]
  public async Task LoadFailureShowsLocalizedErrorAndNonAdministratorSeesNoReviewSection()
  {
    Setup();
    var reviews = new PenaltyReviewTestService();
    reviews.ReportDue.Enqueue(() => Task.FromException<ReportIntegrityPenaltiesResponse>(
        Http(HttpStatusCode.BadGateway)));
    var model = new ReportIntegrityPenaltyReviewViewModel(Ledger, reviews, Admin);
    var page = Create(new ReportIntegrityPenaltiesPage(
        new ReportIntegrityPenaltyViewModel(Ledger, Admin), model));
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.Text ==
        UiLocalization.English.Localize(UiMessageKey.ModerationPenaltyReviewsLoadFailed));

    var moderator = new NavigationViewer(true, ["moderator"]);
    var deniedModel = new VoteIntegrityPenaltyReviewViewModel(Ledger, reviews, moderator);
    var denied = Create(new VoteIntegrityPenaltiesPage(
        new VoteIntegrityPenaltyViewModel(Ledger, moderator), deniedModel));
    Assert.False(Assert.Single(VisualTreeElements.Descendants<IntegrityPenaltyReviewSection>(denied)).IsVisible);
  }

  private static void AssertLabel(Page page, UiMessageKey key) =>
      Assert.Contains(VisualTreeElements.Descendants<Label>(page), label =>
          label.Text == UiLocalization.English.Localize(key) ||
          label.GetValue(Label.TextProperty) as string == UiLocalization.English.Localize(key));

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(VisualTreeElements.Descendants<T>(root), element => element.AutomationId == automationId);

  private static T Create<T>(T page) where T : Page => page;

  private static void Setup()
  {
    var localization = UiLocalization.English;
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
  }
}
