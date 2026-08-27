using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.IdentityVerificationAdministration;

public interface IIdentityVerificationAdministrationService
{
  Task<UserResponse> FetchUserAsync(string idOrSlug, CancellationToken cancellationToken = default);
  Task<GrantIdentityVerificationAttemptResponse> GrantAsync(
      string userId,
      string note,
      CancellationToken cancellationToken = default);
}
