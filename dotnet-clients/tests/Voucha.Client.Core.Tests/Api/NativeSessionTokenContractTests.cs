using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class NativeSessionTokenContractTests
{
  [Fact]
  public void PublishedNativeResponsesPreserveIdentityAndTokenFields()
  {
    var email = JsonSerializer.Deserialize<NativeEmailTokenResponse>(
        ApiFixtureLoader.LoadResponse("native.auth.email-address.tokens"), VouchaApiJson.Options)!;
    Assert.Equal("fixture@example.test", email.EmailAddress);

    var mfa = JsonSerializer.Deserialize<NativeEmailMfaResponse>(
        ApiFixtureLoader.LoadResponse("native.auth.email-address.login.mfa"), VouchaApiJson.Options)!;
    Assert.Equal("00000000-0000-7000-8000-000000000731", mfa.LoginAttemptId);
    Assert.True(mfa.MfaRequired);
    using var mfaJson = JsonDocument.Parse(JsonSerializer.Serialize(mfa, VouchaApiJson.Options));
    Assert.True(mfaJson.RootElement.GetProperty("mfa_required").GetBoolean());

    var login = JsonSerializer.Deserialize<NativeEmailAuthenticatedResponse>(
        ApiFixtureLoader.LoadResponse("native.auth.email-address.login.authenticated"), VouchaApiJson.Options)!;
    Assert.Equal("00000000-0000-7000-8000-000000000732", login.Did);
    Assert.Equal("00000000-0000-7000-8000-000000000733", login.Sid);
    Assert.Equal("00000000-0000-7000-8000-000000000701", login.Uid);
    Assert.Equal(login.Did, login.Dt.Payload.Did);
    Assert.Equal("fixture-device-jwt", login.Dt.Token);
    Assert.Equal(login.Did, login.Session.Did);
    Assert.Equal(login.Sid, login.Session.Sid);
    Assert.Equal(login.Uid, login.Session.Uid);
    Assert.Equal(login.Did, login.St.Payload.Did);
    Assert.Equal(login.Sid, login.St.Payload.Sid);
    Assert.Equal(login.Uid, login.St.Payload.Uid);
    Assert.Equal("fixture-session-jwt", login.St.Token);
    Assert.Equal(login.Uid, login.User.Id);
    Assert.Equal(NativeAuthenticatedEntityType.User, login.User.EntityType);
    using var loginJson = JsonDocument.Parse(JsonSerializer.Serialize(login, VouchaApiJson.Options));
    Assert.Equal("fixture-session-jwt", loginJson.RootElement.GetProperty("st").GetProperty("token").GetString());
    Assert.Equal(login.Sid, loginJson.RootElement.GetProperty("session").GetProperty("sid").GetString());

    var token = JsonSerializer.Deserialize<NativeSessionMcpTokenResponse>(
        ApiFixtureLoader.LoadResponse("native.oauth.session-mcp-token.ios"), VouchaApiJson.Options)!;
    Assert.Equal("voucha_access_fixture-secret", token.AccessToken);
    Assert.Equal(3600, token.ExpiresIn);
    Assert.Equal("https://voucha.ai/api/v1/mcp", token.Resource);
    Assert.Equal("mcp.user:read mcp.user:write", token.Scope);
    Assert.Equal(NativeSessionTokenType.Bearer, token.TokenType);
    using var tokenJson = JsonDocument.Parse(JsonSerializer.Serialize(token, VouchaApiJson.Options));
    Assert.Equal("Bearer", tokenJson.RootElement.GetProperty("token_type").GetString());
    Assert.Equal(token.AccessToken, tokenJson.RootElement.GetProperty("access_token").GetString());
  }

  [Fact]
  public void LiteralValuesAndRequiredFieldsAreValidated()
  {
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeEmailMfaResponse>(
        """{"login_attempt_id":"a","mfa_required":false}""", VouchaApiJson.Options));
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeEmailAuthenticatedUser>(
        """{"id":"u","__entity_type":"post"}""", VouchaApiJson.Options));
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSessionMcpTokenResponse>(
        """{"access_token":"t","expires_in":3600,"resource":"mcp","scope":"read","token_type":"Basic"}""", VouchaApiJson.Options));
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSessionMcpTokenResponse>(
        """{"expires_in":3600,"resource":"mcp","scope":"read","token_type":"Bearer"}""", VouchaApiJson.Options));
  }

  [Fact]
  public void LiteralNamesAreCaseSensitive()
  {
    Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new NativeEmailMfaResponse("a", false), VouchaApiJson.Options));
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeAuthenticatedEntityType>("\"USER\"", VouchaApiJson.Options));
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSessionTokenType>("\"bearer\"", VouchaApiJson.Options));
    Assert.Throws<JsonException>(() => JsonSerializer.Serialize((NativeSessionTokenType)99, VouchaApiJson.Options));
  }

  [Fact]
  public void SparseAuthenticatedUserPreservesNullAccountType()
  {
    const string json = """{"id":"u","__entity_type":"user","account_type":null,"email_address":"a@example.test","roles":[]}""";
    var user = JsonSerializer.Deserialize<NativeEmailAuthenticatedUser>(json, VouchaApiJson.Options)!;
    Assert.Equal(NativeAuthenticatedEntityType.User, user.EntityType);
    Assert.Equal("a@example.test", user.EmailAddress);
    using var output = JsonDocument.Parse(JsonSerializer.Serialize(user, VouchaApiJson.Options));
    Assert.Equal(JsonValueKind.Null, output.RootElement.GetProperty("account_type").ValueKind);
  }

  [Fact]
  public void MintEndpointRetainsExplicitHeaderAndNativeClientBody()
  {
    var endpoint = VouchaApiEndpoints.MintNativeSessionMcpToken("ios");
    Assert.Equal(HttpMethod.Post, endpoint.Method);
    Assert.Equal("/api/v1/oauth/native-session-tokens", endpoint.Path);
    Assert.Equal("1", endpoint.Headers["x-voucha-native-mcp-mint"]);
    Assert.Equal("""{"client_app":"ios"}""", JsonSerializer.Serialize(endpoint.Body, VouchaApiJson.Options));
  }
}
