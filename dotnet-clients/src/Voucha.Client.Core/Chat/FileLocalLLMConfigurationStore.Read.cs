using System.Text.Json;

namespace Voucha.Client.Core.Chat;

public sealed partial class FileLocalLLMConfigurationStore
{
  private LocalLLMConfiguration Read()
  {
    try { return ReadForMutation(); }
    catch (IOException) { return new(); }
    catch (JsonException) { return new(); }
    catch (UnauthorizedAccessException) { return new(); }
    catch (InvalidOperationException) { return new(); }
  }

  private LocalLLMConfiguration ReadForMutation()
  {
    if (!File.Exists(path)) return new();
    var serialized = File.ReadAllText(path);
    if (TryMigrateLegacyConfiguration(serialized, JsonOptions, out var migrated))
    {
      Write(migrated);
      return WithNormalizedProviderId(migrated);
    }
    return WithNormalizedProviderId(JsonSerializer.Deserialize<LocalLLMConfiguration>(serialized, JsonOptions)
        ?? throw new InvalidOperationException("The local model configuration could not be read."));
  }

  private static LocalLLMConfiguration WithNormalizedProviderId(LocalLLMConfiguration configuration)
  {
    var normalized = LocalChatProviderIds.Normalize(configuration.SelectedProviderId);
    return normalized == configuration.SelectedProviderId ? configuration :
        configuration with { SelectedProviderId = normalized };
  }
}
