using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithStoryEndpoints(this Dictionary<string, ApiRequest> registry) =>
      StoryFixtureRequests.AddTo(registry);
}
