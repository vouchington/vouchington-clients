using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public void PostsFeedResponseDeserializesPostVoteSidecars()
  {
    const string Json = """
        {
          "results": [
            { "entity_id": "post-1" }
          ],
          "page_info": { "has_next_page": false },
          "posts": {
            "post-1": {
              "id": "post-1",
              "post_type": "discussion",
              "title": "Voting post",
              "markdown": "Body",
              "created_by_id": "user-1"
            }
          },
          "users": {},
          "communities": {},
          "post_elections": {
            "post-1": {
              "id": "election-1",
              "votes_score_net": 3.5,
              "votes_count_up": 4,
              "votes_count_down": 1
            }
          },
          "election_votes": {
            "post-1": {
              "__entity_type": "election_vote",
              "user_id": "user-1",
              "choice": "like",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """;

    var response = JsonSerializer.Deserialize<PostsFeedResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal("election-1", response.PostElections?["post-1"].Id);
    Assert.Equal(3.5, response.PostElections?["post-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Like, response.ElectionVotes?["post-1"].Choice);
  }

  [Fact]
  public void TopicSearchResponseDeserializesTopicVoteSidecars()
  {
    const string Json = """
        {
          "results": [
            { "entity_id": "topic-1" }
          ],
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
              "id": "election-2",
              "votes_score_net": 8.25,
              "votes_count_up": 10,
              "votes_count_down": 2
            }
          },
          "election_votes": {
            "topic-1": {
              "__entity_type": "election_vote",
              "user_id": "user-1",
              "choice": "dislike",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """;

    var response = JsonSerializer.Deserialize<TopicSearchResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal("election-2", response.TopicElections?["topic-1"].Id);
    Assert.Equal(8.25, response.TopicElections?["topic-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Dislike, response.ElectionVotes?["topic-1"].Choice);
  }

  [Fact]
  public void RssFeedItemsFeedResponseDeserializesRssFeedItemVoteSidecars()
  {
    const string Json = """
        {
          "results": [
            { "id": "item-1" }
          ],
          "page_info": { "has_next_page": false },
          "rss_feed_items": {
            "item-1": {
              "id": "item-1",
              "published_at": "2026-01-01T00:00:00Z",
              "rss_feed_sources": [],
              "title": "Article",
              "data": null
            }
          },
          "rss_feed_item_elections": {
            "item-1": {
              "id": "election-3",
              "votes_score_net": 2.5,
              "votes_count_up": 5,
              "votes_count_down": 3
            }
          },
          "election_votes": {
            "item-1": {
              "__entity_type": "election_vote",
              "user_id": "user-1",
              "choice": "neutral",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """;

    var response = JsonSerializer.Deserialize<RssFeedItemsFeedResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal("election-3", response.RssFeedItemElections?["item-1"].Id);
    Assert.Equal(2.5, response.RssFeedItemElections?["item-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Neutral, response.ElectionVotes?["item-1"].Choice);
  }
}
