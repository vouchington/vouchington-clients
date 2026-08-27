namespace Voucha.Client.App;

public sealed class AppShell : Shell
{
  public string? LastOpenedPath { get; private set; }

  public Task OpenNativePathAsync(string targetPath)
  {
    LastOpenedPath = targetPath;
    return Task.CompletedTask;
  }
}
