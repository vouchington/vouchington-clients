using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Chat;

/// <summary>
/// Prompt-fit signals from the pinned Windows language-model surface.
/// A fitting prompt does not reserve future assistant-answer tokens.
/// </summary>
public readonly record struct WindowsBuiltInPromptObservation(
    ulong UsablePromptIndex,
    bool PromptLargerThanContext,
    int? SummarizerCutoffIndex);

public readonly record struct WindowsBuiltInModelTurn(string Status, string? Text);

public static class WindowsBuiltInPrompt
{
  public static bool Fits(string prompt, WindowsBuiltInPromptObservation observation)
  {
    ArgumentNullException.ThrowIfNull(prompt);
    if (observation.PromptLargerThanContext) return false;
    if (observation.UsablePromptIndex < (ulong)prompt.Length) return false;
    return observation.SummarizerCutoffIndex is not int cutoff || cutoff >= prompt.Length;
  }

  public static string Compose(
      IReadOnlyList<LocalLLMResponseInput> history, string message, IReadOnlyList<MemberMcpFunctionTool> tools)
  {
    ArgumentNullException.ThrowIfNull(history);
    ArgumentNullException.ThrowIfNull(message);
    ArgumentNullException.ThrowIfNull(tools);
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer))
    {
      writer.WriteStartArray();
      foreach (var tool in tools)
      {
        writer.WriteStartObject();
        writer.WriteString("name", tool.Name);
        writer.WriteString("description", tool.Description);
        writer.WritePropertyName("parameters");
        writer.WriteRawValue(tool.Parameters.GetRawText());
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }

    var schemas = Encoding.UTF8.GetString(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    var prior = string.Join('\n', history.Select(item => item.Role + ": " + item.Content));
    var separator = prior.Length == 0 ? "" : "\n";
    var prompt = string.Concat("tools: ", schemas, "\n", prior, separator, "user: ", message);
    return prompt;
  }
}
