using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class UserMetricsPresenceTests
{
  [Fact]
  public void SparseViewerCountsPreserveExplicitZeroAndMissingFields()
  {
    const string json = """
        {
          "__entity_type": "user_metrics",
          "id": "user-1",
          "count": {
            "reviews": 4,
            "discussions": 3,
            "comments": 2,
            "topics_following": 7
          },
          "viewer_count": {
            "reviews": 0
          }
        }
        """;

    var metrics = JsonSerializer.Deserialize<UserMetrics>(json, VouchaApiJson.Options);

    Assert.NotNull(metrics);
    Assert.Equal(0, metrics.ViewerCount?.Reviews);
    Assert.Null(metrics.ViewerCount?.Discussions);
    Assert.Null(metrics.ViewerCount?.Comments);
    Assert.Equal(7, metrics.Count.TopicsFollowing);
  }
}
