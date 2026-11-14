namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<HackerNewsConfigResponse> FetchHackerNewsConfigAsync(CancellationToken cancellationToken = default) =>
      SendAsync<HackerNewsConfigResponse>(VouchaApiEndpoints.HackerNewsConfig(), cancellationToken);
}
