using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private readonly CursorPaginationState<CommunityModerationQueueEntry, string> automodFlagPages =
      new(entry => entry.Id);
  private readonly Dictionary<string, long> dismissingAutomodPosts = new(StringComparer.Ordinal);
  private long automodDismissalSequence;
  private bool automodFlagAccessGranted;
  private string? automodFlagActionError;
  private string? automodFlagNotice;

  public IReadOnlyList<CommunityModerationQueueEntry> AutomodFlags =>
      automodFlagAccessGranted && CanModerateCommunity ? automodFlagPages.Items : [];
  public bool HasMoreAutomodFlags => automodFlagAccessGranted && automodFlagPages.HasMore;
  public bool IsLoadingAutomodFlags => automodFlagPages.IsLoading;
  public string? AutomodFlagError => automodFlagPages.LastError ?? automodFlagActionError;
  public string? AutomodFlagNotice => automodFlagNotice;
  public bool CanAutomaticallyLoadAutomodFlags =>
      SelectedSection == CommunityDetailSurfaceSection.Moderation &&
      automodFlagAccessGranted && CanModerateCommunity && automodFlagPages.CanAutomaticallyLoad;
  public bool IsDismissingAutomodFlag(string postId) => dismissingAutomodPosts.ContainsKey(postId);

  public async Task LoadAutomodFlagsAsync(CancellationToken cancellationToken = default)
  {
    automodFlagPages.Reset();
    automodFlagAccessGranted = false;
    automodFlagActionError = null;
    automodFlagNotice = null;
    NotifyAutomodFlags();
    if (!CanModerateCommunity) return;
    await LoadAutomodFlagPageAsync(true, cancellationToken).ConfigureAwait(true);
  }

  public Task LoadMoreAutomodFlagsAsync(CancellationToken cancellationToken = default) =>
      LoadAutomodFlagPageAsync(false, cancellationToken);

  private async Task LoadAutomodFlagPageAsync(bool propagateFailure, CancellationToken cancellationToken)
  {
    if (!CanModerateCommunity || SelectedSection != CommunityDetailSurfaceSection.Moderation) return;
    var request = automodFlagPages.BeginNextPage();
    if (request is null) return;
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    try
    {
      var response = await service.FetchAutomodFlagPageAsync(community, request.Cursor, 20, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentAutomodContext(request, community, revision)) return;
      automodFlagAccessGranted = string.Equals(response.ViewerTier, "moderator", StringComparison.Ordinal) && CanModerateCommunity;
      automodFlagPages.Complete(
          request,
          automodFlagAccessGranted
              ? response.Entries.Where(entry => string.Equals(entry.QueueSource, "automod_flag", StringComparison.Ordinal))
              : [],
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage);
      NotifyAutomodFlags();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      automodFlagPages.Cancel(request);
      NotifyAutomodFlags();
      if (propagateFailure) throw;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException)
    {
      automodFlagPages.Fail(request, ex.Message);
      NotifyAutomodFlags();
      if (propagateFailure) throw;
    }
  }

  public async Task DismissAutomodFlagAsync(string postId, CancellationToken cancellationToken = default)
  {
    if (!CanModerateCommunity || !automodFlagAccessGranted ||
        !AutomodFlags.Any(entry => string.Equals(CommunityAutomodFlagTarget.PostId(entry), postId, StringComparison.Ordinal)) ||
        dismissingAutomodPosts.ContainsKey(postId)) return;
    var dismissal = unchecked(++automodDismissalSequence);
    dismissingAutomodPosts.Add(postId, dismissal);
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    automodFlagActionError = null;
    automodFlagNotice = null;
    NotifyAutomodFlags();
    try
    {
      await service.DismissAutomodFlagAsync(community, postId, cancellationToken).ConfigureAwait(true);
      if (IsCurrentAutomodContext(community, revision))
      {
        automodFlagPages.Remove(entry => string.Equals(CommunityAutomodFlagTarget.PostId(entry), postId, StringComparison.Ordinal));
        automodFlagNotice = localization.Localize(UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelAutomodFlagDismissed66ee7e46);
      }
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      if (IsCurrentAutomodContext(community, revision))
        await LoadAutomodFlagsAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException)
    {
      if (IsCurrentAutomodContext(community, revision))
        automodFlagActionError = localization.Localize(UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelFailedToDismissTheAutomodFlag0e50ec68);
    }
    finally
    {
      if (dismissingAutomodPosts.TryGetValue(postId, out var active) && active == dismissal)
        dismissingAutomodPosts.Remove(postId);
      NotifyAutomodFlags();
    }
  }

  private bool IsCurrentAutomodContext(CursorPageRequest request, string community, long revision) =>
      automodFlagPages.IsCurrent(request) && IsCurrentAutomodContext(community, revision);

  private bool IsCurrentAutomodContext(string community, long revision) =>
      string.Equals(communityIdOrSlug, community, StringComparison.Ordinal) &&
      revision == Volatile.Read(ref communityContextRevision) &&
      SelectedSection == CommunityDetailSurfaceSection.Moderation && CanModerateCommunity;

  private void InvalidateAutomodFlagRequests()
  {
    automodFlagPages.Reset();
    automodFlagAccessGranted = false;
    automodFlagActionError = null;
    automodFlagNotice = null;
    dismissingAutomodPosts.Clear();
    NotifyAutomodFlags();
  }

  private void NotifyAutomodFlags()
  {
    OnPropertyChanged(nameof(AutomodFlags));
    OnPropertyChanged(nameof(HasMoreAutomodFlags));
    OnPropertyChanged(nameof(IsLoadingAutomodFlags));
    OnPropertyChanged(nameof(AutomodFlagError));
    OnPropertyChanged(nameof(AutomodFlagNotice));
    OnPropertyChanged(nameof(CanAutomaticallyLoadAutomodFlags));
  }
}
