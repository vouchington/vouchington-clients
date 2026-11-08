using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityApplicationReviewRowTests
{
  [Fact]
  public async Task RowUsesActionLabelsAndSubmitsTheOptionalRejectionReason()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var version = new UiLocaleVersion(controller);
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    InstallResources(localization, version);
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationService>();
    var captured = (ApplicationService)service;
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityApplicationsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    var row = (View)typeof(CommunitySectionPage).GetMethod("ApplicationRowTemplate",
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
    row.BindingContext = new CommunityApplicationRow("application-1", "applicant-1", "pending", "Preview",
        DateTimeOffset.UnixEpoch, 1, UiText.UserContent("Applicant"), UiLocalization.English);
    var approve = Find<Button>(row, "community-application-approve");
    var reject = Find<Button>(row, "community-application-reject");
    Assert.Equal("Approve", approve.Text);
    Assert.Equal("Reject", reject.Text);
    Assert.True(reject.IsVisible);
    Find<Entry>(row, "community-application-rejection-reason").Text = "Please include the requested details.";

    reject.SendClicked();
    var request = await captured.Reviewed.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.Equal("community-1", request.Community);
    Assert.Equal("application-1", request.Application);
    Assert.Equal("rejected", request.Request.Status);
    Assert.Equal("Please include the requested details.", request.Request.Reason);
    row.BindingContext = new CommunityApplicationRow("application-2", "applicant-2", "approved", "Preview",
        DateTimeOffset.UnixEpoch, 0, UiText.UserContent("Other applicant"), UiLocalization.English);
    Assert.Null(Find<Entry>(row, "community-application-rejection-reason").Text);
    Assert.False(reject.IsVisible);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task VisibleRowRelocalizesWithoutRebindingOrDiscardingTheRejectionReason(bool namedApplicant)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var version = new UiLocaleVersion(controller);
    using var copy = UiCopy.PushLocalization(localization);
    InstallResources(localization, version);
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationService>();
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session, localization, controller);
    var page = new CommunityApplicationsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    var row = (View)typeof(CommunitySectionPage).GetMethod("ApplicationRowTemplate",
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
    var applicantText = namedApplicant ? UiText.UserContent("Applicant <verbatim>") :
        UiText.Localized(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDeletedUser);
    var application = new CommunityApplicationRow("application-1", "applicant-1", "pending", "Preview",
        DateTimeOffset.UnixEpoch, 12345, applicantText, localization);
    row.BindingContext = application;
    var applicant = Find<Label>(row, "community-application-applicant");
    var status = Find<Label>(row, "community-application-status");
    var date = Find<Label>(row, "community-application-created");
    var count = Find<Label>(row, "community-application-answer-count");
    var reason = Find<Entry>(row, "community-application-rejection-reason");
    Assert.True(reason.IsVisible);
    reason.Text = "Please include the requested details.";
    var originalDate = date.Text;
    var originalCount = count.Text;
    var originalRows = model.ApplicationRows;
    Assert.Equal("pending", status.Text);
    Assert.Equal(namedApplicant ? "Applicant <verbatim>" : "Deleted user", applicant.Text);

    controller.ApplySavedLocale("fr");

    Assert.Same(application, row.BindingContext);
    Assert.Same(originalRows, model.ApplicationRows);
    Assert.Equal(namedApplicant ? "Applicant <verbatim>" : "Utilisateur supprimé", applicant.Text);
    Assert.Equal("en attente", status.Text);
    Assert.NotEqual(originalDate, date.Text);
    Assert.NotEqual(originalCount, count.Text);
    Assert.Equal(localization.FormatDateTime(application.CreatedAt, TimeZoneInfo.Local), date.Text);
    Assert.Equal(localization.FormatNumber(application.AnswerCount), count.Text);
    Assert.Equal("Please include the requested details.", reason.Text);
  }

  private static void InstallResources(IUiLocalization localization, UiLocaleVersion version) =>
      _ = new Application { Resources =
      {
        [UiMessageKey.NativeSwiftModerationReportsReviewQueueApprove.Value] = "Approve",
        [UiMessageKey.NativeSwiftModerationReportsReviewQueueReject.Value] = "Reject",
        ["UiLocaleVersion"] = version,
        ["UiLocalizedValue"] = new UiLocalizedValueConverter(localization),
      } };

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(root.GetVisualTreeDescendants().OfType<T>(), item => item.AutomationId == id);

  public class ApplicationService : CommunityModerationPageAutomodNavigationTests.CommunityServiceProxy
  {
    public TaskCompletionSource<(string Community, string Application, UpdateCommunityPostReviewRequest Request)> Reviewed { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
      if (method?.Name == nameof(ICommunitiesService.FetchApplicationsPageAsync))
        return Task.FromResult(JsonSerializer.Deserialize<CommunityApplicationsResponse>(File.ReadAllText(
            FilamentsContractPaths.ApiFixture("responses/web.communities.applications.default.json")), VouchaApiJson.Options)!);
      if (method?.Name != nameof(ICommunitiesService.ReviewApplicationAsync)) return base.Invoke(method, args);
      Reviewed.SetResult(((string)args![0]!, (string)args[1]!, (UpdateCommunityPostReviewRequest)args[2]!));
      return Task.CompletedTask;
    }
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User(Id: "moderator-1", Username: "moderator", Roles: [], EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
