using Sentry;
using Voucha.Client.Core;

namespace Voucha.Client.App;

internal static class SentryTransactionProjection
{
  public static SentryTransaction Scrub(SentryTransaction transaction)
  {
    transaction.Description = SentryTelemetryFields.ScrubAddress(transaction.Description) ?? "";
    ScrubRequest(transaction.Request);
    ScrubUser(transaction.User);
    ScrubData(transaction.Data);
    ScrubTags(transaction);
    foreach (var span in transaction.Spans)
    {
      span.Description = SentryTelemetryFields.ScrubAddress(span.Description) ?? "";
      ScrubData(span.Data);
    }

    return transaction;
  }

  private static void ScrubTags(SentryTransaction transaction)
  {
    foreach (var key in transaction.Tags.Keys.ToArray())
    {
      var kept = SentryTelemetryFields.ScrubField(key, transaction.Tags[key]);
      if (kept is null)
      {
        transaction.UnsetTag(key);
      }
      else
      {
        transaction.SetTag(key, kept);
      }
    }
  }

  private static void ScrubRequest(SentryRequest request)
  {
    var headers = request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    var scrubbed = SentryEventScrubber.Scrub(new SentryOutboundEvent(
        null,
        null,
        null,
        null,
        request.Url,
        request.QueryString,
        headers,
        request.Cookies,
        request.Data as string,
        null,
        null));
    request.Url = scrubbed.RequestTarget ?? "";
    request.QueryString = null;
    request.Cookies = scrubbed.Cookies;
    request.Data = null;
    foreach (var key in request.Headers.Keys.ToArray())
    {
      if (scrubbed.Headers is not null && scrubbed.Headers.TryGetValue(key, out var value))
      {
        request.Headers[key] = value;
      }
      else
      {
        request.Headers.Remove(key);
      }
    }
  }

  private static void ScrubUser(SentryUser user)
  {
    string? account = null;
    if (user.Other.TryGetValue(SentryAccountSync.AccountMarker, out var captured) &&
        !string.IsNullOrWhiteSpace(captured))
    {
      account = captured.Trim();
    }
    else if (string.Equals(user.Id, SentryAccountSync.BoundAccountId, StringComparison.Ordinal))
    {
      account = user.Id;
    }

    user.Id = account;
    user.Email = null;
    user.Username = null;
    user.IpAddress = null;
    SentryEventProjection.RetainAccountMarker(user, account);
  }

  private static void ScrubData(IReadOnlyDictionary<string, object?> data)
  {
    if (data is not IDictionary<string, object?> values)
    {
      return;
    }

    foreach (var key in values.Keys.ToArray())
    {
      switch (values[key])
      {
        case string text:
          var kept = SentryTelemetryFields.ScrubField(key, text);
          if (kept is null)
          {
            values.Remove(key);
          }
          else
          {
            values[key] = kept;
          }

          break;
        case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            when SentryTelemetryFields.KeepsNumber(key):
          break;
        default:
          values.Remove(key);
          break;
      }
    }
  }
}
