using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed class ApiKeyScopeSelection
{
  private Dictionary<string, ScopeCatalogEntry> scopes = new(StringComparer.Ordinal);
  private readonly HashSet<string> selected = new(StringComparer.Ordinal);

  public ApiKeyScopeSelection(IReadOnlyList<ScopeCatalogEntry> scopes) => Replace(scopes);

  public IReadOnlyList<ScopeCatalogEntry> Scopes => scopes.Values.ToArray();

  public IReadOnlyList<string> SelectedScopes => scopes.Keys.Where(selected.Contains).ToArray();

  public void Replace(IReadOnlyList<ScopeCatalogEntry> entries)
  {
    ArgumentNullException.ThrowIfNull(entries);
    scopes = entries.ToDictionary(entry => entry.Scope, StringComparer.Ordinal);
    selected.Clear();
  }

  public void SetSelected(string scope, bool value)
  {
    ArgumentNullException.ThrowIfNull(scope);
    if (!scopes.ContainsKey(scope)) throw new ArgumentException("Unknown scope.", nameof(scope));
    if (value)
    {
      var closure = new HashSet<string>(StringComparer.Ordinal);
      IncludePrerequisites(scope, closure, new HashSet<string>(StringComparer.Ordinal));
      selected.UnionWith(closure);
      return;
    }
    selected.Remove(scope);
    while (true)
    {
      var dependants = selected.Where(name => scopes[name].Requires is { } required && !selected.Contains(required)).ToArray();
      if (dependants.Length == 0) return;
      selected.ExceptWith(dependants);
    }
  }

  private void IncludePrerequisites(string scope, HashSet<string> closure, HashSet<string> visiting)
  {
    if (!scopes.TryGetValue(scope, out var entry) || !visiting.Add(scope))
      throw new InvalidOperationException("Invalid scope prerequisites.");
    if (entry.Requires is { } required) IncludePrerequisites(required, closure, visiting);
    visiting.Remove(scope);
    closure.Add(scope);
  }
}
