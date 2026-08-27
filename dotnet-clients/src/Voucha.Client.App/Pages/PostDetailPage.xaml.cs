using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.HnDiscussions;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.FollowerDistributions;

namespace Voucha.Client.App.Pages;

public partial class PostDetailPage : ContentPage
{
  private readonly PostDetailPageBinding binding;
  private readonly ISessionStore sessionStore;
  private readonly IServiceProvider serviceProvider;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly string focusedCommentId;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private bool isSessionChangedAttached;

  public PostDetailPage(
      ICommentThreadService commentThreadService,
      IPostsService postsService,
      ISessionStore sessionStore,
      IServiceProvider serviceProvider,
      string postId,
      string? focusedCommentId = null)
  {
    InitializeComponent();
    this.sessionStore = sessionStore;
    this.serviceProvider = serviceProvider;
    emailRecovery = serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>();
    this.focusedCommentId = focusedCommentId ?? string.Empty;
    binding = new PostDetailPageBinding(
        new CommentThreadViewModel(commentThreadService, postsService, postId, sessionStore.Current.Identity?.Id),
        sessionStore,
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>(),
        serviceProvider.GetRequiredService<IHnDiscussionsSettings>(),
        serviceProvider.GetRequiredService<HnDiscussionsClient>());
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() =>
    {
      binding.RefreshViewerState();
      binding.RefreshRows();
    });
    BindingContext = binding;
  }

  private async void OnHnDiscussionClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: HnDiscussionRow row })
    {
      await Browser.Default.OpenAsync(row.ItemUrl).ConfigureAwait(true);
    }
  }

  private async void OnFollowerSendClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostDetailPageRow row })
      if (FollowerDistributionActions.CanSendPost(sessionStore, row.Post.CreatedById, row.Post.PostType, row.Post.ParentId, row.Post.Privacy, row.Post.Broadcast))
        await FollowerDistributionActions.ShowAsync(this, serviceProvider, sessionStore, FollowerDistributionTargetKind.Post, row.Id);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    AttachSessionChanged();
    try
    {
      await binding.LoadAsync(string.IsNullOrWhiteSpace(focusedCommentId) ? null : focusedCommentId).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    DetachSessionChanged();
    base.OnDisappearing();
  }
}
