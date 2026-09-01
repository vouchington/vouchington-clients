using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiStoryEndpointParityTests
{
  [Theory]
  [MemberData(nameof(StoryEndpointCases))]
  public void StoryEndpointsUseExpectedRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  public static IEnumerable<object[]> StoryEndpointCases()
  {
    yield return Case(
        "createStoryPostFromStory",
        VouchaApiEndpoints.CreateStoryPostFromStory("story 1", "00000000-0000-4000-8000-000000000054"),
        HttpMethod.Post,
        "/api/v1/stories/story%201/discussions",
        Query(),
        true);

    yield return Case(
        "createLinkPostFromRssFeedItem",
        VouchaApiEndpoints.CreateLinkPostFromRssFeedItem("item 1"),
        HttpMethod.Post,
        "/api/v1/rss-feed-items/item%201/discussions",
        Query(),
        true);
  }

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
