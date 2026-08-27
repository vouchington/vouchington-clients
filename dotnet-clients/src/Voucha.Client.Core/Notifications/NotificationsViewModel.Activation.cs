using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Notifications;

public sealed partial class NotificationsViewModel
{
  public async Task<string?> ActivateAsync(NotificationRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);

    var targetPath = row.TargetPath?.Trim();
    if (row.IsUnread)
    {
      await MarkReadCoreAsync(row.Id, cancellationToken).ConfigureAwait(true);
    }

    if (!row.IsRead && !locallyReadIds.Contains(row.Id))
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(targetPath))
    {
      return null;
    }

    return await ResolveActivationTargetPathAsync(targetPath, cancellationToken).ConfigureAwait(true);
  }

  private async Task<string?> ResolveActivationTargetPathAsync(
      string targetPath,
      CancellationToken cancellationToken)
  {
    var redirectNotificationId = GetNotificationRedirectId(targetPath);
    if (redirectNotificationId is null)
    {
      return targetPath;
    }

    try
    {
      var response = await notificationsService
          .FetchRedirectTargetAsync(redirectNotificationId, cancellationToken)
          .ConfigureAwait(true);
      var resolvedTargetPath = response.TargetUrl.Trim();
      return string.IsNullOrWhiteSpace(resolvedTargetPath) ? null : resolvedTargetPath;
    }
    catch (OperationCanceledException)
    {
      return null;
    }
    catch (VouchaApiException)
    {
      return null;
    }
    catch (HttpRequestException)
    {
      return null;
    }
    catch (InvalidOperationException)
    {
      return null;
    }
  }

  private static string? GetNotificationRedirectId(string targetPath)
  {
    if (!TryCreateTargetUri(targetPath, out var uri))
    {
      return null;
    }

    if (!string.Equals(
        uri.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault(),
        "notification-redirect",
        StringComparison.Ordinal))
    {
      return null;
    }

    foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
      var separator = pair.IndexOf('=', StringComparison.Ordinal);
      var key = separator < 0 ? pair : pair[..separator];
      if (!string.Equals(DecodeQueryValue(key), "notification_id", StringComparison.Ordinal))
      {
        continue;
      }

      var value = separator < 0 ? "" : pair[(separator + 1)..];
      var notificationId = DecodeQueryValue(value);
      return string.IsNullOrWhiteSpace(notificationId) ? null : notificationId;
    }

    return null;
  }

  private static bool TryCreateTargetUri(string targetPath, [NotNullWhen(true)] out Uri? uri)
  {
    if (Uri.TryCreate(targetPath, UriKind.Absolute, out uri) && !uri.IsFile)
    {
      return true;
    }

    var relativeTargetPath = targetPath.StartsWith('/')
        ? targetPath
        : $"/{targetPath}";
    return Uri.TryCreate(new Uri("https://voucha.local"), relativeTargetPath, out uri);
  }

  private static string DecodeQueryValue(string value) =>
      Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
}
