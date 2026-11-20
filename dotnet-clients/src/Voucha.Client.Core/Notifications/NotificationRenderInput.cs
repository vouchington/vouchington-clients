using System.Globalization;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>One notification plus the hydrated facts and web origin its copy is rendered from.</summary>
internal readonly record struct NotificationRenderInput(
    Notification Notification,
    NotificationsResponse Hydration,
    Uri WebBaseUrl)
{
  /// <summary>The hydrated username, or null when the user is gone, hidden or anonymous.</summary>
  public string? Username(string? userId) =>
      userId is not null && Hydration.Actors?.GetValueOrDefault(userId)?.Username is { Length: > 0 } name
          ? name
          : null;

  public NotificationPost? Post(string? postId) =>
      postId is null ? null : Hydration.Posts?.GetValueOrDefault(postId);

  public NotificationRssFeedItem? FeedItem() =>
      Notification.RssFeedItemId is { } id ? Hydration.RssFeedItems?.GetValueOrDefault(id) : null;

  public NotificationCommunity? Community(string? communityId) =>
      communityId is null ? null : Hydration.Communities?.GetValueOrDefault(communityId);

  /// <summary>The community's display name, or the catalog fallback when it is gone or not visible.</summary>
  public UiText CommunityName() =>
      Community(Notification.CommunityId)?.Name is { } name
          ? UiText.UserContent(name)
          : UiText.Localized(UiMessageKey.NotificationsKindFallbackCommunity);

  /// <summary>Moderator-authored text for the row that owns it; empty when it is unsent or gone.</summary>
  public string PublicText(string? ownerId) =>
      ownerId is null ? string.Empty : Hydration.PublicTexts?.GetValueOrDefault(ownerId) ?? string.Empty;

  public string? StringParam(string name) =>
      Param(name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

  public bool BoolParam(string name) => Param(name) is { ValueKind: JsonValueKind.True };

  /// <summary>A numeric param; numeric strings count, anything missing or non-numeric is 0.</summary>
  public decimal NumberParam(string name) => Param(name) switch
  {
    { ValueKind: JsonValueKind.Number } number when number.TryGetDecimal(out var value) => value,
    { ValueKind: JsonValueKind.String } text when decimal.TryParse(
        text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
    { ValueKind: JsonValueKind.True } => 1,
    _ => 0,
  };

  private JsonElement? Param(string name) =>
      Notification.Params is { } map && map.TryGetValue(name, out var value) ? value : null;
}
