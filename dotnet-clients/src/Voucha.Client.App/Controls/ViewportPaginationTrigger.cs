namespace Voucha.Client.App.Controls;

internal sealed class ViewportPaginationTrigger<T> where T : notnull
{
  private HashSet<T> visibleControls = [];

  internal void Rearm(T control) => visibleControls.Remove(control);

  internal IReadOnlyList<T> EnteredViewport(
      IReadOnlyList<(T Control, double Top, double Height)> controls,
      double viewportTop,
      double viewportHeight)
  {
    var viewportBottom = viewportTop + viewportHeight;
    var nextVisibleControls = controls
        .Where(candidate => candidate.Top < viewportBottom && candidate.Top + candidate.Height > viewportTop)
        .Select(candidate => candidate.Control)
        .ToHashSet();
    var enteredControls = nextVisibleControls.Where(control => !visibleControls.Contains(control)).ToArray();
    visibleControls = nextVisibleControls;
    return enteredControls;
  }
}

internal static class ViewportPagination
{
  internal static double VerticalOffset(VisualElement control, VisualElement content)
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
