using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using Voucha.Client.Core.Tests.Api;
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

  [Theory]
  [InlineData("en", "Unknown recipient")]
  [InlineData("es", "Destinatario desconocido")]
  public async Task MetadataOnlyInviteRowRendersUnknownRecipient(string language, string expected)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var service = DispatchProxy.Create<ICommunitiesService, InviteSurfaceService>();
    var fixture = JsonNode.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture(
        "responses/web.communities.invites.default.json")))!;
    foreach (var entry in fixture["community_invites"]!.AsObject())
    {
      entry.Value!["invited_email"] = null;
      entry.Value["invited_user_id"] = null;
    }
    ((InviteSurfaceService)service).Invites = fixture.Deserialize<CommunityInvitesResponse>(
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider(language));
    using var model = new CommunityDetailViewModel(service, new SessionStore(), new UiLocalization(locale), locale);
    var page = new CommunityInvitesPage(model, new SessionStore()) { Slug = "community-1" };
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Invites, TestContext.Current.CancellationToken);
    var collection = Assert.IsType<CollectionView>(typeof(CommunitySectionPage)
        .GetField("rowsView", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page));
    var row = Assert.IsAssignableFrom<View>(collection.ItemTemplate!.CreateContent());
    row.BindingContext = Assert.Single(model.Invites);
    Assert.Contains(row.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == expected);
    Assert.DoesNotContain(row.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == model.Invites[0].Id);
  }

  [Theory]
  [InlineData("member", true, false, false, true, false)]
  [InlineData("member", false, false, false, false, false)]
  [InlineData("moderator", false, false, false, true, true)]
  [InlineData(null, true, false, false, false, false)]
  [InlineData(null, true, false, true, false, false)]
  [InlineData("moderator", false, true, false, false, false)]
  public async Task InvitePanelSeparatesMemberCreationFromModeratorRevocation(
      string? membershipRole, bool memberInvitesAllowed, bool archived, bool administrator, bool canCreate, bool canRevoke)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var service = DispatchProxy.Create<ICommunitiesService, InviteSurfaceService>();
    var script = (InviteSurfaceService)service;
    script.MembershipRole = membershipRole;
    script.MemberInvitesAllowed = memberInvitesAllowed;
    script.Archived = archived;
    var session = new SessionStore(administrator);
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityInvitesPage(model, session) { Slug = "community-1" };

    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Invites, TestContext.Current.CancellationToken);

    var buttons = page.GetVisualTreeDescendants().OfType<Button>().Where(button => button.IsVisible).ToArray();
    Assert.Equal(canCreate, buttons.Any(button => button.AutomationId == "community-create-invite"));
    Assert.Equal(canRevoke, buttons.Any(button => button.AutomationId == "community-revoke-invite"));
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

  private sealed class SessionStore(bool administrator = false) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User(Id: "moderator-1", Username: "moderator", Roles: administrator ? ["administrator"] : [], EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  public class InviteSurfaceService : CommunityModerationPageAutomodNavigationTests.CommunityServiceProxy
  {
    public string? MembershipRole { get; set; } = "moderator";
    public bool MemberInvitesAllowed { get; set; }
    public bool Archived { get; set; }
    public CommunityInvitesResponse? Invites { get; set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
      nameof(ICommunitiesService.FetchInvitesPageAsync) => MembershipRole is "owner" or "moderator"
          ? Task.FromResult(Invites ?? new CommunityInvitesResponse([], new PageInfo(null, false, null), new Dictionary<string, CommunityInvite>()))
          : Task.FromException<CommunityInvitesResponse>(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden)),
      nameof(ICommunitiesService.FetchDetailAsync) => DetailAsync((Task<CommunityResponse>)base.Invoke(method, args)!),
      _ => base.Invoke(method, args),
    };

    private async Task<CommunityResponse> DetailAsync(Task<CommunityResponse> pending)
    {
      var detail = await pending.ConfigureAwait(false);
      var role = MembershipRole;
      return detail with
      {
        Community = detail.Community with
        {
          MemberInvitesAllowedAt = MemberInvitesAllowed ? DateTimeOffset.UnixEpoch : null,
          ArchivedAt = Archived ? DateTimeOffset.UnixEpoch : null,
        },
        Membership = role is null ? null : detail.Membership! with { Role = role },
      };
    }
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
