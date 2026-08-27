using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class MemberAppealsPage
{
  private void AddTracking(VerticalStackLayout content)
  {
    content.Add(Heading(
        UiMessageKey.NativeSwiftModerationAppealsMemberYourAppeals,
        "member-appeals-tracking-heading"));
    foreach (var appeal in viewModel.Appeals) content.Add(AppealCard(appeal));
    if (viewModel.Appeals.Count == 0 &&
        !viewModel.IsLoading &&
        !viewModel.HasLoadError)
    {
      content.Add(UiCopy.Bind(
          new Label { AutomationId = "member-appeals-tracking-empty" },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsNoAppealsMatch));
    }
    foreach (var status in Enum.GetValues<ModerationAppealStatus>())
    {
      var pagination = viewModel.AppealPagination(status);
      AddPagination(
          content,
          $"member-appeals-{StatusId(status)}",
          pagination,
          token => viewModel.LoadMoreAppealsAsync(status, token));
    }
  }

  private void AddNoticePagination(VerticalStackLayout content)
  {
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.Warnings)
      AddPagination(content, "member-warnings", viewModel.WarningPagination,
          viewModel.LoadMoreWarningsAsync);
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.Bans)
      AddPagination(content, "member-bans", viewModel.BanPagination,
          viewModel.LoadMoreBansAsync);
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.RemovedPosts)
      AddPagination(content, "member-removed-posts", viewModel.RemovedPostPagination,
          viewModel.LoadMoreRemovedPostsAsync);
  }

  private void AddPagination(
      VerticalStackLayout content,
      string id,
      MemberAppealPaginationState state,
      Func<CancellationToken, Task> load)
  {
    if (!state.HasMore && !state.HasError && !state.IsLoading) return;
    var pagination = new HybridPaginationControl
    {
      PaginationId = id,
      HasMore = state.HasMore || state.HasError || state.IsLoading,
      IsLoading = state.IsLoading,
      HasError = state.HasError,
    };
    pagination.LoadNextPageRequested += async (_, _) =>
        await LoadPageAsync(load).ConfigureAwait(true);
    content.Add(pagination);
  }

  private async Task LoadPageAsync(Func<CancellationToken, Task> load)
  {
    EnsureLifetime();
    var operation = load(lifetimeCancellation.Token);
    Content = BuildContent();
    await RunPageOperationAsync(() => operation).ConfigureAwait(true);
    Content = BuildContent();
  }

  private static string StatusId(ModerationAppealStatus status) => status switch
  {
    ModerationAppealStatus.Pending => "pending",
    ModerationAppealStatus.Resolved => "resolved",
    ModerationAppealStatus.Dismissed => "dismissed",
    _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
  };
}
