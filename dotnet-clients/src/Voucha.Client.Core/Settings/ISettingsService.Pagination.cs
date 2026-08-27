using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public partial interface ISettingsService
{
  Task<AuthSessionListResponse> FetchAuthSessionsPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      FetchAuthSessionsAsync(cancellationToken);

  Task<ApiKeyListResponse> FetchApiKeysPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      FetchApiKeysAsync(cancellationToken);

  Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      FetchPushSubscriptionsAsync(cancellationToken);
}
