using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinatorTests
{
  [Fact]
  public async Task GoogleNativeStartAndCallbackAuthenticateThroughBroker()
  {
    var handler = new RecordingHandler([
      new RecordedResponse("""
          {"providers":["facebook","x","github","google"],"broker_capabilities":{
            "facebook":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
            "x":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
            "github":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
            "google":{"version":1,"modes":{"web":true,"native":true},"purposes":["authenticate","connect"]}}
          }
          """),
      new RecordedResponse("""
          {"flow_id":"019fafb8-a44c-73e2-890a-497ff3dd27a6","redirect_url":"https://accounts.google.com/o/oauth2/v2/auth","expires_at":"2026-07-29T22:00:00.000Z"}
          """),
      new RecordedResponse("""
          {"user":{"id":"user-1","username":"alice","roles":[]}}
          """),
    ]);
    var persistence = new InMemoryPersistence();
    var browser = new RecordingBrowser(() => persistence.Pending is not null);
    var session = new RecordingSessionStore
    {
      RefreshSnapshot = new SessionSnapshot(new User("user-1", "alice")),
    };
    using var coordinator = Coordinator(handler, persistence, browser, session);

    await coordinator.StartAsync(
        OAuthBrokerProvider.Google,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    Assert.Equal(OAuthBrokerProvider.Google, persistence.Pending?.Provider);
    Assert.Equal("/api/v1/auth/oauth/google/authorizations", handler.Requests[1].PathAndQuery);
    var outcome = await coordinator.HandleCallbackAsync(
        new Uri("voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token"),
        TestContext.Current.CancellationToken);

    Assert.True(outcome.Handled);
    Assert.Equal(NativeOAuthAuthorizationResultKind.Authenticated, persistence.Result?.Kind);
    Assert.Equal(OAuthBrokerProvider.Google, persistence.Result?.Provider);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task GoogleConnectedResultRequiresRefreshedGoogleIdentity()
  {
    var handler = new RecordingHandler([
      new RecordedResponse("""
          {"oauth_account":{"id":"google-user","name":"Google User","email_address":null}}
          """),
    ]);
    var persistence = new InMemoryPersistence
    {
      Pending = new PendingNativeOAuthAuthorization(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          OAuthBrokerProvider.Google,
          OAuthAuthorizationPurpose.Connect,
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          new DateTimeOffset(2026, 7, 29, 22, 0, 0, TimeSpan.Zero)),
    };
    var session = new RecordingSessionStore
    {
      RefreshSnapshot = new SessionSnapshot(new User(
          "user-1",
          "testuser",
          GoogleAccount: new UserDisplayAccount("google-user", "Google User"))),
    };
    using var coordinator = Coordinator(
        handler,
        persistence,
        new RecordingBrowser(() => true),
        session);

    var outcome = await coordinator.HandleCallbackAsync(
        new Uri("voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token"),
        TestContext.Current.CancellationToken);

    Assert.True(outcome.Handled);
    Assert.Equal(OAuthAuthorizationPurpose.Connect, outcome.Purpose);
    Assert.Equal(NativeOAuthAuthorizationResultKind.Connected, persistence.Result?.Kind);
    Assert.Equal(OAuthBrokerProvider.Google, persistence.Result?.Provider);
    Assert.Equal("google-user", session.Current.Identity?.GoogleAccount?.Id);
    Assert.Equal(1, session.RefreshCount);
  }
}
