using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record PublicLandingPageResponse
{
  [JsonConstructor]
  public PublicLandingPageResponse(PublicLandingPageUser user, LandingPage landingPage)
  {
    User = user ?? throw new JsonException();
    // Owner list rows may omit items; the public response must always carry them.
    LandingPage = landingPage is { Items: not null } ? landingPage : throw new JsonException();
  }

  [JsonPropertyName("user"), JsonRequired]
  public PublicLandingPageUser User { get; init; }

  [JsonPropertyName("landing_page"), JsonRequired]
  public LandingPage LandingPage { get; init; }
}

public sealed record PublicLandingPageUser(
    [property: JsonPropertyName("id"), JsonRequired] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("display_name"), JsonRequired] string DisplayName);

public sealed partial class VouchaApiClient
{
  public Task<PublicLandingPageResponse> FetchPublicLandingPageAsync(
      string username, string? slug = null, CancellationToken cancellationToken = default) =>
      SendAsync<PublicLandingPageResponse>(VouchaApiEndpoints.PublicLandingPage(username, slug), cancellationToken);
}

public static partial class VouchaApiEndpoints
{
  public static ApiRequest PublicLandingPage(string username, string? slug = null) =>
      Get(slug is null
          ? $"/api/v1/users/{Path(username)}/landing-page"
          : $"/api/v1/users/{Path(username)}/landing-pages/{Path(slug)}");
}
