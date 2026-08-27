using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record UpdateDisputeBody(
    [property: JsonPropertyName("public_response")] string? PublicResponse,
    [property: JsonPropertyName("internal_notes")] string? InternalNotes);

public sealed record ResolveDisputeBody(
    [property: JsonPropertyName("action")] ModerationDisputeResolutionAction Action,
    [property: JsonPropertyName("body_text")] string? BodyText);

public sealed record ModerationRevealBody(
    [property: JsonPropertyName("postId")] string? PostId,
    [property: JsonPropertyName("reportId")] string? ReportId,
    [property: JsonPropertyName("surface")] ModerationRevealSurface Surface);
