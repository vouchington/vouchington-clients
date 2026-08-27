using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class BottomTabFactoryTests
{
  [Fact]
  public void RebuiltTabKeepsItsStableRoute()
  {
    var model = new NavigationIntentViewModel("news", "News", "/news", []);

    var tab = BottomTabFactory.Create(model, () => new ContentPage());

    Assert.Equal("news", tab.Route);
    Assert.Equal("news", Assert.Single(tab.Items).Route);
  }
}
