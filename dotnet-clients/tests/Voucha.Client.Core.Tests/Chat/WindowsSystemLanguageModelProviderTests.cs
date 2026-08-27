using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class WindowsSystemLanguageModelProviderTests
{
  [Fact]
  public void WindowsPromptKeepsTheLatestInputWithinTheCombinedBudget()
  {
    var prompt = LocalLLMHistoryBudget.BuildWindowsPrompt(
        [new("assistant", new string('h', 1_000))], new string('u', 32_768));

    Assert.Equal(12_000, prompt.Length);
    Assert.StartsWith("user: ", prompt, StringComparison.Ordinal);
    Assert.DoesNotContain("assistant:", prompt, StringComparison.Ordinal);
  }

  [Fact]
  public void WindowsPromptUsesRemainingBudgetForTheMostRecentHistory()
  {
    var history = Enumerable.Range(0, 12)
        .Select(index => new LocalLLMResponseInput("assistant", $"{index}:{new string('h', 2_000)}"))
        .ToArray();

    var prompt = LocalLLMHistoryBudget.BuildWindowsPrompt(history, "latest input");

    Assert.True(prompt.Length <= 12_000);
    Assert.EndsWith("user: latest input", prompt, StringComparison.Ordinal);
    Assert.Contains("assistant: 11:", prompt, StringComparison.Ordinal);
    Assert.DoesNotContain("assistant: 0:", prompt, StringComparison.Ordinal);
  }

  [Fact]
  public void LocalModelBudgetsDoNotSplitUnicodeTextElements()
  {
    const string emoji = "👩‍💻";
    var current = new string('u', 11_993) + emoji;
    var prompt = LocalLLMHistoryBudget.BuildWindowsPrompt([], current);
    var endpointInput = LocalLLMHistoryBudget.BuildEndpointInput([], new string('u', 11_999) + emoji);
    var endpointHistory = LocalLLMHistoryBudget.BuildEndpointInput([new("assistant", emoji)], new string('u', 11_999));
    var promptHistory = LocalLLMHistoryBudget.BuildWindowsPrompt([new("assistant", emoji)], new string('u', 11_981));

    Assert.DoesNotContain(emoji, prompt, StringComparison.Ordinal);
    Assert.DoesNotContain(emoji, endpointInput.Single().Content, StringComparison.Ordinal);
    Assert.Single(endpointHistory);
    Assert.DoesNotContain(emoji, promptHistory, StringComparison.Ordinal);
    Assert.False(char.IsHighSurrogate(prompt[^1]));
    Assert.False(char.IsHighSurrogate(endpointInput.Single().Content[^1]));
  }

  [Theory]
  [InlineData("en", WindowsSystemLanguageModelReadiness.Ready, "Ready.")]
  [InlineData("es", WindowsSystemLanguageModelReadiness.Disabled, "El modelo de lenguaje del sistema Windows está desactivado.")]
  [InlineData("fr", WindowsSystemLanguageModelReadiness.NotReady, "Le téléchargement et la configuration sont nécessaires.")]
  [InlineData("pt", WindowsSystemLanguageModelReadiness.Unsupported, "Este dispositivo não suporta o modelo de linguagem do sistema Windows.")]
  public void RuntimeReadinessUsesTheSelectedUiLocale(string locale, WindowsSystemLanguageModelReadiness readiness, string expected)
  {
    using var controller = new UiLocaleController(new DeviceLanguageProvider(locale));
    var provider = new WindowsSystemLanguageModelProvider(new Runtime(readiness), new UiLocalization(controller));

    Assert.Equal(expected, provider.Status.StatusText);
    Assert.NotNull(provider.Status.StatusTextValue.Key);
  }

  private sealed class Runtime(WindowsSystemLanguageModelReadiness readiness) : IWindowsSystemLanguageModelRuntime
  {
    public WindowsSystemLanguageModelState GetState() => new(readiness);
    public Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string?> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("Local reply");
  }

  private sealed class DeviceLanguageProvider(string locale) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [locale];
  }
}
