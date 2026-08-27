using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record PodcastEpisodeChapter(
    [property: JsonPropertyName("start_seconds")] double StartSeconds,
    [property: JsonPropertyName("end_seconds")] double? EndSeconds,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("image_url")] Uri? ImageUrl = null,
    [property: JsonPropertyName("is_visible")] bool IsVisible = true);

public sealed record PodcastPlaybackPosition(
    [property: JsonPropertyName("position_seconds")] double PositionSeconds,
    [property: JsonPropertyName("completed_at")] DateTimeOffset? CompletedAt)
{
  public bool IsCompleted => CompletedAt is not null;
}

public sealed record PodcastEpisodeChaptersResponse(
    [property: JsonPropertyName("chapters")] IReadOnlyList<PodcastEpisodeChapter> Chapters);

public sealed record PodcastPlaybackPositionResponse(
    [property: JsonPropertyName("playback_position")] PodcastPlaybackPosition? PlaybackPosition);
