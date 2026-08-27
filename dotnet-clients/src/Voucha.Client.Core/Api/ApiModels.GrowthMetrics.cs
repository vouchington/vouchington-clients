using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public readonly record struct GrowthMetricsRange(string Value)
{
  public static GrowthMetricsRange Today { get; } = new("today");
  public static GrowthMetricsRange SevenDays { get; } = new("7d");
  public static GrowthMetricsRange ThirtyDays { get; } = new("30d");
  public static GrowthMetricsRange NinetyDays { get; } = new("90d");
  public static GrowthMetricsRange All { get; } = new("all");

  public static bool TryParse(string? value, out GrowthMetricsRange range)
  {
    range = value switch
    {
      "today" => Today,
      "7d" => SevenDays,
      "30d" => ThirtyDays,
      "90d" => NinetyDays,
      "all" => All,
      _ => default,
    };
    return !string.IsNullOrWhiteSpace(range.Value);
  }

  public override string ToString() => Value;
}

public sealed record GrowthMetricsResponse(
    [property: JsonPropertyName("range")] string Range,
    [property: JsonPropertyName("period_start")] DateTimeOffset PeriodStart,
    [property: JsonPropertyName("period_end")] DateTimeOffset PeriodEnd,
    [property: JsonPropertyName("user_growth")] UserGrowthMetrics UserGrowth,
    [property: JsonPropertyName("content_production")] ContentProductionMetrics ContentProduction,
    [property: JsonPropertyName("engagement")] EngagementMetrics Engagement,
    [property: JsonPropertyName("network_effects")] NetworkEffectsMetrics NetworkEffects,
    [property: JsonPropertyName("revenue")] RevenueMetrics Revenue,
    [property: JsonPropertyName("infrastructure")] InfrastructureMetrics Infrastructure);

public sealed record DailyDataPoint(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("count")] int Count);

public sealed record UserGrowthMetrics(
    [property: JsonPropertyName("total_users")] int TotalUsers,
    [property: JsonPropertyName("new_users")] int NewUsers,
    [property: JsonPropertyName("dau")] int Dau,
    [property: JsonPropertyName("mau")] int Mau,
    [property: JsonPropertyName("dau_mau_ratio")] double DauMauRatio,
    [property: JsonPropertyName("signups_over_time")] IReadOnlyList<DailyDataPoint> SignupsOverTime);

public sealed record ContentByType(
    [property: JsonPropertyName("review")] int Review,
    [property: JsonPropertyName("data_point")] int DataPoint,
    [property: JsonPropertyName("discussion")] int Discussion,
    [property: JsonPropertyName("comment")] int Comment,
    [property: JsonPropertyName("story")] int Story);

public sealed record ContentProductionMetrics(
    [property: JsonPropertyName("total_posts")] int TotalPosts,
    [property: JsonPropertyName("posts_by_type")] ContentByType PostsByType,
    [property: JsonPropertyName("contributions_per_active_user")] double ContributionsPerActiveUser,
    [property: JsonPropertyName("clearance_approval_rate")] double ClearanceApprovalRate,
    [property: JsonPropertyName("content_over_time")] IReadOnlyList<DailyDataPoint> ContentOverTime);

public sealed record EngagementMetrics(
    [property: JsonPropertyName("votes_cast")] int VotesCast,
    [property: JsonPropertyName("comments_created")] int CommentsCreated,
    [property: JsonPropertyName("follows_created")] int FollowsCreated,
    [property: JsonPropertyName("avg_follows_per_user")] double AvgFollowsPerUser,
    [property: JsonPropertyName("votes_over_time")] IReadOnlyList<DailyDataPoint> VotesOverTime,
    [property: JsonPropertyName("comments_over_time")] IReadOnlyList<DailyDataPoint> CommentsOverTime,
    [property: JsonPropertyName("follows_over_time")] IReadOnlyList<DailyDataPoint> FollowsOverTime);

public sealed record NetworkEffectsMetrics(
    [property: JsonPropertyName("referral_coefficient")] double ReferralCoefficient,
    [property: JsonPropertyName("topic_coverage_rate")] double TopicCoverageRate,
    [property: JsonPropertyName("landing_page_visits")] int LandingPageVisits,
    [property: JsonPropertyName("new_signups")] int NewSignups,
    [property: JsonPropertyName("signup_visit_ratio")] double SignupVisitRatio);

public sealed record RevenueMetrics(
    [property: JsonPropertyName("active_memberships")] int ActiveMemberships,
    [property: JsonPropertyName("memberships_by_tier")] IReadOnlyDictionary<string, int> MembershipsByTier,
    [property: JsonPropertyName("mrr_by_currency")] IReadOnlyList<ScaledMoneyAggregate> MrrByCurrency,
    [property: JsonPropertyName("upgrades")] int Upgrades,
    [property: JsonPropertyName("downgrades")] int Downgrades,
    [property: JsonPropertyName("cancellations")] int Cancellations,
    [property: JsonPropertyName("churn_rate")] double ChurnRate);

public sealed record InfrastructureMetrics(
    [property: JsonPropertyName("crawler_success_rate")] double? CrawlerSuccessRate,
    [property: JsonPropertyName("queue_throughput")] long? QueueThroughput,
    [property: JsonPropertyName("cache_hit_rate")] double? CacheHitRate,
    [property: JsonPropertyName("ai_token_usage")] long? AiTokenUsage);
