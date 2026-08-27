using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Auth;

internal sealed record SessionCookieState(IReadOnlyList<SessionCookie> Cookies);

internal sealed record SessionCookie(
    string Name,
    string Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ExpiresUtc = null);
