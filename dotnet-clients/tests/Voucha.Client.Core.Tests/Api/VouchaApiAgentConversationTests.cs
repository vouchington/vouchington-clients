using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchAgentListsForwardOpaqueCursorsAndDecodeFixtures()
  {
    var (agentsClient, agentsHandler) = CreateClient("native.agents.page-2");
    var agents = await agentsClient.FetchAgentsAsync(
        new FetchAgentsRequest(
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ", 2),
        TestContext.Current.CancellationToken);
    AssertRequest(
        agentsHandler, HttpMethod.Get,
        "/api/v1/agents?after=eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ&limit=2");
    Assert.Single(agents.Results);
    Assert.Single(agents.Users);

    var (conversationsClient, conversationsHandler) = CreateClient("native.agents.conversations.page-2");
    var conversations = await conversationsClient.FetchAgentConversationsAsync(
        new FetchAgentConversationsRequest(
            "helper", "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ", 2),
        TestContext.Current.CancellationToken);
    AssertRequest(
        conversationsHandler, HttpMethod.Get,
        "/api/v1/agents/helper/conversations?after=" +
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ&limit=2");
    Assert.Single(conversations.Results);
    Assert.Single(conversations.Users);
  }

  [Fact]
  public async Task FetchAgentConversationForwardsOpaqueCursorAndDecodesFixture()
  {
    var (client, handler) = CreateClient("native.agents.conversation.page-2");

    var response = await client.FetchAgentConversationAsync(
        new FetchAgentConversationRequest(
            "helper",
            "00000000-0000-7000-8000-000000000101",
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9",
            2),
        TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Get,
        "/api/v1/agents/helper/conversations/00000000-0000-7000-8000-000000000101" +
        "?after=eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9&limit=2");
    Assert.Equal("Earlier context", Assert.Single(response.Results).Content?.Content);
    Assert.False(response.PageInfo.HasNextPage);
  }

  [Theory]
  [InlineData(AgentConversationFilterKind.UserId, "user_id")]
  [InlineData(AgentConversationFilterKind.Username, "username")]
  [InlineData(AgentConversationFilterKind.PostId, "post_id")]
  [InlineData(AgentConversationFilterKind.PostSlug, "post_slug")]
  [InlineData(AgentConversationFilterKind.RssFeedItemId, "rss_feed_item_id")]
  public void AgentConversationFiltersUseOneExactQueryName(AgentConversationFilterKind kind, string queryName)
  {
    var request = VouchaApiEndpoints.AgentConversations(
        "agent/name", "cursor", 2, new AgentConversationFilter(kind, "value: %"));

    Assert.Equal("/api/v1/agents/agent%2Fname/conversations", request.Path);
    Assert.Equal("cursor", request.Query["after"]);
    Assert.Equal("2", request.Query["limit"]);
    Assert.Equal("value: %", request.Query[queryName]);
  }
}
