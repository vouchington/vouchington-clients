namespace Voucha.Client.Core.Api;

public sealed class PublicKeyPin
{
  public PublicKeyPin(string host, string spkiSha256Base64, string label)
  {
    Host = NormalizeHost(host);
    SpkiSha256Base64 = Normalize(spkiSha256Base64, nameof(spkiSha256Base64));
    Label = Normalize(label, nameof(label));
  }

  public string Host { get; }

  public string SpkiSha256Base64 { get; }

  public string Label { get; }

  private static string NormalizeHost(string value) => Normalize(value, nameof(Host)).ToUpperInvariant();

  private static string Normalize(string value, string name) =>
      !string.IsNullOrWhiteSpace(value)
          ? value.Trim()
          : throw new ArgumentException($"Value cannot be empty for {name}.", name);
}
