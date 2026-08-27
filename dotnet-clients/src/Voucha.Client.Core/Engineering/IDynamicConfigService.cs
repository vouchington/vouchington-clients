using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public interface IDynamicConfigService
{
  Task<DynamicConfigNamespacesResponse> FetchNamespacesAsync(CancellationToken cancellationToken = default);
  Task<DynamicConfigNamespaceResponse> FetchNamespaceAsync(string namespaceName, CancellationToken cancellationToken = default);
  Task<DynamicConfigUpdateResponse> UpdateFieldAsync(string namespaceName, string field, DynamicConfigValue value, CancellationToken cancellationToken = default);
  Task<DynamicConfigHistoryResponse> FetchHistoryAsync(string namespaceName, CancellationToken cancellationToken = default);
}
