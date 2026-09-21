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
    string? Error = null)
{
  public bool IsUser => string.Equals(Role, "user", StringComparison.Ordinal);

  public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.Ordinal);
}
