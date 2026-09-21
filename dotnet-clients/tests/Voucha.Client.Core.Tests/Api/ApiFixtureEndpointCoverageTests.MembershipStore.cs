using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static IReadOnlyDictionary<string, ApiRequest> WithMembershipStoreEndpoints(
      IReadOnlyDictionary<string, ApiRequest> existing) =>
      MembershipStoreFixtureRequests.AddTo(new Dictionary<string, ApiRequest>(existing, StringComparer.Ordinal));
}
