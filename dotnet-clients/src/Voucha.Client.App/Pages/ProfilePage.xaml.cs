using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tags;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage : ContentPage
{
  private readonly ProfileViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly IServiceProvider serviceProvider;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly string? idOrUsername;
  private readonly NativeUserProfileScope initialScope;
  private bool hasLoaded;

  public ProfilePage(
      ProfileViewModel viewModel,
      ISessionStore sessionStore,
      IServiceProvider serviceProvider,
      VouchaApiClient apiClient,
      string? idOrUsername = null,
      NativeUserProfileScope? initialScope = null)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.serviceProvider = serviceProvider;
    emailRecovery = serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>();
    this.idOrUsername = idOrUsername;
    this.initialScope = initialScope ?? NativeUserProfileScope.Overview;
    BioMarkdownEditor.ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    viewModel.ConfigureProfileSafetyService(serviceProvider.GetRequiredService<IProfileSafetyService>());
    viewModel.ConfigureProfileCollectionsService(serviceProvider.GetRequiredService<IProfileCollectionsService>());
    viewModel.ConfigureUserTagClient(apiClient);
    viewModel.ConfigureUserTrustClient(apiClient);
    SetValue(IsPublicProfileProperty, !string.IsNullOrWhiteSpace(idOrUsername));
    BindingContext = viewModel;
  }

  public static readonly BindableProperty IsPublicProfileProperty = BindableProperty.Create(
      nameof(IsPublicProfile),
      typeof(bool),
      typeof(ProfilePage),
      false);

  public bool IsPublicProfile => (bool)GetValue(IsPublicProfileProperty);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      viewModel.SetCurrentViewer(
          sessionStore.Current.Identity?.Id,
          sessionStore.Current.Identity?.Username,
          sessionStore.Current.CanCastPublicVotes(),
          sessionStore.Current.CanCreateEntityRelationVote(isUserTag: true));
      if (string.IsNullOrWhiteSpace(idOrUsername))
      {
        await viewModel.LoadOwnAsync();
      }
      else
      {
        var scope = hasLoaded ? viewModel.ProfileScope : initialScope;
        await viewModel.LoadPublicScopeAsync(idOrUsername, scope);
        await viewModel.LoadUserTagsAsync();
        var isAdministrator = sessionStore.Current.Identity?.Roles?.Contains("administrator", StringComparer.OrdinalIgnoreCase) == true;
        await viewModel.LoadAdminLandingPagesAsync(viewModel.User?.Id ?? string.Empty, isAdministrator);
      }

      hasLoaded = true;
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    avatarPreviewGeneration++;
    avatarPreviewCancellation.CancelCurrent();
    ClearLocalAvatarPreview();
    base.OnDisappearing();
  }

  private async void OnUserAdminClicked(object? sender, EventArgs e)
  {
    var target = viewModel.User?.Username ?? viewModel.User?.Id ?? idOrUsername;
    if (string.IsNullOrWhiteSpace(target)) return;
    var page = serviceProvider.GetRequiredService<IdentityVerificationAttemptGrantPage>();
    var isAdministrator = sessionStore.Current.Identity?.Roles?.Contains("administrator", StringComparer.OrdinalIgnoreCase) == true;
    await page.ApplyRouteAsync(target, isAdministrator).ConfigureAwait(true);
    await Navigation.PushAsync(page).ConfigureAwait(true);
  }

  private async void OnRefreshClicked(object? sender, EventArgs e)
  {
    await viewModel.RefreshAsync();
  }

  private async void OnSaveBioClicked(object? sender, EventArgs e)
  {
    await viewModel.SaveBioAsync(viewModel.BioMarkdown);
  }

  private async void OnHistoryTabClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: ProfileHistoryTabRow row })
    {
      await viewModel.SelectHistoryTabAsync(row.Tab);
    }
  }

  private async void OnLandingPageAnalyticsClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: LandingPageRow row })
    {
      await Navigation.PushAsync(ActivatorUtilities.CreateInstance<LandingPageAnalyticsPage>(serviceProvider, row.Id));
    }
  }

  private async void OnPostClicked(object? sender, TappedEventArgs e)
  {
    if (sender is Border { BindingContext: PostRow item })
    {
      var isComment = string.Equals(item.ProtocolPostType, "comment", StringComparison.Ordinal);
      var postId = isComment && item.RootId is { Length: > 0 } rootId ? rootId : item.Id;
      var focusedCommentId = isComment && !string.Equals(postId, item.Id, StringComparison.Ordinal) ? item.Id : null;

      await Navigation.PushAsync(new PostDetailPage(
          serviceProvider.GetRequiredService<ICommentThreadService>(),
          serviceProvider.GetRequiredService<IPostsService>(),
          sessionStore,
          serviceProvider,
          postId,
          focusedCommentId));
    }
  }

  private async void OnMuteUserClicked(object? sender, EventArgs e) =>
      await viewModel.ToggleMuteUserAsync();

  private async void OnBlockUserClicked(object? sender, EventArgs e) =>
      await viewModel.ToggleBlockUserAsync();

  private async void OnManageUserTagsClicked(object? sender, EventArgs e)
  {
    if (viewModel.User?.Id is not { Length: > 0 } userId) return;
    var page = serviceProvider.GetRequiredService<TagManagementPage>();
    page.SetContext(new TagManagementRouteContext("user", userId, "topic"));
    await Navigation.PushModalAsync(new NavigationPage(page));
  }

}
