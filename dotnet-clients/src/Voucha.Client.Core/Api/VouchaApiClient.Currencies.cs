namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CurrenciesResponse> FetchCurrenciesAsync(
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CurrenciesResponse>(VouchaApiEndpoints.Currencies(after, limit), cancellationToken);
}
