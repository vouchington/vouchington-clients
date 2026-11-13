using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData("/api/v1/session")]
  [InlineData("/api/v1/auth/email-address/login")]
  [InlineData("/api/v1/login")]
  [InlineData("/api/v1/logout")]
  public async Task AuthenticationRoutesNeverReplay(string path)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    if (path == "/api/v1/session")
      fixture.Terminal.Renew = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Post, path), TestContext.Current.CancellationToken));
    Assert.Equal("AUTH_REQUIRED", error.ErrorCode);
    Assert.Equal(path == "/api/v1/session" ? 1 : 0, fixture.Terminal.RenewCount);
  }

  [Theory]
  [InlineData("x-app-attest-challenge-id")]
  [InlineData("x-app-attest-assertion")]
  public async Task RequestsWithOneUseAttestationProofNeverRenewOrReplay(string header)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var requests = 0;
    var persisted = fixture.Persistence.Value;
    fixture.Terminal.Other = request =>
    {
      requests++;
      Assert.Equal("one-use-proof", request.Headers.GetValues(header).Single());
      return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    };
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Post, "/api/v1/posts")
        {
          Body = new { message = "repeatable-json" },
          Headers = new Dictionary<string, string> { [header] = "one-use-proof" },
        }, TestContext.Current.CancellationToken));
    Assert.Equal("AUTH_REQUIRED", error.ErrorCode);
    Assert.Equal(1, requests);
    Assert.Equal(0, fixture.Terminal.RenewCount);
    Assert.Equal(persisted, fixture.Persistence.Value);
  }

  [Theory]
  [InlineData("OTHER")]
  [InlineData("auth_required")]
  public async Task OtherUnauthorizedCodesNeverReplayAndRetainErrorBody(string code)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code }));
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Equal(code, error.ErrorCode);
    Assert.Equal(0, fixture.Terminal.RenewCount);
  }

  [Fact]
  public async Task ReplayedUnauthorizedResponseIsReturnedWithoutAnotherRotation()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync<JsonElement>(
        new ApiRequest(HttpMethod.Post, "/api/v1/posts") { Body = new { value = 1 } },
        TestContext.Current.CancellationToken));
    Assert.Equal("AUTH_REQUIRED", error.ErrorCode);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Null(fixture.Persistence.Value);
    fixture.Terminal.Renew = _ => Task.FromResult(Rotated(uid: null, dt: "anonymous-dt", st: "anonymous-st"));
    for (var attempt = 0; attempt < 2; attempt++)
      await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
          new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Equal(2, fixture.Terminal.RenewCount);
    Assert.Contains("anonymous-st", fixture.Persistence.Value!);
    Assert.False(fixture.Jar.CanRecoverAuthentication);
  }

  [Fact]
  public async Task RawRequestsAreCookieFencedWithoutAutomaticReplay()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    using var response = await fixture.RawAsync("/api/v1/posts");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal(0, fixture.Terminal.RenewCount);
    Assert.Contains("old-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task OversizedUnauthorizedBodiesAreBoundedAndNeverRenewed(bool knownLength)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var content = new BoundedProbeContent(knownLength);
    fixture.Terminal.Other = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
    {
      Content = content,
    });
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Null(error.ErrorCode);
    Assert.Equal(0, fixture.Terminal.RenewCount);
    Assert.InRange(content.BytesRead, 0, 72 * 1024);
  }

  [Fact]
  public async Task RenewalUnauthorizedClearsPreviouslyAuthenticatedIdentity()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    fixture.Terminal.Renew = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.False(fixture.Store.Current.IsAuthenticated);
    Assert.Null(fixture.Persistence.Value);
  }

  [Fact]
  public async Task ExternalRequestNeverReceivesFirstPartyCookieOrAppliesExternalCookies()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = request =>
    {
      Assert.False(request.Headers.Contains("Cookie"));
      return Task.FromResult(WithCookies("external-dt", "external-st"));
    };
    using var response = await fixture.RawAsync("https://uploads.test/object");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("old-st", fixture.Persistence.Value!);
    Assert.Equal(0, fixture.Terminal.RenewCount);
  }

  [Theory]
  [InlineData("null")]
  [InlineData("[]")]
  [InlineData("not-json")]
  public async Task NonObjectUnauthorizedPayloadDoesNotAttemptRecovery(string body)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
    {
      Content = new StringContent(body),
    });
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Null(error.ErrorCode);
    Assert.Equal(0, fixture.Terminal.RenewCount);
    Assert.Contains("old-st", fixture.Persistence.Value!);
  }

  [Fact]
  public async Task RetryBodyReadFailureDisposesBothOwnedResponses()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var content = new FailingReadContent();
    fixture.Terminal.Other = request => Task.FromResult(
        request.Headers.GetValues("Cookie").Single().Contains("st=new-st", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = content }
            : Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    await Assert.ThrowsAsync<IOException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.True(content.WasDisposed);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  private sealed class FailingReadContent : HttpContent
  {
    public bool WasDisposed { get; private set; }
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new FailingStream());
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        throw new NotSupportedException();
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    private sealed class FailingStream : MemoryStream
    {
      public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
          ValueTask.FromException<int>(new IOException("read failed"));
    }
  }

  private sealed class BoundedProbeContent : HttpContent
  {
    private readonly bool knownLength;
    private readonly ProbeStream stream = new();
    public BoundedProbeContent(bool knownLength) => this.knownLength = knownLength;
    public long BytesRead => stream.ReadBytes;
    protected override bool TryComputeLength(out long length) { length = 100 * 1024; return knownLength; }
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(stream);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        throw new InvalidOperationException("The bounded reader must consume the stream directly.");
    private sealed class ProbeStream : MemoryStream
    {
      public ProbeStream() : base(Encoding.UTF8.GetBytes(new string(' ', 100 * 1024))) { }
      public long ReadBytes { get; private set; }
      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        var count = await base.ReadAsync(buffer, cancellationToken);
        ReadBytes += count;
        return count;
      }
    }
  }
}
