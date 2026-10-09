using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage : ContentPage
{
  private readonly ITopicsService topicsService;
  private readonly IImageUploadService imageUploadService;
  private readonly AppConfig config;
  private TopicImagePlacement? logoPlacement;
  private TopicImagePlacement? heroPlacement;
  private int logoPreviewGeneration;
  private int heroPreviewGeneration;
  private CancellationTokenSource? logoUploadCancellation;
  private CancellationTokenSource? heroUploadCancellation;
  private bool isSaving;
  private string? logoLocalImageId;
  private string? heroLocalImageId;
  private string? topicIdOrSlug;
  private RssFeedSource? source;

  public TopicManagementPage(
      ITopicsService topicsService,
      IImageUploadService imageUploadService,
      VouchaApiClient apiClient,
      string? topicIdOrSlug = null,
      AppConfig? config = null)
  {
    InitializeComponent();
    this.topicsService = topicsService ?? throw new ArgumentNullException(nameof(topicsService));
    this.imageUploadService = imageUploadService ?? throw new ArgumentNullException(nameof(imageUploadService));
    this.config = config ?? AppConfig.FromEnvironment();
    MarkdownEditor.ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    this.topicIdOrSlug = topicIdOrSlug;
    SyncAliasesPaginationControl();
    SyncHostnamesPaginationControl();
  }

  protected override void OnDisappearing()
  {
    logoPreviewGeneration++;
    heroPreviewGeneration++;
    CancelImageUpload(isLogo: true);
    CancelImageUpload(isLogo: false);
    ClearLocalPreview(LogoLocalPreviewImage);
    ClearLocalPreview(HeroLocalPreviewImage);
    LogoPreviewUnavailableLabel.IsVisible = false;
    HeroPreviewUnavailableLabel.IsVisible = false;
    logoLocalImageId = null;
    heroLocalImageId = null;
    base.OnDisappearing();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (topicIdOrSlug is null) return;
    try
    {
      var response = await topicsService.FetchTopicAsync(topicIdOrSlug).ConfigureAwait(true);
      ApplyTopic(response.Topic);
      await RefreshRelatedAsync(response.Topic.Id).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      StatusLabel.Text = ex.Message;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI async event handlers display API failures inline.")]
  private async void OnSaveClicked(object? sender, EventArgs e) => await SaveTopicAsync().ConfigureAwait(true);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI save failures are displayed inline.")]
  internal async Task SaveTopicAsync()
  {
    if (isSaving || logoUploadCancellation is not null || heroUploadCancellation is not null) return;
    isSaving = true;
    UpdateImageActionState();
    try
    {
      var response = topicIdOrSlug is null
          ? await topicsService.CreateTopicAsync(CreateRequest()).ConfigureAwait(true)
          : await topicsService.UpdateTopicAsync(topicIdOrSlug, UpdateBody()).ConfigureAwait(true);
      if (topicIdOrSlug is null)
      {
        topicIdOrSlug = response.Topic.Id;
        response = await topicsService.UpdateTopicAsync(response.Topic.Id, UpdateBody()).ConfigureAwait(true);
      }
      topicIdOrSlug = response.Topic.Id;
      ApplyTopic(response.Topic);
      await RefreshRelatedAsync(response.Topic.Id).ConfigureAwait(true);
      StatusLabel.Text = UiCopy.Format(
          UiMessageKey.NativeDotnetCsharpStatusSavedItem,
          ("id", response.Topic.Id));
    }
    catch (Exception ex)
    {
      StatusLabel.Text = ex.Message;
    }
    finally
    {
      isSaving = false;
      UpdateImageActionState();
    }
  }

  private async void OnAddAliasClicked(object? sender, EventArgs e)
  {
    if (!TryTopicId(out var topicId) || string.IsNullOrWhiteSpace(AliasEntry.Text)) return;
    await RunAsync(async () =>
    {
      await topicsService
          .CreateTopicAliasesAsync(topicId, new CreateTopicAliasesBody(AliasEntry.Text.Trim()))
          .ConfigureAwait(true);
      AliasEntry.Text = string.Empty;
      await RefreshAliasesAsync(topicId).ConfigureAwait(true);
    }).ConfigureAwait(true);
  }

  private async void OnAddHostnameClicked(object? sender, EventArgs e)
  {
    if (!TryTopicId(out var topicId) || string.IsNullOrWhiteSpace(AdditionalHostnameEntry.Text)) return;
    await RunAsync(async () =>
    {
      await topicsService.CreateTopicAdditionalHostnameAsync(topicId, AdditionalHostnameEntry.Text.Trim()).ConfigureAwait(true);
      AdditionalHostnameEntry.Text = string.Empty;
      await RefreshHostnamesAsync(topicId).ConfigureAwait(true);
    }).ConfigureAwait(true);
  }

  private async void OnMergeClicked(object? sender, EventArgs e)
  {
    if (!TryTopicId(out var topicId) || string.IsNullOrWhiteSpace(MergeDestinationEntry.Text)) return;
    await RunAsync(async () =>
    {
      var response = await topicsService.MergeTopicAliasesAsync(topicId, MergeDestinationEntry.Text.Trim()).ConfigureAwait(true);
      StatusLabel.Text = UiCopy.Format(
          UiMessageKey.NativeDotnetCsharpStatusMergedInto,
          ("id", response.Topic.Id));
    }).ConfigureAwait(true);
  }

  private CreateTopicRequest CreateRequest() =>
      new(
          NameEntry.Text ?? "",
          SlugEntry.Text ?? "",
          TypeEntry.Text ?? "topic",
          MarkdownEditor.Markdown,
          TrimmedOrNull(HostnameEntry.Text));

  private UpdateTopicBody UpdateBody() =>
      new(
          NameEntry.Text,
          SlugEntry.Text,
          MarkdownEditor.Markdown,
          TypeEntry.Text,
          NoindexCheckBox.IsChecked,
          AllowReviewsCheckBox.IsChecked,
          NullableString(HostnameEntry.Text),
          NullableString(LogoImageEntry.Text),
          NullableString(HeroImageEntry.Text));

  private bool TryTopicId(out string topicId)
  {
    topicId = topicIdOrSlug ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(topicId)) return true;
    StatusLabel.Text = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpStatusSaveTopicFirst);
    return false;
  }

  private static JsonNullableString NullableString(string? value) =>
      string.IsNullOrWhiteSpace(value)
          ? JsonNullableString.Null
          : JsonNullableString.FromString(value.Trim());

  private static string? TrimmedOrNull(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
