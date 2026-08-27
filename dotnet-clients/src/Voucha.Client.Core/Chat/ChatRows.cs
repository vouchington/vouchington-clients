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

public sealed partial record SupportThreadRow(
    string Id,
    string Subject,
    UiText DisplaySubjectText,
    string ProtocolStatus,
    UiText StatusText,
    string? ConversationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IUiLocalization Localization);

public sealed partial record SupportThreadRow
{
  public SupportThreadRow(
      string id,
      string subject,
      string displaySubject,
      string status,
      string? conversationId,
      DateTimeOffset createdAt,
      DateTimeOffset updatedAt)
      : this(
          id,
          subject,
          UiText.Verbatim(displaySubject),
          status,
          UiTaxonomy.SupportStatus(status),
          conversationId,
          createdAt,
          updatedAt,
          UiLocalization.English)
  {
  }

  public string DisplaySubject => Localization.Resolve(DisplaySubjectText);

  public string LocalizedStatus => Localization.Resolve(StatusText);
}

public sealed record SupportMessageRow(
    string Id,
    string ProtocolDirection,
    UiText DirectionText,
    string UserContent,
    DateTimeOffset CreatedAt,
    IUiLocalization Localization)
{
  public string LocalizedDirection => Localization.Resolve(DirectionText);

  public SupportMessageRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}
