using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  [Theory]
  [MemberData(nameof(RegisteredFixtureIds))]
  public void RegisteredFixtureRoutesMatchDotnetEndpointBuilders(string fixtureId)
  {
    var fixture = ApiFixtureLoader.DotnetCoreRouteFixtures.Single(entry => entry.Id == fixtureId);
    var request = Registry[fixtureId];

    Assert.Equal(fixture.Method, request.Method);
    Assert.Equal(fixture.Path, request.Path);
    Assert.Equal(fixture.HasBody, request.Body is not null);
    Assert.True(
        JsonNode.DeepEquals(fixture.RequestBody, RequestBodyJson(request)),
        $"{fixtureId} request body mismatch. Expected {fixture.RequestBody?.ToJsonString() ?? "<none>"}.");
    Assert.Equal(fixture.Query, request.Query);
  }

  [Fact]
  public void EveryDotnetRouteFixtureIsRegistered()
  {
    var consumed = new HashSet<string>(
        ApiFixtureLoader.DotnetCoreRouteFixtures.Select(entry => entry.Id),
        StringComparer.Ordinal);
    var accountedFor = new HashSet<string>(Registry.Keys, StringComparer.Ordinal);

    var unaccounted = new HashSet<string>(consumed, StringComparer.Ordinal);
    unaccounted.ExceptWith(accountedFor);
    Assert.True(
        unaccounted.Count == 0,
        "Route-bearing fixtures consumed by dotnet-core need endpoint coverage: " +
            string.Join(", ", unaccounted.OrderBy(id => id, StringComparer.Ordinal)));

    var stale = new HashSet<string>(Registry.Keys, StringComparer.Ordinal);
    stale.ExceptWith(consumed);
    Assert.True(
        stale.Count == 0,
        "ApiFixtureEndpointCoverageTests.Registry has entries no longer consumed by dotnet-core: " +
            string.Join(", ", stale.OrderBy(id => id, StringComparer.Ordinal)));
  }

  public static IEnumerable<object[]> RegisteredFixtureIds() =>
      Registry.Keys.Select(id => new object[] { id });

  private static JsonNode? RequestBodyJson(ApiRequest request) =>
      request.Body is null ? null : JsonSerializer.SerializeToNode(request.Body, VouchaApiJson.Options);
}
