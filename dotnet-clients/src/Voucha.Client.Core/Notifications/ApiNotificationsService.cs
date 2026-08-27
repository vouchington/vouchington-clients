using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Notifications;

public sealed class ApiNotificationsService : INotificationsService
{
  private readonly VouchaApiClient client;

  public ApiNotificationsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<NotificationsResponse> FetchAsync(
      FetchNotificationsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchNotificationsAsync(request, cancellationToken);

  public Task MarkReadAsync(
      string notificationId,
      CancellationToken cancellationToken = default) =>
      client.MarkNotificationReadAsync(notificationId, cancellationToken);

  public Task<NotificationRedirectTargetResponse> FetchRedirectTargetAsync(
      string notificationId,
      CancellationToken cancellationToken = default) =>
      client.FetchNotificationRedirectTargetAsync(notificationId, cancellationToken);

  public Task MarkAllReadAsync(CancellationToken cancellationToken = default) =>
      client.MarkAllNotificationsReadAsync(cancellationToken);
}
