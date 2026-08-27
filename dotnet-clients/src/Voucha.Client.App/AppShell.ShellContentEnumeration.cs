namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private IEnumerable<Page> EnumerateShellContentPages(string route)
  {
    foreach (var content in EnumerateShellContents(route))
    {
      if (content.Content is Page page)
      {
        yield return page;
      }
    }
  }

  private IEnumerable<ShellContent> EnumerateShellContents(string route)
  {
    foreach (var item in Items)
    {
      foreach (var section in item.Items)
      {
        foreach (var content in section.Items)
        {
          if (content.Route == route)
          {
            yield return content;
          }
        }
      }
    }
  }
}
