using System.Globalization;

namespace Voucha.Client.Core.Chat;

public static class LocalLLMHistoryBudget
{
  private const int MaximumMessages = 12;
  private const int MaximumCharacters = 12_000;

  internal static LocalLLMResponseInput[] Apply(IEnumerable<ChatMessageRow> messages)
  {
    ArgumentNullException.ThrowIfNull(messages);
    var retained = new List<LocalLLMResponseInput>();
    var remainingCharacters = MaximumCharacters;
    foreach (var message in messages.Reverse())
    {
      if (retained.Count == MaximumMessages || remainingCharacters == 0) break;
      if (message.HasError || string.IsNullOrWhiteSpace(message.Content) || (!message.IsUser && !message.IsAssistant)) continue;
      var content = Truncate(message.Content, remainingCharacters);
      retained.Add(new(message.Role, content));
      remainingCharacters -= content.Length;
    }
    retained.Reverse();
    return [.. retained];
  }

  public static string BuildWindowsPrompt(IReadOnlyList<LocalLLMResponseInput> history, string message)
  {
    ArgumentNullException.ThrowIfNull(history); ArgumentNullException.ThrowIfNull(message);
    const string userPrefix = "user: ";
    var currentMessage = Truncate(message, MaximumCharacters - userPrefix.Length);
    var remaining = MaximumCharacters - userPrefix.Length - currentMessage.Length;
    var retained = new List<string>();
    foreach (var item in history.Reverse().Take(MaximumMessages))
    {
      var prefix = item.Role + ": ";
      if (remaining <= prefix.Length + 1) break;
      var content = Truncate(item.Content, remaining - prefix.Length - 1);
      retained.Add(prefix + content);
      remaining -= prefix.Length + content.Length + 1;
    }
    retained.Reverse(); retained.Add(userPrefix + currentMessage);
    return string.Join(Environment.NewLine, retained);
  }

  public static LocalLLMResponseInput[] BuildEndpointInput(IReadOnlyList<LocalLLMResponseInput> history, string message)
  {
    ArgumentNullException.ThrowIfNull(history); ArgumentNullException.ThrowIfNull(message);
    var currentMessage = Truncate(message, MaximumCharacters);
    var remaining = MaximumCharacters - currentMessage.Length;
    var retained = new List<LocalLLMResponseInput>();
    foreach (var item in history.Reverse().Take(MaximumMessages))
    {
      if (remaining == 0) break;
      var content = Truncate(item.Content, remaining);
      if (content.Length == 0) continue;
      retained.Add(new(item.Role, content));
      remaining -= content.Length;
    }
    retained.Reverse(); retained.Add(new("user", currentMessage));
    return [.. retained];
  }

  private static string Truncate(string value, int maximumCharacters)
  {
    if (value.Length <= maximumCharacters) return value;
    var previousEnd = 0;
    foreach (var start in StringInfo.ParseCombiningCharacters(value))
    {
      if (start > maximumCharacters) return value[..previousEnd];
      previousEnd = start;
    }
    return value[..previousEnd];
  }
}
