using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.Core.Profiles;

public static class ProfileNavigationTargets
{
  public const string SignIn = "/login";

  public static string User(FriendRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    return NativeRoutePath.Entity("user", row.Username ?? row.Id);
  }

  public static string Topic(TopicRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    return NativeEntityDetailPaths.Topic(row.Id, row.Slug, row.ProtocolTopicType);
  }

  public static string Source(RssFeedSource row)
  {
    ArgumentNullException.ThrowIfNull(row);
    return NativeEntityDetailPaths.Source(row.Id, row.Topic?.Id, row.Topic?.Slug);
  }

  public static string Community(CommunityBrowseRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    return NativeRoutePath.Entity("communities", row.Slug);
  }
}
