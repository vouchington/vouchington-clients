using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityActionInvitePageLifecycleTests
{
  [Fact]
  public async Task ModeratorInvitesPanelOpensNativeCreationWithCommunityContext()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var handler = new InviteHandler(false);
    var api = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = DispatchProxy.Create<ICommunitiesService, InviteSurfaceService>();
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session);
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var services = new ServiceCollection()
        .AddSingleton<ICommunitiesService>(new ApiCommunitiesService(api))
        .AddSingleton(new AppConfig(new Uri("https://api.test"), "site-key"))
        .AddSingleton<IUiLocalization>(UiLocalization.English)
        .AddSingleton<IUiLocaleController>(locale)
        .AddSingleton<ITurnstileTokenProvider>(new TokenProvider())
        .AddSingleton(api).AddSingleton(model).AddSingleton<ISessionStore>(session)
        .AddTransient<CommunityInvitesPage>().BuildServiceProvider();
    var page = services.GetRequiredService<CommunityInvitesPage>();
    page.Slug = "community-1";
    var navigation = new NavigationPage(page);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Invites, TestContext.Current.CancellationToken);

    var create = Find<Button>(page, "community-create-invite");
    create.SendClicked();
    var action = Assert.IsType<CommunityActionPage>(navigation.Navigation.NavigationStack.Last());
    Assert.Equal(CommunityActionKind.Invite, action.Kind);
    Find<Entry>(action, "community-invite-email").Text = "person@example.com";
    await SubmitAsync(action);

    Assert.Equal("/api/v1/communities/community-1/invites", handler.RequestPath);
    Assert.Equal("one-time-code", Find<Entry>(action, "community-created-invite-code").Text);
  }

  [Fact]
  public async Task DelayedInviteAfterPageDisappearsDoesNotRenderOneTimeCode()
  {
    var (page, viewModel, handler) = CreatePage(deferResponse: true);
    Find<Entry>(page, "community-invite-email").Text = "person@example.com";
    var submission = SubmitAsync(page);
    await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    typeof(CommunityActionPage).GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, null);
    Assert.Equal(LoadState.Idle, viewModel.State);
    handler.Complete("stale-code");
    await submission;

    Assert.Null(viewModel.CreatedInviteCode);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(Find<VerticalStackLayout>(page, "community-invite-code-notice").IsVisible);
    Assert.DoesNotContain(page.GetVisualTreeDescendants().OfType<Entry>(),
        entry => entry.AutomationId == "community-created-invite-code");
  }

  [Fact]
  public async Task RenderedInviteCodeIsReadOnlyAndDismissButtonClearsIt()
  {
    var (page, viewModel, handler) = CreatePage(deferResponse: false);
    Find<Entry>(page, "community-invite-email").Text = "person@example.com";
    var submission = SubmitAsync(page);
    await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await submission;

    var notice = Find<VerticalStackLayout>(page, "community-invite-code-notice");
    var code = Find<Entry>(page, "community-created-invite-code");
    Assert.True(notice.IsVisible);
    Assert.True(code.IsReadOnly);
    Assert.Equal("one-time-code", code.Text);
    await SubmitAsync(page);
    Assert.Equal(1, handler.RequestCount);
    Assert.Equal("one-time-code", code.Text);
    Assert.True(notice.IsVisible);

    Find<Button>(page, "community-invite-code-dismiss").SendClicked();

    Assert.Null(viewModel.CreatedInviteCode);
    Assert.False(notice.IsVisible);
    Assert.Null(code.Text);
  }

  private static (CommunityActionPage Page, CommunityActionViewModel ViewModel, InviteHandler Handler) CreatePage(
      bool deferResponse)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var handler = new InviteHandler(deferResponse);
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var apiClient = new VouchaApiClient(httpClient);
    var viewModel = new CommunityActionViewModel(
        new ApiCommunitiesService(apiClient),
        CommunityActionKind.Invite,
        new Voucha.Client.Core.AppConfig(new Uri("https://api.test"), "site-key"));
    var page = new CommunityActionPage(viewModel, new TokenProvider(), apiClient);
    return (page, viewModel, handler);
  }

  private static Task SubmitAsync(CommunityActionPage page) =>
      (Task)typeof(CommunityActionPage)
          .GetMethod("SubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, null)!;

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(root.GetVisualTreeDescendants().OfType<T>(), element => element.AutomationId == automationId);

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User(Id: "moderator-1", Username: "moderator", Roles: [], EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  public class InviteSurfaceService : CommunityModerationPageAutomodNavigationTests.CommunityServiceProxy
  {
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(ICommunitiesService.FetchInvitesPageAsync)
        ? Task.FromResult(new CommunityInvitesResponse([], new PageInfo(null, false, null), new Dictionary<string, CommunityInvite>()))
        : base.Invoke(method, args);
  }

  private sealed class TokenProvider : Voucha.Client.App.ITurnstileTokenProvider
  {
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("token");
  }

  private sealed class InviteHandler(bool deferResponse) : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage> response =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int RequestCount { get; private set; }
    public string? RequestPath { get; private set; }

    public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete(string code) => response.SetResult(CreateResponse(code));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      RequestCount++;
      RequestPath = request.RequestUri?.AbsolutePath;
      RequestStarted.TrySetResult();
      if (deferResponse)
      {
        return await response.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      }

      return CreateResponse("one-time-code");
    }

    private static HttpResponseMessage CreateResponse(string code) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(
          $$$"""{"community_invite":{"id":"invite-1","community_id":"community-1","code":"{{{code}}}","invited_user_id":null,"invited_email":"person@example.com","invited_by_id":"moderator-1","accepted_at":null,"accepted_by_id":null,"declined_at":null,"revoked_at":null,"created_at":"2026-07-01T00:00:00Z"}}""",
          Encoding.UTF8,
          "application/json"),
    };
  }
}
