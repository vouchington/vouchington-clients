using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private void ApplyDetail(CommunityResponse response)
  {
    Community = response.Community;
    Owner = response.User;
    Membership = response.Membership;
    CommunityMetrics = response.CommunityMetrics;
    HasPendingApplication = response.HasPendingApplication == true;
  }

  private void ApplyMembers(CommunityMembersResponse response, bool append = false)
  {
    var membersById = response.CommunityMembers;
    var rows = response.Results
        .Select(reference => reference.Id)
        .Where(id => id is not null && membersById.ContainsKey(id))
        .Select(id =>
        {
          var member = membersById[id!];
          response.Users.TryGetValue(member.UserId, out var user);
          return CommunityMemberRow.FromMember(member, user, localization);
        })
        .ToArray();
    Members = append ? AppendUnique(Members, rows, row => row.Id) : rows;
  }

  private void ApplyPosts(CommunityPostsResponse response, bool append = false)
  {
    var postsById = response.Posts;
    var metricsById = response.PostsMetrics;
    var rows = response.Results
        .Select(reference => reference.Id)
        .Where(id => id is not null && postsById.ContainsKey(id))
        .Select(id =>
        {
          UrlEmbed? embed = null;
          response.PostLinkEmbeds?.TryGetValue(id!, out embed);
          return CommunityPostRow.FromPost(
              postsById[id!],
              metricsById.TryGetValue(id!, out var metrics) ? metrics : null,
              embed,
              localization);
        })
        .ToArray();
    Posts = append ? AppendUnique(Posts, rows, row => row.Id) : rows;
  }

  private static IReadOnlyList<T> AppendUnique<T>(
      IReadOnlyList<T> existing,
      IEnumerable<T> additions,
      Func<T, string> idSelector)
  {
    var ids = existing.Select(idSelector).ToHashSet(StringComparer.Ordinal);
    return [.. existing, .. additions.Where(item => ids.Add(idSelector(item)))];
  }

  private bool HandleLoadException(Exception ex)
  {
    if (ex is not (VouchaApiException or HttpRequestException or InvalidOperationException))
    {
      return false;
    }

    ErrorMessage = ex.Message;
    State = LoadState.Error;
    return true;
  }

  public bool HandleActionException(Exception ex)
  {
    ArgumentNullException.ThrowIfNull(ex);

    if (ex is OperationCanceledException)
    {
      State = LoadState.Idle;
      return true;
    }

    ErrorMessage = ex.Message;
    State = LoadState.Error;
    return true;
  }

  private void RaiseDerivedStateChanged()
  {
    OnPropertyChanged(nameof(IsArchived));
    OnPropertyChanged(nameof(CanJoin));
    OnPropertyChanged(nameof(CanLeave));
    OnPropertyChanged(nameof(CanArchive));
    OnPropertyChanged(nameof(CanUnarchive));
    OnPropertyChanged(nameof(CanModerateCommunity));
    OnPropertyChanged(nameof(CanViewRawModerationAnalytics));
    OnPropertyChanged(nameof(CanManageCommunity));
    OnPropertyChanged(nameof(CanUseModmail));
  }
}
