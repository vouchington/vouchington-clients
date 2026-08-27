using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Tags;

public sealed class UserTagManagementViewModelTests
{
  [Fact]
  public async Task UserManagementLoadsVoteStateAndTogglesSelectedVoteOff()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(RelationsJson),
        new RecordedResponse(CatalogJson),
        new RecordedResponse("{}"),
        new RecordedResponse(RelationsJson.Replace("\"choice\": \"confirm\"", "\"choice\": \"neutral\"", StringComparison.Ordinal)),
        new RecordedResponse(CatalogJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("user", "user-2", "topic"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Manage user tags", viewModel.Title);
    Assert.Equal(new TagRelationRow("relation-1", "Bot", "user-tag-bot", 3, ElectionVoteChoice.Confirm), Assert.Single(viewModel.Relations));

    await viewModel.VoteRelationAsync("relation-1", ElectionVoteChoice.Confirm, TestContext.Current.CancellationToken);

    Assert.Equal("{\"choice\":\"confirm\"}", handler.Requests[2].Body);
  }

  private const string CatalogJson = """
      { "user_tags": [{ "id": "user-tag-bot", "slug": "bot", "label": "Bot" }] }
      """;

  private const string RelationsJson = """
      {
        "results": [{ "id": "relation-1" }],
        "page_info": { "has_next_page": false },
        "entity_relations": {
          "relation-1": {
            "id": "relation-1",
            "object_id": "user-tag-bot",
            "object_data": { "name": "Bot" },
            "votes_score_net": 3
          }
        },
        "election_votes": { "relation-1": { "choice": "confirm" } }
      }
      """;
}
