namespace Voucha.Client.Core.Search;

public enum FediverseProvider
{
  All,
  PeerTube,
  Mastodon,
  Lemmy,
  Bluesky,
}

public static class FediverseProviderSelection
{
  public static bool ShouldRoute(
      bool isChecked,
      FediverseProvider selected,
      FediverseProvider requested) =>
      isChecked && selected != requested;
}

public static class FediverseProviderExtensions
{
  public static string? QueryValue(this FediverseProvider provider) => provider switch
  {
    FediverseProvider.All => null,
    FediverseProvider.PeerTube => "peertube",
    FediverseProvider.Mastodon => "mastodon",
    FediverseProvider.Lemmy => "lemmy",
    FediverseProvider.Bluesky => "bluesky",
    _ => null,
  };

  public static FediverseProvider Parse(string? value) => value switch
  {
    "peertube" => FediverseProvider.PeerTube,
    "mastodon" => FediverseProvider.Mastodon,
    "lemmy" => FediverseProvider.Lemmy,
    "bluesky" => FediverseProvider.Bluesky,
    _ => FediverseProvider.All,
  };
}

public static class FediverseSearchRoute
{
  public static string Build(FediverseProvider provider, string? query)
  {
    var parameters = new List<string>();
    if (!string.IsNullOrWhiteSpace(query)) parameters.Add($"q={Uri.EscapeDataString(query.Trim())}");
    if (provider.QueryValue() is { } value) parameters.Add($"provider={Uri.EscapeDataString(value)}");
    return parameters.Count == 0 ? "/fediverse" : $"/fediverse?{string.Join("&", parameters)}";
  }
}
