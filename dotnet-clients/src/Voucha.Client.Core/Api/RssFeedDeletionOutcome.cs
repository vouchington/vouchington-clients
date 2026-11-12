using System.Text.Json.Serialization;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Api;

public sealed record RssFeedDeletionAcknowledgement(
    [property: JsonPropertyName("rss_feed_id"), JsonRequired] string RssFeedId,
    [property: JsonPropertyName("status"), JsonRequired] string Status);

/// <summary>Acceptance means deletion is pending; it does not confirm removal.</summary>
public sealed record RssFeedDeletionOutcome
{
  private RssFeedDeletionOutcome(string? rssFeedId, bool isAcceptedPending, UiMessageKey messageKey)
  {
    RssFeedId = rssFeedId;
    IsAcceptedPending = isAcceptedPending;
    Message = UiText.Localized(messageKey);
  }

  public string? RssFeedId { get; }
  public bool IsAcceptedPending { get; }
  public string? Status => IsAcceptedPending ? "deleting" : null;
  public UiText Message { get; }

  internal static RssFeedDeletionOutcome AcceptedPending(string rssFeedId) =>
      new(rssFeedId, true, UiMessageKey.SharedRssFeedDeletionAccepted);

  internal static RssFeedDeletionOutcome BlockedByAdminImports() =>
      new(null, false, UiMessageKey.SharedRssFeedDeletionBlockedByAdminImports);
}
