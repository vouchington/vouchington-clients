using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CommunityAgentPromptsResponse
{
  [JsonConstructor]
  public CommunityAgentPromptsResponse(IReadOnlyList<CommunityAgentPrompt> communityAgentPrompts,
      CommunityAgentPromptSlotInfo slotInfo, CommunityAgentPromptPageInfo pageInfo)
  {
    CommunityAgentPrompts = communityAgentPrompts ?? throw new JsonException("Missing community_agent_prompts.");
    SlotInfo = slotInfo;
    PageInfo = pageInfo;
  }

  [JsonPropertyName("community_agent_prompts"), JsonRequired]
  public IReadOnlyList<CommunityAgentPrompt> CommunityAgentPrompts { get; init; }
  [JsonPropertyName("slot_info"), JsonRequired]
  public CommunityAgentPromptSlotInfo SlotInfo { get; init; }
  [JsonPropertyName("page_info"), JsonRequired]
  public CommunityAgentPromptPageInfo PageInfo { get; init; }
}

public readonly record struct CommunityAgentPromptPageInfo(
    [property: JsonPropertyName("end_cursor"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? EndCursor,
    [property: JsonPropertyName("has_next_page"), JsonRequired] bool HasNextPage,
    [property: JsonPropertyName("start_cursor"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? StartCursor);

public readonly record struct CommunityAgentPromptSlotInfo(
    [property: JsonPropertyName("used"), JsonRequired] int Used,
    [property: JsonPropertyName("limit"), JsonRequired] int Limit,
    [property: JsonPropertyName("remaining"), JsonRequired] int Remaining,
    [property: JsonPropertyName("limits_by_plan"), JsonRequired] CommunityAgentPromptPlanLimits LimitsByPlan);

public readonly record struct CommunityAgentPromptPlanLimits(
    [property: JsonPropertyName("plus"), JsonRequired] int Plus,
    [property: JsonPropertyName("pro"), JsonRequired] int Pro);
