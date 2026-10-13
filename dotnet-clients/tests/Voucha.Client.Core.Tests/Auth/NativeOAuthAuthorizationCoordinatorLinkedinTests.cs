using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinatorTests
{
  [Fact]
  public async Task LinkedinNativeStartAndCallbackAuthenticateThroughBroker()
  {
    var handler = new RecordingHandler([
      Response("native.oauth.providers.broker-capabilities"),
      new RecordedResponse("""
          {"flow_id":"019fafb8-a44c-73e2-890a-497ff3dd27a6","redirect_url":"https://www.linkedin.com/oauth/v2/authorization","expires_at":"2026-07-29T22:00:00.000Z"}
          """),
      Response("native.oauth.authorization.complete.authenticated"),
    ]);
    var persistence = new InMemoryPersistence();
    var browser = new RecordingBrowser(() => persistence.Pending is not null);
    var session = new RecordingSessionStore
    {
      RefreshSnapshot = new SessionSnapshot(new User("user-1", "testuser")),
    };
    using var coordinator = Coordinator(handler, persistence, browser, session);

    await coordinator.StartAsync(
        OAuthBrokerProvider.Linkedin,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    Assert.Equal(OAuthBrokerProvider.Linkedin, persistence.Pending?.Provider);
    Assert.Equal("/api/v1/auth/oauth/linkedin/authorizations", handler.Requests[1].PathAndQuery);

    var callback = new Uri(
        "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token");
    var outcome = await coordinator.HandleCallbackAsync(
        callback,
        TestContext.Current.CancellationToken);

    Assert.True(outcome.Handled);
    Assert.Equal(OAuthAuthorizationPurpose.Authenticate, outcome.Purpose);
    Assert.True(browser.SawPersistedAuthorization);
    Assert.Null(persistence.Pending);
    Assert.Equal(NativeOAuthAuthorizationResultKind.Authenticated, persistence.Result?.Kind);
    Assert.Equal(OAuthBrokerProvider.Linkedin, persistence.Result?.Provider);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task LinkedinConnectedResultRequiresRefreshedLinkedinIdentity()
  {
    var handler = new RecordingHandler([
      new RecordedResponse("""
          {"oauth_account":{"id":"linkedin-user","name":"LinkedIn User","email_address":null}}
          """),
    ]);
    var persistence = new InMemoryPersistence
    {
      Pending = new PendingNativeOAuthAuthorization(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          OAuthBrokerProvider.Linkedin,
          OAuthAuthorizationPurpose.Connect,
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          new DateTimeOffset(2026, 7, 29, 22, 0, 0, TimeSpan.Zero)),
    };
    var session = new RecordingSessionStore
    {
      RefreshSnapshot = new SessionSnapshot(new User(
          "user-1",
          "testuser",
          LinkedinAccount: new UserDisplayAccount("linkedin-user", "LinkedIn User"))),
    };
    using var coordinator = Coordinator(
        handler,
        persistence,
        new RecordingBrowser(() => true),
        session);

    var outcome = await coordinator.HandleCallbackAsync(
        new Uri(
            "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token"),
        TestContext.Current.CancellationToken);

    Assert.True(outcome.Handled);
    Assert.Equal(OAuthAuthorizationPurpose.Connect, outcome.Purpose);
    Assert.Equal(NativeOAuthAuthorizationResultKind.Connected, persistence.Result?.Kind);
    Assert.Equal(OAuthBrokerProvider.Linkedin, persistence.Result?.Provider);
    Assert.Equal("linkedin-user", session.Current.Identity?.LinkedinAccount?.Id);
    Assert.Equal(1, session.RefreshCount);
  }
}
