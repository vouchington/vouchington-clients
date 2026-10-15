using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class VisualTreeElementsTests
{
  [Fact]
  public void DescendantsWalksNestedChildrenAndSkipsTheRoot()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var nested = new VerticalStackLayout
    {
      AutomationId = "nested",
      Children =
      {
        new Label { AutomationId = "label" },
        new Button { AutomationId = "button" },
      },
    };
    var root = new VerticalStackLayout
    {
      AutomationId = "root",
      Children = { new Label { AutomationId = "direct" }, nested },
    };

    Assert.Equal(
        ["direct", "label"],
        VisualTreeElements.Descendants<Label>(root).Select(element => element.AutomationId).ToArray());
    Assert.Equal(
        ["button"],
        VisualTreeElements.Descendants<Button>(root).Select(element => element.AutomationId).ToArray());
    Assert.Equal(
        ["nested"],
        VisualTreeElements.Descendants<VerticalStackLayout>(root).Select(element => element.AutomationId).ToArray());
    Assert.Empty(VisualTreeElements.Descendants<Label>(new VerticalStackLayout()));
  }
}
