using System.Text.Json;

namespace Voucha.Client.Core.Chat;

public sealed partial class OpenAICompatibleResponsesClient
{
  private sealed record ToolCall(string CallId, string Name, string Arguments);

  private static void RequireCompletedStatuses(JsonElement response, JsonElement output)
  {
    RequireCompletedStatus(response);
    foreach (var item in output.EnumerateArray())
    {
      if (item.ValueKind == JsonValueKind.Object &&
          item.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
          type.GetString() == "message") RequireCompletedStatus(item);
    }
  }

  private static void RequireCompletedStatus(JsonElement item)
  {
    if (item.TryGetProperty("status", out var status) &&
        (status.ValueKind != JsonValueKind.String || status.GetString() != "completed"))
      throw new InvalidDataException("Incomplete local model response.");
  }

  private static List<ToolCall> ToolCalls(JsonElement output, HashSet<string> seenCallIds)
  {
    var calls = new List<ToolCall>();
    foreach (var item in output.EnumerateArray())
    {
      if (item.ValueKind != JsonValueKind.Object ||
          !item.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
          type.GetString() != "function_call") continue;
      RequireCompletedStatus(item);
      if (!item.TryGetProperty("call_id", out var callIdValue) ||
          callIdValue.ValueKind != JsonValueKind.String ||
          callIdValue.GetString() is not { Length: > 0 } callId ||
          !seenCallIds.Add(callId) ||
          !item.TryGetProperty("name", out var nameValue) ||
          nameValue.ValueKind != JsonValueKind.String ||
          nameValue.GetString() is not { Length: > 0 } name ||
          !item.TryGetProperty("arguments", out var argumentsValue) ||
          argumentsValue.ValueKind != JsonValueKind.String)
        throw new InvalidDataException("Invalid local model function call.");
      calls.Add(new ToolCall(callId, name, argumentsValue.GetString()!));
    }
    return calls;
  }

  private static string? ToolResponseText(JsonElement response, JsonElement output)
  {
    if (response.TryGetProperty("output_text", out var outputText) &&
        outputText.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(outputText.GetString())) return outputText.GetString()!.Trim();
    var text = string.Concat(output.EnumerateArray()
        .Where(item => item.ValueKind == JsonValueKind.Object &&
            item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        .SelectMany(item => item.GetProperty("content").EnumerateArray())
        .Where(content => content.ValueKind == JsonValueKind.Object &&
            content.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
        .Select(content => content.GetProperty("text").GetString()));
    return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
  }
}
