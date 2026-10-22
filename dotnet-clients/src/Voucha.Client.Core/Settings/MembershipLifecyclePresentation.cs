using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed class MembershipLifecycleLineViewModel
{
  private readonly UiText title;
  private readonly UiText? value;
  private readonly UiText? token;
  private readonly IUiLocalization localization;

  public MembershipLifecycleLineViewModel(
      string id,
      UiText title,
      UiText? value,
      UiText? token,
      IUiLocalization localization)
  {
    Id = id;
    this.title = title;
    this.value = value;
    this.token = token;
    this.localization = localization;
  }

  public string Id { get; }

  public string Text
  {
    get
    {
      var parts = new List<string> { localization.Resolve(title) };
      if (value is { } valueText)
      {
        parts.Add(localization.Resolve(valueText));
      }

      if (token is { } tokenText)
      {
        parts.Add(localization.Resolve(tokenText));
      }

      return string.Join(" ", parts.Where(part => part.Length > 0));
    }
  }
}

public static class MembershipLifecyclePresentation
{
  public static IReadOnlyList<MembershipLifecycleLineViewModel> Lines(
      NativeMembershipOverview overview,
      bool stale,
      IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(overview);
    ArgumentNullException.ThrowIfNull(localization);
    var lines = new List<MembershipLifecycleLineViewModel>();
    if (stale)
    {
      lines.Add(Line("stale", Unavailable(), null, null, localization));
    }

    lines.AddRange(overview.Sources.Select(source => Line(
        "source-" + source.Id,
        StatusLabel(source.Status),
        PlanText(source.Plan),
        UiText.ProtocolValue(string.Join(
            ' ',
            new[] { source.Status, source.EffectiveAt, source.AccessEndsAt, source.Renewal?.EffectiveAt }
                .Where(part => !string.IsNullOrEmpty(part)))),
        localization)));
    lines.AddRange(overview.Pending.Switches.Select(item => Line(
        "switch-" + item.SourceId,
        Pending(),
        PlanText(item.Plan),
        UiText.ProtocolValue(item.EligibleAt),
        localization)));
    if (overview.Pending.Grants > 0)
    {
      lines.Add(Line(
          "grants",
          UiText.Localized(UiMessageKey.NativeSwiftMembershipMembershipGrants),
          null,
          UiText.ProtocolValue(overview.Pending.Grants.ToString(System.Globalization.CultureInfo.InvariantCulture)),
          localization));
    }

    lines.AddRange(overview.Pending.FinancialOperations.Select(operation => Line(
        "operation-" + operation.Id,
        operation.Status == "pending" ? Pending() : Unavailable(),
        UiText.ProtocolValue(operation.Kind),
        UiText.ProtocolValue(operation.Status),
        localization)));
    lines.AddRange(overview.Pending.Verifications.Select(verification => Line(
        "verification-" + verification.Id,
        verification.Status == "pending" ? Pending() : Unavailable(),
        UiText.ProtocolValue(verification.Status),
        verification.ReasonCode is { } reason ? UiText.ProtocolValue(reason) : null,
        localization)));
    if (overview.Management?.Destination is { } destination)
    {
      lines.Add(Line("management", Unavailable(), null, UiText.ProtocolValue(destination), localization));
    }

    return lines;
  }

  private static MembershipLifecycleLineViewModel Line(
      string id,
      UiText title,
      UiText? value,
      UiText? token,
      IUiLocalization localization) =>
      new(id, title, value, token, localization);

  private static UiText Pending() => UiText.Localized(UiMessageKey.NativeDotnetResidualNativeBillingPending);

  private static UiText Unavailable() =>
      UiText.Localized(UiMessageKey.NativeDotnetResidualBillingUnavailable);

  private static UiText PlanText(string plan) => plan switch
  {
    "free" => UiText.Localized(UiMessageKey.NativeDotnetSettingsPlanFree),
    "plus" => UiText.Localized(UiMessageKey.NativeDotnetSettingsPlanPlus),
    "pro" => UiText.Localized(UiMessageKey.NativeDotnetSettingsPlanPro),
    _ => UiText.ProtocolValue(plan),
  };

  private static UiText StatusLabel(string status) => status switch
  {
    "active" => UiText.Localized(UiMessageKey.NativeDotnetSettingsStatusActive),
    "past_due" => UiText.Localized(UiMessageKey.NativeDotnetSettingsStatusPastDue),
    "paused" => UiText.Localized(UiMessageKey.NativeDotnetSettingsStatusPaused),
    _ => UiText.ProtocolValue(status),
  };
}
