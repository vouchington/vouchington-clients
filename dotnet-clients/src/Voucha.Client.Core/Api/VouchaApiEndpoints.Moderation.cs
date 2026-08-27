namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Appeals(ModerationAppealStatus? status = null, int limit = 25, string? after = null, bool mine = false) =>
      Get(
          "/api/v1/appeals",
          Query(("limit", limit), ("status", AppealStatusValue(status)), ("after", after), ("mine", mine ? "true" : null)));

  public static ApiRequest Appeal(string id) => Get($"/api/v1/appeals/{Path(id)}");

  public static ApiRequest SubmitAppeal(ModerationAppealSubmissionRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    return new(HttpMethod.Post, "/api/v1/appeals")
    {
      Body = new SubmitAppealBody(
          request.TargetType,
          request.TargetId,
          $"[{AppealReasonValue(request.Reason)}] {request.Details.Trim()}",
          request.PostRemovalKind,
          request.TurnstileToken),
    };
  }

  public static ApiRequest UpdateAppeal(
      string id,
      string? publicResponse = null,
      string? internalNotes = null)
  {
    if (publicResponse is null && internalNotes is null)
    {
      throw new ArgumentException(
          "At least one appeal draft field must be provided.",
          nameof(publicResponse));
    }

    return new(HttpMethod.Patch, $"/api/v1/appeals/{Path(id)}")
    {
      Body = new UpdateAppealBody(publicResponse, internalNotes),
    };
  }

  public static ApiRequest AppealApproval(string id) =>
      new(HttpMethod.Post, $"/api/v1/appeals/{Path(id)}/approval");

  public static ApiRequest AppealDelivery(string id) =>
      new(HttpMethod.Post, $"/api/v1/appeals/{Path(id)}/delivery");

  public static ApiRequest AppealResolution(string id, ModerationAppealAction action) =>
      new(HttpMethod.Post, $"/api/v1/appeals/{Path(id)}/resolution")
      {
        Body = new ResolveAppealBody(action),
      };

  public static ApiRequest AppealResolutionDrafts(string id) =>
      new(HttpMethod.Post, $"/api/v1/appeals/{Path(id)}/resolution-drafts");

  public static ApiRequest ModerationCaseCount(string apiPath, int limit = 25, string? after = null) =>
      Get(apiPath, Query(("limit", limit), ("after", after)));

  public static ApiRequest PersonalRemovedPosts(int limit = 25, string? after = null) =>
      Get(
          "/api/v1/my/removed-posts",
          Query(("limit", limit), ("include_platform", "true"), ("after", after)));

  public static ApiRequest PersonalWarnings(int limit = 25, string? after = null) =>
      ModerationCaseCount("/api/v1/my/warnings", limit, after);

  private static string? AppealStatusValue(ModerationAppealStatus? status) => status switch
  {
    ModerationAppealStatus.Pending => "pending",
    ModerationAppealStatus.Dismissed => "dismissed",
    ModerationAppealStatus.Resolved => "resolved",
    null => null,
    _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
  };

  private static string AppealReasonValue(ModerationAppealReason reason) => reason switch
  {
    ModerationAppealReason.IncorrectFacts => "incorrect_facts",
    ModerationAppealReason.WrongRule => "wrong_rule",
    ModerationAppealReason.ContextMissing => "context_missing",
    ModerationAppealReason.Disproportionate => "disproportionate",
    ModerationAppealReason.Other => "other",
    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
  };
}
