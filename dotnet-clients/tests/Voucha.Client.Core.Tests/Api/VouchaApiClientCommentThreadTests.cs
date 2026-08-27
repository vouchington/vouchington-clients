using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientCommentThreadTests
{
  [Fact]
  public void PostResponseDeserializesDetailSidecars()
  {
    const string Json = """
        {
          "post": {
            "id": "root-1",
            "post_type": "discussion",
            "title": "Root",
            "markdown": "Root body",
            "created_by_id": "user-1",
            "created_by": {
              "id": "user-1",
              "username": "owner",
              "markdown": "Owner"
            },
            "deleted_at": null,
            "locked_at": "2026-06-28T10:00:00Z",
            "locked_by_id": "mod-1",
            "can_edit_content": true,
            "can_delete": true,
            "can_lock": true
          },
          "html": "<p>Root body</p>",
          "post_metrics": {
            "__entity_type": "post_metrics",
            "id": "root-1",
            "count": { "descendants": 2, "ancestors": 0 }
          },
          "post_election": {
            "__entity_type": "post_election",
            "id": "root-1",
            "votes_score_net": 4,
            "votes_count_up": 5,
            "votes_count_down": 1
          },
          "bookmarks": { "root-1": { "save": true } }
        }
        """;

    var response = JsonSerializer.Deserialize<PostResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal("owner", response!.Post.CreatedBy?.Username);
    Assert.Equal("mod-1", response.Post.LockedById);
    Assert.True(response.Post.CanEditContent);
    Assert.Equal("<p>Root body</p>", response.Html);
    Assert.Equal(2, response.PostMetrics!.Count.Descendants);
    Assert.True(response.Bookmarks!["root-1"]["save"]);
  }

  [Fact]
  public void PostThreadResponseDeserializesThreadSidecars()
  {
    const string Json = """
        {
          "results": [
            { "entity_id": "comment-1" },
            { "entity_id": "comment-2" }
          ],
          "page_info": { "has_next_page": false },
          "posts": {
            "comment-1": {
              "id": "comment-1",
              "post_type": "comment",
              "title": null,
              "markdown": "First reply",
              "created_by_id": "user-1",
              "parent_id": "root-1",
              "root_id": "root-1",
              "created_at": "2026-06-28T10:01:00Z"
            },
            "comment-2": {
              "id": "comment-2",
              "post_type": "comment",
              "title": null,
              "markdown": "Nested reply",
              "created_by_id": "user-2",
              "parent_id": "comment-1",
              "root_id": "root-1",
              "created_at": "2026-06-28T10:02:00Z"
            }
          },
          "users": {
            "user-1": {
              "id": "user-1",
              "username": "owner",
              "markdown": "Owner"
            }
          },
          "communities": {},
          "posts_metrics": {
            "comment-1": {
              "id": "comment-1",
              "count": { "descendants": 1, "ancestors": 1 }
            }
          },
          "post_elections": {
            "comment-1": {
              "id": "comment-1",
              "votes_score_net": 3,
              "votes_count_up": 4,
              "votes_count_down": 1
            }
          },
          "election_votes": {
            "comment-1": {
              "__entity_type": "election_vote",
              "entity_id": "comment-1",
              "user_id": "user-1",
              "choice": "like",
              "created_at": "2026-06-28T10:00:00Z"
            }
          },
          "markdown_to_html": {
            "comment-1": "<p>First reply</p>"
          },
          "bookmarks": {
            "comment-1": { "save": true }
          }
        }
        """;

    var response = JsonSerializer.Deserialize<PostThreadResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal("comment-1", response!.Results[0].EntityId);
    Assert.Equal(1, response.PostsMetrics!["comment-1"].Count.Descendants);
    Assert.Equal(3, response.PostElections!["comment-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Like, response.ElectionVotes!["comment-1"].Choice);
    Assert.Equal("<p>First reply</p>", response.MarkdownToHtml!["comment-1"]);
    Assert.True(response.Bookmarks!["comment-1"]["save"]);
  }

  [Fact]
  public async Task ApiPostsServiceTargetsCommentThreadAndMutationEndpoints()
  {
    var responses = new[]
    {
      new RecordedResponse(
          """
          {
            "post": {
              "id": "root-1",
              "post_type": "discussion",
              "title": "Root",
              "markdown": "Root body",
              "created_by_id": "user-1"
            },
            "html": "<p>Root body</p>"
          }
          """),
      new RecordedResponse("""
          {
            "results": [{ "entity_id": "comment-1" }],
            "page_info": { "has_next_page": false },
            "posts": {
              "comment-1": {
                "id": "comment-1",
                "post_type": "comment",
                "title": null,
                "markdown": "Reply",
                "created_by_id": "user-1",
                "parent_id": "root-1",
                "root_id": "root-1"
              }
            },
            "users": {},
            "communities": {},
            "posts_metrics": {},
            "markdown_to_html": {}
          }
          """),
      new RecordedResponse("""
          {
            "results": [{ "entity_id": "root-1" }],
            "page_info": { "has_next_page": false },
            "posts": {
              "root-1": {
                "id": "root-1",
                "post_type": "discussion",
                "title": "Root",
                "markdown": "Root body",
                "created_by_id": "user-1"
              }
            },
            "users": {},
            "communities": {},
            "posts_metrics": {},
            "markdown_to_html": {}
          }
          """),
      new RecordedResponse("{}", HttpStatusCode.OK),
      new RecordedResponse("{}", HttpStatusCode.OK),
      new RecordedResponse("{}", HttpStatusCode.OK),
      new RecordedResponse("{}", HttpStatusCode.OK),
      new RecordedResponse("{}", HttpStatusCode.OK),
      new RecordedResponse("{}", HttpStatusCode.OK),
    };
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiPostsService(client);

    var detail = await service.FetchPostAsync("root-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/posts/root-1", handler.PathAndQuery);
    Assert.Equal("<p>Root body</p>", detail.Html);

    var descendants = await service.FetchPostDescendantsAsync("root-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/posts/root-1/descendants", handler.PathAndQuery);
    Assert.Equal("comment-1", descendants.Posts.Keys.Single());

    var ancestors = await service.FetchPostAncestorsAsync("comment-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/posts/comment-1/ancestors", handler.PathAndQuery);
    Assert.Equal("root-1", ancestors.Results[0].EntityId);

    await service.LockPostAsync("root-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/posts/root-1/lock", handler.PathAndQuery);

    await service.UnlockPostAsync("root-1", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);

    await service.BookmarkPostAsync("comment-1", cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/bookmarks/post/comment-1/save", handler.PathAndQuery);

    await service.UnbookmarkPostAsync("comment-1", cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);

    await service.ReportAsync(
        new ReportBody("post", "comment-1", "spam", "details", "turnstile"),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/reports", handler.PathAndQuery);
    Assert.Contains("\"entityType\":\"post\"", handler.RequestBody!, StringComparison.Ordinal);
  }
}
