namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<PodcastEpisodeChaptersResponse> FetchPodcastEpisodeChaptersAsync(
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync<PodcastEpisodeChaptersResponse>(
          VouchaApiEndpoints.PodcastEpisodeChapters(rssFeedItemId),
          cancellationToken);

  public Task<PodcastPlaybackPositionResponse> FetchPodcastPlaybackPositionAsync(
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync<PodcastPlaybackPositionResponse>(
          VouchaApiEndpoints.PodcastPlaybackPosition(rssFeedItemId),
          cancellationToken);

  public Task UpdatePodcastPlaybackPositionAsync(
      string rssFeedItemId,
      double positionSeconds,
      bool completed,
      CancellationToken cancellationToken = default) =>
      SendAsync(
          VouchaApiEndpoints.UpdatePodcastPlaybackPosition(rssFeedItemId, positionSeconds, completed),
          cancellationToken);
}
