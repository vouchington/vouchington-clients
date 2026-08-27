using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiElectionSidecarTests
{
  [Fact]
  public void PostsFeedResponseDecodesElectionSidecars()
  {
    const string Json = """
        {
          "results": [{ "entity_id": "post-1" }],
          "page_info": { "has_next_page": false },
          "posts": {
            "post-1": {
              "id": "post-1",
              "post_type": "discussion",
              "title": "Post",
              "markdown": "Body",
              "created_by_id": "user-1"
            }
          },
          "users": {},
          "communities": {},
          "post_elections": {
            "post-1": {
              "__entity_type": "post_election",
              "id": "post-1",
              "votes_score_net": 2.25,
              "votes_count_up": 3,
              "votes_count_down": 1
            }
          },
          "election_votes": {
            "post-1": {
              "__entity_type": "election_vote",
              "entity_id": "post-1",
              "user_id": "user-1",
              "choice": "like",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """;

    var response = JsonSerializer.Deserialize<PostsFeedResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal(2.25, response!.PostElections!["post-1"].VotesScoreNet);
    Assert.Equal(3, response.PostElections["post-1"].VotesCountUp);
    Assert.Equal(ElectionVoteChoice.Like, response.ElectionVotes!["post-1"].Choice);
  }

  [Fact]
  public void RssFeedItemsFeedResponseDecodesElectionSidecars()
  {
    const string Json = """
        {
          "results": [{ "entity_id": "item-1", "id": "item-1", "published_at": "2026-01-01T00:00:00Z" }],
          "page_info": { "has_next_page": false },
          "rss_feed_items": {
            "item-1": {
              "id": "item-1",
              "published_at": "2026-01-01T00:00:00Z",
              "rss_feed_sources": [],
              "title": "Article"
            }
          },
          "rss_feed_item_elections": {
            "item-1": {
              "__entity_type": "rss_feed_item_election",
              "id": "item-1",
              "votes_score_net": 4.5,
              "votes_count_up": 5,
              "votes_count_down": 1
            }
          },
          "election_votes": {
            "item-1": {
              "__entity_type": "election_vote",
              "entity_id": "item-1",
              "user_id": "user-1",
              "choice": "dislike",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """;

    var response = JsonSerializer.Deserialize<RssFeedItemsFeedResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal(4.5, response!.RssFeedItemElections!["item-1"].VotesScoreNet);
    Assert.Equal(5, response.RssFeedItemElections["item-1"].VotesCountUp);
    Assert.Equal(ElectionVoteChoice.Dislike, response.ElectionVotes!["item-1"].Choice);
  }

  [Fact]
  public void TopicResponsesDecodeElectionSidecars()
  {
    const string SearchJson = """
        {
          "results": [{ "entity_id": "topic-1" }],
          "page_info": { "has_next_page": false },
          "topics": {
            "topic-1": {
              "id": "topic-1",
              "name": "Topic",
              "slug": "topic",
              "topic_type": "topic"
            }
          },
          "topics_metrics": {},
          "topic_elections": {
            "topic-1": {
              "__entity_type": "topic_election",
              "id": "topic-1",
              "votes_score_net": 6.75,
              "votes_count_up": 8,
              "votes_count_down": 2
            }
          },
          "election_votes": {
            "topic-1": {
              "__entity_type": "election_vote",
              "entity_id": "topic-1",
              "user_id": "user-1",
              "choice": "like",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """;

    const string DetailJson = """
        {
          "topic": {
            "id": "topic-1",
            "name": "Topic",
            "slug": "topic",
            "topic_type": "topic"
          },
          "topic_metrics": null,
          "topic_election": {
            "__entity_type": "topic_election",
            "id": "topic-1",
            "votes_score_net": 6.75,
            "votes_count_up": 8,
            "votes_count_down": 2
          },
          "election_vote": {
            "__entity_type": "election_vote",
            "entity_id": "topic-1",
            "user_id": "user-1",
            "choice": "dislike",
            "created_at": "2026-01-01T00:00:00Z"
          }
        }
        """;

    var searchResponse = JsonSerializer.Deserialize<TopicSearchResponse>(SearchJson, VouchaApiJson.Options);
    var detailResponse = JsonSerializer.Deserialize<TopicResponse>(DetailJson, VouchaApiJson.Options);

    Assert.NotNull(searchResponse);
    Assert.Equal(6.75, searchResponse!.TopicElections!["topic-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Like, searchResponse.ElectionVotes!["topic-1"].Choice);
    Assert.NotNull(detailResponse);
    Assert.Equal(2, detailResponse!.TopicElection!.VotesCountDown);
    Assert.Equal(ElectionVoteChoice.Dislike, detailResponse.ElectionVote!.Choice);
  }
}
