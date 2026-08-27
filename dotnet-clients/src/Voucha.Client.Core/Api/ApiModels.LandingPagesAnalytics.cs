using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record LandingPageItemClickStats(
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("item_type")] string ItemType,
    [property: JsonPropertyName("click_count")] int ClickCount);

public sealed record LandingPageDailyStats(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("visits")] int Visits,
    [property: JsonPropertyName("clicks")] int Clicks,
    [property: JsonPropertyName("unique_visitors")] int UniqueVisitors);

public sealed record LandingPageUtmSourceStats(
    [property: JsonPropertyName("utm_source")] string UtmSource,
    [property: JsonPropertyName("visits")] int Visits);

public sealed record LandingPageConversionFunnel(
    [property: JsonPropertyName("total_visits")] int TotalVisits,
    [property: JsonPropertyName("total_clicks")] int TotalClicks,
    [property: JsonPropertyName("total_signups")] int TotalSignups,
    [property: JsonPropertyName("visit_to_click_rate")] double VisitToClickRate);

public sealed record LandingPageAnalytics(
    [property: JsonPropertyName("total_visits")] int TotalVisits,
    [property: JsonPropertyName("total_clicks")] int TotalClicks,
    [property: JsonPropertyName("ctr")] double Ctr,
    [property: JsonPropertyName("unique_visitors")] int UniqueVisitors,
    [property: JsonPropertyName("item_clicks")] IReadOnlyList<LandingPageItemClickStats> ItemClicks,
    [property: JsonPropertyName("daily_stats")] IReadOnlyList<LandingPageDailyStats> DailyStats,
    [property: JsonPropertyName("utm_sources")] IReadOnlyList<LandingPageUtmSourceStats> UtmSources,
    [property: JsonPropertyName("conversion_funnel")] LandingPageConversionFunnel ConversionFunnel);

public sealed record LandingPageAnalyticsResponse(
    [property: JsonPropertyName("analytics")] LandingPageAnalytics Analytics);

public sealed record AdminLandingPageAnalyticsResponse(
    [property: JsonPropertyName("landing_page")] LandingPage LandingPage,
    [property: JsonPropertyName("analytics")] LandingPageAnalytics Analytics);
