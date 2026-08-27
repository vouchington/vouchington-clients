namespace Voucha.Client.Core.Chat;

public interface IChatProviderResolver
{
  IReadOnlyList<ChatProviderStatus> GetProviderStatuses();

  ChatProviderStatus GetDefaultProviderStatus();
}

public interface ILocalChatProviderResolver
{
  ILocalChatProvider? GetLocalProvider(string providerId);
}

public interface IChatProviderSelectionStore
{
  void SaveSelectedProvider(string? providerId);
}
