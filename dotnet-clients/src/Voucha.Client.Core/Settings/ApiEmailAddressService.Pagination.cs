using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class ApiEmailAddressService
{
  public Task<EmailAddressListResponse> FetchEmailAddressesPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchEmailAddressesPageAsync(after, limit, cancellationToken);
}
