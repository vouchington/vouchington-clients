using System.ComponentModel;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial record ChatConversationRow(
    string Id,
    string Title,
    UiText DisplayTitleText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IUiLocalization Localization,
    bool IsDeleted = false);

public sealed partial record ChatConversationRow
{
  public ChatConversationRow(
      string id,
      string title,
      string displayTitle,
      DateTimeOffset createdAt,
      DateTimeOffset updatedAt,
      bool isDeleted = false)
      : this(id, title, UiText.Verbatim(displayTitle), createdAt, updatedAt, UiLocalization.English, isDeleted)
  {
  }

  public string DisplayTitle => Localization.Resolve(DisplayTitleText);
}

public sealed record ChatMessageRow(
    string Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt,
    bool HasError = false,
    string? Error = null,
    string? CompletionStatus = null,
    UiMessageKey? ErrorMessageKey = null,
    IUiLocalization? Localization = null) : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;

  public bool IsUser => string.Equals(Role, "user", StringComparison.Ordinal);

  public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.Ordinal);

  public bool HasContent => !string.IsNullOrWhiteSpace(Content);

  public bool IsIncomplete => string.Equals(CompletionStatus, "incomplete", StringComparison.Ordinal);

  public string? DisplayError => ErrorMessageKey is UiMessageKey key
      ? Localization?.Localize(key)
      : Error;

  internal void NotifyLocalizationChanged()
  {
    if (ErrorMessageKey is not null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayError)));
    }
  }
}
