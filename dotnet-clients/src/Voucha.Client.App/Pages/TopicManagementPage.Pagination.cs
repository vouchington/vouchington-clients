using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private readonly CursorPaginationState<string, string> aliasesPagination = new(alias => alias);
  private readonly CursorPaginationState<TopicAdditionalHostname, string> hostnamesPagination = new(host => host.HostnameId);

  private async void OnLoadMoreAliasesRequested(object? sender, EventArgs e)
  {
    if (!TryTopicId(out var topicId)) return;
    var request = aliasesPagination.BeginNextPage();
    if (request is null) return;
    SyncAliasesPaginationControl();
    try
    {
      var response = await topicsService.FetchTopicAliasesAsync(topicId, request.Cursor).ConfigureAwait(true);
      if (aliasesPagination.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage))
      {
        ApplyAliases();
      }
    }
    catch (OperationCanceledException)
    {
      aliasesPagination.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      aliasesPagination.Fail(request, ex.Message);
      StatusLabel.Text = ex.Message;
    }
    finally
    {
      SyncAliasesPaginationControl();
    }
  }

  private async void OnLoadMoreHostnamesRequested(object? sender, EventArgs e)
  {
    if (!TryTopicId(out var topicId)) return;
    var request = hostnamesPagination.BeginNextPage();
    if (request is null) return;
    SyncHostnamesPaginationControl();
    try
    {
      var response = await topicsService.FetchTopicAdditionalHostnamesAsync(topicId, request.Cursor).ConfigureAwait(true);
      if (hostnamesPagination.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage))
      {
        ApplyHostnames();
      }
    }
    catch (OperationCanceledException)
    {
      hostnamesPagination.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      hostnamesPagination.Fail(request, ex.Message);
      StatusLabel.Text = ex.Message;
    }
    finally
    {
      SyncHostnamesPaginationControl();
    }
  }

  private void ApplyAliases()
  {
    AliasesLabel.Text = aliasesPagination.Items.Count == 0
        ? UiCopy.Localize(UiMessageKey.NativeDotnetTopicManagementNoAliases)
        : string.Join(", ", aliasesPagination.Items);
  }

  private void ApplyHostnames()
  {
    HostnamesLabel.Text = hostnamesPagination.Items.Count == 0
        ? UiCopy.Localize(UiMessageKey.NativeDotnetTopicManagementNoAdditionalHostnames)
        : string.Join(", ", hostnamesPagination.Items.Select(host => host.Hostname));
  }

  private void SyncAliasesPaginationControl()
  {
    AliasesPaginationControl.HasMore = aliasesPagination.HasLoadedPage && aliasesPagination.HasMore;
    AliasesPaginationControl.IsLoading = aliasesPagination.IsLoading;
    AliasesPaginationControl.HasError = aliasesPagination.LastError is not null;
  }

  private void SyncHostnamesPaginationControl()
  {
    HostnamesPaginationControl.HasMore = hostnamesPagination.HasLoadedPage && hostnamesPagination.HasMore;
    HostnamesPaginationControl.IsLoading = hostnamesPagination.IsLoading;
    HostnamesPaginationControl.HasError = hostnamesPagination.LastError is not null;
  }
}
