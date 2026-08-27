using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Crm;

public sealed record CrmContactRow(
    string Id,
    string Name,
    string Email,
    string? ProtocolVertical,
    UiText VerticalText,
    UiText StatusText,
    int? FollowerCount,
    DateTimeOffset? ContactedAtInstant,
    string? UserId,
    IUiLocalization Localization)
{
  public string LocalizedVertical => Localization.Resolve(VerticalText);

  public string LocalizedStatus => Localization.Resolve(StatusText);

  public string? ContactedAt => ContactedAtInstant is DateTimeOffset instant
      ? Localization.FormatDateTime(instant, TimeZoneInfo.Local)
      : null;

  public static CrmContactRow FromContact(
      CrmContact contact,
      IUiLocalization? localization = null)
  {
    var status = UiText.Localized(contact switch
    {
      { OptedOutAt: not null } => UiMessageKey.NativeDotnetCrmCrmOptedOut,
      { ArchivedAt: not null } => UiMessageKey.NativeDotnetCrmCrmArchived,
      { ConvertedAt: not null } => UiMessageKey.NativeDotnetCrmCrmConverted,
      { RespondedAt: not null } => UiMessageKey.NativeDotnetCrmCrmInConversation,
      { ContactedAt: not null } => UiMessageKey.NativeDotnetCrmCrmAwaitingResponse,
      _ => UiMessageKey.NativeDotnetCrmCrmNew,
    });

    return new(
        contact.Id,
        contact.Name,
        contact.Email,
        contact.Vertical.ToCrmFieldValue(),
        UiTaxonomy.CrmVertical(contact.Vertical.ToCrmFieldValue()),
        status,
        contact.FollowerCount,
        contact.ContactedAt,
        contact.UserId,
        localization ?? UiLocalization.English);
  }

  public CrmContactRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}

public sealed record CrmEmailRow(
    string Id,
    UiText SubjectText,
    string ToEmail,
    string? BodyText,
    string? BodyHtml,
    UiText DirectionText,
    DateTimeOffset? SentAtInstant,
    IUiLocalization Localization)
{
  public string Subject => Localization.Resolve(SubjectText);

  public string LocalizedDirection => Localization.Resolve(DirectionText);

  public string? SentAt => SentAtInstant is DateTimeOffset instant
      ? Localization.FormatDateTime(instant, TimeZoneInfo.Local)
      : null;

  public static CrmEmailRow FromMessage(
      CrmMessage message,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(message);
    return new(
        message.Id,
        message.Subject is { Length: > 0 } subject
            ? UiText.Verbatim(subject)
            : UiText.Localized(UiMessageKey.NativeDotnetCrmCrmNoSubject),
        message.ToEmail,
        message.BodyText,
        message.BodyHtml,
        UiText.Localized(message.Direction switch
        {
          CrmMessageDirection.Inbound => UiMessageKey.NativeDotnetCrmCrmInbound,
          CrmMessageDirection.Outbound => UiMessageKey.NativeDotnetCrmCrmOutbound,
          _ => UiMessageKey.NativeDotnetCrmCrmUnknownDirection,
        }),
        message.SentAt,
        localization ?? UiLocalization.English);
  }

  public CrmEmailRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}

public sealed record CrmNoteRow(
    string Id,
    string Body,
    string CreatedById,
    DateTimeOffset CreatedAtInstant,
    IUiLocalization Localization)
{
  public string CreatedAt => Localization.FormatDateTime(CreatedAtInstant, TimeZoneInfo.Local);

  public static CrmNoteRow FromNote(CrmNote note, IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(note);
    return new(
        note.Id,
        note.Body,
        note.CreatedById,
        note.CreatedAt,
        localization ?? UiLocalization.English);
  }

  public CrmNoteRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}
