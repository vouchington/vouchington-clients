using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private static readonly HashSet<string> activeMembershipStatuses =
      new(StringComparer.OrdinalIgnoreCase) { "active", "past_due" };

  private IReadOnlyList<MembershipPlanOptionViewModel> membershipPlanOptions = [];
  private IReadOnlyDictionary<string, IReadOnlyList<MembershipSku>>? lastMembershipPlans;
  private MembershipBenefitCatalog? lastMembershipBenefitCatalog;
  private string membershipActionNotice = string.Empty;

  public IReadOnlyList<MembershipPlanOptionViewModel> MembershipPlanOptions
  {
    get => membershipPlanOptions;
    private set => SetProperty(ref membershipPlanOptions, value);
  }

  public string MembershipActionNotice
  {
    get => membershipActionNotice;
    private set => SetProperty(ref membershipActionNotice, value);
  }

  private MembershipPlanOptionViewModel[] BuildMembershipPlanOptions(
      IReadOnlyDictionary<string, IReadOnlyList<MembershipSku>> plans,
      Membership? membership,
      MembershipBenefitCatalog? benefitCatalog)
  {
    lastMembershipPlans = plans;
    lastMembershipBenefitCatalog = benefitCatalog;
    var effectivePlan = GetEffectivePlanSlug(membership);

    return MembershipPlanCatalog.Definitions.Select(definition =>
    {
      var priceOptions = BuildPriceOptions(definition.Slug, plans);
      var isCurrentPlan = string.Equals(definition.Slug, effectivePlan, StringComparison.OrdinalIgnoreCase);
      var name = UiText.Localized(definition.NameKey);

      return new MembershipPlanOptionViewModel(
          definition.Slug,
          name,
          priceOptions,
          UiText.Localized(isCurrentPlan ? UiMessageKey.NativeDotnetResidualCurrentPlan : UiMessageKey.NativeDotnetResidualAvailable),
          UiText.Localized(isCurrentPlan ? UiMessageKey.NativeDotnetResidualCurrentPlan : UiMessageKey.NativeDotnetResidualNativeBillingPending),
          isCurrentPlan
              ? UiText.Localized(UiMessageKey.NativeDotnetResidualAlreadyOnPlan, ("plan", name))
              : UiText.Localized(UiMessageKey.NativeDotnetResidualBillingUnavailable),
          isCurrentPlan,
          BuildMembershipBenefitBullets(definition.Slug, benefitCatalog),
          localization);
    }).ToArray();
  }

  private MembershipBenefitBullet[] BuildMembershipBenefitBullets(string plan, MembershipBenefitCatalog? catalog)
  {
    if (catalog is null)
    {
      return LegacyMembershipBenefitBullets(plan);
    }

    return catalog.Groups
        .SelectMany(group => group.Benefits)
        .Where(benefit => benefit.Placements.Contains("card", StringComparer.Ordinal))
        .Select(benefit => PresentMembershipBenefit(benefit, plan))
        .OfType<MembershipBenefitBullet>()
        .ToArray();
  }

  private MembershipBenefitBullet? PresentMembershipBenefit(MembershipBenefit benefit, string plan)
  {
    var labels = new Dictionary<string, UiMessageKey>(StringComparer.Ordinal)
    {
      ["public_contribution_access"] = UiMessageKey.ExtractedMembershipsBenefitCatalogAccess0c11c1a5,
      ["contribution_capacity"] = UiMessageKey.ExtractedMembershipsBenefitCatalogContributionCapacity0c11c1a7,
      ["automatic_post_topics"] = UiMessageKey.ExtractedMembershipsBenefitCatalogAutomaticPostTopics0c11c1ab,
      ["ugc_downvote_counts"] = UiMessageKey.ExtractedMembershipsBenefitCatalogDownvoteCounts0c11c1b1,
      ["community_agent_rules"] = UiMessageKey.ExtractedMembershipsBenefitCatalogCommunityAgentRules0c11c1b5,
      ["support_service_level"] = UiMessageKey.ExtractedMembershipsBenefitCatalogSupportServiceLevel0c11c1c1,
    };
    if (!labels.TryGetValue(benefit.Id, out var labelKey)
        || !benefit.Values.TryGetValue(plan, out var value)
        || PresentMembershipBenefitValue(value) is not { } valueText)
    {
      return null;
    }
    return new MembershipBenefitBullet(UiText.Localized(labelKey), valueText, localization);
  }

  private static UiText? PresentMembershipBenefitValue(MembershipBenefitValue value)
  {
    return value.Kind switch
    {
      "availability" when value.Included is not null => UiText.Localized(value.Included == true
          ? UiMessageKey.ExtractedMembershipsPlanComparisonTableIncludedBa829a98
          : UiMessageKey.ExtractedMembershipsPlanComparisonTableNotIncludedB665bfc2),
      "quantity" when value.Quantity == 0 => UiText.Localized(UiMessageKey.ExtractedMembershipsBenefitCatalogNone0c11c1bb),
      "quantity" when value.Quantity > 0 => UiText.Verbatim(value.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
      "access" when value.Access == "immediate" => UiText.Localized(UiMessageKey.ExtractedMembershipsBenefitCatalogImmediate0c11c1ba),
      "access" when value.Access == "after_wait" => UiText.Localized(UiMessageKey.ExtractedMembershipsBenefitCatalogAfterWait0c11c1b9),
      "level" => PresentMembershipBenefitLevel(value.Level),
      _ => null,
    };
  }

  private static UiText? PresentMembershipBenefitLevel(string? level)
  {
    var key = level switch
    {
      "none" => UiMessageKey.ExtractedMembershipsBenefitCatalogNone0c11c1bb,
      "standard" => UiMessageKey.ExtractedMembershipsBenefitCatalogStandard0c11c1bc,
      "more" => UiMessageKey.ExtractedMembershipsBenefitCatalogMore0c11c1bd,
      "most" => UiMessageKey.ExtractedMembershipsBenefitCatalogMost0c11c1be,
      "higher" => UiMessageKey.ExtractedMembershipsBenefitCatalogHigher0c11c1bf,
      "priority" => UiMessageKey.ExtractedMembershipsBenefitCatalogPriority0c11c1c3,
      "highest_priority" => UiMessageKey.ExtractedMembershipsBenefitCatalogHighestPriority0c11c1c4,
      _ => (UiMessageKey?)null,
    };
    return key is null ? null : UiText.Localized(key.Value);
  }

  private static string GetEffectivePlanSlug(Membership? membership)
  {
    if (membership is null)
    {
      return "free";
    }

    return activeMembershipStatuses.Contains(membership.Status) ? membership.Plan ?? "free" : "free";
  }

  private MembershipPlanPriceOptionViewModel[] BuildPriceOptions(
      string planSlug,
      IReadOnlyDictionary<string, IReadOnlyList<MembershipSku>> plans)
  {
    if (planSlug == "free")
    {
      return [new MembershipPlanPriceOptionViewModel(
          "free",
          UiText.Verbatim(FormatPrice(new Money(0, "usd"))),
          UiText.Localized(UiMessageKey.NativeDotnetResidualFreeTier),
          localization)];
    }

    if (!plans.TryGetValue(planSlug, out var skus) || skus.Count == 0)
    {
      return [];
    }

    return skus
        .OrderBy(sku => IntervalRank(sku.Interval))
        .ThenBy(sku => sku.Price.Amount)
        .Select(sku =>
        {
          return new MembershipPlanPriceOptionViewModel(
              $"{sku.Plan}-{sku.Interval}-{sku.Price.Currency}-{sku.Price.Amount}",
              UiText.Verbatim(string.Concat(FormatPrice(sku.Price), " ", FormatInterval(sku.Interval))),
              IntervalText(sku.Interval),
              localization);
        })
        .ToArray();
  }

  private static int IntervalRank(string? interval) => interval?.ToUpperInvariant() switch
  {
    "MONTH" or "MONTHLY" => 0,
    "YEAR" or "YEARLY" => 1,
    _ => 2,
  };

  private string FormatPrice(Money price)
  {
    var amount = price.ToKnownCurrencyMajorUnits();
    var exponent = Currency.MinorUnitExponentFor(price.Currency);
    return amount is null || exponent is null
        ? localization.Localize(UiMessageKey.NativeSwiftMembershipPriceUnavailable)
        : localization.FormatCurrency(
            amount.Value,
            price.Currency,
            exponent.Value,
            exponent.Value);
  }

  private static UiText IntervalText(string? interval) => interval?.ToUpperInvariant() switch
  {
    "MONTH" or "MONTHLY" => UiText.Localized(UiMessageKey.NativeDotnetResidualMonthly),
    "YEAR" or "YEARLY" => UiText.Localized(UiMessageKey.NativeDotnetResidualYearly),
    _ => UiText.ProtocolValue(interval),
  };

  private string FormatInterval(string? interval) => localization.Resolve(IntervalText(interval));
}
