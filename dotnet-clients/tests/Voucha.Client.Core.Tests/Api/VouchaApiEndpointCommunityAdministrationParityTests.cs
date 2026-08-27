using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  public static IEnumerable<object[]> CommunityAdministrationEndpointCases()
  {
    yield return Case("enableCommunityAiAgent", VouchaApiEndpoints.EnableCommunityAiAgent("test community", "agent 1"), HttpMethod.Put, "/api/v1/communities/test%20community/ai-agents/agent%201", Query());
    yield return Case("communityModlog", VouchaApiEndpoints.CommunityModlog("test community", "cursor-8", "ban"), HttpMethod.Get, "/api/v1/communities/test%20community/modlog", Query(("after", "cursor-8"), ("action_type", "ban")));
    yield return Case("communityModmail", VouchaApiEndpoints.CommunityModmail("test community", "cursor-9", 20), HttpMethod.Get, "/api/v1/communities/test%20community/modmail", Query(("after", "cursor-9"), ("limit", "20")));
    yield return Case("openCommunityModmailForReport", VouchaApiEndpoints.OpenCommunityModmailForReport("test community", "report 1"), HttpMethod.Post, "/api/v1/communities/test%20community/reports/report%201/modmail", Query(), true);
    yield return Case("communityModmailMessages", VouchaApiEndpoints.CommunityModmailMessages("test community", "thread 1", "cursor-10", 21), HttpMethod.Get, "/api/v1/communities/test%20community/modmail/thread%201/messages", Query(("after", "cursor-10"), ("limit", "21")));
    yield return Case("resolveCommunityModerationReport", VouchaApiEndpoints.ResolveCommunityModerationReport("test community", "report 1", "resolved"), HttpMethod.Patch, "/api/v1/communities/test%20community/reports/report%201", Query(), true);
    yield return Case("testCommunityAgentPrompt", VouchaApiEndpoints.TestCommunityAgentPrompt("test community", "prompt 1", new CommunityAgentPromptTestRunRequest("body")), HttpMethod.Post, "/api/v1/communities/test%20community/agent-prompts/prompt%201/test-runs", Query(), true);
    yield return Case("confirmCommunityBanEvasion", VouchaApiEndpoints.ConfirmCommunityBanEvasion("test community", "user 1"), HttpMethod.Post, "/api/v1/communities/test%20community/ban-evasion/user%201", Query());
    yield return Case("dismissCommunityBanEvasion", VouchaApiEndpoints.DismissCommunityBanEvasion("test community", "user 1"), HttpMethod.Delete, "/api/v1/communities/test%20community/ban-evasion/user%201", Query());
  }

  [Theory]
  [MemberData(nameof(CommunityAdministrationEndpointCases))]
  public void CommunityAdministrationEndpointsMatchExpectedRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody) =>
      AssertEndpoint(name, request, method, path, query, hasBody);
}
