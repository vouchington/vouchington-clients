using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PostCategoryInputTests
{
  [Fact]
  public void CreateAndUpdateBodiesEncodeTypedTopicAndHashtagCategories()
  {
    var categories = new PostCategoryInput[]
    {
      new TopicPostCategoryInput("topic-1"),
      new HashtagPostCategoryInput("#Travel.Deals__2026"),
    };

    var create = JsonSerializer.Serialize(
        new CreatePostBody("discussion", "Title", "Body", Categories: categories),
        VouchaApiJson.Options);
    var update = JsonSerializer.Serialize(
        new UpdatePostBody(Categories: categories),
        VouchaApiJson.Options);

    const string expectedCategories = "\"categories\":[{\"type\":\"topic\",\"topic_id\":\"topic-1\"},{\"type\":\"hashtag\",\"hashtag\":\"#Travel.Deals__2026\"}]";
    Assert.Contains(expectedCategories, create, StringComparison.Ordinal);
    Assert.Equal($"{{{expectedCategories}}}", update);
  }
}
