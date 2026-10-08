using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  internal Func<string, Task>? AutomodPostNavigationOverride { get; set; }
  private readonly CommunityAutomodFlagsView automodFlagsView = new(
      _ => Task.CompletedTask,
      _ => Task.CompletedTask);

  private void InitializeAutomodFlags()
  {
    if (section != CommunityDetailSurfaceSection.Moderation) return;
    automodFlagsView.DismissRequested = postId => InvokeAndRenderAsync(ct => viewModel.DismissAutomodFlagAsync(postId, ct));
    automodFlagsView.OpenPostRequested = OpenAutomodPostAsync;
    automodFlagsView.LoadMoreRequested += async (_, _) =>
      await InvokeAndRenderAsync(viewModel.LoadMoreAutomodFlagsAsync).ConfigureAwait(true);
  }

  private Task OpenAutomodPostAsync(string postId)
  {
    if (AutomodPostNavigationOverride is { } navigate) return navigate(postId);
    var provider = serviceProvider ?? throw new InvalidOperationException("Community post navigation is unavailable.");
    return Navigation.PushAsync(ActivatorUtilities.CreateInstance<PostDetailPage>(provider, postId));
  }

  private void RenderAutomodFlags()
  {
    automodFlagsView.IsVisible = section == CommunityDetailSurfaceSection.Moderation && viewModel.CanModerateCommunity;
    if (!automodFlagsView.IsVisible) return;
    automodFlagsView.Update(
        viewModel.AutomodFlags,
        viewModel.IsDismissingAutomodFlag,
        viewModel.AutomodFlagError,
        viewModel.AutomodFlagNotice,
        viewModel.HasMoreAutomodFlags,
        viewModel.IsLoadingAutomodFlags);
  }

  private static bool AffectsAutomodFlags(string? propertyName) => propertyName is null or
      nameof(CommunityDetailViewModel.AutomodFlags) or
      nameof(CommunityDetailViewModel.HasMoreAutomodFlags) or
      nameof(CommunityDetailViewModel.IsLoadingAutomodFlags) or
      nameof(CommunityDetailViewModel.AutomodFlagError) or
      nameof(CommunityDetailViewModel.AutomodFlagNotice) or
      nameof(CommunityDetailViewModel.CanModerateCommunity);
}
