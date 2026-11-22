using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatTitleGenerationTests
{
  private static readonly ChatProviderStatus Ready =
      new(ChatProviderKind.Local, "Local", true, "Ready.", "openai_compatible", "model-1");

  [Fact]
  public async Task GeneratesFromTheTwoLatestMessagesAndSavesThroughPatch()
  {
    var (viewModel, service, provider) = await LoadAsync(
        "\"Planning a trip\"\nextra line", ("user", "one"), ("assistant", "two"), ("user", "three"));

    Assert.True(viewModel.CanGenerateTitle);
    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    var prompt = Assert.Single(provider.Calls);
    Assert.Contains("six words or fewer", prompt.Message, StringComparison.Ordinal);
    Assert.Equal(["two", "three"], prompt.History.Select(item => item.Content));
    Assert.Equal([("conversation-1", "Planning a trip")], service.UpdatedTitles);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.CanGenerateTitle);
  }

  [Fact]
  public async Task NoMessagesSavesTheLocalizedFallbackWithoutCallingTheProvider()
  {
    var (viewModel, service, provider) = await LoadAsync("unused");

    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    Assert.Empty(provider.Calls);
    Assert.Equal([("conversation-1", "New chat")], service.UpdatedTitles);
  }

  [Theory]
  [InlineData("")]
  [InlineData("  \n \"\" ")]
  public async Task EmptyProviderOutputSavesNothingAndKeepsThePreviousTitle(string output)
  {
    var (viewModel, service, _) = await LoadAsync(output, ("user", "hello"));

    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    Assert.Empty(service.UpdatedTitles);
    Assert.Equal(string.Empty, viewModel.Title);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
  }

  [Theory]
  [InlineData(typeof(InvalidOperationException))]
  [InlineData(typeof(TaskCanceledException))]
  public async Task ProviderFailureSavesNothingAndRestoresThePreviousTitle(Type failure)
  {
    var (viewModel, service, provider) = await LoadAsync("unused", ("user", "hello"));
    provider.Failure = (Exception)Activator.CreateInstance(failure)!;

    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    Assert.Empty(service.UpdatedTitles);
    Assert.Equal(string.Empty, viewModel.Title);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task PatchFailureRestoresThePreviousTitle()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    service.UpdateConversationTitleError = new HttpRequestException("offline");

    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, viewModel.Title);
    Assert.Equal("offline", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task CallerCancellationPropagates()
  {
    var (viewModel, service, provider) = await LoadAsync("unused", ("user", "hello"));
    provider.Failure = new OperationCanceledException();
    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();

    await Assert.ThrowsAsync<OperationCanceledException>(() => viewModel.GenerateTitleAsync(cancelled.Token));

    Assert.Empty(service.UpdatedTitles);
  }

  [Fact]
  public async Task UserRenameDuringGenerationWins()
  {
    var (viewModel, service, provider) = await LoadAsync("Generated", ("user", "hello"));
    var generating = new TaskCompletionSource();
    provider.Gate = generating.Task;

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    await viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    generating.SetResult();
    await pending;

    Assert.Equal([("conversation-1", "Mine")], service.UpdatedTitles);
    Assert.Equal("Updated", viewModel.Title);
  }

  [Fact]
  public async Task UserRenameDuringPatchIsRepairedAfterTheGeneratedTitleLands()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    service.UpdateConversationTitleResult = service.UpdateConversationTitleResult with
    {
      Conversation = service.UpdateConversationTitleResult.Conversation with { Title = "Mine" },
    };
    service.UpdateConversationTitleAsyncOverride = (_, title) =>
        title == "Generated" ? patch.Task : Task.FromResult(service.UpdateConversationTitleResult);

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    await viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    patch.SetResult(service.UpdateConversationTitleResult);
    await pending;

    Assert.Equal(["Generated", "Mine", "Mine"], service.UpdatedTitles.Select(item => item.Title));
  }

  [Fact]
  public async Task RaceRepairFailureIsSwallowed()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    service.UpdateConversationTitleAsyncOverride = (_, title) => title switch
    {
      "Generated" => patch.Task,
      "Mine" when service.UpdatedTitles.Count > 2 => Task.FromException<ChatConversationResponse>(new HttpRequestException("x")),
      _ => Task.FromResult(service.UpdateConversationTitleResult),
    };

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    await viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    patch.SetResult(service.UpdateConversationTitleResult);
    await pending;

    Assert.Equal(3, service.UpdatedTitles.Count);
  }

  [Fact]
  public async Task DeleteDuringPatchSkipsTheRepair()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    service.UpdateConversationTitleAsyncOverride = (_, _) => patch.Task;

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    await viewModel.DeleteAsync(TestContext.Current.CancellationToken);
    patch.SetResult(service.UpdateConversationTitleResult);
    await pending;

    Assert.Single(service.UpdatedTitles);
  }

  [Fact]
  public async Task OnlyUntitledConversationsWithAReadyProviderCanGenerate()
  {
    var (titled, titledService, _) = await LoadAsync("Generated", title: "Existing");
    await titled.GenerateTitleAsync(TestContext.Current.CancellationToken);
    Assert.False(titled.CanGenerateTitle);
    Assert.Empty(titledService.UpdatedTitles);

    var service = new FakeChatService();
    var unavailable = new ChatConversationViewModel(service);
    await unavailable.LoadAsync("conversation-1", null, TestContext.Current.CancellationToken);
    Assert.False(unavailable.CanGenerateTitle);
    await unavailable.GenerateTitleAsync(TestContext.Current.CancellationToken);
    Assert.Empty(service.UpdatedTitles);
  }

  [Fact]
  public async Task ProviderThatCannotReportStatusDisablesGeneration()
  {
    var service = new FakeChatService();
    var viewModel = new ChatConversationViewModel(
        service, new Resolver(Ready, new ThrowingStatusProvider()), new ThrowingStatusProvider());
    await viewModel.LoadAsync("conversation-1", null, TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanGenerateTitle);
  }

  [Fact]
  public async Task ConversationNeverCallsTheRetiredServerTitleRoute()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));

    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    Assert.Null(service.LastChatPath);
    Assert.DoesNotContain(typeof(IChatService).GetMethods(), method => method.Name.Contains("GenerateConversationTitle", StringComparison.Ordinal));
    Assert.DoesNotContain(typeof(VouchaApiEndpoints).GetMethods(), method => method.Name.Contains("GenerateChatConversationTitle", StringComparison.Ordinal));
  }

  [Theory]
  [InlineData(null, null)]
  [InlineData("  ", null)]
  [InlineData("\"A\"\n\"\"\nB", "A")]
  [InlineData("\r\n\r\n  Trip plan  \r\nmore", "Trip plan")]
  public void SanitizeTakesOneTrimmedLine(string? input, string? expected) =>
      Assert.Equal(expected, ChatTitleText.Sanitize(input));

  [Fact]
  public void SanitizeCapsLengthWithoutSplittingASurrogatePair()
  {
    Assert.Equal(new string('a', ChatTitleText.MaximumLength), ChatTitleText.Sanitize(new string('a', 500)));
    var emoji = new string('a', ChatTitleText.MaximumLength - 1) + "\U0001F600";
    Assert.Equal(new string('a', ChatTitleText.MaximumLength - 1), ChatTitleText.Sanitize(emoji));
  }

  private static async Task<(ChatConversationViewModel ViewModel, FakeChatService Service, FakeTitleProvider Provider)>
      LoadAsync(string output, params (string Role, string Text)[] turns) =>
      await LoadAsync(output, null, turns);

  private static async Task<(ChatConversationViewModel ViewModel, FakeChatService Service, FakeTitleProvider Provider)>
      LoadAsync(string output, string? title, params (string Role, string Text)[] turns)
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse(
          [.. turns.Select((turn, index) => new ChatMessage(
              $"message-{index}", "conversation-1", DateTimeOffset.UnixEpoch.AddMinutes(index), null,
              DateTimeOffset.UnixEpoch, null, null, null, new ChatMessageContent(turn.Role, turn.Text, null)))],
          new PageInfo(null, false, null)),
    };
    var provider = new FakeTitleProvider(output);
    var viewModel = new ChatConversationViewModel(service, new Resolver(Ready, provider), provider);
    await viewModel.LoadAsync("conversation-1", title, TestContext.Current.CancellationToken);
    return (viewModel, service, provider);
  }

  private sealed class Resolver(ChatProviderStatus status, ILocalChatProvider provider)
      : IChatProviderResolver, ILocalChatProviderResolver
  {
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [status];

    public ChatProviderStatus GetDefaultProviderStatus() => status;

    public ILocalChatProvider? GetLocalProvider(string providerId) => provider;
  }

  private sealed class FakeTitleProvider(string output) : ILocalChatProvider
  {
    public List<(string Message, IReadOnlyList<LocalLLMResponseInput> History)> Calls { get; } = [];

    public Exception? Failure { get; set; }

    public Task? Gate { get; set; }

    public string Id => "openai_compatible";

    public ChatProviderStatus Status => Ready;

    public async Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default)
    {
      Calls.Add((message, history));
      if (Gate is not null) await Gate;
      if (Failure is not null) throw Failure;
      return new LocalChatGenerationResult(output, "openai_compatible", "model-1");
    }
  }

  private sealed class ThrowingStatusProvider : ILocalChatProvider
  {
    public string Id => "throwing";

    public ChatProviderStatus Status => throw new InvalidOperationException("status");

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unused");
  }
}
