using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

public sealed partial class NotificationsViewModel
{
  private NotificationRow[] RowsFrom(NotificationsResponse response)
  {
    var rows = new List<NotificationRow>();
    foreach (var reference in response.Results)
    {
      if (reference.Id is not null &&
          response.Notifications.TryGetValue(reference.Id, out var notification) &&
          notification is not null)
      {
        rows.Add(RowFrom(notification, reference, response));
      }
    }

    return [.. rows];
  }

  private NotificationRow RowFrom(Notification notification, EntityReference reference, NotificationsResponse response)
  {
    var isRead = notification.ReadAt is not null ||
        reference.ReadAt is not null ||
        locallyReadIds.Contains(notification.Id);
    return new(
        notification.Id,
        string.IsNullOrWhiteSpace(notification.Title)
            ? UiText.Localized(UiMessageKey.NativeTaxonomyNotificationGenericTitle)
            : UiText.UserContent(notification.Title),
        notification.Body ?? "",
        notification.CreatedAt,
        StructuredTargetPath(notification, response) ?? notification.TargetPath,
        notification.EntityType,
        isRead,
        localization);
  }

  private static string? StructuredTargetPath(Notification notification, NotificationsResponse response)
  {
    if (notification.TargetEntity is { EntityType: "community" } target)
    {
      return response.Communities?.TryGetValue(target.Id, out var community) == true
          ? $"/communities/{community.Slug}"
          : "/my/notifications";
    }

    return notification.TargetIntent == "notifications_inbox" ? "/my/notifications" : null;
  }

  private static NotificationRow[] MarkRowsRead(
      IReadOnlyList<NotificationRow> source,
      IReadOnlyCollection<string> ids) =>
      source.Select(item => ids.Contains(item.Id) ? item with { IsRead = true } : item).ToArray();
}
