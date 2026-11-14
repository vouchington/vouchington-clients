using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private NativeMembershipOverview? membershipOverview;
  private bool membershipSnapshotIsStale;
  private IReadOnlyList<MembershipLifecycleLineViewModel> membershipLifecycleLines = [];

  public NativeMembershipOverview? MembershipOverview
  {
    get => membershipOverview;
    private set => SetProperty(ref membershipOverview, value);
  }

  public IReadOnlyList<MembershipLifecycleLineViewModel> MembershipLifecycleLines
  {
    get => membershipLifecycleLines;
    private set => SetProperty(ref membershipLifecycleLines, value);
  }

  public void NoteMembershipOverviewStale()
  {
    if (MembershipOverview is null)
    {
      return;
    }

    membershipSnapshotIsStale = true;
    MembershipLifecycleLines = MembershipLifecyclePresentation.Lines(
        MembershipOverview,
        membershipSnapshotIsStale,
        localization);
  }

  private void ApplyNativeMembershipOverview(
      NativeMembershipOverview overview,
      MembershipBenefitCatalog? benefitCatalog)
  {
    MembershipOverview = overview;
    Membership = null;
    membershipSnapshotIsStale = false;
    lastMembershipBenefitCatalog = benefitCatalog;
    UpdateMembershipSummary();
    MembershipPlanOptions = BuildNativeMembershipPlanOptions(overview, benefitCatalog);
    MembershipLifecycleLines = MembershipLifecyclePresentation.Lines(overview, stale: false, localization);
  }

  private MembershipPlanOptionViewModel[] BuildNativeMembershipPlanOptions(
      NativeMembershipOverview overview,
      MembershipBenefitCatalog? benefitCatalog)
  {
    var effectivePlan = overview.VisiblePlan;
    return MembershipPlanCatalog.Definitions.Select(definition =>
    {
      var isCurrentPlan = string.Equals(definition.Slug, effectivePlan, StringComparison.OrdinalIgnoreCase);
      var name = UiText.Localized(definition.NameKey);
      return new MembershipPlanOptionViewModel(
          definition.Slug,
          name,
          NativePriceOptions(definition.Slug),
          UiText.Localized(isCurrentPlan
              ? UiMessageKey.NativeDotnetResidualCurrentPlan
              : UiMessageKey.NativeDotnetResidualAvailable),
          UiText.Localized(isCurrentPlan
              ? UiMessageKey.NativeDotnetResidualCurrentPlan
              : UiMessageKey.NativeDotnetResidualNativeBillingPending),
          isCurrentPlan
              ? UiText.Localized(UiMessageKey.NativeDotnetResidualAlreadyOnPlan, ("plan", name))
              : UiText.Localized(UiMessageKey.NativeDotnetResidualBillingUnavailable),
          isCurrentPlan,
          BuildMembershipBenefitBullets(definition.Slug, benefitCatalog),
          localization);
    }).ToArray();
  }

  private MembershipPlanPriceOptionViewModel[] NativePriceOptions(string planSlug)
  {
    if (planSlug == "free")
    {
      return
      [
        new MembershipPlanPriceOptionViewModel(
            "free",
            UiText.Localized(UiMessageKey.NativeDotnetSettingsPlanFree),
            UiText.Localized(UiMessageKey.NativeDotnetSettingsPlanFree),
            localization),
      ];
    }

    return
    [
      new MembershipPlanPriceOptionViewModel(
          planSlug + "-price",
          UiText.Localized(UiMessageKey.NativeSwiftMembershipPriceUnavailable),
          UiText.Localized(UiMessageKey.NativeSwiftMembershipPriceUnavailable),
          localization),
    ];
  }
}
