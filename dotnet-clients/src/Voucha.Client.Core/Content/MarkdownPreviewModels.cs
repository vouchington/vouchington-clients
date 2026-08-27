using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Content;

public sealed record MarkdownPreviewRequest(
    [property: JsonPropertyName("markdown")] string Markdown);

public sealed record MarkdownPreviewResponse(
    [property: JsonPropertyName("html")] string Html);
