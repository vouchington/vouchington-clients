using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class ApiEmailAddressService(VouchaApiClient client) : IEmailAddressService
{
  private readonly VouchaApiClient client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<EmailAddressListResponse> FetchEmailAddressesAsync(CancellationToken cancellationToken = default) =>
      client.FetchEmailAddressesAsync(cancellationToken);

  public Task<EmailAddressRequestResponse> RequestEmailVerificationAsync(
      string emailAddress,
      CancellationToken cancellationToken = default) =>
      client.RequestEmailAddressVerificationAsync(emailAddress, cancellationToken);

  public Task<EmailAddressListResponse> VerifyEmailAddressAsync(
      string emailAddress,
      string token,
      CancellationToken cancellationToken = default) =>
      client.VerifyEmailAddressAsync(emailAddress, token, cancellationToken);
}
