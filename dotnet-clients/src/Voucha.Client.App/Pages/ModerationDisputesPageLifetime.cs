namespace Voucha.Client.App.Pages;

public static class ModerationDisputesPageLifetime
{
  public static void Replace(ShellContent content, Page page)
  {
    ArgumentNullException.ThrowIfNull(content);
    ArgumentNullException.ThrowIfNull(page);
    if (ReferenceEquals(content.Content, page)) return;
    if (content.Content is IDisposable disposable) disposable.Dispose();
    content.Content = page;
  }
}
