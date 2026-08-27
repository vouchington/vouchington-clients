using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.MembershipAdministration;

public sealed class ApiMembershipAdministrationService(VouchaApiClient client) : IMembershipAdministrationService
{
  private readonly VouchaApiClient client = client ?? throw new ArgumentNullException(nameof(client));
  public Task<MembershipPlansResponse> FetchPlansAsync(CancellationToken cancellationToken = default) => client.FetchMembershipPlansAsync(cancellationToken);
  public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default) => client.SearchUsersAsync(request, cancellationToken);
  public Task<GrantMembershipResponse> GrantAsync(GrantMembershipBody body, CancellationToken cancellationToken = default) => client.GrantMembershipAsync(body, cancellationToken);
}
