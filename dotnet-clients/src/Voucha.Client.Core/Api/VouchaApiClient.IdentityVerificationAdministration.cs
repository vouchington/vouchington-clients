namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<GrantIdentityVerificationAttemptResponse> GrantIdentityVerificationAttemptAsync(
      string userId,
      GrantIdentityVerificationAttemptBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<GrantIdentityVerificationAttemptResponse>(
          VouchaApiEndpoints.GrantIdentityVerificationAttempt(userId, body), cancellationToken);
}
