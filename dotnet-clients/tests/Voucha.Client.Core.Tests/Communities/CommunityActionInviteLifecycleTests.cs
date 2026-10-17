using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityActionInviteLifecycleTests
{
  [Fact]
  public async Task NavigationDismissalIgnoresLateInviteWithoutResettingNewSubmission()
  {
    using var handler = new DeferredInviteHandler();
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiCommunitiesService(new VouchaApiClient(httpClient));
    var viewModel = new CommunityActionViewModel(
        service,
        CommunityActionKind.Invite,
        new AppConfig(new Uri("https://api.test"), "site-key"));
    viewModel.CommunitySlug = "test-community";
    viewModel.Email = "first@example.com";

    var firstSubmission = viewModel.SubmitAsync(TestContext.Current.CancellationToken);
    await handler.WaitForRequestAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Loading, viewModel.State);

    // CommunityActionPage.OnDisappearing uses this same public invalidation path.
    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    viewModel.DismissInviteCode();
    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    handler.Complete(0, "stale-code");
    Assert.False(await firstSubmission);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Null(viewModel.CreatedInviteCode);

    viewModel.Email = "second@example.com";
    var secondSubmission = viewModel.SubmitAsync(TestContext.Current.CancellationToken);
    await handler.WaitForRequestAsync(TestContext.Current.CancellationToken);
    handler.Complete(1, "current-code");
    Assert.True(await secondSubmission);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("current-code", viewModel.CreatedInviteCode);
  }

  private sealed class DeferredInviteHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage>[] responses =
    [
      new(TaskCreationOptions.RunContinuationsAsynchronously),
      new(TaskCreationOptions.RunContinuationsAsynchronously),
    ];
    private readonly SemaphoreSlim requestsStarted = new(0);
    private int requestIndex;

    public async Task WaitForRequestAsync(CancellationToken cancellationToken)
    {
      Assert.True(
          await requestsStarted.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken),
          "The invite request did not reach the test handler before the timeout.");
    }

    public void Complete(int index, string code)
    {
      responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(
            $$$"""{"community_invite":{"id":"invite-1","community_id":"community-1","code":"{{{code}}}","invited_user_id":null,"invited_email":"person@example.com","invited_by_id":"moderator-1","accepted_at":null,"accepted_by_id":null,"declined_at":null,"revoked_at":null,"created_at":"2026-07-01T00:00:00Z"}}""",
            Encoding.UTF8,
            "application/json"),
      });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var index = Interlocked.Increment(ref requestIndex) - 1;
      if (request.Method != HttpMethod.Post || request.RequestUri?.AbsolutePath != "/api/v1/communities/test-community/invites")
      {
        throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
      }

      requestsStarted.Release();
      return await responses[index].Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        requestsStarted.Dispose();
      }

      base.Dispose(disposing);
    }
  }
}
