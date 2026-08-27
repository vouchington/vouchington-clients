using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

internal static class BottomTabFactory
{
  public static Tab Create(NavigationIntentViewModel model, Func<Page> createPage) => new()
  {
    Title = model.Label,
    Route = model.Id,
    Items =
    {
      new ShellContent
      {
        Title = model.Label,
        Route = model.Id,
        ContentTemplate = new DataTemplate(createPage),
      },
    },
  };
}
