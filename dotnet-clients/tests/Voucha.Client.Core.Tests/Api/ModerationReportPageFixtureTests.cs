using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationReportPageFixtureTests
{
  [Fact]
  public void ClusteredReportPageFixturesDecodeRecurringSidecarAndDistinctCursors()
  {
    var first = Decode("native.moderation.reports.clustered.default");
    var second = Decode("native.moderation.reports.clustered.page-2");

    Assert.True(first.PageInfo.HasNextPage);
    Assert.False(first.PageInfo.HasPreviousPage);
    Assert.NotNull(first.PageInfo.EndCursor);
    Assert.NotEqual(first.PageInfo.StartCursor, first.PageInfo.EndCursor);
    Assert.False(second.PageInfo.HasNextPage);
    Assert.True(second.PageInfo.HasPreviousPage);
    Assert.NotNull(second.PageInfo.StartCursor);
    Assert.NotEqual(first.PageInfo.StartCursor, second.PageInfo.StartCursor);

    var firstSidecar = Assert.Single(first.DuplicateClusters);
    var secondSidecar = Assert.Single(second.DuplicateClusters);
    Assert.Equal(firstSidecar.Id, secondSidecar.Id);
    Assert.Equal(4, first.Clusters.Count);
    Assert.Equal(3, firstSidecar.Clusters.Count);
    Assert.Equal(3, second.Clusters.Count);
    Assert.Equal(3, secondSidecar.Clusters.Count);
    Assert.True(firstSidecar.Clusters.Select(cluster => cluster.Id).ToHashSet(StringComparer.Ordinal)
        .IsSubsetOf(first.Clusters.Select(cluster => cluster.Id)));
    Assert.True(secondSidecar.Clusters.Select(cluster => cluster.Id).ToHashSet(StringComparer.Ordinal)
        .IsSubsetOf(second.Clusters.Select(cluster => cluster.Id)));
    Assert.Empty(first.Clusters.Select(cluster => cluster.Id)
        .Intersect(second.Clusters.Select(cluster => cluster.Id), StringComparer.Ordinal));
    AssertSidecarMembersMatchResults(first);
    AssertSidecarMembersMatchResults(second);
  }

  private static StaffClusteredModerationReportsResponse Decode(string fixtureId) =>
      JsonSerializer.Deserialize<StaffClusteredModerationReportsResponse>(
          ApiFixtureLoader.LoadResponse(fixtureId),
          VouchaApiJson.Options)!;

  private static void AssertSidecarMembersMatchResults(StaffClusteredModerationReportsResponse page)
  {
    foreach (var member in page.DuplicateClusters.SelectMany(sidecar => sidecar.Clusters))
    {
      var result = page.Clusters.Single(cluster => cluster.Id == member.Id);
      Assert.Equal(JsonSerializer.Serialize(result, VouchaApiJson.Options),
          JsonSerializer.Serialize(member, VouchaApiJson.Options));
    }
  }
}
