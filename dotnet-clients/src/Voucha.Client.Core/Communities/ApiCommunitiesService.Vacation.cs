using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService
{
  public Task SetModeratorVacationAsync(string idOrSlug, DateTimeOffset? endsAt = null, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.SetCommunityModeratorVacation(idOrSlug, endsAt), cancellationToken);

  public Task SetSuppressCommunityDigestsWhileOnVacationAsync(string idOrSlug, bool suppress, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.SetSuppressCommunityDigestsWhileOnVacation(idOrSlug, suppress), cancellationToken);

  public Task ClearModeratorVacationAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ClearCommunityModeratorVacation(idOrSlug), cancellationToken);
}
