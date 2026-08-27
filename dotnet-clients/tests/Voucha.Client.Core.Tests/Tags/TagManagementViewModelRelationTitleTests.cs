using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Tags;

public sealed class TagManagementViewModelRelationTitleTests
{
  [Fact]
  public async Task LoadAsyncUsesReadableLabelsFromEntityRelationObjectData()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(PostResponseJson()),
        new RecordedResponse(EntityRelationsWithReadableObjectDataJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "post"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        [
          new TagRelationRow("relation-1", "Related Topic", "topic-2", 4.5),
          new TagRelationRow("relation-2", "Related Post", "post-2", 3.0),
          new TagRelationRow("relation-3", "https://example.com/related-url", "url-2", 2.0),
        ],
        viewModel.Relations);
  }

  private static string PostResponseJson() =>
      """
      {
        "post": {
          "id": "post-1",
          "post_type": "review",
          "title": "Review Post",
          "markdown": "Body",
          "created_by_id": "user-1"
        }
      }
      """;

  private static string EntityRelationsWithReadableObjectDataJson() =>
      """
      {
        "results": [
          { "__entity_type": "entity_relation", "id": "relation-1" },
          { "__entity_type": "entity_relation", "id": "relation-2" },
          { "__entity_type": "entity_relation", "id": "relation-3" }
        ],
        "page_info": { "has_next_page": false },
        "entity_relations": {
          "relation-1": {
            "id": "relation-1",
            "object_data": {
              "name": "Related Topic"
            },
            "object_id": "topic-2",
            "votes_score_net": 4.5
          },
          "relation-2": {
            "id": "relation-2",
            "object_data": {
              "title": "Related Post"
            },
            "object_id": "post-2",
            "votes_score_net": 3
          },
          "relation-3": {
            "id": "relation-3",
            "object_data": {
              "url": "https://example.com/related-url"
            },
            "object_id": "url-2",
            "votes_score_net": 2
          }
        }
      }
      """;
}
