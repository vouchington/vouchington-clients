using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Chat;

public enum WindowsBuiltInTurnKind { Declined, Stopped, Answer, Retained }

public sealed record WindowsBuiltInToolOutput(
    string Envelope, McpFeedPostReceipt? Receipt, McpFeedRssItemReceipt? RssItemReceipt = null);

public sealed record WindowsBuiltInTurn(
    WindowsBuiltInTurnKind Kind,
    string? Answer,
    string? Envelope,
    McpFeedPostReceipt? Receipt,
    string? Status,
    McpFeedRssItemReceipt? RssItemReceipt = null)
{
  public WindowsBuiltInTurn ForPersistenceRetry() =>
      Kind is WindowsBuiltInTurnKind.Answer or WindowsBuiltInTurnKind.Retained
          ? this with { }
          : throw new InvalidOperationException("Only a completed Windows built-in turn can be persisted again.");
}

public sealed class WindowsBuiltInResultRetainedException : InvalidOperationException
{
  private const string DefaultMessage = "The validated Windows tool result was kept. The tool was not called again.";

  public WindowsBuiltInResultRetainedException() : base(DefaultMessage) => Envelope = "";

  public WindowsBuiltInResultRetainedException(string message) : base(message) => Envelope = "";

  public WindowsBuiltInResultRetainedException(string message, Exception innerException)
      : base(message, innerException) => Envelope = "";

  public WindowsBuiltInResultRetainedException(
      string envelope, McpFeedPostReceipt? receipt, McpFeedRssItemReceipt? rssItemReceipt = null) : base(DefaultMessage)
  {
    Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    Receipt = receipt;
    RssItemReceipt = rssItemReceipt;
  }

  public string Envelope { get; }
  public McpFeedPostReceipt? Receipt { get; }
  public McpFeedRssItemReceipt? RssItemReceipt { get; }
}

public static class WindowsBuiltInToolLoop
{
  public static async Task<WindowsBuiltInTurn> RunAsync(
      string prompt,
      Func<string, CancellationToken, Task<WindowsBuiltInPromptObservation>> observe,
      Func<string, CancellationToken, Task<WindowsBuiltInModelTurn>> generate,
      Func<WindowsBuiltInToolCall, Action, CancellationToken, Task<WindowsBuiltInToolOutput>> execute,
      Func<WindowsBuiltInToolCall, CancellationToken, Task> validate,
      Func<bool> isCurrent,
      ISet<string>? usedCallIds,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(prompt);
    ArgumentNullException.ThrowIfNull(observe);
    ArgumentNullException.ThrowIfNull(generate);
    ArgumentNullException.ThrowIfNull(execute);
    ArgumentNullException.ThrowIfNull(validate);
    ArgumentNullException.ThrowIfNull(isCurrent);
    var seen = usedCallIds ?? new HashSet<string>(StringComparer.Ordinal);
    RequireCurrent(isCurrent);
    if (!WindowsBuiltInPrompt.Fits(prompt, await observe(prompt, cancellationToken).ConfigureAwait(false)))
      return new(WindowsBuiltInTurnKind.Declined, null, null, null, null);
    var first = await generate(prompt, cancellationToken).ConfigureAwait(false);
    RequireCurrent(isCurrent);
    if (!string.Equals(first.Status, "Complete", StringComparison.Ordinal))
      return new(WindowsBuiltInTurnKind.Stopped, null, null, null, first.Status);
    var command = WindowsBuiltInCommandParser.Parse(first.Text ?? "");
    if (command.ToolCall is not { } call)
      return new(WindowsBuiltInTurnKind.Answer, command.Answer, null, null, "Complete");
    await validate(call, cancellationToken).ConfigureAwait(false);
    RequireCurrent(isCurrent);
    cancellationToken.ThrowIfCancellationRequested();
    if (!seen.Add(call.Id)) throw new InvalidDataException("Windows built-in call id was already used.");
    WindowsBuiltInToolOutput output;
    var admitted = false;
    try { output = await execute(call, () => admitted = true, cancellationToken).ConfigureAwait(false); }
    catch (Exception error) when (admitted && error is not PossibleMcpToolSideEffectException)
    {
      throw new PossibleMcpToolSideEffectException(error);
    }
    if (!isCurrent()) throw new PossibleMcpToolSideEffectException();
    var continuation = string.Concat(prompt, "\nresult:\n", output.Envelope,
        "\nReply with one answer object and do not call a tool.");
    WindowsBuiltInModelTurn second;
    try
    {
      if (!WindowsBuiltInPrompt.Fits(continuation, await observe(continuation, cancellationToken).ConfigureAwait(false)))
        return new(WindowsBuiltInTurnKind.Retained, null, output.Envelope, output.Receipt, "Complete", output.RssItemReceipt);
      second = await generate(continuation, cancellationToken).ConfigureAwait(false);
      if (!isCurrent()) throw new PossibleMcpToolSideEffectException();
    }
    catch (Exception error) when (error is not PossibleMcpToolSideEffectException)
    {
      throw new PossibleMcpToolSideEffectException(error);
    }
    if (!string.Equals(second.Status, "Complete", StringComparison.Ordinal))
      return new(WindowsBuiltInTurnKind.Retained, null, output.Envelope, output.Receipt, second.Status, output.RssItemReceipt);
    WindowsBuiltInParsedCommand continued;
    try { continued = WindowsBuiltInCommandParser.Parse(second.Text ?? ""); }
    catch (Exception error) when (error is not PossibleMcpToolSideEffectException)
    {
      throw new PossibleMcpToolSideEffectException(error);
    }
    if (continued.ToolCall is not null) throw new PossibleMcpToolSideEffectException();
    return new(WindowsBuiltInTurnKind.Answer, continued.Answer, output.Envelope, output.Receipt, "Complete",
        output.RssItemReceipt);
  }

  private static void RequireCurrent(Func<bool> isCurrent)
  {
    if (!isCurrent()) throw new OperationCanceledException("MCP account or catalog changed.");
  }
}
