using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Notifications;

public sealed partial class NotificationsViewModel
{
  public async Task MarkReadAsync(string notificationId, CancellationToken cancellationToken = default)
  {
    if (inFlight) return;
    await MarkReadCoreAsync(notificationId, cancellationToken).ConfigureAwait(true);
  }

  private async Task MarkReadCoreAsync(string notificationId, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(notificationId)) return;
    var item = Items.FirstOrDefault(item => string.Equals(item.Id, notificationId, StringComparison.Ordinal));
    if (item is null || item.IsRead) return;
    var previousItems = Items;
    var optimisticItems = MarkRowsRead(previousItems, [notificationId]);
    locallyReadIds.Add(notificationId);
    Items = optimisticItems;
    ErrorMessage = null;
    try
    {
      await notificationsService.MarkReadAsync(notificationId, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      locallyReadIds.Remove(notificationId);
      RollbackOptimisticRead(optimisticItems, previousItems, [notificationId], null);
    }
    catch (VouchaApiException ex)
    {
      locallyReadIds.Remove(notificationId);
      RollbackOptimisticRead(optimisticItems, previousItems, [notificationId], ex.Message);
    }
    catch (HttpRequestException ex)
    {
      locallyReadIds.Remove(notificationId);
      RollbackOptimisticRead(optimisticItems, previousItems, [notificationId], ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      locallyReadIds.Remove(notificationId);
      RollbackOptimisticRead(optimisticItems, previousItems, [notificationId], ex.Message);
    }
  }

  public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
  {
    if (inFlight || Items.Count == 0) return;
    var newlyRead = Items
        .Where(item => item.IsUnread)
        .Select(item => item.Id)
        .Where(id => locallyReadIds.Add(id))
        .ToArray();
    if (newlyRead.Length == 0) return;
    var previousItems = Items;
    var optimisticItems = MarkRowsRead(previousItems, newlyRead);
    Items = optimisticItems;
    ErrorMessage = null;
    try
    {
      await notificationsService.MarkAllReadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackMarkAllRead(newlyRead, optimisticItems, previousItems, null);
    }
    catch (VouchaApiException ex)
    {
      RollbackMarkAllRead(newlyRead, optimisticItems, previousItems, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      RollbackMarkAllRead(newlyRead, optimisticItems, previousItems, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      RollbackMarkAllRead(newlyRead, optimisticItems, previousItems, ex.Message);
    }
  }

  private void RollbackMarkAllRead(
      IReadOnlyList<string> newlyRead,
      IReadOnlyList<NotificationRow> optimisticItems,
      IReadOnlyList<NotificationRow> previousItems,
      string? message)
  {
    foreach (var id in newlyRead)
    {
      locallyReadIds.Remove(id);
    }

    RollbackOptimisticRead(optimisticItems, previousItems, newlyRead, message);
  }

  private void RollbackOptimisticRead(
      IReadOnlyList<NotificationRow> optimisticItems,
      IReadOnlyList<NotificationRow> previousItems,
      IReadOnlyCollection<string> rolledBackIds,
      string? message)
  {
    if (ReferenceEquals(Items, optimisticItems))
    {
      Items = previousItems;
    }
    else if (rolledBackIds.Count > 0)
    {
      Items = Items
          .Select(item => rolledBackIds.Contains(item.Id) ? item with { IsRead = false } : item)
          .ToArray();
    }

    ErrorMessage = message;
  }
}
