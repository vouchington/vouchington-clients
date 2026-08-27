using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class LocalLLMSettingsTests
{
  [Theory]
  [InlineData("http://127.0.0.1:11434", "http://127.0.0.1:11434/v1/responses")]
  [InlineData("http://127.0.0.1:11434/v1", "http://127.0.0.1:11434/v1/responses")]
  [InlineData("https://models.example.test/openai", "https://models.example.test/openai/responses")]
  public void TryBuildResponsesUriNormalizesSupportedEndpointShapes(string endpoint, string expected) =>
      Assert.Equal(expected, LocalLLMEndpointProfile.TryBuildResponsesUri(endpoint)?.ToString());

  [Theory]
  [InlineData("")]
  [InlineData("ftp://127.0.0.1:11434")]
  [InlineData("http://models.example.test")]
  [InlineData("https://models.example.test/v1?secret=no")]
  public void TryBuildResponsesUriRejectsUnsafeEndpoints(string endpoint) =>
      Assert.Null(LocalLLMEndpointProfile.TryBuildResponsesUri(endpoint));

  [Fact]
  public void ConfigurationKeepsExplicitMissingProviderSelection()
  {
    var selected = LocalChatProviderIds.OpenAICompatible(Guid.NewGuid());
    var configuration = new LocalLLMConfiguration([], null, selected);
    Assert.Empty(configuration.Profiles); Assert.Null(configuration.SelectedEndpoint); Assert.Equal(selected, configuration.SelectedProviderId);
  }

  [Fact]
  public void FileConfigurationStorePersistsProfilesAndRecoversInvalidJson()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Ollama", true, "http://localhost:11434/v1", ["gpt-oss"], "gpt-oss");
    var store = new FileLocalLLMConfigurationStore(path);
    store.Save(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var loaded = Assert.Single(store.Load().Profiles);
    Assert.Equal(profile.Id, loaded.Id);
    Assert.Equal(profile.DisplayName, loaded.DisplayName);
    Assert.Equal(profile.Endpoint, loaded.Endpoint);
    Assert.Equal(profile.ModelNames, loaded.ModelNames);
    Assert.Equal(profile.SelectedModelName, loaded.SelectedModelName);
    File.WriteAllText(path, "{"); Assert.Equal(new(), store.Load());
    store.Clear(); Assert.False(File.Exists(path));
  }

  [Fact]
  public void FileConfigurationStorePersistsCredentialReplacementRequirement()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Ollama", false, "http://localhost:11434/v1", ["gpt-oss"], "gpt-oss", true);
    using var store = new FileLocalLLMConfigurationStore(path);

    store.Save(new([profile]));

    Assert.True(Assert.Single(store.Load().Profiles).RequiresCredentialReplacement);
  }

  [Fact]
  public void FileConfigurationStoreMigratesLegacySettingsToOneStableSelectedProfile()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, """{"isEnabled":true,"endpoint":"http://localhost:11434/v1","modelNames":["gpt-oss", "  llama  "],"selectedModelName":"gpt-oss"}""");
    using var store = new FileLocalLLMConfigurationStore(path);

    var migrated = Assert.Single(store.Load().Profiles);
    var reloaded = Assert.Single(store.Load().Profiles);

    Assert.Equal(LocalLLMLegacyMigration.ProfileId, migrated.Id);
    Assert.Equal(migrated.Id, reloaded.Id);
    Assert.True(migrated.IsEnabled);
    Assert.Equal("http://localhost:11434/v1", migrated.Endpoint);
    Assert.Equal(["gpt-oss", "  llama  "], migrated.ModelNames);
    Assert.Equal("gpt-oss", migrated.SelectedModelName);
    var configuration = store.Load();
    Assert.Equal(migrated.Id, configuration.SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(migrated.Id), configuration.SelectedProviderId);
    using var persisted = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
    Assert.False(persisted.RootElement.TryGetProperty("isEnabled", out _));
    Assert.True(persisted.RootElement.TryGetProperty("endpoints", out _));
  }

  [Fact]
  public void FileConfigurationStoreMigratesDisabledLegacySettingsWithoutSelectingTheProfile()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, """{"isEnabled":false,"endpoint":"https://models.example.test","modelNames":["model"],"selectedModelName":"model"}""");
    using var store = new FileLocalLLMConfigurationStore(path);

    var configuration = store.Load();
    var profile = Assert.Single(configuration.Profiles);

    Assert.False(profile.IsEnabled);
    Assert.Equal("https://models.example.test", profile.Endpoint);
    Assert.Null(configuration.SelectedEndpointId);
    Assert.Null(configuration.SelectedProviderId);
  }

  [Fact]
  public void FileConfigurationStoreReturnsNormalizedHyphenProviderIdsWithoutRewritingTheFile()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Ollama", true, "http://localhost:11434/v1", ["gpt-oss"], "gpt-oss");
    var hyphenId = $"openai-compatible:{profile.Id:D}";
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var store = new FileLocalLLMConfigurationStore(path);
    store.Save(new([profile], profile.Id, hyphenId));

    var loaded = store.Load();

    Assert.Equal(LocalChatProviderIds.OpenAICompatible(profile.Id), loaded.SelectedProviderId);
    Assert.Contains(hyphenId, File.ReadAllText(path), StringComparison.Ordinal);
  }

  [Fact]
  public void FileConfigurationStorePersistsNormalizedProviderIdsOnTheNextMutation()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Ollama", true, "http://localhost:11434/v1", ["gpt-oss"], "gpt-oss");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var store = new FileLocalLLMConfigurationStore(path);
    store.Save(new([profile], profile.Id, $"openai-compatible:{profile.Id:D}"));

    store.Update(configuration => configuration);

    Assert.Contains(LocalChatProviderIds.OpenAICompatible(profile.Id), File.ReadAllText(path), StringComparison.Ordinal);
    Assert.DoesNotContain("openai-compatible:", File.ReadAllText(path), StringComparison.Ordinal);
  }

  [Fact]
  public async Task FileConfigurationStoreDoesNotOverwriteUnreadableSettingsDuringMutations()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "local-llm.json");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, "{");
    using var store = new FileLocalLLMConfigurationStore(path);

    Assert.Equal(new(), store.Load());
    Assert.ThrowsAny<System.Text.Json.JsonException>(() => store.Update(_ => new()));
    await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => store.UpdateAsync(
        _ => Task.FromResult(new LocalLLMConfiguration()), TestContext.Current.CancellationToken));
    Assert.Equal("{", File.ReadAllText(path));
  }
}
