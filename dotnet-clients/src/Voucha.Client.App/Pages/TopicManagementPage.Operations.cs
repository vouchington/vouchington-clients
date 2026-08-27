using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private async void OnToggleSourceEnabledClicked(object? sender, EventArgs e)
  {
    if (source is null) return;
    await RunAsync(async () =>
    {
      await topicsService.UpdateSourceAsync(source.Id, new UpdateRssFeedBody(Enabled: source.IsEnabled != true)).ConfigureAwait(true);
      await RefreshSourceAsync(source.Topic?.Id ?? topicIdOrSlug ?? "").ConfigureAwait(true);
    }).ConfigureAwait(true);
  }

  private async void OnToggleSourceDiscoverableClicked(object? sender, EventArgs e)
  {
    if (source is null) return;
    await RunAsync(async () =>
    {
      await topicsService.UpdateSourceAsync(source.Id, new UpdateRssFeedBody(Discoverable: source.IsDiscoverable != true)).ConfigureAwait(true);
      await RefreshSourceAsync(source.Topic?.Id ?? topicIdOrSlug ?? "").ConfigureAwait(true);
    }).ConfigureAwait(true);
  }

  private async void OnUploadLogoClicked(object? sender, EventArgs e) =>
      await UploadIntoAsync(LogoImageEntry).ConfigureAwait(true);

  private async void OnUploadHeroClicked(object? sender, EventArgs e) =>
      await UploadIntoAsync(HeroImageEntry).ConfigureAwait(true);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "File picker and upload failures are displayed inline.")]
  private async Task UploadIntoAsync(Entry target)
  {
    try
    {
      var file = await FilePicker.Default.PickAsync(new PickOptions
      {
        PickerTitle = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpEditorSelectImage),
      }).ConfigureAwait(true);
      if (file is null) return;
      await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
      var upload = await imageUploadService
          .CreateUploadUrlAsync(new CreateImageUploadUrlBody(file.ContentType ?? "application/octet-stream", checked((int)stream.Length)))
          .ConfigureAwait(true);
      await imageUploadService.UploadAsync(upload.Upload, stream, stream.Length).ConfigureAwait(true);
      var completed = await imageUploadService.CompleteAsync(upload.Upload.ImageId).ConfigureAwait(true);
      target.Text = completed.Image.Id;
    }
    catch (Exception ex)
    {
      StatusLabel.Text = ex.Message;
    }
  }

  private async Task RefreshRelatedAsync(string topicId)
  {
    await RefreshAliasesAsync(topicId).ConfigureAwait(true);
    await RefreshHostnamesAsync(topicId).ConfigureAwait(true);
    await RefreshSourceAsync(topicId).ConfigureAwait(true);
  }

  private async Task RefreshAliasesAsync(string topicId)
  {
    var response = await topicsService.FetchTopicAliasesAsync(topicId).ConfigureAwait(true);
    aliasesPagination.Reset(response.Results);
    aliasesPagination.RestoreContinuation(response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
    ApplyAliases();
    SyncAliasesPaginationControl();
  }

  private async Task RefreshHostnamesAsync(string topicId)
  {
    var response = await topicsService.FetchTopicAdditionalHostnamesAsync(topicId).ConfigureAwait(true);
    hostnamesPagination.Reset(response.Results);
    hostnamesPagination.RestoreContinuation(response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
    ApplyHostnames();
    SyncHostnamesPaginationControl();
  }

  private async Task RefreshSourceAsync(string topicId)
  {
    var response = await topicsService
        .FetchRssFeedsForTopicAsync(topicId, RssFeedEnabledFilter.All)
        .ConfigureAwait(true);
    source = response.Results.Count > 0 ? response.Results[0] : null;
    SourceLabel.Text = source is null
        ? UiCopy.Localize(UiMessageKey.NativeDotnetTopicManagementNoManagedSource)
        : UiCopy.Format(
            UiMessageKey.NativeDotnetTopicManagementSourceSummary,
            ("title", source.Title),
            ("enabled", UiCopy.Localize(source.IsEnabled == true
                ? UiMessageKey.NativeDotnetCsharpCommunitiesEnabled
                : UiMessageKey.NativeDotnetCsharpCommunitiesDisabled)),
            ("discoverable", UiCopy.Localize(source.IsDiscoverable == true
                ? UiMessageKey.NativeDotnetTopicManagementDiscoverable
                : UiMessageKey.NativeDotnetTopicManagementNotDiscoverable)));
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI async event handlers display API failures inline.")]
  private async Task RunAsync(Func<Task> action)
  {
    try
    {
      await action().ConfigureAwait(true);
      StatusLabel.Text = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpSaved);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      StatusLabel.Text = ex.Message;
    }
  }
}
