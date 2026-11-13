using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiFixtureCoveragePolicyTests
{
  private const string Manifest = """
      {"fixtures": [
        {"id": "a.plain", "method": "GET", "path": "/a", "consumers": ["dotnet-core"], "status": 200,
         "bodyFile": "a.json", "route": {"routeTemplate": "/a"}},
        {"id": "b.pending", "method": "GET", "path": "/b", "consumers": ["dotnet-core"], "status": 200,
         "bodyFile": "b.json", "route": {"routeTemplate": "/b"}, "pending": true},
        {"id": "c.explicit-false", "method": "GET", "path": "/c", "consumers": ["dotnet-core"], "status": 200,
         "bodyFile": "c.json", "pending": false},
        {"id": "d.other-client", "method": "GET", "path": "/d", "consumers": ["swift-core"], "status": 200,
         "bodyFile": "d.json", "pending": true}
      ]}
      """;

  [Fact]
  public void ManifestDecodingTreatsMissingPendingAsFalse()
  {
    var entries = ApiFixtureLoader.ParseManifest(Manifest);

    Assert.Equal(new[] { false, true, false, true }, entries.Select(entry => entry.Pending));
  }

  [Fact]
  public void BodyAndRouteFixturesCarryThePendingFlag()
  {
    var entries = ApiFixtureLoader.ParseManifest(Manifest);

    Assert.Equal(
        new[]
        {
          new FixtureCoverageCandidate("a.plain", false),
          new FixtureCoverageCandidate("b.pending", true),
          new FixtureCoverageCandidate("c.explicit-false", false),
        },
        ApiFixtureLoader.DotnetCoreBodyFixturesIn(entries));
    Assert.Equal(
        new[] { ("a.plain", false), ("b.pending", true) },
        ApiFixtureLoader.DotnetCoreRouteFixturesIn(entries).Select(entry => (entry.Id, entry.Pending)));
  }

  [Fact]
  public void UnregisteredPendingFixtureDoesNotFailTheGate()
  {
    var candidates = ApiFixtureLoader.DotnetCoreBodyFixturesIn(ApiFixtureLoader.ParseManifest(Manifest));

    Assert.Empty(ApiFixtureCoveragePolicy.Unregistered(candidates, new[] { "a.plain", "c.explicit-false" }));
  }

  [Fact]
  public void UnregisteredNonPendingFixtureStillFailsTheGate()
  {
    var candidates = ApiFixtureLoader.DotnetCoreBodyFixturesIn(ApiFixtureLoader.ParseManifest(Manifest));

    Assert.Equal(
        new[] { "a.plain", "c.explicit-false" },
        ApiFixtureCoveragePolicy.Unregistered(candidates, new[] { "b.pending" }));
  }

  [Fact]
  public void RegisteredPendingFixtureIsNotExemptFromItsChecks()
  {
    var pending = new FixtureCoverageCandidate("b.pending", true);

    Assert.True(ApiFixtureCoveragePolicy.IsExemptPending(pending, Array.Empty<string>()));
    Assert.False(ApiFixtureCoveragePolicy.IsExemptPending(pending, new[] { "b.pending" }));
    Assert.False(ApiFixtureCoveragePolicy.IsExemptPending(new FixtureCoverageCandidate("a.plain", false), Array.Empty<string>()));
    Assert.Empty(ApiFixtureCoveragePolicy.Unregistered(new[] { pending }, new[] { "b.pending" }));
  }
}
