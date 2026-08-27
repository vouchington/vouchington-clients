using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private IProfileSafetyService? profileSafetyService;
  private bool canActOnUser;
  private bool isMutedUser;
  private bool isBlockedUser;
  private bool isReportingUser;
  private int isTogglingUserBookmark;

  public bool CanActOnUser
  {
    get => canActOnUser;
    private set
    {
      if (SetProperty(ref canActOnUser, value))
      {
        OnPropertyChanged(nameof(CanViewUserTags));
        OnPropertyChanged(nameof(CanVoteUserTrust));
      }
    }
  }

  public bool IsMutedUser
  {
    get => isMutedUser;
    private set
    {
      if (SetProperty(ref isMutedUser, value)) OnPropertyChanged(nameof(MuteUserButtonText));
    }
  }

  public bool IsBlockedUser
  {
    get => isBlockedUser;
    private set
    {
      if (SetProperty(ref isBlockedUser, value))
      {
        OnPropertyChanged(nameof(BlockUserButtonText));
        RefreshFollowActionProperties();
      }
    }
  }

  public bool IsReportingUser
  {
    get => isReportingUser;
    private set => SetProperty(ref isReportingUser, value);
  }

  public string MuteUserButtonText => localization.Localize(
      IsMutedUser
          ? UiMessageKey.NativeDotnetProfileUnmute
          : UiMessageKey.NativeDotnetProfileMute);

  public string BlockUserButtonText => localization.Localize(
      IsBlockedUser
          ? UiMessageKey.NativeDotnetProfileUnblock
          : UiMessageKey.NativeDotnetProfileBlock);


  public void ConfigureProfileSafetyService(IProfileSafetyService safetyService) =>
      profileSafetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));

  public async Task ToggleMuteUserAsync(CancellationToken cancellationToken = default) =>
      await ToggleUserBookmarkAsync(BookmarkPredicate.Mute, !IsMutedUser, cancellationToken).ConfigureAwait(true);

  public async Task ToggleBlockUserAsync(CancellationToken cancellationToken = default) =>
      await ToggleUserBookmarkAsync(BookmarkPredicate.Block, !IsBlockedUser, cancellationToken).ConfigureAwait(true);

  public async Task<bool> ReportUserAsync(
      string reason = "spam",
      string? note = null,
      string? turnstileToken = null,
      CancellationToken cancellationToken = default)
  {
    if (IsReportingUser || !CanActOnUser || User?.Id is not { Length: > 0 } userId || profileSafetyService is null) return false;
    IsReportingUser = true;
    ErrorMessage = null;
    try
    {
      await profileSafetyService.ReportUserAsync(userId, reason, note, turnstileToken, cancellationToken)
          .ConfigureAwait(true);
      return true;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsReportingUser = false;
    }
  }

  private async Task LoadProfileSafetyAsync(CancellationToken cancellationToken)
  {
    RefreshProfileActionEligibility();
    IsMutedUser = false;
    IsBlockedUser = false;
    IsFollowingUser = false;
    if (!CanActOnUser || User?.Id is not { Length: > 0 } userId || profileSafetyService is null) return;

    ErrorMessage = null;
    try
    {
      var bookmarks = await profileSafetyService.FetchUserBookmarksAsync(userId, cancellationToken)
          .ConfigureAwait(true);
      IsMutedUser = bookmarks.IsActive(BookmarkPredicate.Mute);
      IsBlockedUser = bookmarks.IsActive(BookmarkPredicate.Block);
      IsFollowingUser = bookmarks.IsActive(BookmarkPredicate.Follow);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
  }

  private async Task ToggleUserBookmarkAsync(
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken)
  {
    if (System.Threading.Interlocked.CompareExchange(ref isTogglingUserBookmark, 1, 0) != 0) return;
    if (!CanActOnUser || User?.Id is not { Length: > 0 } userId || profileSafetyService is null)
    {
      System.Threading.Interlocked.Exchange(ref isTogglingUserBookmark, 0);
      return;
    }
    var wasMuted = IsMutedUser;
    var wasBlocked = IsBlockedUser;
    var wasFollowing = IsFollowingUser;
    ErrorMessage = null;
    SetLocalBookmarkState(predicate, active);
    try
    {
      await profileSafetyService.SetUserBookmarkAsync(userId, predicate, active, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      IsMutedUser = wasMuted;
      IsBlockedUser = wasBlocked;
      IsFollowingUser = wasFollowing;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      IsMutedUser = wasMuted;
      IsBlockedUser = wasBlocked;
      IsFollowingUser = wasFollowing;
      ErrorMessage = ex.Message;
    }
    finally
    {
      System.Threading.Interlocked.Exchange(ref isTogglingUserBookmark, 0);
    }
  }

  private void SetLocalBookmarkState(BookmarkPredicate predicate, bool active)
  {
    if (predicate == BookmarkPredicate.Mute) IsMutedUser = active;
    if (predicate == BookmarkPredicate.Block)
    {
      IsBlockedUser = active;
      if (active) IsFollowingUser = false;
    }
    if (predicate == BookmarkPredicate.Follow) IsFollowingUser = active;
  }

}
