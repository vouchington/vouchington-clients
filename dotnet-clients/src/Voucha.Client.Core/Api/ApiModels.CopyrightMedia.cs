using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record PostImagePlacement(
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("placement_id")] string PlacementId,
    [property: JsonPropertyName("placement_revision")] int PlacementRevision,
    [property: JsonPropertyName("order_index")] int OrderIndex,
    [property: JsonPropertyName("caption")] string Caption);

public sealed record PostImagePlacementResponse(
    [property: JsonPropertyName("images")] IReadOnlyList<PostImagePlacement> Images);

public enum CopyrightImageSimilarityAvailability
{
  Available,
  Unavailable,
}

public sealed record CopyrightImageSimilarityCandidate(
    [property: JsonPropertyName("placement_id")] string PlacementId,
    [property: JsonPropertyName("placement_revision")] int PlacementRevision,
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("similarity")] double Similarity);

public sealed record CopyrightImageSimilarityCandidatesResponse(
    [property: JsonPropertyName("availability")] CopyrightImageSimilarityAvailability Availability,
    [property: JsonPropertyName("copyright_image_similarity_candidates")]
    IReadOnlyList<CopyrightImageSimilarityCandidate> CopyrightImageSimilarityCandidates);
