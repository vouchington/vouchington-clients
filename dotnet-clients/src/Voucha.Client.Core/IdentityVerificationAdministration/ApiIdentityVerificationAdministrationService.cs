using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.IdentityVerificationAdministration;

public sealed class ApiIdentityVerificationAdministrationService(VouchaApiClient client)
    : IIdentityVerificationAdministrationService
{
  private readonly VouchaApiClient client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<UserResponse> FetchUserAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchUserAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<GrantIdentityVerificationAttemptResponse> GrantAsync(
      string userId,
      string note,
      CancellationToken cancellationToken = default) =>
      client.GrantIdentityVerificationAttemptAsync(
          userId,
          new GrantIdentityVerificationAttemptBody(note),
          cancellationToken);
}
