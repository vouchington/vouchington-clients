using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Notifications;

public interface INotificationsService
{
  Task<NotificationsResponse> FetchAsync(
      FetchNotificationsRequest request,
      CancellationToken cancellationToken = default);

  Task MarkReadAsync(
      string notificationId,
      CancellationToken cancellationToken = default);

  Task<NotificationRedirectTargetResponse> FetchRedirectTargetAsync(
      string notificationId,
      CancellationToken cancellationToken = default);

  Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
