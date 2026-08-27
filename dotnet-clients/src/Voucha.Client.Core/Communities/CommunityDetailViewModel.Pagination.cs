using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private string? communityListEndCursor;
  private string? modmailThreadSurfaceId;
  private string? communityListPaginationErrorMessage;
  private bool isLoadingMoreCommunityList;
  private int communityListRevision;
  private int communityListPageRequestId;

  public bool CanLoadMoreCommunityList =>
      communityListEndCursor is not null && !isLoadingMoreCommunityList &&
      IsPagedCommunitySection;

  public bool HasMoreCommunityList => communityListEndCursor is not null &&
      IsPagedCommunitySection;

  public bool IsLoadingMoreCommunityList => isLoadingMoreCommunityList;

  public bool CanAutomaticallyLoadCommunityList =>
      HasMoreCommunityList && modmailThreadSurfaceId is null;

  private bool IsPagedCommunitySection => SelectedSection is
      CommunityDetailSurfaceSection.Members or
      CommunityDetailSurfaceSection.Posts or
      CommunityDetailSurfaceSection.News or
      CommunityDetailSurfaceSection.Applications or
      CommunityDetailSurfaceSection.Invites or
      CommunityDetailSurfaceSection.Bans or
      CommunityDetailSurfaceSection.Restrictions or
      CommunityDetailSurfaceSection.Modlog or
      CommunityDetailSurfaceSection.Moderation or
      CommunityDetailSurfaceSection.Modmail or
      CommunityDetailSurfaceSection.Settings;

  public string? CommunityListPaginationErrorMessage
  {
    get => communityListPaginationErrorMessage;
    private set
    {
      if (SetProperty(ref communityListPaginationErrorMessage, value))
      {
        OnPropertyChanged(nameof(HasCommunityListPaginationError));
      }
    }
  }

  public bool HasCommunityListPaginationError =>
      !string.IsNullOrWhiteSpace(CommunityListPaginationErrorMessage);

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "This UI command must convert every non-cancellation continuation failure into retry state.")]
  public async Task LoadMoreCommunityListAsync(CancellationToken cancellationToken = default)
  {
    if (communityListEndCursor is not { } after || isLoadingMoreCommunityList) return;

    var revision = Volatile.Read(ref communityListRevision);
    var pageRequest = Interlocked.Increment(ref communityListPageRequestId);
    var community = communityIdOrSlug;
    var section = SelectedSection;
    var threadIdAtRequest = modmailThreadSurfaceId;
    isLoadingMoreCommunityList = true;
    CommunityListPaginationErrorMessage = null;
    OnPropertyChanged(nameof(CanLoadMoreCommunityList));
    try
    {
      await LoadCommunityListPageAsync(
          after,
          revision,
          pageRequest,
          community,
          section,
          threadIdAtRequest,
          cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      if (IsCurrentCommunityPage(revision, pageRequest, community, section, threadIdAtRequest))
      {
        CommunityListPaginationErrorMessage = ex.Message;
      }
    }
    finally
    {
      if (pageRequest == Volatile.Read(ref communityListPageRequestId))
      {
        isLoadingMoreCommunityList = false;
        OnPropertyChanged(nameof(CanLoadMoreCommunityList));
      }
    }
  }

  private async Task LoadCommunityListPageAsync(
      string after,
      int revision,
      int pageRequest,
      string community,
      CommunityDetailSurfaceSection section,
      string? threadIdAtRequest,
      CancellationToken cancellationToken)
  {
    if (await TryLoadForwardCommunityPageAsync(
        after,
        revision,
        pageRequest,
        community,
        section,
        threadIdAtRequest,
        cancellationToken).ConfigureAwait(true)) return;

    if (section == CommunityDetailSurfaceSection.Settings)
    {
      var page = await service.FetchSavedRepliesPageAsync(community, after, 50, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadIdAtRequest)) return;
      AppendModeration(page.Results.Select(reply => Summary(
          reply.Id,
          UiText.UserContent(reply.Title),
          UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesSavedReply),
          UiText.UserContent(reply.Body))));
      communityListEndCursor = page.PageInfo.EndCursor;
      return;
    }

    if (threadIdAtRequest is { } threadId)
    {
      var page = await service.FetchModmailMessagesPageAsync(community, threadId, after, 50, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadIdAtRequest)) return;
      PrependModeration(page.Results.Select(message => Summary(
          message.Id,
          string.IsNullOrWhiteSpace(message.SenderUsername)
              ? UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesMessage)
              : UiText.UserContent($"@{message.SenderUsername}"),
          UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesModmail),
          UiText.UserContent(message.BodyText))));
      communityListEndCursor = page.PageInfo.EndCursor;
      return;
    }

    var threads = await service.FetchModmailPageAsync(community, after, 50, cancellationToken).ConfigureAwait(true);
    if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadIdAtRequest)) return;
    AppendModeration(threads.Results.Select(thread => Summary(
        thread.Id,
        UiText.Localized(
            UiMessageKey.NativeSwiftRebasedRouteSurfacesThread,
            ("id", UiText.ProtocolValue(thread.Id))),
        UiText.Localized(thread.ResolvedAt is null
            ? UiMessageKey.NativeSwiftRebasedRouteSurfacesOpen
            : UiMessageKey.NativeSwiftModerationAppealsResolved),
        (thread.SubjectUserId ?? thread.AssignedModId) is { } actor
            ? UiText.UserContent(actor)
            : UiText.Verbatim(localization.FormatDateTime(thread.UpdatedAt, TimeZoneInfo.Local)))));
    communityListEndCursor = threads.PageInfo.EndCursor;
  }

  private bool IsCurrentCommunityPage(
      int revision,
      int pageRequest,
      string community,
      CommunityDetailSurfaceSection section,
      string? threadId) =>
      revision == Volatile.Read(ref communityListRevision) &&
      pageRequest == Volatile.Read(ref communityListPageRequestId) &&
      string.Equals(communityIdOrSlug, community, StringComparison.Ordinal) &&
      SelectedSection == section &&
      string.Equals(modmailThreadSurfaceId, threadId, StringComparison.Ordinal);

  private void AppendModeration(IEnumerable<CommunitySummaryRow> rows)
  {
    var existingIds = Moderation.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
    Moderation = [.. Moderation, .. rows.Where(row => existingIds.Add(row.Id))];
  }

  private void PrependModeration(IEnumerable<CommunitySummaryRow> rows)
  {
    var existingIds = Moderation.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
    Moderation = [.. rows.Where(row => existingIds.Add(row.Id)), .. Moderation];
  }

  private void InvalidateCommunityListRequests()
  {
    Interlocked.Increment(ref communityListRevision);
    Interlocked.Increment(ref communityListPageRequestId);
    isLoadingMoreCommunityList = false;
    CommunityListPaginationErrorMessage = null;
    OnPropertyChanged(nameof(CanLoadMoreCommunityList));
  }

}
