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
  [Fact]
  public void PrivateMetricMapsPreserveCountValuesAndExplicitCapFlags()
  {
    const string json = """
        {
          "__entity_type": "user_metrics", "id": "user-1", "count": {},
          "private_count": { "posts_saved": 1000, "users_muted": 3 },
          "private_count_capped": { "posts_saved": true, "users_muted": false },
          "bookmarks": { "posts_saved": 1000 }, "bookmarkers": {},
          "bookmarks__updated_at": "2026-11-11T00:00:00Z"
        }
        """;
    var metrics = JsonSerializer.Deserialize<UserMetrics>(json, VouchaApiJson.Options)!;

    Assert.Equal(1000, metrics.PrivateCount!["posts_saved"]);
    Assert.Equal(3, metrics.PrivateCount["users_muted"]);
    Assert.True(metrics.PrivateCountCapped!["posts_saved"]);
    Assert.False(metrics.PrivateCountCapped["users_muted"]);
    Assert.Equal(metrics.PrivateCount.Keys.Order(), metrics.PrivateCountCapped.Keys.Order());
    Assert.Null(metrics.CountCapped);
    var encoded = JsonSerializer.SerializeToElement(metrics, VouchaApiJson.Options);
    Assert.False(encoded.TryGetProperty("bookmarks", out _));
    Assert.False(encoded.TryGetProperty("bookmarkers", out _));
    Assert.False(encoded.TryGetProperty("bookmarks__updated_at", out _));
  }

  [Fact]
  public void PublicResponsesOmitPrivateMetricsRatherThanInventingEmptyMaps()
  {
    var metrics = JsonSerializer.Deserialize<UserMetrics>(
        """{"id":"user-1","count":{},"count_capped":{"reviews":true}}""", VouchaApiJson.Options)!;

    Assert.Null(metrics.PrivateCount);
    Assert.Null(metrics.PrivateCountCapped);
    Assert.True(metrics.CountCapped!.Reviews);
  }
}
