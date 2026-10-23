using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class NativeSessionTokenContractTests
{
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
