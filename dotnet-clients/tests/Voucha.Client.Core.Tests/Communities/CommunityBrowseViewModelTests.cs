using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityBrowseViewModelTests
{
  [Fact]
  public async Task SearchAsyncTrimsQueryAndMapsRows()
  {
    var (viewModel, handler) = CreateViewModel(new RecordingHandler("""
        {
          "results": [
            { "id": "community-1" },
            { "id": "missing-community" }
          ],
          "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
          "communities": {
            "community-1": {
              "id": "community-1",
              "name": "Community One",
              "slug": "community-one",
              "markdown": "About",
              "visibility": "public",
              "member_roster_visibility": "members",
              "list_type": null,
              "member_invites_allowed_at": null,
              "post_approval_required_at": null,
              "allow_review_posts": true,
              "allow_data_point_posts": true,
              "created_by_id": "user-1",
              "created_at": "2026-07-01T00:00:00Z",
              "updated_at": "2026-07-01T00:00:00Z"
            }
          },
          "users": {
            "user-1": {
              "id": "user-1",
              "username": "owner",
              "roles": ["user"],
              "name": "Owner"
            }
          },
          "community_metrics": {
            "community-1": {
              "id": "community-1",
              "member_count": 7,
              "post_count": 3
            }
          }
        }
        """));

    viewModel.Query = "  test query  ";

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.HasError);
    Assert.Single(viewModel.Results);
    Assert.Equal("community-1", viewModel.Results[0].Id);
    Assert.Equal(7, viewModel.Results[0].MemberCount);
    Assert.Equal(3, viewModel.Results[0].PostCount);
    Assert.False(viewModel.Results[0].Archived);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("/api/v1/communities?q=test%20query", handler.PathAndQuery);
  }

  [Fact]
  public async Task SearchAsyncCapturesApiFailures()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordingHandler("""{"error":"nope"}""", HttpStatusCode.BadRequest));
    var changed = new List<string?>();
    viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    viewModel.Query = "test";

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.NotNull(viewModel.ErrorMessage);
    Assert.Empty(viewModel.Results);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("/api/v1/communities?q=test", handler.PathAndQuery);
    Assert.Contains(nameof(CommunityBrowseViewModel.HasError), changed);
  }

  [Fact]
  public async Task SearchAsyncReturnsIdleWhenCanceled()
  {
    var (viewModel, handler) = CreateViewModel(new RecordingHandler("""{"results":[],"page_info":{"end_cursor":null,"has_next_page":false,"start_cursor":null},"communities":{},"users":{},"community_metrics":{}}"""));
    var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await viewModel.SearchAsync(cancellationTokenSource.Token);

    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.HasError);
    Assert.Empty(viewModel.Results);
  }

  private static (CommunityBrowseViewModel ViewModel, RecordingHandler Handler) CreateViewModel(RecordingHandler handler)
  {
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));
    return (new CommunityBrowseViewModel(service), handler);
  }
}
