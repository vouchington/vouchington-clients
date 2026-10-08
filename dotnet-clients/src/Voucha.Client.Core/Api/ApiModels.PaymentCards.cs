using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record PaymentCardTopic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug);

public sealed record PaymentCardParentSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("opened_on")] DateOnly? OpenedOn,
    [property: JsonPropertyName("closed_on")] DateOnly? ClosedOn,
    [property: JsonPropertyName("card")] PaymentCardTopic Card);

public sealed record PaymentCardWire(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("card_topic_id")] string CardId,
    [property: JsonPropertyName("opened_on")] DateOnly? OpenedOn,
    [property: JsonPropertyName("closed_on")] DateOnly? ClosedOn,
    [property: JsonPropertyName("received_sign_up_bonus_on")] DateOnly? ReceivedSignUpBonusOn,
    [property: JsonPropertyName("credit_limit")] Money? CreditLimit,
    [property: JsonPropertyName("is_authorized_user")] bool IsAuthorizedUser,
    [property: JsonPropertyName("authorized_user_of_card_id")] string? AuthorizedUserOfId,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("card")] PaymentCardTopic Card,
    [property: JsonPropertyName("authorized_user_of_card")] PaymentCardParentSummary? AuthorizedUserOfCard);

public sealed record PaymentCardsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<PaymentCardWire> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record PaymentCardResponse([property: JsonPropertyName("card")] PaymentCardWire Card);

public sealed record CreatePaymentCardBody([property: JsonPropertyName("card_topic_id")] string CardId);

public sealed record UpdatePaymentCardBody(
    [property: JsonPropertyName("opened_on")] JsonNullableDate? OpenedOn = null,
    [property: JsonPropertyName("closed_on")] JsonNullableDate? ClosedOn = null,
    [property: JsonPropertyName("received_sign_up_bonus_on")] JsonNullableDate? ReceivedSignUpBonusOn = null,
    [property: JsonPropertyName("credit_limit")] JsonNullableMoney? CreditLimit = null,
    [property: JsonPropertyName("is_authorized_user")] bool? IsAuthorizedUser = null,
    [property: JsonPropertyName("authorized_user_of_card_id")] JsonNullableString? AuthorizedUserOfId = null,
    [property: JsonPropertyName("note")] JsonNullableString? Note = null);
