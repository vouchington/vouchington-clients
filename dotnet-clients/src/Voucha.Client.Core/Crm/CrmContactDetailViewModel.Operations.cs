using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactDetailViewModel
{
  public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 }) return false;
    if (IsSaving) return false;
    if (string.IsNullOrWhiteSpace(Name))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpNameRequired);
      return false;
    }
    if (string.IsNullOrWhiteSpace(Email))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpEmailRequired);
      return false;
    }
    IsSaving = true;
    ErrorMessage = null;
    try
    {
      var response = await service.UpdateContactAsync(
          ContactId,
          new UpdateCrmContactBody(
              string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
              string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
              NullableString(Phone),
              NullableVertical(ParseVertical(Vertical)),
              ParseContactType(ContactType),
              NullableInt(ParseInt(FollowerCount)),
              NullableString(NotesText)),
          cancellationToken).ConfigureAwait(true);
      ApplyContact(response.Contact);
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsSaving = false;
    }
  }

  public async Task<bool> ArchiveAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || IsArchiving) return false;
    IsArchiving = true;
    ErrorMessage = null;
    try
    {
      await service.ArchiveContactAsync(ContactId, cancellationToken).ConfigureAwait(true);
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsArchiving = false;
    }
  }

  public async Task<bool> LinkUserAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || string.IsNullOrWhiteSpace(LinkUserId) || IsLinking) return false;
    IsLinking = true;
    ErrorMessage = null;
    try
    {
      var response = await service.LinkUserAsync(ContactId, new LinkCrmContactToUserBody(LinkUserId.Trim()), cancellationToken).ConfigureAwait(true);
      ApplyContact(response.Contact);
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsLinking = false;
    }
  }

  public async Task<bool> UnlinkUserAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || IsLinking) return false;
    IsLinking = true;
    ErrorMessage = null;
    try
    {
      var response = await service.UnlinkUserAsync(ContactId, cancellationToken).ConfigureAwait(true);
      ApplyContact(response.Contact);
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsLinking = false;
    }
  }

  public async Task<bool> SendEmailAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || string.IsNullOrWhiteSpace(EmailSubject) || IsSendingEmail) return false;
    if (string.IsNullOrWhiteSpace(EmailBodyHtml) && string.IsNullOrWhiteSpace(EmailBodyText))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpBodyRequired);
      return false;
    }
    IsSendingEmail = true;
    ErrorMessage = null;
    try
    {
      var response = await service.SendEmailAsync(
          ContactId,
          new SendCrmEmailBody(
              EmailSubject.Trim(),
              string.IsNullOrWhiteSpace(EmailBodyHtml) ? null : EmailBodyHtml,
              string.IsNullOrWhiteSpace(EmailBodyText) ? null : EmailBodyText,
              EmailProvider,
              null,
              string.IsNullOrWhiteSpace(DraftPrompt) ? null : DraftPrompt,
              DraftGeneratedAt),
          cancellationToken).ConfigureAwait(true);
      Emails = [CrmEmailRow.FromMessage(response.Message, localization), .. Emails];
      SynchronizeEmailPage();
      DraftGeneratedAt = null;
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsSendingEmail = false;
    }
  }

  public async Task<bool> GenerateDraftAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || IsDrafting) return false;
    IsDrafting = true;
    ErrorMessage = null;
    try
    {
      var response = await service.GenerateEmailDraftAsync(
          ContactId,
          new GenerateCrmEmailDraftBody(string.IsNullOrWhiteSpace(DraftPrompt) ? null : DraftPrompt, string.IsNullOrWhiteSpace(DraftTone) ? null : DraftTone),
          cancellationToken).ConfigureAwait(true);
      EmailSubject = response.Draft.Subject;
      EmailBodyHtml = response.Draft.BodyHtml;
      EmailBodyText = response.Draft.BodyText;
      DraftGeneratedAt = DateTimeOffset.UtcNow;
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsDrafting = false;
    }
  }

  private static JsonNullableString NullableString(string? value) =>
      string.IsNullOrWhiteSpace(value) ? JsonNullableString.Null : JsonNullableString.FromString(value.Trim());

  private static JsonNullableCrmContactVertical NullableVertical(CrmContactVertical? value) =>
      value.HasValue
          ? JsonNullableCrmContactVertical.FromVertical(value.Value)
          : JsonNullableCrmContactVertical.Null;

  private static JsonNullableInt NullableInt(int? value) =>
      value.HasValue ? JsonNullableInt.FromInt(value.Value) : JsonNullableInt.Null;
}
