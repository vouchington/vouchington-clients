using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public bool CanModerateCommunity
  {
    get
    {
      var identity = sessionStore?.Current.Identity;
      if (identity is null || Community is null)
      {
        return false;
      }

      return CanModerateAsCommunityMember ||
          UserHasRole(identity, "administrator");
    }
  }

  public bool CanManageCommunity
  {
    get
    {
      var identity = sessionStore?.Current.Identity;
      if (identity is null || Community is null)
      {
        return false;
      }

      return Membership is { Role: "owner" } ||
          UserHasRole(identity, "administrator");
    }
  }

  public bool CanManageMembers => CanModerateAsCommunityMember;

  public bool CanViewRawModerationAnalytics
  {
    get
    {
      return CanModerateCommunity || HasDurableSiteModerationRole;
    }
  }

  public bool CanUseModmail
  {
    get
    {
      var identity = sessionStore?.Current.Identity;
      return Membership is not null ||
          (identity is not null && (UserHasRole(identity, "administrator") || UserHasRole(identity, "moderator")));
    }
  }

  private bool CanModerateAsCommunityMember => Membership is { Role: "owner" or "moderator" };

  private bool HasDurableSiteModerationRole
  {
    get
    {
      var identity = sessionStore?.Current.Identity;
      return identity is not null &&
          (UserHasRole(identity, "administrator") || UserHasRole(identity, "moderator"));
    }
  }

  private static bool UserHasRole(User identity, string role) =>
      identity.Roles?.Contains(role, StringComparer.Ordinal) == true;

  private void BeginMutation()
  {
    State = LoadState.Loading;
    ErrorMessage = null;
  }
}
