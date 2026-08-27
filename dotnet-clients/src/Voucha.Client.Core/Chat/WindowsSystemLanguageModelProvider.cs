using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public enum WindowsSystemLanguageModelReadiness { Ready, NotReady, Unsupported, Disabled }

public sealed record WindowsSystemLanguageModelState(
    WindowsSystemLanguageModelReadiness Readiness,
    string ModelName = "Windows system language model");

public interface IWindowsSystemLanguageModelRuntime
{
  WindowsSystemLanguageModelState GetState();
  Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default);
  Task<string?> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default);
}

public sealed class WindowsSystemLanguageModelProvider : ILocalChatProvider
{
  private readonly IWindowsSystemLanguageModelRuntime runtime;
  private readonly IUiLocalization localization;

  public WindowsSystemLanguageModelProvider(IWindowsSystemLanguageModelRuntime runtime, IUiLocalization? localization = null)
  {
    this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    this.localization = localization ?? UiLocalization.English;
  }

  public string Id => LocalChatProviderIds.WindowsSystemLanguageModel;

  public WindowsSystemLanguageModelReadiness Readiness => TryGetState()?.Readiness ?? WindowsSystemLanguageModelReadiness.Unsupported;

  public ChatProviderStatus Status
  {
    get
    {
      var state = TryGetState();
      if (state is null)
      {
        return new(
            ChatProviderKind.Local,
            UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal),
            false,
            UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable),
            localization,
            Id);
      }
      return new(ChatProviderKind.Local, UiText.Verbatim(state.ModelName), state.Readiness == WindowsSystemLanguageModelReadiness.Ready,
          UiText.Localized(StatusKey(state.Readiness)), localization, Id, state.ModelName);
    }
  }

  public Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
      runtime.EnsureReadyAsync(progress, cancellationToken);

  public async Task<LocalChatGenerationResult> GenerateAssistantContentAsync(string message,
      IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default)
  {
    var readiness = Readiness;
    if (readiness != WindowsSystemLanguageModelReadiness.Ready)
    {
      throw new InvalidOperationException(localization.Localize(StatusKey(readiness)));
    }
    var content = await runtime.GenerateAssistantContentAsync(message, history, cancellationToken).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse));
    return new(content, "windows_foundry", null);
  }

  private WindowsSystemLanguageModelState? TryGetState()
  {
    try { return runtime.GetState(); }
    catch (InvalidOperationException) { return null; }
  }

  private static UiMessageKey StatusKey(WindowsSystemLanguageModelReadiness readiness) => readiness switch
  {
    WindowsSystemLanguageModelReadiness.Ready => UiMessageKey.NativeDotnetChatConversationWindowsModelReady,
    WindowsSystemLanguageModelReadiness.Disabled => UiMessageKey.NativeDotnetChatConversationWindowsModelDisabled,
    WindowsSystemLanguageModelReadiness.Unsupported => UiMessageKey.NativeDotnetChatConversationWindowsModelUnsupported,
    _ => UiMessageKey.NativeDotnetChatConversationWindowsModelSetupRequired,
  };
}
