using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private const string TitlePrompt =
      "Suggest a concise conversation title of six words or fewer.\nReturn only the title.";

  /// <summary>Titles are only generated for an untitled conversation, when a local provider is ready.</summary>
  public bool CanGenerateTitle =>
      CanRename && !IsLoading && string.IsNullOrWhiteSpace(Title) && ReadyTitleProvider() is not null;

  private readonly SemaphoreSlim titleWriteGate = new(1, 1);

  /// <summary>
  /// Generates a title on this device with the provider the conversation replies with, then saves it
  /// through the conversation PATCH. A user edit made while generating always wins.
  /// </summary>
  public async Task GenerateTitleAsync(CancellationToken cancellationToken = default)
  {
    if (ConversationId is not { } id || !CanGenerateTitle || ReadyTitleProvider() is not { } provider) return;

    var previous = Title;
    var currentRequest = BeginMutation();
    try
    {
      var generated = await GenerateTitleTextAsync(provider, cancellationToken).ConfigureAwait(true);
      if (currentRequest != Volatile.Read(ref requestId)) return;
      if (generated is null)
      {
        FailTitleGeneration(previous, localization.Localize(
            UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse));
        return;
      }

      Title = generated;
      var updated = await SaveGeneratedTitleAsync(id, generated, currentRequest, cancellationToken)
          .ConfigureAwait(true);
      if (updated is null) return;

      Title = updated.Conversation.Title;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        Title = previous;
        State = LoadState.Loaded;
      }
      throw;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
    {
      if (currentRequest == Volatile.Read(ref requestId)) FailTitleGeneration(previous, ex.Message);
    }
  }

  private void FailTitleGeneration(string previous, string message)
  {
    Title = previous;
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private async Task<string?> GenerateTitleTextAsync(
      ILocalChatProvider provider, CancellationToken cancellationToken)
  {
    var recent = LocalLLMHistoryBudget.Apply(messages).TakeLast(2).ToArray();
    if (recent.Length == 0) return localization.Localize(UiMessageKey.NativeDotnetDynamicNewChat);

    var result = await provider.GenerateAssistantContentAsync(TitlePrompt, recent, cancellationToken)
        .ConfigureAwait(true);
    return ChatTitleText.Sanitize(result.AssistantContent);
  }

  /// <summary>Every title write for the conversation, rename or generated, goes through one gate.</summary>
  private async Task<ChatConversationResponse> WriteTitleAsync(
      string id, string title, CancellationToken cancellationToken)
  {
    await titleWriteGate.WaitAsync(cancellationToken).ConfigureAwait(true);
    try
    {
      return await chatService.UpdateConversationTitleAsync(id, title, cancellationToken).ConfigureAwait(true);
    }
    finally
    {
      _ = titleWriteGate.Release();
    }
  }

  private async Task<ChatConversationResponse?> SaveGeneratedTitleAsync(
      string id, string generated, int currentRequest, CancellationToken cancellationToken)
  {
    await titleWriteGate.WaitAsync(cancellationToken).ConfigureAwait(true);
    try
    {
      var updated = await chatService.UpdateConversationTitleAsync(id, generated, cancellationToken)
          .ConfigureAwait(true);
      if (currentRequest == Volatile.Read(ref requestId)) return updated;
      await RepairTitleRaceAsync(id, generated, cancellationToken).ConfigureAwait(true);
      return null;
    }
    finally
    {
      _ = titleWriteGate.Release();
    }
  }

  /// <summary>Re-sends the user's title until the saved title equals the current one; never retries an error.</summary>
  private async Task RepairTitleRaceAsync(string id, string saved, CancellationToken cancellationToken)
  {
    var owner = Volatile.Read(ref requestId);
    while (ConversationId == id && !IsDeleted)
    {
      var current = Title;
      if (string.IsNullOrWhiteSpace(current) || current == saved) return;
      try
      {
        _ = await chatService.UpdateConversationTitleAsync(id, current, cancellationToken).ConfigureAwait(true);
      }
      catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        if (ConversationId == id && owner == Volatile.Read(ref requestId))
        {
          ErrorMessage = ex.Message;
          State = LoadState.Error;
        }
        return;
      }
      saved = current;
    }
  }

  private ILocalChatProvider? ReadyTitleProvider()
  {
    try
    {
      var provider = providerResolver is ILocalChatProviderResolver resolver
          ? resolver.GetLocalProvider(SelectedProviderStatus.ModelProvider ?? string.Empty)
          : localChatProvider;
      return provider?.Status.IsAvailable == true ? provider : null;
    }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
    {
      return null;
    }
  }
}
