using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static IReadOnlyDictionary<string, ApiRequest> WithNativeContentEndpoints(
      IReadOnlyDictionary<string, ApiRequest> existing) =>
      NativeContentFixtureRequests.AddTo(new Dictionary<string, ApiRequest>(existing, StringComparer.Ordinal));
}
