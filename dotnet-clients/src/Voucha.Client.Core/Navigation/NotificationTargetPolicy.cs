namespace Voucha.Client.Core.Navigation;

public enum NotificationTargetKind
{
  InternalAppLink,
  ExternalWebLink,
}

public sealed record NotificationTargetResolution(Uri Uri, NotificationTargetKind Kind)
{
  public bool IsInternalAppLink => Kind == NotificationTargetKind.InternalAppLink;

  public bool IsExternalWebLink => Kind == NotificationTargetKind.ExternalWebLink;
}

public static class NotificationTargetPolicy
{
  public static bool TryResolve(
      string? targetPath,
      [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out NotificationTargetResolution? resolution)
  {
    var trimmedTargetPath = targetPath?.Trim();
    if (string.IsNullOrWhiteSpace(trimmedTargetPath))
    {
      resolution = null;
      return false;
    }

    if (HasSchemePrefix(trimmedTargetPath))
    {
      if (Uri.TryCreate(trimmedTargetPath, UriKind.Absolute, out var absoluteUri))
      {
        return TryResolveAbsoluteUri(absoluteUri, out resolution);
      }

      resolution = null;
      return false;
    }

    if (!TryCreateInternalUri(trimmedTargetPath, out var internalUri))
    {
      resolution = null;
      return false;
    }

    resolution = new NotificationTargetResolution(internalUri, NotificationTargetKind.InternalAppLink);
    return true;
  }

  private static bool TryResolveAbsoluteUri(
      Uri absoluteUri,
      out NotificationTargetResolution? resolution)
  {
    if (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
      resolution = new NotificationTargetResolution(absoluteUri, NotificationTargetKind.ExternalWebLink);
      return true;
    }

    if (string.Equals(absoluteUri.Scheme, "voucha", StringComparison.OrdinalIgnoreCase))
    {
      resolution = new NotificationTargetResolution(absoluteUri, NotificationTargetKind.InternalAppLink);
      return true;
    }

    resolution = null;
    return false;
  }

  private static bool TryCreateInternalUri(
      string targetPath,
      [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? uri)
  {
    var normalizedTargetPath = targetPath.TrimStart('/');
    if (string.IsNullOrEmpty(normalizedTargetPath))
    {
      uri = null;
      return false;
    }

    if (!Uri.TryCreate($"voucha://{normalizedTargetPath}", UriKind.Absolute, out var createdUri))
    {
      uri = null;
      return false;
    }

    uri = createdUri;
    return true;
  }

  private static bool HasSchemePrefix(string targetPath)
  {
    var colonIndex = targetPath.IndexOf(':', StringComparison.Ordinal);
    if (colonIndex <= 0)
    {
      return false;
    }

    var firstSlashIndex = targetPath.IndexOf('/', StringComparison.Ordinal);
    var colonBelongsToPathSegment = firstSlashIndex >= 0 && colonIndex > firstSlashIndex;
    if (colonBelongsToPathSegment)
    {
      return false;
    }

    return Uri.CheckSchemeName(targetPath[..colonIndex]);
  }
}
