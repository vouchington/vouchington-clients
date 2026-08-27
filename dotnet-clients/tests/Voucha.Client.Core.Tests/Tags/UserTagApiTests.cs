using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Tags;

public sealed class UserTagApiTests
{
  [Fact]
  public void UserTagsUsesCatalogEndpoint()
  {
    var request = VouchaApiEndpoints.UserTags();
    Assert.Equal("/api/v1/topics/user-tags", request.Path);
  }

  [Fact]
  public void UserTagCatalogDecodes()
  {
    const string Json = """{"user_tags":[{"id":"bot-id","slug":"bot","label":"Bot"}]}""";
    var response = JsonSerializer.Deserialize<UserTagTopicsResponse>(Json, VouchaApiJson.Options);
    Assert.Equal("Bot", Assert.Single(response!.UserTags).Label);
  }
}
