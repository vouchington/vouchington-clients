using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class AuthSessionMcpConnectionTests
{
  private const string Connection = """{"grant_id":"g","client_name":"Client","attestation_status":"attested","last_minted_at":"time"}""";
  private const string Session = """{"id":"s","device_id":"d","device_name":"Mac","user_agent":"Agent","ip_address":null,"created_at":"2026-07-06T12:00:00Z","last_seen_at":null,"expires_at":null,"is_current":true,"mcp_connections":[]}""";

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void EmptyAndPopulatedConnectionListsRoundTrip(bool populated)
  {
    var input = JsonNode.Parse(Session)!.AsObject();
    input["mcp_connections"] = populated ? new JsonArray(JsonNode.Parse(Connection)) : new JsonArray();
    var session = JsonSerializer.Deserialize<AuthSession>(input.ToJsonString(), VouchaApiJson.Options)!;
    Assert.Equal(populated ? 1 : 0, session.McpConnections.Count);
    using var output = JsonDocument.Parse(JsonSerializer.Serialize(session, VouchaApiJson.Options));
    Assert.Equal(populated ? 1 : 0, output.RootElement.GetProperty("mcp_connections").GetArrayLength());
    if (populated)
    {
      var connection = Assert.Single(session.McpConnections);
      Assert.Equal("g", connection.GrantId);
      Assert.Equal("Client", connection.ClientName);
      Assert.Equal("attested", connection.AttestationStatus);
      Assert.Equal("time", connection.LastMintedAt);
    }
  }

  [Fact]
  public void ConnectionListRejectsMissingNullAndNullItems()
  {
    var input = JsonNode.Parse(Session)!.AsObject();
    input.Remove("mcp_connections");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AuthSession>(input.ToJsonString(), VouchaApiJson.Options));
    input["mcp_connections"] = null;
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AuthSession>(input.ToJsonString(), VouchaApiJson.Options));
    input["mcp_connections"] = new JsonArray((JsonNode?)null);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AuthSession>(input.ToJsonString(), VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("grant_id")]
  [InlineData("client_name")]
  [InlineData("attestation_status")]
  [InlineData("last_minted_at")]
  public void ConnectionStringsRejectMissingAndNull(string key)
  {
    var input = JsonNode.Parse(Connection)!.AsObject();
    input.Remove(key);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AuthSessionMcpConnection>(input.ToJsonString(), VouchaApiJson.Options));
    input[key] = null;
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AuthSessionMcpConnection>(input.ToJsonString(), VouchaApiJson.Options));
  }
}
