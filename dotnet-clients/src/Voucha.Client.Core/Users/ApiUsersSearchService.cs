using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Users;

public sealed class ApiUsersSearchService(VouchaApiClient client) : IUsersSearchService
{
  private readonly VouchaApiClient client = client ?? throw new ArgumentNullException(nameof(client));
  public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default) => client.SearchUsersAsync(request, cancellationToken);
}
