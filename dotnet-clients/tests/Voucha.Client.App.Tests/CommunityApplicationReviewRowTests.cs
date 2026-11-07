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
    _ = new Application { Resources =
    {
      [UiMessageKey.NativeSwiftModerationReportsReviewQueueApprove.Value] = "Approve",
      [UiMessageKey.NativeSwiftModerationReportsReviewQueueReject.Value] = "Reject",
    } };
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
