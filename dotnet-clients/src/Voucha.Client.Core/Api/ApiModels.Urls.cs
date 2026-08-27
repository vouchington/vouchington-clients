using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record Url(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("hostname")] Hostname? Hostname,
    [property: JsonPropertyName("url")] string UrlValue,
    [property: JsonPropertyName("pathname")] string? Pathname,
    [property: JsonPropertyName("canonical_url_id")] string? CanonicalUrlId = null,
    [property: JsonPropertyName("search_params")] IReadOnlyDictionary<string, string>? SearchParams = null);

public sealed record Crawl(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("url_id")] string? UrlId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("completed_at")] DateTimeOffset? CompletedAt,
    [property: JsonPropertyName("response_status_code")] int? ResponseStatusCode,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("meta_tags")] IReadOnlyDictionary<string, object>? MetaTags = null,
    [property: JsonPropertyName("lang")] string? Lang = null,
    [property: JsonPropertyName("request_headers")] IReadOnlyDictionary<string, string>? RequestHeaders = null,
    [property: JsonPropertyName("response_headers")] IReadOnlyDictionary<string, string>? ResponseHeaders = null,
    [property: JsonPropertyName("links")] IReadOnlyDictionary<string, object>? Links = null,
    [property: JsonPropertyName("crawler_id")] string? CrawlerId = null,
    [property: JsonPropertyName("embeddings_generated_at")] DateTimeOffset? EmbeddingsGeneratedAt = null,
    [property: JsonPropertyName("etag")] string? Etag = null,
    [property: JsonPropertyName("has_pending_embeddings")] bool? HasPendingEmbeddings = null,
    [property: JsonPropertyName("html_sha256")] JsonElement? HtmlSha256 = null,
    [property: JsonPropertyName("html_snapshot_uploaded_at")] DateTimeOffset? HtmlSnapshotUploadedAt = null,
    [property: JsonPropertyName("last_modified_at")] DateTimeOffset? LastModifiedAt = null,
    [property: JsonPropertyName("network_error")] object? NetworkError = null,
    [property: JsonPropertyName("redirect_url_id")] string? RedirectUrlId = null);

public sealed record UrlSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<Url> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record UrlDetailResponse(
    [property: JsonPropertyName("url")] Url Url,
    [property: JsonPropertyName("latest_crawl")] Crawl? LatestCrawl,
    [property: JsonPropertyName("can_view_latest_crawl")] bool CanViewLatestCrawl,
    [property: JsonPropertyName("can_view_crawl_history")] bool CanViewCrawlHistory,
    [property: JsonPropertyName("can_trigger_crawl")] bool CanTriggerCrawl,
    [property: JsonPropertyName("url_type")] string UrlType,
    [property: JsonPropertyName("rss_feed_id")] string? RssFeedId);

public sealed record UrlCrawlsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<Crawl> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record UrlCrawlResponse(
    [property: JsonPropertyName("crawl")] Crawl Crawl,
    [property: JsonPropertyName("og_image_sideload")] string? OgImageSideload);

public sealed record UrlCrawlTriggerResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("enqueued_count")] int EnqueuedCount,
    [property: JsonPropertyName("rss_feed_id")] string? RssFeedId = null);

#pragma warning restore CA1054, CA1056, CA1720
