using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Chat;

public sealed record WindowsBuiltInToolCall(string Id, string Name, string ArgumentsJson);

public readonly record struct WindowsBuiltInParsedCommand(string? Answer, WindowsBuiltInToolCall? ToolCall);

public static class WindowsBuiltInCommandParser
{
  public const int MaximumCommandBytes = 1_100_000;

  public static WindowsBuiltInParsedCommand Parse(string text)
  {
    ArgumentNullException.ThrowIfNull(text);
    var utf8 = Encoding.UTF8.GetBytes(text);
    if (utf8.Length > MaximumCommandBytes)
      throw new InvalidDataException("Windows built-in command is too large.");
    try
    {
      RejectDuplicateKeys(utf8);
      using var document = JsonDocument.Parse(utf8);
      if (document.RootElement.ValueKind != JsonValueKind.Object)
        throw new InvalidDataException("Windows built-in command must be one JSON object.");
      var properties = document.RootElement.EnumerateObject().ToArray();
      if (properties.Length != 1)
        throw new InvalidDataException("Windows built-in command must be an answer or one call.");
      return properties[0].Name switch
      {
        "answer" => new(RequiredAnswer(properties[0].Value), null),
        "call" => new(null, RequiredCall(properties[0].Value)),
        _ => throw new InvalidDataException("Windows built-in command has an unknown key."),
      };
    }
    catch (JsonException error)
    {
      throw new InvalidDataException("Windows built-in command is not closed JSON.", error);
    }
  }

  private static string RequiredAnswer(JsonElement value) =>
      value.ValueKind == JsonValueKind.String
          ? value.GetString()!
          : throw new InvalidDataException("Windows built-in answer must be a string.");

  private static WindowsBuiltInToolCall RequiredCall(JsonElement value)
  {
    if (value.ValueKind != JsonValueKind.Object)
      throw new InvalidDataException("Windows built-in call must be one object.");
    var properties = value.EnumerateObject().ToArray();
    if (properties.Length != 3 || properties.Any(property => property.Name is not ("id" or "name" or "arguments")))
      throw new InvalidDataException("Windows built-in call has unknown or missing keys.");
    var id = RequiredString(value, "id");
    var name = RequiredString(value, "name");
    if (id.Length is 0 or > 128 || name.Length is 0 or > 64 ||
        !name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
      throw new InvalidDataException("Windows built-in call identity is invalid.");
    if (!value.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Object)
      throw new InvalidDataException("Windows built-in call arguments must be an object.");
    return new WindowsBuiltInToolCall(id, name, arguments.GetRawText());
  }

  private static string RequiredString(JsonElement value, string name)
  {
    if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
      throw new InvalidDataException("Windows built-in call identity must be a string.");
    return property.GetString()!;
  }

  private static void RejectDuplicateKeys(byte[] utf8)
  {
    var reader = new Utf8JsonReader(utf8, new JsonReaderOptions
    {
      CommentHandling = JsonCommentHandling.Disallow,
      MaxDepth = 32,
    });
    var objects = new Stack<HashSet<string>>();
    while (reader.Read())
    {
      switch (reader.TokenType)
      {
        case JsonTokenType.StartObject:
          objects.Push(new HashSet<string>(StringComparer.Ordinal));
          break;
        case JsonTokenType.EndObject:
          if (objects.Count == 0) throw new InvalidDataException("Windows built-in command is not closed JSON.");
          objects.Pop();
          break;
        case JsonTokenType.PropertyName when objects.Count == 0 || !objects.Peek().Add(reader.GetString()!):
          throw new InvalidDataException("Windows built-in command repeats a key.");
      }
    }
    if (objects.Count != 0) throw new InvalidDataException("Windows built-in command is not closed JSON.");
  }
}
