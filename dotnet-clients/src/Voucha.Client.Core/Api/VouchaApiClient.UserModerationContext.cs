namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<UserModerationContextResponse> FetchUserModerationContextAsync(
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserModerationContextResponse>(VouchaApiEndpoints.UserModerationContext(userId), cancellationToken);
}
