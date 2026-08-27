using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class EntityRelationsApiModelTests
{
  [Fact]
  public void EntityRelationsResponseDeserializesFractionalVoteScores()
  {
    const string Json = """
        {
          "results": [{ "__entity_type": "entity_relation", "id": "relation-1" }],
          "page_info": { "has_next_page": false },
          "entity_relations": {
            "relation-1": {
              "id": "relation-1",
              "votes_score_net": 0.01,
              "votes_score_sort": 0.5
            }
          },
          "entity_relation_elections": {
            "relation-1": {
              "__entity_type": "entity_relation_election",
              "id": "relation-1",
              "votes_score_net": 0.25,
              "votes_count_up": 1,
              "votes_count_down": 0
            }
          },
          "election_votes": { "relation-1": { "choice": "confirm" } }
        }
        """;

    var response = JsonSerializer.Deserialize<EntityRelationsResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal(0.01, response.EntityRelations["relation-1"].VotesScoreNet);
    Assert.Equal(0.5, response.EntityRelations["relation-1"].VotesScoreSort);
    Assert.Equal(0.25, response.EntityRelationElections?["relation-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Confirm, response.ElectionVotes?["relation-1"].Choice);
  }
}
