using Sentry;
using Voucha.Client.Core;

namespace Voucha.Client.App;

internal static class SentryEventProjection
{
  internal static SentryEvent Scrub(SentryEvent sentryEvent)
  {
    var extras = StringExtras(sentryEvent.Extra);
    var scrubbed = SentryEventScrubber.Scrub(new SentryOutboundEvent(
        AccountIdFor(sentryEvent.User),
        sentryEvent.User.Email,
        sentryEvent.User.Username,
        sentryEvent.User.IpAddress,
        sentryEvent.Request.Url,
        sentryEvent.Request.QueryString,
        sentryEvent.Request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        sentryEvent.Request.Cookies,
        sentryEvent.Request.Data as string ?? ExtraString(extras, "body") ?? ExtraString(extras, "request_body"),
        extras,
        Breadcrumbs: null));
    sentryEvent.User.Id = scrubbed.AccountId;
    sentryEvent.User.Email = null;
    sentryEvent.User.Username = null;
    sentryEvent.User.IpAddress = null;
    RetainAccountMarker(sentryEvent.User, scrubbed.AccountId);
    foreach (var key in sentryEvent.Tags.Keys.ToArray())
    {
      var kept = sentryEvent.Tags[key] is string text ? SentryTelemetryFields.ScrubField(key, text) : null;
      if (kept is null)
      {
        sentryEvent.UnsetTag(key);
      }
      else
      {
        sentryEvent.SetTag(key, kept);
      }
    }

    sentryEvent.Request.Url = scrubbed.RequestTarget;
    sentryEvent.Request.QueryString = null;
    sentryEvent.Request.Cookies = scrubbed.Cookies;
    sentryEvent.Request.Data = null;
    if (scrubbed.Headers is not null)
    {
      foreach (var pair in scrubbed.Headers)
      {
        sentryEvent.Request.Headers[pair.Key] = pair.Value;
      }
    }

    WriteExtras(sentryEvent.Extra, scrubbed.Extras);
    return sentryEvent;
  }

  internal static void WriteExtras(object? source, IReadOnlyDictionary<string, string>? kept)
  {
    if (source is not IDictionary<string, object?> extra)
    {
      return;
    }

    foreach (var key in extra.Keys.ToArray())
    {
      if (kept is not null && kept.TryGetValue(key, out var value))
      {
        extra[key] = value;
      }
      else
      {
        extra.Remove(key);
      }
    }
  }

  internal static void RetainAccountMarker(SentryUser user, string? accountId)
  {
    foreach (var key in user.Other.Keys.ToArray())
    {
      if (!string.Equals(key, SentryAccountSync.AccountMarker, StringComparison.Ordinal))
      {
        user.Other.Remove(key);
      }
    }

    if (accountId is null)
    {
      user.Other.Remove(SentryAccountSync.AccountMarker);
    }
    else
    {
      user.Other[SentryAccountSync.AccountMarker] = accountId;
    }
  }

  private static Dictionary<string, string> StringExtras(object? source)
  {
    var extras = new Dictionary<string, string>(StringComparer.Ordinal);
    if (source is not IDictionary<string, object?> extra)
    {
      return extras;
    }

    foreach (var pair in extra)
    {
      if (pair.Value is string text)
      {
        extras[pair.Key] = text;
      }
    }

    return extras;
  }

  private static string? AccountIdFor(SentryUser user)
  {
    if (user.Other.TryGetValue(SentryAccountSync.AccountMarker, out var captured) &&
        !string.IsNullOrWhiteSpace(captured))
    {
      return captured;
    }

    return string.Equals(user.Id, SentryAccountSync.BoundAccountId, StringComparison.Ordinal)
        ? user.Id
        : null;
  }

  private static string? ExtraString(IReadOnlyDictionary<string, string> extras, string key) =>
      extras.TryGetValue(key, out var value) ? value : null;
}
