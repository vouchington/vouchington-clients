namespace Voucha.Client.Core.Localization;

public static partial class UiTaxonomy
{
  public static readonly IReadOnlyList<UiProtocolOptionDefinition> CrmStatuses =
  [
    O("new", UiMessageKey.NativeDotnetCrmCrmNew),
    O("awaiting_response", UiMessageKey.NativeDotnetCrmCrmAwaitingResponse),
    O("in_conversation", UiMessageKey.NativeDotnetCrmCrmInConversation),
    O("converted", UiMessageKey.NativeDotnetCrmCrmConverted),
    O("archived", UiMessageKey.NativeDotnetCrmCrmArchived),
    O("opted_out", UiMessageKey.NativeDotnetCrmCrmOptedOut),
  ];

  public static readonly IReadOnlyList<UiProtocolOptionDefinition> CrmVerticals =
  [
    O("credit_cards", UiMessageKey.NativeTaxonomyCrmCreditCards),
    O("travel", UiMessageKey.NativeTaxonomyCrmTravel),
    O("cars", UiMessageKey.NativeTaxonomyCrmCars),
    O("ai", UiMessageKey.NativeTaxonomyCrmAi),
    O("technology", UiMessageKey.NativeTaxonomyCrmTechnology),
    O("finance", UiMessageKey.NativeTaxonomyCrmFinance),
    O("lifestyle", UiMessageKey.NativeTaxonomyCrmLifestyle),
    O("other", UiMessageKey.NativeTaxonomyCrmOther),
  ];

  public static readonly IReadOnlyList<UiProtocolOptionDefinition> CrmContactTypes =
  [
    O("influencer", UiMessageKey.NativeTaxonomyCrmInfluencer),
    O("customer", UiMessageKey.NativeTaxonomyCrmCustomer),
    O("partner", UiMessageKey.NativeTaxonomyCrmPartner),
  ];

  public static UiText CrmStatus(string? value) =>
      OptionText(CrmStatuses, value);

  public static UiText CrmVertical(string? value) =>
      OptionText(CrmVerticals, value);

  public static UiText CrmContactType(string? value) =>
      OptionText(CrmContactTypes, value);

  public static UiText MessageDirection(string? value) => UiText.Localized(value switch
  {
    "inbound" => UiMessageKey.NativeDotnetCrmCrmInbound,
    "outbound" => UiMessageKey.NativeDotnetCrmCrmOutbound,
    _ => UiMessageKey.NativeDotnetCrmCrmUnknownDirection,
  });
}
