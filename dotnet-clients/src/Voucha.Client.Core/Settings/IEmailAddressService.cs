using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public partial interface IEmailAddressService
{
  Task<EmailAddressListResponse> FetchEmailAddressesAsync(CancellationToken cancellationToken = default);

  Task<EmailAddressRequestResponse> RequestEmailVerificationAsync(
      string emailAddress,
      CancellationToken cancellationToken = default);

  Task<EmailAddressListResponse> VerifyEmailAddressAsync(
      string emailAddress,
      string token,
      CancellationToken cancellationToken = default);
}
