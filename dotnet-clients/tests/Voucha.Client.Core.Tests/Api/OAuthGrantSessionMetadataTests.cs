using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class OAuthGrantSessionMetadataTests
{
  private const string GrantJson = """
      {"id":"grant-1","client":{"id":"client-1","client_id":"app-1","client_name":"Agent","verified":true},
      "resource":"https://voucha.ai/api/v1/mcp","scopes":["mcp.user:read"],"consented_at":"2026-09-01T12:00:00Z",
      "last_used_at":null,"attestation_status":null,"session_id":null}
      """;

  [Theory]
  [InlineData(null, null)]
  [InlineData("verified", "session-1")]
  public void RequiredNullableMetadataRoundTrips(string? attestation, string? session)
  {
    var input = JsonNode.Parse(GrantJson)!.AsObject();
    input["attestation_status"] = attestation;
    input["session_id"] = session;
    var grant = JsonSerializer.Deserialize<OAuthGrant>(input.ToJsonString(), VouchaApiJson.Options)!;
    Assert.Equal(attestation, grant.AttestationStatus);
    Assert.Equal(session, grant.SessionId);
    using var output = JsonDocument.Parse(JsonSerializer.Serialize(grant, VouchaApiJson.Options));
    Assert.Equal(attestation, output.RootElement.GetProperty("attestation_status").GetString());
    Assert.Equal(session, output.RootElement.GetProperty("session_id").GetString());
    Assert.Equal(JsonValueKind.Null, output.RootElement.GetProperty("last_used_at").ValueKind);
  }

  [Theory]
  [InlineData("attestation_status")]
  [InlineData("session_id")]
  [InlineData("last_used_at")]
  public void MissingAndNonStringMetadataAreRejected(string key)
  {
    var input = JsonNode.Parse(GrantJson)!.AsObject();
    input.Remove(key);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OAuthGrant>(input.ToJsonString(), VouchaApiJson.Options));
    input[key] = 1;
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OAuthGrant>(input.ToJsonString(), VouchaApiJson.Options));
  }
}
