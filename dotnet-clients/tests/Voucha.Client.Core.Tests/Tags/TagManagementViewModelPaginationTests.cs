using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Tags;

public sealed partial class TagManagementViewModelTests
{
  [Fact]
  public async Task RelationContinuationRetriesTheSameCursorAndClearsTheGlobalError()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(PostResponseJson()),
        new RecordedResponse(PagedEntityRelationsJson("relation-1", "First", "next", true)),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError),
        new RecordedResponse(PagedEntityRelationsJson("relation-2", "Second", null, false)),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreRelationsAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasRelationPaginationError);
    Assert.NotNull(viewModel.ErrorMessage);

    await viewModel.LoadMoreRelationsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["relation-1", "relation-2"], viewModel.Relations.Select(row => row.Id));
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.HasRelationPaginationError);
    Assert.Equal(2, handler.Requests.Count(request =>
        request.PathAndQuery?.Contains("after=next", StringComparison.Ordinal) == true));
  }

  private static string PagedEntityRelationsJson(
      string relationId,
      string title,
      string? endCursor,
      bool hasNextPage) =>
      JsonSerializer.Serialize(new
      {
        results = new[] { new { id = relationId } },
        page_info = new { has_next_page = hasNextPage, end_cursor = endCursor },
        entity_relations = new Dictionary<string, object>
        {
          [relationId] = new
          {
            id = relationId,
            object_id = "topic-2",
            object_data = new { title },
            votes_score_net = 4.5,
          },
        },
      });
}
