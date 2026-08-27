namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Dispute(string id) =>
      Get($"/api/v1/disputes/{Path(id)}");

  public static ApiRequest UpdateDispute(
      string id,
      string? publicResponse = null,
      string? internalNotes = null)
  {
    if (publicResponse is null && internalNotes is null)
    {
      throw new ArgumentException(
          "At least one dispute draft field must be provided.",
          nameof(publicResponse));
    }

    return new(HttpMethod.Patch, $"/api/v1/disputes/{Path(id)}")
    {
      Body = new UpdateDisputeBody(publicResponse, internalNotes),
    };
  }

  public static ApiRequest DisputeApproval(string id) =>
      new(HttpMethod.Post, $"/api/v1/disputes/{Path(id)}/approval");

  public static ApiRequest DisputeDelivery(string id) =>
      new(HttpMethod.Post, $"/api/v1/disputes/{Path(id)}/delivery");

  public static ApiRequest DisputeResolution(
      string id,
      ModerationDisputeResolutionAction action,
      string? bodyText = null)
  {
    if (action == ModerationDisputeResolutionAction.Annotate &&
        string.IsNullOrWhiteSpace(bodyText))
    {
      throw new ArgumentException(
          "An annotation body is required for annotate resolutions.",
          nameof(bodyText));
    }

    if (action != ModerationDisputeResolutionAction.Annotate && bodyText is not null)
    {
      throw new ArgumentException(
          "An annotation body is only valid for annotate resolutions.",
          nameof(bodyText));
    }

    return new(HttpMethod.Post, $"/api/v1/disputes/{Path(id)}/resolution")
    {
      Body = new ResolveDisputeBody(action, bodyText),
    };
  }

  public static ApiRequest DisputeResolutionDrafts(string id) =>
      new(HttpMethod.Post, $"/api/v1/disputes/{Path(id)}/resolution-drafts");

  public static ApiRequest ModerationExposure() =>
      Get("/api/v1/moderation/exposure");

  public static ApiRequest RecordModerationReveal(
      string? postId,
      string? reportId,
      ModerationRevealSurface surface) =>
      new(HttpMethod.Post, "/api/v1/moderation/reveals")
      {
        Body = new ModerationRevealBody(postId, reportId, surface),
      };
}
