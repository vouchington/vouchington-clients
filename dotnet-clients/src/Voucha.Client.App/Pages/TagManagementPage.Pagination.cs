using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public sealed partial class TagManagementPage
{
  private readonly ViewportPaginationTrigger<HybridPaginationControl> paginationVisibility = new();

  private void ConfigurePaginationViewport()
  {
    pageScroll.Scrolled += (_, args) => TryLoadVisiblePagination(args.ScrollY);
    pageScroll.SizeChanged += (_, _) => TryLoadVisiblePagination(pageScroll.ScrollY);
  }

  private async void OnLoadMoreRelationsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreRelationsAsync().ConfigureAwait(true);
    paginationVisibility.Rearm(paginationControl);
    TryLoadVisiblePagination(pageScroll.ScrollY);
  }

  private void TryLoadVisiblePagination(double scrollY)
  {
    if (pageScroll.Height <= 0 || pageScroll.Content is not VisualElement content) return;
    var height = paginationControl.Height > 0
        ? paginationControl.Height
        : paginationControl.DesiredSize.Height;
    if (height <= 0) return;
    var candidates = new[]
    {
      (Control: paginationControl, Top: VerticalOffset(paginationControl, content), Height: height),
    };
    foreach (var control in paginationVisibility.EnteredViewport(
        candidates,
        scrollY,
        pageScroll.Height)) control.TryLoadAutomatically();
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
