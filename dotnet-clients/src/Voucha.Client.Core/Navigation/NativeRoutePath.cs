namespace Voucha.Client.Core.Navigation;

public readonly record struct NativeRoutePath
{
  private NativeRoutePath(string value) => Value = value;

  public string Value { get; }

  public static NativeRoutePath Entity(string segment, string identifier)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(segment);
    ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
    return new($"/{segment}/{Uri.EscapeDataString(identifier)}");
  }

  public static NativeRoutePath Segments(params string[] segments)
  {
    ArgumentNullException.ThrowIfNull(segments);
    if (segments.Length == 0 || segments.Any(string.IsNullOrWhiteSpace))
      throw new ArgumentException("A route requires non-empty segments.", nameof(segments));
    return new("/" + string.Join('/', segments.Select(Uri.EscapeDataString)));
  }

  public NativeRoutePath WithQuery(string name, string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(value);
    return new($"{Value}?{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
  }

  public static implicit operator string(NativeRoutePath path) => path.Value;
}
