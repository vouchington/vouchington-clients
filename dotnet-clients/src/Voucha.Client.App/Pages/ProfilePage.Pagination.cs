using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private readonly ViewportPaginationTrigger<HybridPaginationControl> profilePaginationVisibility = new();

  private async void OnLoadMoreUserTagsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreUserTagsAsync().ConfigureAwait(true);
    profilePaginationVisibility.Rearm(UserTagsPagination);
    TryLoadVisibleUserTags(ProfileScroll.ScrollY);
  }

  private void OnProfileScrolled(object? sender, ScrolledEventArgs args) =>
      TryLoadVisibleUserTags(args.ScrollY);

  private void OnProfileViewportChanged(object? sender, EventArgs args) =>
      TryLoadVisibleUserTags(ProfileScroll.ScrollY);

  private void TryLoadVisibleUserTags(double scrollY)
  {
    if (!UserTagsPagination.HasMore || ProfileScroll.Height <= 0 ||
        ProfileScroll.Content is not VisualElement content) return;
    var height = UserTagsPagination.Height > 0
        ? UserTagsPagination.Height
        : UserTagsPagination.DesiredSize.Height;
    if (height <= 0) return;
    var candidate = (
        Control: UserTagsPagination,
        Top: VerticalOffset(UserTagsPagination, content),
        Height: height);
    foreach (var control in profilePaginationVisibility.EnteredViewport(
        [candidate],
        scrollY,
        ProfileScroll.Height)) control.TryLoadAutomatically();
  }

  private static double VerticalOffset(VisualElement control, VisualElement content)
  {
    var offset = 0d;
    Element? current = control;
    while (current is VisualElement visual && !ReferenceEquals(current, content))
    {
      offset += visual.Y;
      current = visual.Parent;
    }
    return ReferenceEquals(current, content) ? offset : double.PositiveInfinity;
  }
}
