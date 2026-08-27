using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Crm;

internal static class CrmEnumFormatting
{
  public static string? ToCrmFieldValue(this CrmContactVertical? value) =>
      value is null ? null : value.Value.ToCrmFieldValue();

  public static string ToCrmFieldValue(this CrmContactVertical value) => value switch
  {
    CrmContactVertical.CreditCards => "credit_cards",
    CrmContactVertical.Travel => "travel",
    CrmContactVertical.Cars => "cars",
    CrmContactVertical.Ai => "ai",
    CrmContactVertical.Technology => "technology",
    CrmContactVertical.Finance => "finance",
    CrmContactVertical.Lifestyle => "lifestyle",
    CrmContactVertical.Other => "other",
    _ => value.ToString(),
  };

  public static string? ToCrmFieldValue(this CrmContactType value) => value switch
  {
    CrmContactType.Influencer => "influencer",
    CrmContactType.Customer => "customer",
    CrmContactType.Partner => "partner",
    _ => null,
  };
}
