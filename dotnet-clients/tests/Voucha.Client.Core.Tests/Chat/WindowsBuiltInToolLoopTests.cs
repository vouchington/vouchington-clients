using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class WindowsBuiltInToolLoopTests
{
  [Theory]
  [InlineData(4, false, null, true)]
  [InlineData(3, false, null, false)]
  [InlineData(4, true, null, false)]
  [InlineData(4, false, 4, true)]
  [InlineData(4, false, 3, false)]
  public void PromptFitUsesThePinnedSignalsWithoutAnAnswerTokenBudget(
      ulong usableIndex, bool largerThanContext, int? cutoff, bool fits)
  {
    var observation = new WindowsBuiltInPromptObservation(usableIndex, largerThanContext, cutoff);

    Assert.Equal(fits, WindowsBuiltInPrompt.Fits("abcd", observation));
  }

  [Fact]
  public void CommandParserRejectsProseFencesExtraKeysAndDuplicateKeys()
  {
    Assert.Throws<InvalidDataException>(() => WindowsBuiltInCommandParser.Parse("please {\"answer\":\"hi\"}"));
    Assert.Throws<InvalidDataException>(() => WindowsBuiltInCommandParser.Parse("```{\"answer\":\"hi\"}```"));
    Assert.Throws<InvalidDataException>(() => WindowsBuiltInCommandParser.Parse("{\"answer\":\"hi\",\"call\":{}}"));
    Assert.Throws<InvalidDataException>(() => WindowsBuiltInCommandParser.Parse("{\"note\":\"hi\"}"));
    Assert.Throws<InvalidDataException>(() => WindowsBuiltInCommandParser.Parse("{\"answer\":\"one\",\"answer\":\"two\"}"));
    Assert.Throws<InvalidDataException>(() => WindowsBuiltInCommandParser.Parse(
        "{\"call\":{\"id\":\"a\",\"name\":\"read_post\",\"arguments\":{\"id\":\"1\",\"id\":\"2\"}}}"));
    var answer = WindowsBuiltInCommandParser.Parse("{\"answer\":\"kept\"}");
    Assert.Null(answer.ToolCall);
    Assert.Equal("kept", answer.Answer);
  }

  [Fact]
  public void ComposerKeepsTheFullMessageAndOriginalSchema()
  {
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"read_post","description":"Read","inputSchema":{"type":"object","required":["id"]}}]}
        """);
    var bridge = new MemberMcpToolBridge(catalog.RootElement);
    var message = new string('m', 13_000);

    var prompt = WindowsBuiltInPrompt.Compose([new("assistant", "earlier")], message, bridge.Tools);

    Assert.Contains(message, prompt, StringComparison.Ordinal);
    Assert.Contains("assistant: earlier", prompt, StringComparison.Ordinal);
    Assert.Contains("{\"type\":\"object\",\"required\":[\"id\"]}", prompt, StringComparison.Ordinal);
    Assert.True(prompt.Length > message.Length);
  }

  [Fact]
  public async Task CompleteAnswerDoesNotDispatch()
  {
    var harness = new Harness { First = """{"answer":"kept"}""" };

    var turn = await harness.Run();

    Assert.Equal(WindowsBuiltInTurnKind.Answer, turn.Kind);
    Assert.Equal("kept", turn.Answer);
    Assert.Equal(1, harness.Generations);
    Assert.Equal(0, harness.Executions);
    Assert.Equal(1, harness.Generations);
    Assert.Equal("kept", turn.ForPersistenceRetry().Answer);
  }

  [Fact]
  public async Task NonCompleteTextIsNotExecuted()
  {
    var harness = new Harness
    {
      Status = "ResponseBlockedByContentModeration",
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
    };

    var turn = await harness.Run();

    Assert.Equal(WindowsBuiltInTurnKind.Stopped, turn.Kind);
    Assert.Equal("ResponseBlockedByContentModeration", turn.Status);
    Assert.Equal(0, harness.Executions);
    Assert.Null(turn.Answer);
  }

  [Fact]
  public async Task PromptThatDoesNotFitIsNotGenerated()
  {
    var harness = new Harness { ObserveFit = _ => new(0, false, null) };

    var turn = await harness.Run();

    Assert.Equal(WindowsBuiltInTurnKind.Declined, turn.Kind);
    Assert.Equal(0, harness.Generations);
    Assert.Equal(0, harness.Executions);
  }

  [Fact]
  public async Task InvalidCommandAndReusedIdDoNotDispatch()
  {
    var prose = new Harness { First = "not json" };
    await Assert.ThrowsAsync<InvalidDataException>(() => prose.Run());
    Assert.Equal(0, prose.Executions);

    var reused = new Harness
    {
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
      Used = new HashSet<string>(StringComparer.Ordinal) { "call-1" },
    };
    await Assert.ThrowsAsync<InvalidDataException>(() => reused.Run());
    Assert.Equal(0, reused.Executions);
  }

  [Fact]
  public async Task OneCallRetainsTheEnvelopeWhenContinuationDoesNotFit()
  {
    var harness = new Harness
    {
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
      Envelope = """{"structuredContent":{"id":"post"},"content":[],"isError":false}""",
      ObserveFit = text => text.Contains("result:", StringComparison.Ordinal)
          ? new(0, false, null)
          : new((ulong)text.Length, false, null),
    };

    var turn = await harness.Run();

    Assert.Equal(WindowsBuiltInTurnKind.Retained, turn.Kind);
    Assert.Equal(harness.Envelope, turn.Envelope);
    Assert.Equal(1, harness.Generations);
    Assert.Equal(1, harness.Executions);
    Assert.Equal(harness.Envelope, turn.ForPersistenceRetry().Envelope);
  }

  [Fact]
  public async Task ContinuationAnswerIsReturnedAndASecondCallIsNotDispatched()
  {
    var answered = new Harness
    {
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
      Second = """{"answer":"done"}""",
      Receipt = new("account-a", "call-1", ["11111111-1111-1111-1111-111111111111"]),
    };

    var turn = await answered.Run();

    Assert.Equal("done", turn.Answer);
    Assert.Equal(answered.Receipt, turn.Receipt);
    Assert.Equal(2, answered.Generations);
    Assert.Equal(1, answered.Executions);

    var secondCall = new Harness
    {
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
      Second = """{"call":{"id":"call-2","name":"read_post","arguments":{"id":"other"}}}""",
    };
    await Assert.ThrowsAsync<PossibleMcpToolSideEffectException>(() => secondCall.Run());
    Assert.Equal(1, secondCall.Executions);
  }

  [Fact]
  public async Task AccountChangeBeforeDispatchCancelsAndAfterDispatchDoesNotResend()
  {
    var before = new Harness
    {
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
      CurrentUntilGeneration = 1,
    };
    await Assert.ThrowsAsync<OperationCanceledException>(() => before.Run());
    Assert.Equal(0, before.Executions);

    var after = new Harness
    {
      First = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
      CurrentUntilExecution = 1,
    };
    await Assert.ThrowsAsync<PossibleMcpToolSideEffectException>(() => after.Run());
    Assert.Equal(1, after.Executions);
  }

  private sealed class Harness
  {
    public int Generations { get; private set; }
    public int Executions { get; private set; }
    public string First { get; init; } = """{"answer":""}""";
    public string? Second { get; init; }
    public string Status { get; init; } = "Complete";
    public string Envelope { get; init; } = """{"ok":true}""";
    public McpFeedPostReceipt? Receipt { get; init; }
    public ISet<string>? Used { get; init; }
    public Func<string, WindowsBuiltInPromptObservation>? ObserveFit { get; init; }
    public int CurrentUntilGeneration { get; init; } = int.MaxValue;
    public int CurrentUntilExecution { get; init; } = int.MaxValue;

    public Task<WindowsBuiltInTurn> Run() => WindowsBuiltInToolLoop.RunAsync(
        "user: hello",
        (text, _) => Task.FromResult(ObserveFit?.Invoke(text) ?? new WindowsBuiltInPromptObservation((ulong)text.Length, false, null)),
        (_, _) =>
        {
          Generations++;
          return Task.FromResult(new WindowsBuiltInModelTurn(Status, Generations == 1 ? First : Second));
        },
        (_, _) =>
        {
          Executions++;
          return Task.FromResult(new WindowsBuiltInToolOutput(Envelope, Receipt));
        },
        (_, _) => Task.CompletedTask,
        () => Generations < CurrentUntilGeneration && Executions < CurrentUntilExecution,
        Used,
        TestContext.Current.CancellationToken);
  }
}
