using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ReAuthContractTests
{
  private const string FlowId = "00000000-0000-7000-8000-000000000742";

  [Fact]
  public void MethodsFixtureReportsEveryAvailableConfirmationMethod()
  {
    var methods = Decode<ReAuthMethodsResponse>("native.auth.re-auth.methods.default");
    Assert.True(methods.HasEmail);
    Assert.True(methods.HasPasskey);
    Assert.True(methods.HasTotp);
    Assert.Equal(["google"], methods.Providers);
  }

  [Theory]
  [InlineData("native.auth.re-auth.email.verification")]
  [InlineData("native.auth.re-auth.totp.verification")]
  [InlineData("native.auth.re-auth.passkeys.verification")]
  [InlineData("native.auth.re-auth.providers.verification.google")]
  [InlineData("native.auth.re-auth.providers.proof.github")]
  public void EveryVerificationFixtureReturnsTheSameOneUseProof(string fixtureId) =>
      Assert.Equal("fresh-auth-fixture", Decode<ReAuthTokenResponse>(fixtureId).ReAuthToken);

  [Fact]
  public void PasskeyOptionsFixtureKeepsTheFlowAndAssertionOptions()
  {
    var response = Decode<ReAuthPasskeyOptionsResponse>("native.auth.re-auth.passkeys.options");
    Assert.Equal(FlowId, response.FlowId);
    Assert.Equal("voucha.ai", response.Options.RpId);
    Assert.Equal(60000, response.Options.Timeout);
    Assert.Equal("preferred", response.Options.UserVerification);
    var credential = Assert.Single(response.Options.AllowCredentials!);
    Assert.Equal("public-key", credential.Type);
    Assert.Equal(["internal"], credential.Transports);
  }

  [Fact]
  public void ProviderFixturesKeepTheNonceAndTheBrokerHandoff()
  {
    var challenge = Decode<ReAuthProviderChallengeResponse>("native.auth.re-auth.providers.challenge.google");
    Assert.Equal(FlowId, challenge.FlowId);
    Assert.Equal("fixture-provider-nonce", challenge.Nonce);
    Assert.Equal(new DateTimeOffset(2026, 7, 29, 22, 0, 0, TimeSpan.Zero), challenge.ExpiresAt);

    var authorization = Decode<BeginOAuthAuthorizationResponse>(
        "native.auth.re-auth.providers.authorization.github");
    Assert.Equal(FlowId, authorization.FlowId);
    Assert.Equal("github.com", authorization.RedirectUrl.Host);
  }

  [Fact]
  public void RotatedSessionFixtureCarriesBothReplacementTokensAndLifetimes()
  {
    var session = Decode<NativeSessionRefreshResponse>("native.auth.session.refresh.rotated").Session;
    Assert.Equal("fixture-rotated-device-jwt", session.Dt);
    Assert.Equal("fixture-rotated-session-jwt", session.St);
    Assert.Equal(2592000, session.Dte);
    Assert.Equal(172800, session.Ste);
    Assert.True(session.Secure);
    Assert.Equal(session.Sid, session.Session.Sid);
    Assert.Equal("native", session.Session.Client);
    Assert.Empty(session.Session.Rol);
  }

  [Fact]
  public void BotChallengeFixtureIsTheTypedRequiredBody()
  {
    var body = Decode<BotChallengeRequiredResponse>("native.bot-challenge.required");
    Assert.Equal(BotChallengeRequiredResponse.RequiredCode, body.Code);
    Assert.Equal("bot-challenge.mutation", body.Action);
    Assert.Equal("Bot challenge required", body.Message);
  }

  [Fact]
  public void ProviderAuthorizationRequestsTheNativeCallbackMode()
  {
    var request = VouchaApiEndpoints.BeginReAuthProviderAuthorization("github", "challenge");
    var body = Assert.IsType<ReAuthProviderAuthorizationBody>(request.Body);
    Assert.Equal("native", body.CallbackMode);
    Assert.Equal("/api/v1/auth/re-auth/providers/github/authorizations", request.Path);
  }

  [Fact]
  public void ProviderPathEscapesTheProviderSegment()
  {
    Assert.Equal(
        "/api/v1/auth/re-auth/providers/a%2Fb/challenges",
        VouchaApiEndpoints.ReAuthProviderChallenge("a/b").Path);
  }

  [Fact]
  public void RefreshPatchesTheSessionWithBothCurrentTokens()
  {
    var request = VouchaApiEndpoints.RefreshSession("device", "session");
    Assert.Equal(HttpMethod.Patch, request.Method);
    var body = Assert.IsType<RefreshSessionBody>(request.Body);
    Assert.Equal(("device", "session"), (body.Dt, body.St));
  }

  [Fact]
  public void AnonymousRotatedSessionDecodesWithoutAUser()
  {
    const string json = """
        {"session":{"did":"d","dt":"dt","dte":1,"secure":true,"sid":"s","st":"st","ste":1,"uid":null,
        "session":{"client":"native","did":"d","rca":0,"rol":[],"sca":0,"sid":"s","uid":null}}}
        """;
    var session = JsonSerializer.Deserialize<NativeSessionRefreshResponse>(json, VouchaApiJson.Options)!.Session;
    Assert.Null(session.Uid);
    Assert.Null(session.Session.Uid);
  }

  private static T Decode<T>(string fixtureId) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;
}
