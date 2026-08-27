using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class PostDetailPage
{
  private readonly ViewportPaginationTrigger<HybridPaginationControl> descendantPaginationVisibility = new();

  private async void OnLoadMoreDescendantsRequested(object? sender, EventArgs args)
  {
    await binding.LoadMoreDescendantsAsync().ConfigureAwait(true);
    descendantPaginationVisibility.Rearm(DescendantsPagination);
    TryLoadVisibleDescendants(PostDetailScroll.ScrollY);
  }

  private void OnPostDetailScrolled(object? sender, ScrolledEventArgs args) =>
      TryLoadVisibleDescendants(args.ScrollY);

  private void OnPostDetailViewportChanged(object? sender, EventArgs args) =>
      TryLoadVisibleDescendants(PostDetailScroll.ScrollY);

  private void TryLoadVisibleDescendants(double scrollY)
  {
    if (!DescendantsPagination.HasMore || PostDetailScroll.Height <= 0 ||
        PostDetailScroll.Content is not VisualElement content) return;
    var height = DescendantsPagination.Height > 0
        ? DescendantsPagination.Height
        : DescendantsPagination.DesiredSize.Height;
    if (height <= 0) return;
    var candidate = (
        Control: DescendantsPagination,
        Top: VerticalOffset(DescendantsPagination, content),
        Height: height);
    foreach (var control in descendantPaginationVisibility.EnteredViewport(
        [candidate],
        scrollY,
        PostDetailScroll.Height)) control.TryLoadAutomatically();
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
