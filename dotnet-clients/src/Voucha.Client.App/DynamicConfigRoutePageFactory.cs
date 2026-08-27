using Voucha.Client.App.Pages;

namespace Voucha.Client.App;

public static class DynamicConfigRoutePageFactory
{
  public static bool TryCreate(IServiceProvider serviceProvider, string? path, out Page page)
  {
    if (path == "/admin/dynamic-config" || path?.StartsWith("/admin/dynamic-config/", StringComparison.Ordinal) == true)
    {
      page = serviceProvider.GetRequiredService<DynamicConfigPage>();
      return true;
    }
    page = null!;
    return false;
  }
}
