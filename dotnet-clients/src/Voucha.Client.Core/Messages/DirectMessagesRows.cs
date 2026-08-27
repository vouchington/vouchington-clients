using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Messages;

public sealed record DirectConversationRow(
    string Id,
    string Title,
    DateTimeOffset UpdatedAt);

public sealed record DirectMessageRow(
    string Id,
    string BodyText,
    UiText SenderText,
    DateTimeOffset CreatedAt,
    bool IsOptimistic,
    IUiLocalization Localization)
{
  public string SenderLabel => Localization.Resolve(SenderText);
}

public sealed record DirectMessageParticipantRow(
    string Id,
    string? UserId,
    UiText LabelText,
    string RoleSource,
    UiText RoleText,
    bool CanRemove,
    UiText RemoveActionText,
    IUiLocalization Localization)
{
  public string Label => Localization.Resolve(LabelText);

  public string Role => Localization.Resolve(RoleText);

  public string RemoveActionLabel => Localization.Resolve(RemoveActionText);
}

public sealed record DirectMessageUserRow
{
  private readonly IUiLocalization localization;

  public DirectMessageUserRow(
      string id,
      string? username,
      IUiLocalization? localization = null)
  {
    Id = id;
    UsernameSource = username;
    this.localization = localization ?? UiLocalization.English;
  }

  public string Id { get; }

  public string? UsernameSource { get; }

  public string Username => UsernameSource
      ?? localization.Localize(UiMessageKey.NativeDotnetDirectMessagesUnknownRecipient);
}
