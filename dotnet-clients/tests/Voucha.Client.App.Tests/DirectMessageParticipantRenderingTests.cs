using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Messages;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class DirectMessageParticipantRenderingTests
{
  [Fact]
  public void RosterPreservesIdentityAndRoleWhilePlacementClears()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    app.Resources["Body"] = new Style(typeof(Label));
    app.Resources["Metadata"] = new Style(typeof(Label));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    var view = new DirectMessageParticipantView();
    var row = new DirectMessageParticipantRow("participant", "alice", UiText.Verbatim("@alice"),
        "member", UiText.Verbatim("Member"), true, UiText.Verbatim("Remove"), UiLocalization.English,
        new TopicImagePlacement("avatar-1", "participant-placement", 2));

    view.BindingContext = row;

    var image = Assert.Single(view.GetVisualTreeDescendants().OfType<Image>());
    Assert.False(AutomationProperties.GetIsInAccessibleTree(image));
    var grid = Assert.Single(view.GetVisualTreeDescendants().OfType<Grid>());
    Assert.Equal(new Uri("https://images.test/images/placements/participant-placement/2/avatar-1?w=96"),
        Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.True(image.IsVisible);
    Assert.Equal(new GridLength(44), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "@alice");
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "Member");

    view.BindingContext = row with { ProfileImagePlacement = null };

    Assert.Null(image.Source);
    Assert.False(image.IsVisible);
    Assert.Equal(new GridLength(0), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "@alice");
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "Member");

    view.BindingContext = row with
    {
      ProfileImagePlacement = new TopicImagePlacement("avatar-1", "", 2),
    };

    Assert.Null(image.Source);
    Assert.False(image.IsVisible);
    Assert.Equal(new GridLength(0), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "@alice");
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
