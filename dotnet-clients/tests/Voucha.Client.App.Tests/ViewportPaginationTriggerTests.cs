using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ViewportPaginationTriggerTests
{
  [Fact]
  public void EntersViewportOnceUntilTheControlLeaves()
  {
    var trigger = new ViewportPaginationTrigger<string>();

    Assert.Empty(trigger.EnteredViewport([("offscreen", 500, 44)], viewportTop: 0, viewportHeight: 400));
    Assert.Equal(["visible"], trigger.EnteredViewport([("visible", 380, 44)], viewportTop: 0, viewportHeight: 400));
    Assert.Empty(trigger.EnteredViewport([("visible", 380, 44)], viewportTop: 0, viewportHeight: 400));
    Assert.Empty(trigger.EnteredViewport([("visible", 380, 44)], viewportTop: 500, viewportHeight: 400));
    Assert.Equal(["visible"], trigger.EnteredViewport([("visible", 380, 44)], viewportTop: 0, viewportHeight: 400));
  }

  [Fact]
  public void ReturnsOnlyControlsThatIntersectTheViewport()
  {
    var trigger = new ViewportPaginationTrigger<string>();

    var entered = trigger.EnteredViewport(
        [("above", 0, 44), ("visible", 520, 44), ("below", 1_100, 44)],
        viewportTop: 500,
        viewportHeight: 200);

    Assert.Equal(["visible"], entered);
  }

  [Fact]
  public void RearmAllowsAStillVisibleControlToRequestAnotherPage()
  {
    var trigger = new ViewportPaginationTrigger<string>();
    var controls = new[] { (Control: "visible", Top: 380d, Height: 44d) };

    Assert.Equal(["visible"], trigger.EnteredViewport(controls, 0, 400));
    trigger.Rearm("visible");

    Assert.Equal(["visible"], trigger.EnteredViewport(controls, 0, 400));
  }

  [Fact]
  public void ContextualPaginationIdSetsUniqueControlAndActionIds()
  {
    var control = new HybridPaginationControl { PaginationId = "settings-api-keys" };
    var button = Assert.Single(((IVisualTreeElement)control).GetVisualChildren().OfType<Button>());

    Assert.Equal("pagination-settings-api-keys", control.AutomationId);
    Assert.Equal("pagination-settings-api-keys-action", button.AutomationId);
  }

  [Fact]
  public void LoadingStateUsesGeneratedLocalizedResource()
  {
    var control = new HybridPaginationControl();
    var button = Assert.Single(((IVisualTreeElement)control).GetVisualChildren().OfType<Button>());
    control.Resources[UiMessageKey.NativeSwiftCommonLoadMore.Value] = "Load more";
    control.Resources[UiMessageKey.NativeSwiftCommonLoadingMore.Value] = "Loading more";

    control.IsLoading = true;

    Assert.Equal("Loading more", button.Text);
    Assert.Equal("Loading more", SemanticProperties.GetDescription(button));
  }
}
