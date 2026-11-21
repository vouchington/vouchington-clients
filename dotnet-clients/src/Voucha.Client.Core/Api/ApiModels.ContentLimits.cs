using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

/// <summary>Server-configured content caps from <c>GET /api/v1/content-limits</c>.</summary>
public sealed record ContentLimitsResponse(
    [property: JsonPropertyName("post_images_max")] int PostImagesMax,
    [property: JsonPropertyName("copyright_claimant_targets_max")] int CopyrightClaimantTargetsMax,
    [property: JsonPropertyName("follower_distribution_recipients_max")] int FollowerDistributionRecipientsMax);
