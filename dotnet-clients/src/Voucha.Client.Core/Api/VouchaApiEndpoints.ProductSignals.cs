using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record LandingPageVisitSignal(
    [property: JsonPropertyName("referrer")] string? Referrer = null,
    [property: JsonPropertyName("utm_source")] string? UtmSource = null,
    [property: JsonPropertyName("utm_medium")] string? UtmMedium = null,
    [property: JsonPropertyName("utm_campaign")] string? UtmCampaign = null,
    [property: JsonPropertyName("utm_content")] string? UtmContent = null);

public sealed record LandingPageClickSignal(
    [property: JsonPropertyName("landing_page_item_id")] string LandingPageItemId,
    [property: JsonPropertyName("group_member_id")] string? GroupMemberId = null);

public static partial class VouchaApiEndpoints
{
  public static ApiRequest LandingPageVisit(string landingPageId, LandingPageVisitSignal signal) =>
      new(HttpMethod.Post, $"/api/v1/landing-pages/{Path(landingPageId)}/visits") { Body = signal };

  public static ApiRequest LandingPageClick(string landingPageId, LandingPageClickSignal signal) =>
      new(HttpMethod.Post, $"/api/v1/landing-pages/{Path(landingPageId)}/clicks") { Body = signal };

  public static ApiRequest RssFeedView(string rssFeedId) =>
      new(HttpMethod.Post, $"/api/v1/rss-feeds/{Path(rssFeedId)}/views");

  public static ApiRequest RssFeedItemView(string itemId) =>
      new(HttpMethod.Post, $"/api/v1/rss-feed-items/{Path(itemId)}/views");
}
