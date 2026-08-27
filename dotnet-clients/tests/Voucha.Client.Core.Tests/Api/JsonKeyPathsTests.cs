using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class JsonKeyPathsTests
{
  [Fact]
  public void ExtractsNestedObjectPaths()
  {
    var paths = JsonKeyPaths.Extract("""{"community":{"slug":"test","metrics":{"member_count":3}}}""");

    Assert.Equal(
        new HashSet<string> { "community", "community.slug", "community.metrics", "community.metrics.member_count" },
        paths);
  }

  [Fact]
  public void ExtractsDynamicEntityMapKeysLiterally()
  {
    var paths = JsonKeyPaths.Extract("""{"posts":{"p1":{"author_id":"u1"},"p2":{"author_id":"u2"}}}""");

    Assert.Contains("posts.p1.author_id", paths);
    Assert.Contains("posts.p2.author_id", paths);
  }

  [Fact]
  public void ExtractsArrayItemFieldsWithoutIndexSuffix()
  {
    var paths = JsonKeyPaths.Extract("""{"results":[{"id":"a"},{"id":"b","extra":true}]}""");

    Assert.Equal(new HashSet<string> { "results", "results.id", "results.extra" }, paths);
  }

  [Fact]
  public void ScalarAndNullLeavesDoNotRecurseButPropertyPathIsRecorded()
  {
    var paths = JsonKeyPaths.Extract("""{"deleted_at":null,"count":1,"name":"x","active":true}""");

    Assert.Equal(new HashSet<string> { "deleted_at", "count", "name", "active" }, paths);
  }

  [Fact]
  public void EmptyObjectAndEmptyArrayProduceNoNestedPaths()
  {
    var paths = JsonKeyPaths.Extract("""{"empty_object":{},"empty_array":[]}""");

    Assert.Equal(new HashSet<string> { "empty_object", "empty_array" }, paths);
  }

  [Fact]
  public void TopLevelScalarProducesNoPaths()
  {
    var paths = JsonKeyPaths.Extract("42");

    Assert.Empty(paths);
  }
}
