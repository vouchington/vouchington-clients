using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class NativeSessionContractTests
{
  [Fact]
  public void PublishedLoginFixturesDecodeIntoTheirDtos()
  {
    var login = Decode<NativeEmailLoginAuthenticatedResponse>("native.auth.email-address.login.authenticated");
    Assert.Equal("user", login.User.EntityType);
    Assert.Null(login.User.AccountType);
    Assert.Equal(login.Sid, login.Session.Sid);
    Assert.Equal(login.Uid, login.St.Payload.Uid);
    Assert.Null(login.Dt.Payload.Sid);

    var mfa = Decode<NativeEmailLoginMfaResponse>("native.auth.email-address.login.mfa");
    Assert.True(mfa.MfaRequired);

    var tokens = Decode<NativeEmailTokenResponse>("native.auth.email-address.tokens");
    Assert.Equal("fixture@example.test", tokens.EmailAddress);
  }

  [Fact]
  public void PublishedMcpTokenFixtureDecodes()
  {
    var token = Decode<NativeSessionMcpTokenResponse>("native.oauth.session-mcp-token.ios");
    Assert.Equal("Bearer", token.TokenType);
    Assert.Equal(3600, token.ExpiresIn);
  }

  [Fact]
  public void MintSendsTheNativeMintHeader()
  {
    var request = VouchaApiEndpoints.MintNativeSessionMcpToken("macos");
    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal("1", request.Headers["x-voucha-native-mcp-mint"]);
  }

  [Fact]
  public void PublishedSessionsAndGrantsKeepMcpAttestationFields()
  {
    var sessions = Decode<AuthSessionListResponse>("native.auth.sessions.default");
    Assert.Empty(sessions.Results[0].McpConnections!);
    var connection = Assert.Single(sessions.Results[1].McpConnections!);
    Assert.Equal("attested", connection.AttestationStatus);
    Assert.Equal("Voucha iOS", connection.ClientName);

    var grant = Assert.Single(Decode<OAuthGrantListResponse>("native.my.oauth-grants.paginated").Results);
    Assert.Null(grant.SessionId);
    Assert.Null(grant.AttestationStatus);
  }

  private static T Decode<T>(string fixtureId) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;
}
