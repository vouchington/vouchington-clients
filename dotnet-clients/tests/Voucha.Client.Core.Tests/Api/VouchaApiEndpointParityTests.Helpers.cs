using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  private static object[] Case(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody = false) => [name, request, method, path, query, hasBody];

  private static IReadOnlyDictionary<string, string> Query(params (string Key, string Value)[] items) =>
      items.ToDictionary(item => item.Key, item => item.Value);

  private static void AssertEndpoint(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    Assert.Equal(method, request.Method);
    Assert.Equal(path, request.Path);
    Assert.Equal(query, request.Query);
    Assert.Equal(hasBody, request.Body is not null);
    Assert.False(string.IsNullOrWhiteSpace(name));
  }
}
