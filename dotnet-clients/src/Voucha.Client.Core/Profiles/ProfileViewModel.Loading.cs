using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private int historyLoadVersion;
  private PageInfo? historyPageInfo;

  public async Task LoadOwnAsync(CancellationToken cancellationToken = default)
  {
    ResetUserTags();
    CanEdit = true;
    State = LoadState.Loading;
    ErrorMessage = null;

    try
    {
      var identityResponse = await settingsService.FetchMyIdentityAsync(cancellationToken).ConfigureAwait(true);
      Identity = identityResponse.Identity;
      loadedIdOrUsername = identityResponse.Identity.Id;
      var userResponse = await settingsService.FetchUserAsync(
          identityResponse.Identity.Id,
          includeBio: true,
          cancellationToken).ConfigureAwait(true);
      ApplyProfile(userResponse);
      await LoadHistoryAsync(
          identityResponse.Identity.Id,
          SelectedHistoryTab,
          NextHistoryLoadVersion(),
          cancellationToken).ConfigureAwait(true);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      CanEdit = false;
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      CanEdit = false;
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public async Task LoadPublicAsync(
      string idOrUsername,
      ProfileHistoryTab initialTab = ProfileHistoryTab.All,
      CancellationToken cancellationToken = default)
  {
    CanEdit = false;
    Identity = null;
    ResetUserTags();
    SelectedHistoryTab = initialTab;
    State = LoadState.Loading;
    ErrorMessage = null;

    try
    {
      loadedIdOrUsername = idOrUsername;
      var userResponse = await settingsService.FetchUserAsync(
          idOrUsername,
          includeBio: true,
          cancellationToken).ConfigureAwait(true);
      ApplyProfile(userResponse);
      await LoadProfileSafetyAsync(cancellationToken).ConfigureAwait(true);
      await LoadUserTrustContextAsync(cancellationToken).ConfigureAwait(true);
      await LoadHistoryAsync(
          userResponse.User.Id,
          SelectedHistoryTab,
          NextHistoryLoadVersion(),
          cancellationToken).ConfigureAwait(true);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public async Task SelectHistoryTabAsync(ProfileHistoryTab tab, CancellationToken cancellationToken = default)
  {
    if (SelectedHistoryTab == tab && HistoryItems.Count > 0) return;
    var loadVersion = NextHistoryLoadVersion();
    SelectedHistoryTab = tab;
    HistoryTabs = BuildTabs(userMetrics, tab);
    ErrorMessage = null;
    if (User?.Id is { } userId)
    {
      try
      {
        await LoadHistoryAsync(userId, tab, loadVersion, cancellationToken).ConfigureAwait(true);
      }
      catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        ErrorMessage = ex.Message;
      }
    }
  }

  private void ApplyProfile(UserResponse response)
  {
    User = response.User;
    userMetrics = response.UserMetrics;
    BioMarkdown = response.User.Markdown ?? string.Empty;
    BioHtml = response.UserBioHtml;
    ProfileLinks = response.ProfileLinks ?? [];
    HistoryTabs = BuildTabs(response.UserMetrics, SelectedHistoryTab);
    RefreshProfileActionEligibility();
  }

  private async Task LoadHistoryAsync(
      string creatorId,
      ProfileHistoryTab tab,
      int loadVersion,
      CancellationToken cancellationToken,
      string? after = null,
      bool append = false)
  {
    if (!append && loadVersion == historyLoadVersion)
    {
      historyPageInfo = null;
      OnPropertyChanged(nameof(CanLoadMore));
    }
    try
    {
      var response = await postsService.FetchPostsAsync(new FetchPostsRequest(
          Creator: creatorId,
          PostTypes: PostTypes(tab),
          Sort: "new",
          After: after,
          Limit: 25), cancellationToken).ConfigureAwait(true);
      if (loadVersion != historyLoadVersion) return;
      if (response.Posts is not { } posts)
      {
        if (!append) HistoryItems = [];
        historyPageInfo = response.PageInfo;
        OnPropertyChanged(nameof(CanLoadMore));
        return;
      }

      var rows = response.Results
          .Select(result => result.Id)
          .Where(id => id is not null && posts.ContainsKey(id))
          .Select(id => PostRows.From(
              posts[id!],
              response.PostElections,
              response.ElectionVotes,
              response.Bookmarks,
              response.MarkdownToHtml,
              response.PostLinkEmbeds,
              localization))
          .ToArray();
      HistoryItems = append
          ? HistoryItems.Concat(rows).DistinctBy(row => row.Id, StringComparer.Ordinal).ToArray()
          : rows;
      historyPageInfo = response.PageInfo;
      OnPropertyChanged(nameof(CanLoadMore));
    }
    catch (Exception ex) when (loadVersion != historyLoadVersion && ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
    }
  }

  private int NextHistoryLoadVersion() => System.Threading.Interlocked.Increment(ref historyLoadVersion);
}
