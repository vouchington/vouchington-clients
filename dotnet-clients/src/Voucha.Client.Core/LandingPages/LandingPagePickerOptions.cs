using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public enum LandingPageAddType
{
  Link,
  ProfileLink,
  Review,
  ReferralLink,
  TopicGroup,
}

public enum LandingPageGroupMemberType
{
  Review,
  ReferralLink,
}

public sealed record LandingPageItemTypeOption(
    LandingPageAddType Type,
    UiText LabelText,
    IUiLocalization Localization)
{
  public string LocalizedLabel => Localization.Resolve(LabelText);
}

public sealed record LandingPagePickerOption(
    string Id,
    UiText LabelText,
    IUiLocalization Localization)
{
  public string LocalizedLabel => Localization.Resolve(LabelText);
}

public sealed record LandingPageGroupMemberOption(
    string Id,
    LandingPageGroupMemberType Type,
    UiText LabelText,
    bool IsSelected,
    IUiLocalization Localization)
{
  public string LocalizedLabel => Localization.Resolve(LabelText);
}
