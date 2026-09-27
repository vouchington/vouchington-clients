using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static class StoryFixtureRequests
{
  private const string StoryId = "01950000-0000-7000-8000-000000000001";
  private const string PrimaryItemId = "01950000-0000-7000-8000-000000000010";
  private static string ContinuationCursor => JsonSerializer.Deserialize<StoryPageResponse>(
      ApiFixtureLoader.LoadResponse("native.stories.get.default"), VouchaApiJson.Options)?.PageInfo.EndCursor
      ?? throw new InvalidOperationException("The first story fixture must provide a continuation cursor.");

  public static Dictionary<string, ApiRequest> AddTo(Dictionary<string, ApiRequest> registry)
  {
    registry["native.stories.get.default"] = VouchaApiEndpoints.Story(StoryId, excludeItemId: PrimaryItemId, limit: 1);
    registry["native.stories.get.after"] = VouchaApiEndpoints.Story(StoryId, ContinuationCursor, PrimaryItemId);
    return registry;
  }
}
