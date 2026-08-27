using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Chat;

public sealed record LocalLLMEndpointProfile(
    Guid Id,
    string DisplayName,
    bool IsEnabled = false,
    string Endpoint = "",
    IReadOnlyList<string>? ModelNames = null,
    string SelectedModelName = "",
    bool RequiresCredentialReplacement = false)
{
  public IReadOnlyList<string> NormalizedModelNames => (ModelNames ?? [])
      .Select(model => model.Trim()).Where(model => model.Length > 0)
      .Distinct(StringComparer.Ordinal).ToArray();

  public string? SelectedModel => string.IsNullOrWhiteSpace(SelectedModelName) ? null : SelectedModelName.Trim();

  public Uri? ResponsesUri => TryBuildResponsesUri(Endpoint);

  // Bracket-aware and always-explicit-port so this canonicalizes identically to Swift's
  // LocalLLMEndpointProfile.origin: an unbracketed IPv6 host would make "scheme://host:port"
  // ambiguous, since the host itself contains colons.
  public string? Origin => ResponsesUri is { } uri ? $"{uri.Scheme}://{uri.Host}:{uri.Port}" : null;

  public bool IsSelectableAsCurrent => IsEnabled && ResponsesUri is not null && SelectedModel is not null;

  public static Uri? TryBuildResponsesUri(string endpoint)
  {
    ArgumentNullException.ThrowIfNull(endpoint);
    if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) ||
        uri.Scheme is not "http" and not "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
        !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
        HasZoneId(uri) ||
        (uri.Scheme == "http" && !LocalLLMNetworkPolicy.IsPrivateNetworkHost(uri.Host)))
    {
      return null;
    }

    var path = uri.AbsolutePath.Trim('/');
    var builder = new UriBuilder(uri)
    {
      Path = path switch
      {
        "" or "v1" => "/v1/responses",
        var value when value.EndsWith("responses", StringComparison.Ordinal) => "/" + value,
        _ => "/" + path + "/responses",
      }
    };
    return builder.Uri;
  }

  // Uri.Host silently strips an IPv6 zone ID (e.g. "[fe80::1%eth0]" parses to Host "[fe80::1]"),
  // so the scope suffix can only be detected from Uri.IdnHost, which is the one Uri accessor
  // that still carries it. A zone ID is rejected unconditionally, regardless of scheme: it is
  // only meaningful on the interface that assigned it, so an endpoint string containing one can
  // never be verified as portable/safe. Rejecting it here (rather than only under the http-only
  // cleartext gate) also keeps Origin canonicalization from diverging between platforms: this
  // Uri.Host-based Origin silently drops a zone ID while Swift's URL.host retains it
  // percent-decoded, so letting a zone-ID host through under https would make the two clients
  // disagree on the credential-scoping origin for the same endpoint.
  private static bool HasZoneId(Uri uri) => uri.IdnHost.Contains('%', StringComparison.Ordinal);
}

public sealed record LocalLLMConfiguration(
    IReadOnlyList<LocalLLMEndpointProfile>? Endpoints = null,
    Guid? SelectedEndpointId = null,
    string? SelectedProviderId = null)
{
  public IReadOnlyList<LocalLLMEndpointProfile> Profiles => (Endpoints ?? [])
      .GroupBy(profile => profile.Id).Select(group => group.First()).ToArray();
  public LocalLLMEndpointProfile? SelectedEndpoint =>
      SelectedEndpointId is { } id ? Profiles.SingleOrDefault(profile => profile.Id == id) : null;
}

public interface ILocalLLMConfigurationStore
{
  LocalLLMConfiguration Load();
  void Save(LocalLLMConfiguration configuration);
  void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate);
  Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default);
  Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default);
  void Clear();
}
public interface ILocalLLMSecretStore
{
  Task<string?> ReadApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default);
  Task SaveApiKeyAsync(Guid profileId, string apiKey, CancellationToken cancellationToken = default);
  Task ClearApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default);
}

public sealed partial class FileLocalLLMConfigurationStore : ILocalLLMConfigurationStore, IDisposable
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private readonly SemaphoreSlim gate = new(1, 1);
  private readonly string path;
  public FileLocalLLMConfigurationStore(string path)
  {
    this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
  }
  public LocalLLMConfiguration Load()
  {
    gate.Wait(); try { return Read(); } finally { gate.Release(); }
  }
  public void Save(LocalLLMConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration); gate.Wait(); try { Write(configuration); } finally { gate.Release(); }
  }
  public void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate)
  {
    ArgumentNullException.ThrowIfNull(mutate); gate.Wait(); try { Write(mutate(ReadForMutation()) ?? throw new InvalidOperationException("The local model configuration update returned null.")); } finally { gate.Release(); }
  }
  public async Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutate); await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { Write(await mutate(ReadForMutation()).ConfigureAwait(false) ?? throw new InvalidOperationException("The local model configuration update returned null.")); }
    finally { gate.Release(); }
  }
  public async Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutate); await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { await mutate(new LocalLLMConfigurationTransaction(ReadForMutation(), Write, ClearUnsafe)).ConfigureAwait(false); }
    finally { gate.Release(); }
  }
  public void Clear() { gate.Wait(); try { ClearUnsafe(); } finally { gate.Release(); } }
  private void Write(LocalLLMConfiguration configuration)
  {
    var directory = Path.GetDirectoryName(path); if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    File.WriteAllText(path, JsonSerializer.Serialize(configuration, JsonOptions));
  }
  private void ClearUnsafe() { if (File.Exists(path)) File.Delete(path); }
  public void Dispose() => gate.Dispose();
}

public sealed class InMemoryLocalLLMConfigurationStore(LocalLLMConfiguration? configuration = null) : ILocalLLMConfigurationStore, IDisposable
{
  private LocalLLMConfiguration configuration = configuration ?? new();
  private readonly SemaphoreSlim gate = new(1, 1);
  public LocalLLMConfiguration Load() { gate.Wait(); try { return configuration; } finally { gate.Release(); } }
  public void Save(LocalLLMConfiguration configuration) =>
      Update(_ => configuration ?? throw new ArgumentNullException(nameof(configuration)));
  public void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate)
  {
    ArgumentNullException.ThrowIfNull(mutate); gate.Wait(); try { configuration = mutate(configuration) ?? throw new InvalidOperationException("The local model configuration update returned null."); } finally { gate.Release(); }
  }
  public async Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutate); await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { configuration = await mutate(configuration).ConfigureAwait(false) ?? throw new InvalidOperationException("The local model configuration update returned null."); }
    finally { gate.Release(); }
  }
  public async Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutate); await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { await mutate(new LocalLLMConfigurationTransaction(configuration, next => configuration = next, ClearUnsafe)).ConfigureAwait(false); }
    finally { gate.Release(); }
  }
  public void Clear() { gate.Wait(); try { ClearUnsafe(); } finally { gate.Release(); } }
  private void ClearUnsafe() => configuration = new();
  public void Dispose() => gate.Dispose();
}

public sealed class InMemoryLocalLLMSecretStore : ILocalLLMSecretStore
{
  private readonly Dictionary<Guid, string> apiKeys = [];
  public Task<string?> ReadApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default) => Task.FromResult(apiKeys.GetValueOrDefault(profileId));
  public Task SaveApiKeyAsync(Guid profileId, string apiKey, CancellationToken cancellationToken = default) { if (string.IsNullOrWhiteSpace(apiKey)) apiKeys.Remove(profileId); else apiKeys[profileId] = apiKey.Trim(); return Task.CompletedTask; }
  public Task ClearApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default) { apiKeys.Remove(profileId); return Task.CompletedTask; }
}

public sealed record LocalLLMResponseInput([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

public sealed class LocalLLMFeaturePolicy
{
  public LocalLLMFeaturePolicy(IReadOnlyDictionary<string, string?>? environment = null)
  {
    environment ??= Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(entry => Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture)!, entry => Convert.ToString(entry.Value, System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
    IsEnabled = environment.TryGetValue("VOUCHA_NATIVE_LOCAL_LLM_ENABLED", out var value) && !string.IsNullOrWhiteSpace(value)
        ? value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        : OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst();
  }
  public bool IsEnabled { get; }
}
