using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

/// <summary>
/// Fixture-driven DTO field-completeness + manifest endpoint gates (#6773, #6886). Converts the
/// reactive "reviewer catches a missing response field" pattern (#6472, #6673, #6604, #6377)
/// into local red tests: every fixture in <see cref="ApiFixtureCoverage.Registry"/> must
/// round-trip every field through its DTO, and every manifest fixture must be represented by the
/// .NET endpoint registry.
/// </summary>
public sealed class ApiFixtureCoverageTests
{
  // Re-encodes with nulls preserved so a modeled-but-null fixture field (e.g. "deleted_at":
  // null) round-trips as null instead of being omitted and misread as a missing field.
  // Production code still uses VouchaApiJson.Options (WhenWritingNull) unchanged.
  private static readonly JsonSerializerOptions ReEncodeOptions = new(VouchaApiJson.Options)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  };

  public static IEnumerable<object[]> RegisteredFixtures() =>
      ApiFixtureCoverage.Registry.Keys.Select(id => new object[] { id });

  [Theory]
  [MemberData(nameof(RegisteredFixtures))]
  public void FixtureFieldsRoundTripThroughTheDto(string fixtureId)
  {
    var json = ApiFixtureLoader.LoadResponse(fixtureId);
    var dtoType = ApiFixtureCoverage.Registry[fixtureId];
    var dto = JsonSerializer.Deserialize(json, dtoType, VouchaApiJson.Options);
    var reEncoded = JsonSerializer.Serialize(dto, dtoType, ReEncodeOptions);

    var missing = JsonKeyPaths.Extract(json);
    missing.ExceptWith(JsonKeyPaths.Extract(reEncoded));
    if (ApiFixtureCoverage.IgnoredPaths.TryGetValue(fixtureId, out var ignored))
    {
      missing.ExceptWith(ignored);
    }

    Assert.True(
        missing.Count == 0,
        $"{fixtureId} ({dtoType.Name}) is missing fields the DTO drops on decode: " +
            string.Join(", ", missing.OrderBy(path => path, StringComparer.Ordinal)));
  }

  [Fact]
  public void EveryConsumedFixtureIsRegistered()
  {
    var consumed = new HashSet<string>(ApiFixtureLoader.DotnetCoreFixtureIdsWithBody, StringComparer.Ordinal);
    var registered = new HashSet<string>(ApiFixtureCoverage.Registry.Keys, StringComparer.Ordinal);

    var unaccounted = new HashSet<string>(consumed, StringComparer.Ordinal);
    unaccounted.ExceptWith(registered);
    Assert.True(
        unaccounted.Count == 0,
        "Fixtures consumed by dotnet-core but not in ApiFixtureCoverage.Registry: " +
            string.Join(", ", unaccounted.OrderBy(id => id, StringComparer.Ordinal)));
  }

  [Fact]
  public void EveryManifestFixtureIsRepresentedByTheEndpointRegistry()
  {
    var manifestIds = new HashSet<string>(ApiFixtureLoader.DotnetCoreRouteFixtures.Select(entry => entry.Id), StringComparer.Ordinal);
    var registryIds = new HashSet<string>(ApiFixtureCoverage.EndpointRegistry.Keys, StringComparer.Ordinal);

    var missing = new HashSet<string>(manifestIds, StringComparer.Ordinal);
    missing.ExceptWith(registryIds);

    Assert.True(
        missing.Count == 0,
        "Manifest fixtures missing from ApiFixtureCoverage.EndpointRegistry: " +
            string.Join(", ", missing.OrderBy(id => id, StringComparer.Ordinal)));

    var stale = new HashSet<string>(registryIds, StringComparer.Ordinal);
    stale.ExceptWith(ApiFixtureLoader.ManifestFixtures.Select(entry => entry.Id));

    Assert.True(
        stale.Count == 0,
        "ApiFixtureCoverage.EndpointRegistry has entries no longer present in the manifest: " +
            string.Join(", ", stale.OrderBy(id => id, StringComparer.Ordinal)));
  }

  [Theory]
  [MemberData(nameof(ManifestFixtureEntries))]
  public void ManifestFixtureRequestsMatchTheEndpointRegistry(object fixtureData)
  {
    var fixture = (ApiFixtureLoader.ApiFixtureManifestEntry)fixtureData;

    Assert.True(
        ApiFixtureCoverage.EndpointRegistry.TryGetValue(fixture.Id, out var request),
        $"{fixture.Id} is missing from ApiFixtureCoverage.EndpointRegistry.");

    Assert.Equal(fixture.Method, request.Method.Method);
    Assert.Equal(fixture.Path, request.Path);
    AssertQuerySubset(fixture.Id, fixture.Query, request.Query);

    if (fixture.RequestBody is { } expectedBody)
    {
      Assert.NotNull(request.Body);
      var actualBody = JsonSerializer.SerializeToElement(request.Body, request.Body.GetType(), VouchaApiJson.Options);

      Assert.True(
          JsonDeepEquals(expectedBody, actualBody),
          $"{fixture.Id} request body does not match the manifest.");
    }
  }

  public static IEnumerable<object[]> ManifestFixtureEntries()
  {
    var consumedIds = new HashSet<string>(
        ApiFixtureLoader.DotnetCoreRouteFixtures.Select(entry => entry.Id),
        StringComparer.Ordinal);
    return ApiFixtureLoader.ManifestFixtures
        .Where(entry => consumedIds.Contains(entry.Id))
        .Select(entry => new object[] { entry });
  }

  private static void AssertQuerySubset(
      string fixtureId,
      JsonElement? expectedQuery,
      IReadOnlyDictionary<string, string> requestQuery)
  {
    if (expectedQuery is null)
    {
      return;
    }

    foreach (var property in expectedQuery.Value.EnumerateObject())
    {
      Assert.True(
          requestQuery.TryGetValue(property.Name, out var actualValue),
          $"{fixtureId} is missing query parameter '{property.Name}'.");
      Assert.Equal(JsonScalarValue(property.Value), actualValue);
    }
  }

  private static string JsonScalarValue(JsonElement value) => value.ValueKind switch
  {
    JsonValueKind.String => value.GetString() ?? string.Empty,
    JsonValueKind.Number => value.GetRawText(),
    JsonValueKind.True => "true",
    JsonValueKind.False => "false",
    JsonValueKind.Null => string.Empty,
    _ => value.GetRawText(),
  };

  private static bool JsonDeepEquals(JsonElement left, JsonElement right)
  {
    if (left.ValueKind != right.ValueKind)
    {
      return false;
    }

    return left.ValueKind switch
    {
      JsonValueKind.Object => JsonObjectEquals(left, right),
      JsonValueKind.Array => JsonArrayEquals(left, right),
      JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
      JsonValueKind.Number => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal),
      JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
      JsonValueKind.Null or JsonValueKind.Undefined => true,
      _ => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal),
    };
  }

  private static bool JsonObjectEquals(JsonElement left, JsonElement right)
  {
    var rightProperties = right.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

    foreach (var property in left.EnumerateObject())
    {
      if (!rightProperties.TryGetValue(property.Name, out var rightValue) || !JsonDeepEquals(property.Value, rightValue))
      {
        return false;
      }
    }

    return true;
  }

  private static bool JsonArrayEquals(JsonElement left, JsonElement right)
  {
    var leftItems = left.EnumerateArray().ToList();
    var rightItems = right.EnumerateArray().ToList();

    if (leftItems.Count != rightItems.Count)
    {
      return false;
    }

    for (var index = 0; index < leftItems.Count; index++)
    {
      if (!JsonDeepEquals(leftItems[index], rightItems[index]))
      {
        return false;
      }
    }

    return true;
  }
}
