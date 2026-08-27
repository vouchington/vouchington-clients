using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Users;

public interface IUsersSearchService
{
  Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default);
}
