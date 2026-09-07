using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class NativeHtmlContentViewAuthoredLanguageTests
{
  [Fact]
  public void AppliesAuthoredDirectionToRenderedContent()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var view = new NativeHtmlContentView { Fallback = "محتوى", IsRightToLeft = true };

    var stack = Assert.IsType<VerticalStackLayout>(view.Content);
    var label = Assert.IsType<Label>(Assert.Single(stack.Children));

    Assert.Equal("محتوى", Assert.Single(label.FormattedText.Spans).Text);
    Assert.Equal(FlowDirection.RightToLeft, label.FlowDirection);
  }

  [Fact]
  public void AppliesAuthoredDirectionOnlyToImageAltText()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var authored = new NativeHtmlContentView
    {
      FlowDirection = FlowDirection.LeftToRight,
      IsRightToLeft = true,
      Html = "<img src=\"https://example.com/image.jpg\" alt=\"وصف\">",
    };
    var fallback = new NativeHtmlContentView
    {
      FlowDirection = FlowDirection.LeftToRight,
      IsRightToLeft = true,
      Html = "<img src=\"https://example.com/image.jpg\">",
    };

    var authoredStack = Assert.IsType<VerticalStackLayout>(authored.Content);
    var fallbackStack = Assert.IsType<VerticalStackLayout>(fallback.Content);
    var authoredLabel = Assert.IsType<Label>(Assert.Single(authoredStack.Children));
    var fallbackLabel = Assert.IsType<Label>(Assert.Single(fallbackStack.Children));

    Assert.Equal("وصف", authoredLabel.Text);
    Assert.Equal(FlowDirection.RightToLeft, authoredLabel.FlowDirection);
    Assert.Equal(FlowDirection.LeftToRight, fallbackLabel.FlowDirection);
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
