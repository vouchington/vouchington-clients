using Microsoft.Extensions.Logging;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.App;

public sealed partial class MauiLocalLLMSecretStore : ILocalLLMSecretStore
{
  private const string StorageKeyPrefix = "voucha.local-llm.openai-compatible-api-key.";
  private const string LegacyStorageKey = "voucha.local-llm.openai-compatible-api-key";
  private readonly ILogger<MauiLocalLLMSecretStore> logger;

  public MauiLocalLLMSecretStore(ILogger<MauiLocalLLMSecretStore> logger) =>
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

  public async Task<string?> ReadApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default)
  {
    try
    {
      var apiKey = await SecureStorage.Default.GetAsync(StorageKey(profileId)).ConfigureAwait(false);
      if (!string.IsNullOrWhiteSpace(apiKey))
      {
        if (profileId == LocalLLMLegacyMigration.ProfileId) TryClearLegacyApiKey();
        return apiKey;
      }
      if (profileId != LocalLLMLegacyMigration.ProfileId) return apiKey;
      var legacyApiKey = await SecureStorage.Default.GetAsync(LegacyStorageKey).ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(legacyApiKey)) return apiKey;
      legacyApiKey = legacyApiKey.Trim();
      await SecureStorage.Default.SetAsync(StorageKey(profileId), legacyApiKey).ConfigureAwait(false);
      TryClearLegacyApiKey();
      return legacyApiKey;
    }
    catch (Exception ex)
    {
      LogReadFailed(logger, ex);
      return null;
    }
  }

  public async Task SaveApiKeyAsync(Guid profileId, string apiKey, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    try
    {
      if (string.IsNullOrWhiteSpace(apiKey))
      {
        SecureStorage.Default.Remove(StorageKey(profileId));
        if (profileId == LocalLLMLegacyMigration.ProfileId) ClearLegacyApiKey();
        return;
      }
      await SecureStorage.Default.SetAsync(StorageKey(profileId), apiKey.Trim()).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      LogWriteFailed(logger, ex);
      throw;
    }
  }

  public Task ClearApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    try
    {
      SecureStorage.Default.Remove(StorageKey(profileId));
      if (profileId == LocalLLMLegacyMigration.ProfileId) ClearLegacyApiKey();
    }
    catch (Exception ex)
    {
      LogClearFailed(logger, ex);
      throw;
    }
    return Task.CompletedTask;
  }

  private static string StorageKey(Guid profileId) => $"{StorageKeyPrefix}{profileId:D}";

  private static void ClearLegacyApiKey() => SecureStorage.Default.Remove(LegacyStorageKey);

  private void TryClearLegacyApiKey()
  {
    try { ClearLegacyApiKey(); }
    catch (Exception ex) { LogClearFailed(logger, ex); }
  }

  [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "SecureStorage local LLM API key read failed.")]
  private static partial void LogReadFailed(ILogger logger, Exception exception);

  [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "SecureStorage local LLM API key write failed.")]
  private static partial void LogWriteFailed(ILogger logger, Exception exception);

  [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "SecureStorage local LLM API key clear failed.")]
  private static partial void LogClearFailed(ILogger logger, Exception exception);
}
