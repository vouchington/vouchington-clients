using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithAgentEndpoints(Dictionary<string, ApiRequest> endpoints)
  {
    endpoints["native.agents.default"] = VouchaApiEndpoints.Agents(limit: 2);
    endpoints["native.agents.page-2"] = VouchaApiEndpoints.Agents(
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ", 2);
    endpoints["native.agents.detail.default"] = VouchaApiEndpoints.Agent("helper");
    endpoints["native.agents.conversations.default"] = VouchaApiEndpoints.AgentConversations("helper", limit: 2);
    endpoints["native.agents.conversations.filtered-username"] = VouchaApiEndpoints.AgentConversations(
        "helper", limit: 2, filter: new AgentConversationFilter(AgentConversationFilterKind.Username, "fixture-agent-user-011"));
    endpoints["native.agents.conversations.page-2"] = VouchaApiEndpoints.AgentConversations(
        "helper", "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ", 2);
    endpoints["native.agents.conversation.default"] = VouchaApiEndpoints.AgentConversation(
        "helper",
        "00000000-0000-7000-8000-000000000101",
        limit: 2);
    endpoints["native.agents.conversation.page-2"] = VouchaApiEndpoints.AgentConversation(
        "helper",
        "00000000-0000-7000-8000-000000000101",
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9",
        2);
    return endpoints;
  }
}
