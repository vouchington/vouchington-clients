using Voucha.Client.Core.Api;
using System.Globalization;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactDetailViewModel
{
  private static CrmContactVertical? ParseVertical(string? value) => value?.Trim().ToUpperInvariant() switch
  {
    "CREDIT_CARDS" => CrmContactVertical.CreditCards,
    "CREDITCARDS" => CrmContactVertical.CreditCards,
    "TRAVEL" => CrmContactVertical.Travel,
    "CARS" => CrmContactVertical.Cars,
    "AI" => CrmContactVertical.Ai,
    "TECHNOLOGY" => CrmContactVertical.Technology,
    "FINANCE" => CrmContactVertical.Finance,
    "LIFESTYLE" => CrmContactVertical.Lifestyle,
    "OTHER" => CrmContactVertical.Other,
    _ => null,
  };

  private static CrmContactType? ParseContactType(string? value) => value?.Trim().ToUpperInvariant() switch
  {
    "INFLUENCER" => CrmContactType.Influencer,
    "CUSTOMER" => CrmContactType.Customer,
    "PARTNER" => CrmContactType.Partner,
    _ => null,
  };

  private static int? ParseInt(string? value) => int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
