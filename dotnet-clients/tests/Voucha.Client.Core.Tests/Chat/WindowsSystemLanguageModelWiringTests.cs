using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class WindowsSystemLanguageModelWiringTests
{
  [Fact]
  public void WindowsRuntimeCachesFeatureMetadataSerializesInitializationAndRechecksCancellation()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Chat", "WindowsSystemLanguageModelRuntime.Windows.cs"));
    Assert.Contains("static readonly Lazy<bool> featureUnlocked", source, StringComparison.Ordinal);
    Assert.Contains("SemaphoreSlim languageModelGate", source, StringComparison.Ordinal);
    Assert.Contains("languageModelGate.WaitAsync(cancellationToken)", source, StringComparison.Ordinal);
    Assert.True(source.IndexOf("await model.GenerateResponseAsync", StringComparison.Ordinal) <
        source.LastIndexOf("cancellationToken.ThrowIfCancellationRequested();", StringComparison.Ordinal));
  }

  [Fact]
  public void WindowsRuntimeClassifiesLockedFeaturesAsDisabledInsteadOfSetupRequired()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Chat", "WindowsSystemLanguageModelRuntime.Windows.cs"));

    Assert.Contains("if (!IsFeatureUnlocked()) return new(WindowsSystemLanguageModelReadiness.Disabled);", source, StringComparison.Ordinal);
  }

  [Fact]
  public void WindowsRuntimeGatesSystemModelCallsOnTheWindows24H2Runtime()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Chat", "WindowsSystemLanguageModelRuntime.Windows.cs"));

    Assert.Contains("OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100)", source, StringComparison.Ordinal);
    Assert.True(source.IndexOf("if (!IsSystemLanguageModelSupported()) return new(WindowsSystemLanguageModelReadiness.Unsupported);", StringComparison.Ordinal) <
        source.IndexOf("LanguageModel.GetReadyState()", StringComparison.Ordinal));
    Assert.Contains("throw new PlatformNotSupportedException", source, StringComparison.Ordinal);
  }

  [Fact]
  public void WindowsRuntimePreservesCallerCancellationAndTranslatesNativeFailures()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Chat", "WindowsSystemLanguageModelRuntime.Windows.cs"));

    Assert.Contains("catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }", source, StringComparison.Ordinal);
    Assert.Contains("new(\"Windows system language model operation failed.\", ex)", source, StringComparison.Ordinal);
    Assert.Contains("cancellationToken.ThrowIfCancellationRequested();\n      if (!IsSystemLanguageModelSupported())", source, StringComparison.Ordinal);
  }

  [Fact]
  public void WindowsProviderConvertsReadinessFailuresToUnavailableStatuses()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.Core", "Chat", "WindowsSystemLanguageModelProvider.cs"));

    Assert.Contains("private WindowsSystemLanguageModelState? TryGetState()", source, StringComparison.Ordinal);
    Assert.Contains("catch (InvalidOperationException) { return null; }", source, StringComparison.Ordinal);
    Assert.Contains("if (state is null)", source, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable", source, StringComparison.Ordinal);
  }

  [Fact]
  public void ScopedSecretMigrationIsLimitedToTheDeterministicLegacyProfile()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "MauiLocalLLMSecretStore.cs"));

    Assert.Contains("LegacyStorageKey = \"voucha.local-llm.openai-compatible-api-key\"", source, StringComparison.Ordinal);
    Assert.Contains("await SecureStorage.Default.SetAsync(StorageKey(profileId), legacyApiKey).ConfigureAwait(false);\n      TryClearLegacyApiKey();", source, StringComparison.Ordinal);
    Assert.Contains("if (profileId == LocalLLMLegacyMigration.ProfileId) TryClearLegacyApiKey();", source, StringComparison.Ordinal);
    Assert.Contains("if (profileId != LocalLLMLegacyMigration.ProfileId) return apiKey;", source, StringComparison.Ordinal);
  }

  [Fact]
  public void SecretStoreMutationFailuresArePropagated()
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "MauiLocalLLMSecretStore.cs"));

    Assert.Contains("LogWriteFailed(logger, ex);\n      throw;", source, StringComparison.Ordinal);
    Assert.Contains("LogClearFailed(logger, ex);\n      throw;", source, StringComparison.Ordinal);
    Assert.Contains("private static void ClearLegacyApiKey() => SecureStorage.Default.Remove(LegacyStorageKey);", source, StringComparison.Ordinal);
    Assert.Contains("try { ClearLegacyApiKey(); }\n    catch (Exception ex) { LogClearFailed(logger, ex); }", source, StringComparison.Ordinal);
    Assert.Equal(2, source.Split("if (profileId == LocalLLMLegacyMigration.ProfileId) ClearLegacyApiKey();").Length - 1);
  }

  [Fact]
  public void ChatPageExposesAnExplicitWindowsSetupAction()
  {
    var xaml = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ChatConversationPage.xaml"));
    var codeBehind = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ChatConversationPage.xaml.cs"));
    Assert.Contains("OnSetUpWindowsModelClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("CanSetUpSelectedWindowsSystemLanguageModel", xaml, StringComparison.Ordinal);
    Assert.Contains("native.dotnet.chatConversation.setUpWindowsModel", xaml, StringComparison.Ordinal);
    Assert.Contains("SetUpSelectedWindowsSystemLanguageModelAsync", codeBehind, StringComparison.Ordinal);
  }

  [Fact]
  public void AppProviderPickerDelegatesSelectionToTheAtomicCoreStore()
  {
    var resolver = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Chat", "ChatProviderResolver.cs"));
    var xaml = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ChatConversationPage.xaml"));
    var codeBehind = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ChatConversationPage.xaml.cs"));
    Assert.Contains("LocalLLMProviderSelection.Save(configurationStore, providerId)", resolver, StringComparison.Ordinal);
    Assert.Contains("LocalChatProviderIds.RefersTo(providerId, profile.Id)", resolver, StringComparison.Ordinal);
    Assert.Contains("SelectedIndexChanged=\"OnProviderSelected\"", xaml, StringComparison.Ordinal);
    Assert.Contains("PersistSelectedProviderAsync", codeBehind, StringComparison.Ordinal);
  }

  private static string RepoPath(params string[] parts)
  {
    var starts = new[]
    {
      Environment.GetEnvironmentVariable("PWD"),
      Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"),
      Directory.GetCurrentDirectory(),
      Uri.UnescapeDataString(AppContext.BaseDirectory),
    }.Where(start => !string.IsNullOrWhiteSpace(start));
    foreach (var start in starts)
    {
      for (var directory = new DirectoryInfo(start!); directory is not null; directory = directory.Parent)
      {
        var candidate = Path.Combine([directory.FullName, .. parts]);
        if (File.Exists(candidate)) return candidate;
      }
    }
    throw new DirectoryNotFoundException("Could not find the repository root.");
  }
}
