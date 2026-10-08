using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public async Task<bool> UpdateAutomodActionAsync(string action, CancellationToken cancellationToken = default)
  {
    if (!CanModerateCommunity || IsLoading || action is not ("record_only" or "review_queue" or "unpublish"))
      return false;

    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    BeginMutation();
    try
    {
      var response = await service.UpdateAutomodActionAsync(
          community,
          new UpdateCommunityAutomodSettingsRequest(action),
          cancellationToken).ConfigureAwait(true);
      if (!string.Equals(communityIdOrSlug, community, StringComparison.Ordinal) ||
          revision != Volatile.Read(ref communityContextRevision)) return false;
      Community = response.Community;
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      if (revision == Volatile.Read(ref communityContextRevision)) State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (string.Equals(communityIdOrSlug, community, StringComparison.Ordinal) &&
          revision == Volatile.Read(ref communityContextRevision))
        HandleLoadException(ex);
      return false;
    }
  }
}
