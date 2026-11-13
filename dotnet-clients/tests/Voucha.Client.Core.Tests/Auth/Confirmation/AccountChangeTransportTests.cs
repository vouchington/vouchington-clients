using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class AccountChangeTransportTests
{
  [Theory]
  [InlineData(0, "email_address", "new@example.test")]
  [InlineData(1, "token", "ABCDEFGH23")]
  [InlineData(2, "purpose", "connect")]
  [InlineData(3, "re_auth_token", null)]
  [InlineData(4, "callback_mode", "native")]
  [InlineData(5, "handle", "alice.bsky.social")]
  [InlineData(6, "re_auth_token", null)]
  public async Task EachProtectedMutationAcquiresOneFreshProofAndPreservesTypedBody(int kind, string field, string? expected)
  {
    var request = kind switch
    {
      0 => VouchaApiEndpoints.RequestEmailAddressVerification("new@example.test"),
      1 => VouchaApiEndpoints.VerifyEmailAddress("new@example.test", "abcdefgh23"),
      2 => VouchaApiEndpoints.BeginOAuthAuthorization(OAuthBrokerProvider.Github, OAuthAuthorizationPurpose.Connect, "challenge"),
      3 => VouchaApiEndpoints.DisconnectOAuthAccount(OAuthBrokerProvider.Github),
      4 => VouchaApiEndpoints.BeginNativeBlueskyAccountLink("alice.bsky.social", "challenge"),
      5 => VouchaApiEndpoints.BeginBlueskyAccountLink("alice.bsky.social"),
      _ => VouchaApiEndpoints.DisconnectBlueskyAccount(),
    };
    var handler = new RecordingHandler();
    var confirmation = new RecordingConfirmation();
    var client = Client(handler, confirmation);

    await client.SendAsync(request, TestContext.Current.CancellationToken);
    await client.SendAsync(request, TestContext.Current.CancellationToken);

    Assert.Equal(2, confirmation.Calls);
    Assert.Equal(2, handler.Bodies.Count);
    for (var index = 0; index < handler.Bodies.Count; index++)
    {
      using var body = JsonDocument.Parse(handler.Bodies[index]);
      Assert.Equal($"proof-{index + 1}", body.RootElement.GetProperty("re_auth_token").GetString());
      if (expected is not null) Assert.Equal(expected, body.RootElement.GetProperty(field).GetString());
    }
  }

  [Fact]
  public async Task MissingConfirmationServiceFailsClosedBeforeAnyMutation()
  {
    var handler = new RecordingHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        client.SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), TestContext.Current.CancellationToken));
    Assert.Empty(handler.Bodies);
  }

  [Fact]
  public async Task SessionOrDeviceChangeDuringConfirmationNeverSendsMutation()
  {
    var handler = new RecordingHandler();
    var binding = "account/device/session-a";
    var confirmation = new RecordingConfirmation
    {
      Create = () =>
      {
        var original = binding;
        binding = "account/device/session-b";
        return new AccountChangeProof("old-proof", () => binding == original);
      },
    };
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        Client(handler, confirmation).SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), TestContext.Current.CancellationToken));
    Assert.Empty(handler.Bodies);
    Assert.Equal(1, confirmation.Calls);
  }

  [Fact]
  public async Task ReusingTheSameLocalProofDoesNotSendASecondMutation()
  {
    var handler = new RecordingHandler();
    var proof = new AccountChangeProof("one-use", () => true);
    var client = Client(handler, new RecordingConfirmation { Create = () => proof });
    await client.SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), TestContext.Current.CancellationToken);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        client.SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), TestContext.Current.CancellationToken));
    Assert.Single(handler.Bodies);
  }

  [Fact]
  public async Task CallerCancellationAfterDialogSuccessPreventsCommit()
  {
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var handler = new RecordingHandler();
    var confirmation = new RecordingConfirmation { Create = () => { cancellation.Cancel(); return new("proof", () => true); } };
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        Client(handler, confirmation).SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), cancellation.Token));
    Assert.Empty(handler.Bodies);
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest)]
  [InlineData(HttpStatusCode.Unauthorized)]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.Conflict)]
  [InlineData(HttpStatusCode.Gone)]
  public async Task RejectedRevokedOrReplayedServerProofDoesNotRetry(HttpStatusCode status)
  {
    var handler = new RecordingHandler { Status = status };
    var confirmation = new RecordingConfirmation();
    await Assert.ThrowsAsync<VouchaApiException>(() =>
        Client(handler, confirmation).SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), TestContext.Current.CancellationToken));
    Assert.Single(handler.Bodies);
    Assert.Equal(1, confirmation.Calls);
  }

  [Fact]
  public async Task OrdinarySignInAndSessionRefreshAreNotTreatedAsAccountChanges()
  {
    var handler = new RecordingHandler();
    var confirmation = new RecordingConfirmation();
    var client = Client(handler, confirmation);
    await client.SendAsync(VouchaApiEndpoints.BeginOAuthAuthorization(
        OAuthBrokerProvider.Github, OAuthAuthorizationPurpose.Authenticate, "challenge"), TestContext.Current.CancellationToken);
    await client.SendAsync(VouchaApiEndpoints.RefreshSession("existing-device", "existing-session"), TestContext.Current.CancellationToken);
    Assert.Equal(0, confirmation.Calls);
    using var body = JsonDocument.Parse(handler.Bodies[1]);
    Assert.Equal("existing-session", body.RootElement.GetProperty("st").GetString());
  }

  [Theory]
  [InlineData("account")]
  [InlineData("device")]
  [InlineData("session")]
  public void BindingTracksExactInitiatingAccountDeviceAndSession(string changed)
  {
    var origin = new Uri("https://api.test");
    var cookies = new CookieContainer();
    cookies.Add(origin, new Cookie("dt", "device-a"));
    cookies.Add(origin, new Cookie("st", "session-mfa-issued"));
    using var jar = new SessionCookieJar(cookies, origin, new TestSessionCookiePersistence());
    var store = new ConfirmationSessionStore();
    var initial = AccountChangeSessionBinding.Capture(store, jar, origin);
    Assert.NotNull(initial);
    if (changed == "account") store.Change("another-account");
    else cookies.Add(origin, new Cookie(changed == "device" ? "dt" : "st", "changed"));
    Assert.NotEqual(initial, AccountChangeSessionBinding.Capture(store, jar, origin));
    Assert.DoesNotContain("session-mfa-issued", initial, StringComparison.Ordinal);
    Assert.Equal(0, store.RefreshCalls);
  }

  [Fact]
  public void ParentDomainCookiesCannotMakeProofsPortableAcrossOrigins()
  {
    var first = new Uri("https://one.example.test");
    var second = new Uri("https://two.example.test");
    var cookies = new CookieContainer();
    cookies.Add(new Cookie("dt", "same-device", "/", ".example.test"));
    cookies.Add(new Cookie("st", "same-session", "/", ".example.test"));
    using var jar = new SessionCookieJar(cookies, first, new TestSessionCookiePersistence());
    var store = new ConfirmationSessionStore();
    var binding = AccountChangeSessionBinding.Capture(store, jar, first);
    Assert.NotNull(binding);
    Assert.NotNull(AccountChangeSessionBinding.Capture(store, jar, second));
    Assert.NotEqual(binding, AccountChangeSessionBinding.Capture(store, jar, second));
    var sameOrigin = new AccountChangeProof("proof", () => AccountChangeSessionBinding.Capture(store, jar, first) == binding);
    Assert.Equal("proof", sameOrigin.Consume());
    var crossOrigin = new AccountChangeProof("proof", () => AccountChangeSessionBinding.Capture(store, jar, second) == binding);
    Assert.Throws<OperationCanceledException>(() => crossOrigin.Consume());
    Assert.Equal(binding, AccountChangeSessionBinding.Capture(store, jar, new Uri("https://ONE.example.test:443/other-path")));
  }

  private static VouchaApiClient Client(RecordingHandler handler, IAccountChangeConfirmation confirmation) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }, confirmation);

  private sealed class RecordingConfirmation : IAccountChangeConfirmation
  {
    public int Calls { get; private set; }
    public Func<AccountChangeProof>? Create { get; init; }
    public Task<AccountChangeProof> ConfirmAsync(CancellationToken cancellationToken = default)
    { Calls++; return Task.FromResult(Create?.Invoke() ?? new AccountChangeProof($"proof-{Calls}", () => true)); }
  }

  private sealed class RecordingHandler : HttpMessageHandler
  {
    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
    public List<string> Bodies { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Bodies.Add(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken));
      return new HttpResponseMessage(Status) { Content = new StringContent("{}"), RequestMessage = request };
    }
  }
}
