using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class MemberAppealContractTests
{
  [Fact]
  public void TypedSubmissionSerializesReasonAndNullableSuspensionTarget()
  {
    var request = VouchaApiEndpoints.SubmitAppeal(new ModerationAppealSubmissionRequest(
        ModerationAppealTargetType.Suspension,
        null,
        ModerationAppealReason.ContextMissing,
        "The decision missed important context.",
        null,
        null));

    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal("/api/v1/appeals", request.Path);
    Assert.Equal(
        JsonNode.Parse(
            """{"target_type":"suspension","appeal_reason":"[context_missing] The decision missed important context."}"""),
        JsonSerializer.SerializeToNode(request.Body, VouchaApiJson.Options),
        JsonNode.DeepEquals);
  }

  [Fact]
  public void SubmissionEnvelopePreservesDuplicateResult()
  {
    var response = JsonSerializer.Deserialize<ModerationAppealSubmissionResponse>(
        $$"""{"appeal":{{AppealJson}},"isDuplicate":true}""",
        VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.True(response.IsDuplicate);
    Assert.Equal("appeal-1", response.Appeal.Id);
  }

  [Fact]
  public void TypedWarningAndRemovedPostEndpointsForwardIndependentCursors()
  {
    var warnings = VouchaApiEndpoints.PersonalWarnings(7, "warning/cursor");
    var removals = VouchaApiEndpoints.PersonalRemovedPosts(9, "removal/cursor");

    Assert.Equal("/api/v1/my/warnings", warnings.Path);
    Assert.Equal("warning/cursor", warnings.Query["after"]);
    Assert.Equal("7", warnings.Query["limit"]);
    Assert.Equal("/api/v1/my/removed-posts", removals.Path);
    Assert.Equal("removal/cursor", removals.Query["after"]);
    Assert.Equal("true", removals.Query["include_platform"]);
    Assert.Equal("9", removals.Query["limit"]);
  }

  [Fact]
  public void WarningNoticeDecodesMemberSafeRevocationState()
  {
    var response = JsonSerializer.Deserialize<MemberWarningNoticesResponse>(
        """
        {
          "warnings":[{
            "id":"warning-1","case_id":"case-1","user_id":"user-1",
            "community_id":null,"community_slug":null,"public_message":"Notice",
            "revoked_at":"2026-07-02T00:00:00Z",
            "created_at":"2026-07-01T00:00:00Z"
          }],
          "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}
        }
        """,
        VouchaApiJson.Options);

    Assert.NotNull(response);
    var warning = Assert.Single(response.Warnings);
    Assert.Equal(DateTimeOffset.Parse("2026-07-02T00:00:00Z"), warning.RevokedAt);
  }

  private const string AppealJson = """
      {
        "id":"appeal-1","case_id":"case-1","appellant_id":"user-1",
        "user_warning_id":"warning-1","user_suspension_id":null,
        "community_ban_id":null,"post_id":null,"community_id":null,
        "post_removal_kind":null,"appeal_reason":"reason","status":"pending",
        "recommended_action":null,"ai_public_response":null,"ai_internal_response":null,
        "model":null,"ai_drafted_at":null,"public_response":null,"internal_notes":null,
        "drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,
        "approved_by_id":null,"sent_at":null,"resolved_at":null,"resolved_by_id":null,
        "resolution_action":null,"latest_lifecycle_change_id":null,
        "created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z",
        "is_overdue":false,"target_context":null
      }
      """;
}
