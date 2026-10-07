using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityAutomodActionViewTests
{
  [Theory]
  [InlineData("record_only")]
  [InlineData("review_queue")]
  [InlineData("unpublish")]
  public void RendersTheCommunityLevelAction(string action)
  {
    ConfigureResources();
    var view = new CommunityAutomodActionView(action);

    Assert.True(view.IsVisible);
    Assert.Contains(view.Children.OfType<Label>(), label => label.Text == "Automod");
    Assert.Equal(action, Assert.Single(view.Children.OfType<Label>(), label => label.AutomationId == "community-automod-action").Text);
  }

  [Fact]
  public void ReplacesAndClearsThePreviousCommunityAction()
  {
    ConfigureResources();
    var view = new CommunityAutomodActionView("record_only");
    var value = Assert.Single(view.Children.OfType<Label>(), label => label.AutomationId == "community-automod-action");

    view.Update("unpublish");
    Assert.Equal("unpublish", value.Text);
    Assert.True(view.IsVisible);
    view.Update(null);
    Assert.Empty(value.Text);
    Assert.False(view.IsVisible);
  }

  private static void ConfigureResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { [UiMessageKey.NativeDotnetModerationAutomod.Value] = "Automod" } };
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
