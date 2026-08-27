namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest DynamicConfigNamespaces() => Get("/api/v1/dynamic-config/namespaces");

  public static ApiRequest DynamicConfigNamespace(string namespaceName) =>
      Get($"/api/v1/dynamic-config/namespaces/{Path(namespaceName)}");

  public static ApiRequest UpdateDynamicConfigField(string namespaceName, string field, DynamicConfigValue value) =>
      new(HttpMethod.Patch, $"/api/v1/dynamic-config/namespaces/{Path(namespaceName)}")
      {
        Body = new DynamicConfigPatchBody(new Dictionary<string, DynamicConfigValue>(StringComparer.Ordinal) { [field] = value }),
      };

  public static ApiRequest DynamicConfigHistory(string namespaceName) =>
      Get($"/api/v1/dynamic-config/namespaces/{Path(namespaceName)}/history");
}
