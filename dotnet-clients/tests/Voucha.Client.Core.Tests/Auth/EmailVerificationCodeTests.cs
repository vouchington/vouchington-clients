using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class EmailVerificationCodeTests
{
  [Fact]
  public void NullCodeFailsClosed()
  {
    Assert.False(EmailVerificationCode.IsValid(null));
    Assert.Throws<ArgumentNullException>(() => EmailVerificationCode.Normalize(null!));
    Assert.Throws<ArgumentNullException>(() => EmailVerificationCode.RequireValid(null!));
  }

  [Theory]
  [InlineData("")]
  [InlineData("ABCDEFGH2")]
  [InlineData("ABCDEFGH234")]
  [InlineData("123456")]
  [InlineData("ABCDEFGH01")]
  [InlineData("ABCDEFGH89")]
  [InlineData("ABCDEFGH2!")]
  [InlineData("ABCDEFGHIß")]
  [InlineData("ABCDEFGHİ2")]
  [InlineData(" abcdefgh23 ")]
  public void EveryEmailVerificationEndpointRejectsMalformedInput(string code)
  {
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.VerifyEmailOtp("a@example.com", code));
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.VerifyNativeEmailOtp("a@example.com", code));
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.VerifyEmailAddress("a@example.com", code));
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.VerifyReAuthEmailCode(code));
  }

  [Fact]
  public void EmailEndpointsNormalizeAsciiLowercaseAndTotpKeepsSixDigits()
  {
    Assert.Equal("ABCDEFGH23", Assert.IsType<VerifyEmailOtpBody>(
        VouchaApiEndpoints.VerifyEmailOtp("a@example.com", "abcdefgh23").Body).Code);
    Assert.Equal("ABCDEFGH23", Assert.IsType<VerifyEmailAddressBody>(
        VouchaApiEndpoints.VerifyEmailAddress("a@example.com", "abcdefgh23").Body).Token);
    Assert.Equal("ABCDEFGH23", Assert.IsType<ReAuthCodeBody>(
        VouchaApiEndpoints.VerifyReAuthEmailCode("abcdefgh23").Body).Code);
    Assert.Equal("123456", Assert.IsType<ReAuthCodeBody>(VouchaApiEndpoints.VerifyReAuthTotp("123456").Body).Code);
  }

  [Theory]
  [InlineData("invalid code", HttpStatusCode.BadRequest)]
  [InlineData("replaced code", HttpStatusCode.BadRequest)]
  [InlineData("purpose mismatch", HttpStatusCode.BadRequest)]
  [InlineData("quota exceeded", HttpStatusCode.TooManyRequests)]
  public async Task PrefillWaitsForSubmitAndRejectedCodeDoesNotRetry(string error, HttpStatusCode status)
  {
    var handler = new RejectedCodeHandler(error, status);
    var store = new AnonymousSessionStore();
    using var viewModel = new AuthViewModel(new AuthenticationService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        store, new UnusedPasskeyProvider()), store);

    viewModel.ApplyEmailOtpPrefill("a@example.com", "abcdefgh23");
    Assert.Empty(handler.Bodies);
    Assert.Equal("ABCDEFGH23", viewModel.Code);
    await viewModel.VerifyEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.Single(handler.Bodies);
    using var body = JsonDocument.Parse(handler.Bodies[0]);
    Assert.Equal("ABCDEFGH23", body.RootElement.GetProperty("token").GetString());
    Assert.Equal(error, viewModel.StatusMessage);
    Assert.False(viewModel.IsAuthenticated);
    Assert.False(viewModel.IsLoading);
  }

  [Theory]
  [InlineData("123456")]
  [InlineData("ABCDEFGH01")]
  public async Task MalformedPrefillDoesNotReachApiOnSubmit(string code)
  {
    var handler = new RejectedCodeHandler("unexpected", HttpStatusCode.BadRequest);
    var store = new AnonymousSessionStore();
    using var viewModel = new AuthViewModel(new AuthenticationService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        store, new UnusedPasskeyProvider()), store);
    viewModel.ApplyEmailOtpPrefill("a@example.com", code);
    await viewModel.VerifyEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.Empty(handler.Bodies);
    Assert.Equal("One-time code", viewModel.StatusMessage);
  }

  private sealed class RejectedCodeHandler(string error, HttpStatusCode status) : HttpMessageHandler
  {
    public List<string> Bodies { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
      return new HttpResponseMessage(status)
      {
        Content = new StringContent(JsonSerializer.Serialize(new { error = new { message = error } })),
        RequestMessage = request,
      };
    }
  }

  private sealed class UnusedPasskeyProvider : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
