namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<RssFeedItemResponse> FetchRssFeedItemAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<RssFeedItemResponse>(
          VouchaApiEndpoints.RssFeedItem(id),
          cancellationToken);
}
