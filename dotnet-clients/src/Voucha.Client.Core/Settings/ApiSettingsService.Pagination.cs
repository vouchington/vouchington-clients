using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class ApiSettingsService
{
  public Task<AuthSessionListResponse> FetchAuthSessionsPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchAuthSessionsPageAsync(after, limit, cancellationToken);

  public Task<ApiKeyListResponse> FetchApiKeysPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchApiKeysPageAsync(after, limit, cancellationToken);

  public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchPushSubscriptionsPageAsync(after, limit, cancellationToken);
}
