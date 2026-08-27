#if WINDOWS
using System.Reflection;
using Microsoft.Windows.AI.Text;
using Windows.ApplicationModel.LimitedAccessFeatures;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.App.Chat;

public sealed class WindowsSystemLanguageModelRuntime : IWindowsSystemLanguageModelRuntime
{
  private const string FeatureId = "com.microsoft.windows.ai.languagemodel";
  private static readonly Lazy<bool> featureUnlocked = new(CheckFeatureUnlocked);
  private readonly SemaphoreSlim languageModelGate = new(1, 1);
  private LanguageModel? languageModel;

  public WindowsSystemLanguageModelState GetState()
  {
    try
    {
      if (!IsSystemLanguageModelSupported()) return new(WindowsSystemLanguageModelReadiness.Unsupported);
      if (!IsFeatureUnlocked()) return new(WindowsSystemLanguageModelReadiness.Disabled);
      var state = LanguageModel.GetReadyState().ToString();
      return state switch
      {
        "Ready" => new(WindowsSystemLanguageModelReadiness.Ready),
        "DisabledByUser" => new(WindowsSystemLanguageModelReadiness.Disabled),
        "NotSupportedOnCurrentSystem" => new(WindowsSystemLanguageModelReadiness.Unsupported),
        _ => new(WindowsSystemLanguageModelReadiness.NotReady),
      };
    }
    catch (Exception ex) { throw NativeFailure(ex); }
  }

  public async Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
  {
    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (!IsSystemLanguageModelSupported()) throw new PlatformNotSupportedException("Windows system language models require Windows 11 version 24H2 or later.");
      if (!IsFeatureUnlocked()) throw new InvalidOperationException("Windows system language model setup is not enabled for this build.");
      var operation = LanguageModel.EnsureReadyAsync();
      operation.Progress = (_, value) => progress?.Report(value);
      await operation.AsTask(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
    catch (Exception ex) { throw NativeFailure(ex); }
  }

  public async Task<string?> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message); ArgumentNullException.ThrowIfNull(history);
    cancellationToken.ThrowIfCancellationRequested();
    var model = await GetLanguageModelAsync(cancellationToken).ConfigureAwait(false);
    var prompt = LocalLLMHistoryBudget.BuildWindowsPrompt(history, message);
    try
    {
      var response = await model.GenerateResponseAsync(prompt).AsTask(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      return response.Status.ToString() == "Complete" ? response.Text : null;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
    catch (Exception ex) { throw NativeFailure(ex); }
  }

  private async Task<LanguageModel> GetLanguageModelAsync(CancellationToken cancellationToken)
  {
    try
    {
      if (!IsSystemLanguageModelSupported()) throw new PlatformNotSupportedException("Windows system language models require Windows 11 version 24H2 or later.");
      if (languageModel is not null) return languageModel;
      await languageModelGate.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        languageModel ??= await LanguageModel.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
        return languageModel;
      }
      finally { languageModelGate.Release(); }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
    catch (Exception ex) { throw NativeFailure(ex); }
  }

  private static bool IsFeatureUnlocked() => featureUnlocked.Value;

  private static bool IsSystemLanguageModelSupported() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100);

  private static InvalidOperationException NativeFailure(Exception ex) =>
      new("Windows system language model operation failed.", ex);

  private static bool CheckFeatureUnlocked()
  {
    var metadata = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
        .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    if (!metadata.TryGetValue("WindowsSystemLanguageModelLafToken", out var token) ||
        !metadata.TryGetValue("WindowsSystemLanguageModelLafAttestation", out var attestation)) return false;
    var result = LimitedAccessFeatures.TryUnlockFeature(FeatureId, token, attestation);
    return result.ToString() is "Available" or "AvailableWithoutToken";
  }
}
#endif
