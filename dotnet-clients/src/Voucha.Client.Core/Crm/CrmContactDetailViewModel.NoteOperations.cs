using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactDetailViewModel
{
  public async Task<bool> CreateNoteAsync(CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || string.IsNullOrWhiteSpace(NoteBody) || IsCreatingNote) return false;
    IsCreatingNote = true;
    ErrorMessage = null;
    try
    {
      var response = await service.CreateNoteAsync(ContactId, new CreateCrmNoteBody(NoteBody.Trim()), cancellationToken).ConfigureAwait(true);
      Notes = [CrmNoteRow.FromNote(response.Note, localization), .. Notes];
      SynchronizeNotePage();
      NoteBody = string.Empty;
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsCreatingNote = false;
    }
  }

  public async Task<bool> DeleteNoteAsync(string noteId, CancellationToken cancellationToken = default)
  {
    if (ContactId is not { Length: > 0 } || string.IsNullOrWhiteSpace(noteId)) return false;
    ErrorMessage = null;
    try
    {
      await service.DeleteNoteAsync(ContactId, noteId, cancellationToken).ConfigureAwait(true);
      Notes = Notes.Where(note => note.Id != noteId).ToArray();
      SynchronizeNotePage();
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      return false;
    }
  }
}
