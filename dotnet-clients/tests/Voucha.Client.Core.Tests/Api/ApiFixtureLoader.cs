using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static class ApiFixtureLoader
{
  private static readonly JsonSerializerOptions JsonOptions = VouchaApiJson.Options;

  private static readonly Lazy<ApiFixtureManifest> Manifest = new(() =>
  {
    var manifestPath = ContractPaths.ApiFixture("manifest.json");
    return JsonSerializer.Deserialize<ApiFixtureManifest>(
        File.ReadAllText(manifestPath),
        JsonOptions) ?? throw new InvalidOperationException("api-fixtures/v1/manifest.json is empty.");
  });

  internal static IReadOnlyList<ApiFixtureManifestEntry> ManifestFixtures => Manifest.Value.Fixtures;

  public static string LoadResponse(string fixtureId)
  {
    var fixture = Manifest.Value.Fixtures.Single(entry => entry.Id == fixtureId);
    if (!fixture.Consumers.Contains("dotnet-core", StringComparer.Ordinal))
    {
      throw new InvalidOperationException($"{fixtureId} is not marked as a dotnet-core fixture consumer.");
    }

    return File.ReadAllText(ContractPaths.ApiFixture(fixture.BodyFile));
  }

  public static string QueryValue(string fixtureId, string name) =>
      DotnetCoreRouteFixtures.Single(entry => entry.Id == fixtureId).Query[name];

  public static string RouteParameterValue(string fixtureId, string name)
  {
    var fixture = DotnetCoreRouteFixtures.Single(entry => entry.Id == fixtureId);
    var pathSegments = fixture.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var templateSegments = fixture.RouteTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var parameterIndex = Array.IndexOf(templateSegments, $":{name}");
    return parameterIndex >= 0
        ? pathSegments[parameterIndex]
        : throw new KeyNotFoundException($"{fixtureId} has no route parameter named {name}.");
  }

  /// <summary>
  /// Every fixture id this client consumes that has a response body (excludes 204/304
  /// no-content statuses). Feeds the DTO field-completeness coverage gate, which asserts each
  /// id is accounted for in <c>ApiFixtureCoverage.Registry</c>.
  /// </summary>
  public static IReadOnlyCollection<FixtureCoverageCandidate> DotnetCoreBodyFixtures =>
      DotnetCoreBodyFixturesIn(Manifest.Value.Fixtures);

  internal static IReadOnlyCollection<FixtureCoverageCandidate> DotnetCoreBodyFixturesIn(
      IEnumerable<ApiFixtureManifestEntry> entries) =>
      entries
          .Where(entry =>
              entry.Consumers.Contains("dotnet-core", StringComparer.Ordinal) &&
              entry.Status is not (204 or 304))
          .Select(entry => new FixtureCoverageCandidate(entry.Id, entry.Pending))
          .ToList();

  /// <summary>Parses a manifest document. A missing <c>pending</c> property means not pending.</summary>
  internal static IReadOnlyList<ApiFixtureManifestEntry> ParseManifest(string json) =>
      (JsonSerializer.Deserialize<ApiFixtureManifest>(json, JsonOptions)
          ?? throw new InvalidOperationException("Manifest is empty.")).Fixtures;

  public static IReadOnlyCollection<ApiFixtureEndpointEntry> DotnetCoreRouteFixtures =>
      DotnetCoreRouteFixturesIn(Manifest.Value.Fixtures);

  internal static IReadOnlyCollection<ApiFixtureEndpointEntry> DotnetCoreRouteFixturesIn(
      IEnumerable<ApiFixtureManifestEntry> entries) =>
      entries
          .Where(entry =>
              entry.Consumers.Contains("dotnet-core", StringComparer.Ordinal) &&
              entry.Route is not null)
          .Select(entry =>
          {
            var route = entry.Route ?? throw new InvalidOperationException(
                $"{entry.Id} matched DotnetCoreRouteFixtures without a route contract.");
            return new ApiFixtureEndpointEntry(
              entry.Id,
              new HttpMethod(entry.Method),
              entry.Path,
              QueryDictionary(entry.Query),
              entry.RequestBody is not null,
              entry.RequestBody is null ? null : JsonNode.Parse(entry.RequestBody.Value.GetRawText()),
              route.RouteTemplate,
              entry.Pending);
          })
          .ToList();

  private static IReadOnlyDictionary<string, string> QueryDictionary(JsonElement? query)
  {
    if (query is null)
    {
      return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    return query.Value.EnumerateObject().ToDictionary(
        property => property.Name,
        property => property.Value.GetString() ?? property.Value.ToString(),
        StringComparer.Ordinal);
  }

  private sealed record ApiFixtureManifest(
      [property: JsonPropertyName("fixtures")] IReadOnlyList<ApiFixtureManifestEntry> Fixtures);

  internal sealed record ApiFixtureManifestEntry(
      [property: JsonPropertyName("id")] string Id,
      [property: JsonPropertyName("method")] string Method,
      [property: JsonPropertyName("path")] string Path,
      [property: JsonPropertyName("query")] JsonElement? Query,
      [property: JsonPropertyName("requestBody")] JsonElement? RequestBody,
      [property: JsonPropertyName("route")] ApiFixtureRouteContract? Route,
      [property: JsonPropertyName("bodyFile")] string BodyFile,
      [property: JsonPropertyName("consumers")] IReadOnlyList<string> Consumers,
      [property: JsonPropertyName("status")] int Status,
      [property: JsonPropertyName("pending")] bool Pending = false);

  internal sealed record ApiFixtureRouteContract(
      [property: JsonPropertyName("routeTemplate")] string RouteTemplate);
}

internal sealed record ApiFixtureEndpointEntry(
    string Id,
    HttpMethod Method,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    bool HasBody,
    JsonNode? RequestBody,
    string RouteTemplate,
    bool Pending = false);
