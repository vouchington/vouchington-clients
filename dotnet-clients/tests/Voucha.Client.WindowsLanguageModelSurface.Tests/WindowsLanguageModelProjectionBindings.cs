using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;

namespace Voucha.Client.WindowsLanguageModelSurface.Tests;

internal static class WindowsLanguageModelProjectionBindings
{
  internal static int InvocationCount { get; private set; }

  internal static void Bind()
  {
    InvocationCount++;
    var usablePromptLength = (LanguageModel model, string prompt) => model.GetUsablePromptLength(prompt);
    var usablePromptLengthInContext = (LanguageModel model, LanguageModelContext context, string prompt) =>
        model.GetUsablePromptLength(context, prompt);
    var context = (LanguageModel model) => model.CreateContext();
    var instructed = (LanguageModel model, string systemPrompt) => model.CreateContext(systemPrompt);
    var filtered = (LanguageModel model, string systemPrompt, ContentFilterOptions contentFilterOptions) =>
        model.CreateContext(systemPrompt, contentFilterOptions);
    var generated = (LanguageModel model, string prompt) => model.GenerateResponseAsync(prompt);
    var generatedWithOptions = (LanguageModel model, string prompt, LanguageModelOptions options) =>
        model.GenerateResponseAsync(prompt, options);
    var generatedInContext = (
        LanguageModel model,
        LanguageModelContext languageModelContext,
        string prompt,
        LanguageModelOptions options) => model.GenerateResponseAsync(languageModelContext, prompt, options);
    var temperature = (LanguageModelOptions options) => options.Temperature;
    var topP = (LanguageModelOptions options) => options.TopP;
    var topK = (LanguageModelOptions options) => options.TopK;
    var filters = (LanguageModelOptions options) => options.ContentFilterOptions;
    _ = usablePromptLength;
    _ = usablePromptLengthInContext;
    _ = context;
    _ = instructed;
    _ = filtered;
    _ = generated;
    _ = generatedWithOptions;
    _ = generatedInContext;
    _ = temperature;
    _ = topP;
    _ = topK;
    _ = filters;
    _ = LanguageModelResponseStatus.BlockedByPolicy;
    _ = LanguageModelResponseStatus.Complete;
    _ = LanguageModelResponseStatus.Error;
    _ = LanguageModelResponseStatus.InProgress;
    _ = LanguageModelResponseStatus.PromptBlockedByContentModeration;
    _ = LanguageModelResponseStatus.PromptLargerThanContext;
    _ = LanguageModelResponseStatus.ResponseBlockedByContentModeration;
  }
}
