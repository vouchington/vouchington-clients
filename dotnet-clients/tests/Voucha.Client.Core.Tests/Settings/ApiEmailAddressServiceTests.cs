using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class ApiEmailAddressServiceTests
{
  [Fact]
  public async Task ExistingServiceCallsForwardEmailAndVerificationWithoutInventingReauthentication()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.my.email-addresses.request.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.my.email-addresses.verify.default")),
    ]);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiEmailAddressService(new VouchaApiClient(http));
    var token = TestContext.Current.CancellationToken;

    var requested = await service.RequestEmailVerificationAsync("Tests+Native-User@Voucha.ai", token);
    var verified = await service.VerifyEmailAddressAsync("tests+native-user@voucha.ai", "ABCD1234", token);

    Assert.Equal("tests+native-user@voucha.ai", requested.EmailAddress);
    Assert.True(Assert.Single(verified.Results).IsPrimary);
    Assert.Equal("/api/v1/my/email-addresses", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/my/email-addresses/tests%2Bnative-user%40voucha.ai/verifications", handler.Requests[1].PathAndQuery);
    using var request = JsonDocument.Parse(handler.Requests[0].Body!);
    using var verification = JsonDocument.Parse(handler.Requests[1].Body!);
    Assert.Equal("Tests+Native-User@Voucha.ai", request.RootElement.GetProperty("email_address").GetString());
    Assert.Equal("ABCD1234", verification.RootElement.GetProperty("token").GetString());
    Assert.False(request.RootElement.TryGetProperty("re_auth_token", out _));
    Assert.False(verification.RootElement.TryGetProperty("re_auth_token", out _));
    Assert.All(handler.Requests, item => Assert.Equal(HttpMethod.Post, item.Method));
  }

  [Fact]
  public async Task ClientForwardsExplicitReauthenticationSeparatelyFromEmailAndVerificationToken()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.my.email-addresses.request.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.my.email-addresses.verify.default")),
    ]);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);
    var token = TestContext.Current.CancellationToken;

    await client.RequestEmailAddressVerificationAsync("alice@example.test", "request-auth", token);
    await client.VerifyEmailAddressAsync("alice@example.test", "verification-code", "verify-auth", token);

    using var request = JsonDocument.Parse(handler.Requests[0].Body!);
    using var verification = JsonDocument.Parse(handler.Requests[1].Body!);
    Assert.Equal("alice@example.test", request.RootElement.GetProperty("email_address").GetString());
    Assert.Equal("request-auth", request.RootElement.GetProperty("re_auth_token").GetString());
    Assert.Equal("verification-code", verification.RootElement.GetProperty("token").GetString());
    Assert.Equal("verify-auth", verification.RootElement.GetProperty("re_auth_token").GetString());
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ServiceForwardsCallerCancellationAcrossNewOptionalTokenParameter(bool verify)
  {
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    using var handler = new CancellationHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiEmailAddressService(new VouchaApiClient(http));
    Task operation = verify
        ? service.VerifyEmailAddressAsync("alice@example.test", "code", cancellation.Token)
        : service.RequestEmailVerificationAsync("alice@example.test", cancellation.Token);
    await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

    await cancellation.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    Assert.True(handler.ObservedCancellation);
  }

  private sealed class CancellationHandler : HttpMessageHandler
  {
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool ObservedCancellation { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Started.TrySetResult();
      try
      {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Canceled request unexpectedly completed.");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        ObservedCancellation = true;
        throw;
      }
    }
  }
}
