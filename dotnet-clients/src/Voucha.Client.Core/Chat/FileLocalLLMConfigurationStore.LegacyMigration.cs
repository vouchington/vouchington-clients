using System.Text.Json;

namespace Voucha.Client.Core.Chat;

public static class LocalLLMLegacyMigration
{
  public static Guid ProfileId { get; } = Guid.Parse("70cd237f-6920-4a4a-81de-03dad9afbab6");
}

public sealed partial class FileLocalLLMConfigurationStore
{
  private static bool TryMigrateLegacyConfiguration(string serialized, JsonSerializerOptions options,
      out LocalLLMConfiguration configuration)
  {
    using var document = JsonDocument.Parse(serialized);
    if (document.RootElement.ValueKind != JsonValueKind.Object ||
        document.RootElement.TryGetProperty("endpoints", out _) ||
        !document.RootElement.TryGetProperty("isEnabled", out _))
    {
      configuration = new();
      return false;
    }

    var legacy = JsonSerializer.Deserialize<LegacyLocalLLMConfiguration>(serialized, options) ?? new();
    var profile = new LocalLLMEndpointProfile(LocalLLMLegacyMigration.ProfileId, "OpenAI-compatible local model", legacy.IsEnabled,
        legacy.Endpoint, legacy.ModelNames, legacy.SelectedModelName);
    configuration = new([profile], legacy.IsEnabled ? profile.Id : null,
        legacy.IsEnabled ? LocalChatProviderIds.OpenAICompatible(profile.Id) : null);
    return true;
  }

  private sealed record LegacyLocalLLMConfiguration(
      bool IsEnabled = false,
      string Endpoint = "",
      IReadOnlyList<string>? ModelNames = null,
      string SelectedModelName = "");
}
