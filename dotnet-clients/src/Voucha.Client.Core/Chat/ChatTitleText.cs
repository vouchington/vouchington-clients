namespace Voucha.Client.Core.Chat;

/// <summary>Bounds a model-written conversation title before it is saved through PATCH.</summary>
internal static class ChatTitleText
{
  internal const int MaximumLength = 120;

  internal static string? Sanitize(string? generated)
  {
    if (generated is null) return null;
    var firstLine = generated
        .Split(['\r', '\n', '\u2028', '\u2029'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Replace("\"", string.Empty, StringComparison.Ordinal).Trim())
        .FirstOrDefault(line => line.Length > 0);
    if (firstLine is null) return null;
    if (firstLine.Length <= MaximumLength) return firstLine;
    var end = char.IsHighSurrogate(firstLine[MaximumLength - 1]) ? MaximumLength - 1 : MaximumLength;
    return firstLine[..end].TrimEnd();
  }
}
