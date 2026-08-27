using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public partial interface IEmailAddressService
{
  Task<EmailAddressListResponse> FetchEmailAddressesPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      FetchEmailAddressesAsync(cancellationToken);
}
