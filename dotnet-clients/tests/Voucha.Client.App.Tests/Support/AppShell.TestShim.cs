using Microsoft.Extensions.DependencyInjection;

namespace Voucha.Client.App;

public sealed partial class AppShell : Shell
{
  private readonly IServiceProvider serviceProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
      .BuildServiceProvider();

  public Task<bool> ApplySettingsRouteForTestAsync(Voucha.Client.Core.Navigation.NativeRouteMatch match) =>
      TryApplySettingsRouteMatchAsync(match);

  public string? LastOpenedPath { get; private set; }

  public Task OpenNativePathAsync(string targetPath)
  {
    LastOpenedPath = targetPath;
    return Task.CompletedTask;
  }
}
