using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public sealed class ApiDynamicConfigService(VouchaApiClient client) : IDynamicConfigService
{
  public Task<DynamicConfigNamespacesResponse> FetchNamespacesAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<DynamicConfigNamespacesResponse>(VouchaApiEndpoints.DynamicConfigNamespaces(), cancellationToken);

  public Task<DynamicConfigNamespaceResponse> FetchNamespaceAsync(string namespaceName, CancellationToken cancellationToken = default) =>
      client.SendAsync<DynamicConfigNamespaceResponse>(VouchaApiEndpoints.DynamicConfigNamespace(namespaceName), cancellationToken);

  public Task<DynamicConfigUpdateResponse> UpdateFieldAsync(string namespaceName, string field, DynamicConfigValue value, CancellationToken cancellationToken = default) =>
      client.SendAsync<DynamicConfigUpdateResponse>(VouchaApiEndpoints.UpdateDynamicConfigField(namespaceName, field, value), cancellationToken);

  public Task<DynamicConfigHistoryResponse> FetchHistoryAsync(string namespaceName, CancellationToken cancellationToken = default) =>
      client.SendAsync<DynamicConfigHistoryResponse>(VouchaApiEndpoints.DynamicConfigHistory(namespaceName), cancellationToken);
}
