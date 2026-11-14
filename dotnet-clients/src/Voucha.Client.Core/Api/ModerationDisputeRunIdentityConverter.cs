namespace Voucha.Client.Core.Api;

internal sealed class ModerationDisputeRunIdentityConverter : ModerationRunIdentityConverter<ModerationDispute>
{
  private static readonly IReadOnlySet<string> StaffFields = new HashSet<string>(StringComparer.Ordinal)
  {
    "disputant_user_id", "claim_text", "recommended_action", "ai_public_response",
    "ai_internal_response", "model", "ai_drafted_at", "ai_draft_run_id", "ai_draft_requested_run_id",
    "internal_notes", "drafted_at", "edited_at", "edited_by_id", "approved_by_id", "resolved_by_id",
    "latest_lifecycle_change_id", "staff_context",
  };

  protected override IReadOnlySet<string> ProtectedFields => StaffFields;
}
