using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.MembershipAdministration;

public interface IMembershipAdministrationService
{
  Task<MembershipPlansResponse> FetchPlansAsync(CancellationToken cancellationToken = default);
  Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default);
  Task<GrantMembershipResponse> GrantAsync(GrantMembershipBody body, CancellationToken cancellationToken = default);
}
