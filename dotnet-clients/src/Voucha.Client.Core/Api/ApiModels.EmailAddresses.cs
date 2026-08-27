using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record EmailAddress(
    [property: JsonPropertyName("email_address")] string Address,
    [property: JsonPropertyName("is_primary")] bool IsPrimary,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record EmailAddressListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EmailAddress> Results,
    [property: JsonPropertyName("page_info")] PageInfo? PageInfo = null);

public sealed record EmailAddressRequestResponse(
    [property: JsonPropertyName("email_address")] string EmailAddress);

public sealed record RequestEmailAddressVerificationBody(
    [property: JsonPropertyName("email_address")] string EmailAddress);

public sealed record VerifyEmailAddressBody(
    [property: JsonPropertyName("token")] string Token);
