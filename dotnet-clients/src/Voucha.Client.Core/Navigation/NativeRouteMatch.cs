namespace Voucha.Client.Core.Navigation;

public sealed record NativeRouteMatch(
    string Path,
    string Template,
    IReadOnlyDictionary<string, string> Params,
    IReadOnlyDictionary<string, string> QueryItems)
{
  public string? Param(params string[] names) => FirstValue(Params, names);

  public string? QueryValue(params string[] names) => FirstValue(QueryItems, names);

  private static string? FirstValue(IReadOnlyDictionary<string, string> values, string[] names)
  {
    ArgumentNullException.ThrowIfNull(names);

    foreach (var name in names)
    {
      if (values.TryGetValue(name, out var value))
      {
        return value;
      }
    }

    return null;
  }
}
