namespace Voucha.Client.Core;

public sealed record SentryBreadcrumbDraft(
    string? Category,
    string? Message,
    IReadOnlyDictionary<string, string>? Data);

public sealed record SentryOutboundEvent(
    string? AccountId,
    string? Email,
    string? Username,
    string? IpAddress,
    string? RequestTarget,
    string? QueryString,
    IReadOnlyDictionary<string, string>? Headers,
    string? Cookies,
    string? Body,
    IReadOnlyDictionary<string, string>? Extras,
    IReadOnlyList<SentryBreadcrumbDraft>? Breadcrumbs);

public interface ISentryEventTransport
{
  void Send(SentryOutboundEvent scrubbed);
}

public static class SentryEventScrubber
{
  public const string Filtered = "[Filtered]";

  private static readonly HashSet<string> CredentialHeaders = new(StringComparer.OrdinalIgnoreCase)
  {
    "authorization",
    "proxy-authorization",
    "x-api-key",
    "cf-access-jwt-assertion",
    "x-cf-worker-secret",
    "x-bedrock-batch-shared-key",
    "x-voucha-cache-purge-secret",
    "x-app-attest-assertion",
    "x-app-attest-nonce",
    "x-app-attest-challenge-id",
    "stripe-signature",
    "signature",
    "copyright-guest-capability",
    "idempotency-key",
    "cookie",
  };

  private static readonly HashSet<string> DroppedExtraKeys = new(StringComparer.OrdinalIgnoreCase)
  {
    "body",
    "request_body",
    "response_body",
    "http.request.body.data",
    "chat",
    "post",
    "prompt",
    "completion",
    "email_code",
    "otp",
    "token",
    "access_token",
    "refresh_token",
    "authorization",
    "cookie",
    "password",
    "secret",
  };

  private static readonly HashSet<string> TypedCategories = new(StringComparer.OrdinalIgnoreCase)
  {
    "ui.text",
    "ui.input",
    "ui.textfield",
    "user.input",
  };

  public static bool DropsExtra(string key)
  {
    ArgumentNullException.ThrowIfNull(key);
    return DroppedExtraKeys.Contains(key.Trim());
  }

  public static SentryOutboundEvent Scrub(SentryOutboundEvent outbound)
  {
    ArgumentNullException.ThrowIfNull(outbound);
    var extras = outbound.Extras?
        .Where(pair => !DropsExtra(pair.Key))
        .ToDictionary(pair => pair.Key, pair => Retain(pair.Key, pair.Value), StringComparer.Ordinal);
    return outbound with
    {
      AccountId = Trimmed(outbound.AccountId),
      Email = null,
      Username = null,
      IpAddress = null,
      RequestTarget = StripQuery(outbound.RequestTarget),
      QueryString = null,
      Headers = ScrubHeaders(outbound.Headers),
      Cookies = string.IsNullOrEmpty(outbound.Cookies) ? outbound.Cookies : Filtered,
      Body = null,
      Extras = extras,
      Breadcrumbs = outbound.Breadcrumbs?.Select(ScrubBreadcrumb).Where(item => item is not null).Cast<SentryBreadcrumbDraft>().ToArray(),
    };
  }

  public static bool DropsBreadcrumb(string? category, IEnumerable<string>? dataKeys)
  {
    var name = category?.Trim();
    if (name is not null &&
        (TypedCategories.Contains(name) ||
         name.Contains("text", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("input", StringComparison.OrdinalIgnoreCase)))
    {
      return true;
    }

    return dataKeys?.Any(key => IsTypedKey(key)) == true;
  }

  public static SentryBreadcrumbDraft? ScrubBreadcrumb(SentryBreadcrumbDraft breadcrumb)
  {
    ArgumentNullException.ThrowIfNull(breadcrumb);
    if (DropsBreadcrumb(breadcrumb.Category, breadcrumb.Data?.Keys))
    {
      return null;
    }

    var data = breadcrumb.Data?.Where(pair =>
            !DropsExtra(pair.Key) && !IsCredentialHeader(pair.Key) && !IsSeparateRequestPart(pair.Key))
        .ToDictionary(
            pair => pair.Key,
            pair => pair.Key.Contains("url", StringComparison.OrdinalIgnoreCase) ? StripQuery(pair.Value) ?? pair.Value : pair.Value,
            StringComparer.Ordinal);
    return breadcrumb with { Message = StripQuery(breadcrumb.Message), Data = data };
  }

  private static Dictionary<string, string>? ScrubHeaders(IReadOnlyDictionary<string, string>? headers) =>
      headers?.ToDictionary(
          pair => pair.Key,
          pair => IsCredentialHeader(pair.Key) ? Filtered : pair.Value,
          StringComparer.Ordinal);

  private static bool IsSeparateRequestPart(string name) =>
      name.Split(['.', '_', '-', ' '], StringSplitOptions.RemoveEmptyEntries).Any(token =>
          token.StartsWith("query", StringComparison.OrdinalIgnoreCase) ||
          token.StartsWith("fragment", StringComparison.OrdinalIgnoreCase));

  private static string Retain(string key, string value) =>
      key.Contains("url", StringComparison.OrdinalIgnoreCase) || value.Contains("://", StringComparison.Ordinal)
          ? StripQuery(value) ?? value
          : value;

  private static bool IsCredentialHeader(string name)
  {
    var key = name.Trim();
    return key.StartsWith("x-app-attest-", StringComparison.OrdinalIgnoreCase) || CredentialHeaders.Contains(key);
  }

  private static bool IsTypedKey(string key)
  {
    var name = key.Trim();
    return name.Equals("text", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("input", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("typed", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("value", StringComparison.OrdinalIgnoreCase);
  }

  private static string? StripQuery(string? url)
  {
    if (string.IsNullOrEmpty(url))
    {
      return url;
    }

    var query = url.IndexOf('?', StringComparison.Ordinal);
    var fragment = url.IndexOf('#', StringComparison.Ordinal);
    var cut = FirstCut(query, fragment);
    return cut < 0 ? url : url[..cut];
  }

  private static int FirstCut(int query, int fragment)
  {
    if (query < 0)
    {
      return fragment;
    }

    return fragment < 0 ? query : Math.Min(query, fragment);
  }

  private static string? Trimmed(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}
