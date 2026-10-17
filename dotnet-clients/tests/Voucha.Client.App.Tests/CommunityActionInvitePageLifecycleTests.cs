using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityActionInvitePageLifecycleTests
{
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

  private sealed class TokenProvider : Voucha.Client.App.ITurnstileTokenProvider
  {
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("token");
  }

  private sealed class InviteHandler(bool deferResponse) : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage> response =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete(string code) => response.SetResult(CreateResponse(code));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
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
