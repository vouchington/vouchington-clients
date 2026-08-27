using Voucha.Client.Core.Users;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddUsersBrowseServices(IServiceCollection services)
  {
    services.AddSingleton<IUsersSearchService, ApiUsersSearchService>();
    services.AddTransient<UsersBrowseViewModel>();
  }
}
