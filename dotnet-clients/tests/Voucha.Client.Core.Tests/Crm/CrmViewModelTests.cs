using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Crm;
using Xunit;

namespace Voucha.Client.Core.Tests.Crm;

public sealed partial class CrmViewModelTests
{
  [Fact]
  public async Task ContactsViewModelLoadsCreatesAndReportsImportValidation()
  {
    var service = new RecordingCrmService
    {
      ContactsResponse = new CrmContactListResponse([Contact("contact-1", "Original", "orig@example.test")], new PageInfo(null, false, null)),
      CreateResponse = new CrmContactResponse(Contact("contact-2", "Alice Creator", "alice@example.test")),
      ImportResponse = new CrmImportResponse(false, Validation: new CrmImportValidation(false, [new CrmImportValidationRow(0, false, ["follower_count must be a non-negative integer"])])),
    };
    var viewModel = new CrmContactsViewModel(service)
    {
      SearchQuery = "alice",
      StatusFilter = "new",
      VerticalFilter = "credit_cards",
      LinkedFilter = true,
      CreateName = "Alice Creator",
      CreateEmail = "alice@example.test",
      CreatePhone = "+1-415-555-0100",
      CreateVertical = "credit_cards",
      CreateContactType = "influencer",
      CreateFollowerCount = "250000",
      CreateNotes = "Creator outreach contact",
      ImportCsv = "name,email,follower_count\nAlice Creator,alice@example.test,-1",
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(TestContext.Current.CancellationToken);
    await viewModel.ImportAsync(TestContext.Current.CancellationToken);

    Assert.Equal("alice", service.LastContactsRequest!.Query);
    Assert.True(service.LastContactsRequest.Linked);
    Assert.Equal("credit_cards", service.LastContactsRequest.Vertical);
    Assert.Equal("contact-2", viewModel.Contacts[0].Id);
    Assert.Equal("Alice Creator", viewModel.Contacts[0].Name);
    Assert.Equal("Row 2: follower_count must be a non-negative integer", viewModel.ErrorMessage);
    Assert.True(viewModel.HasError);
    Assert.Equal(CrmContactVertical.CreditCards, service.LastCreateContactBody!.Vertical);
    Assert.Equal(CrmContactType.Influencer, service.LastCreateContactBody.ContactType);
    Assert.Equal(250000, service.LastCreateContactBody.FollowerCount);
    Assert.Equal("Creator outreach contact", service.LastCreateContactBody.Notes);
  }

  [Fact]
  public async Task ContactDetailViewModelRunsTheCoreContactActions()
  {
    var service = new RecordingCrmService
    {
      DetailResponse = new CrmContactDetailResponse(
          Contact("contact-1", "Alice Creator", "alice@example.test", userId: null),
          [SocialAccount("social-1")]),
      EmailsResponse = new CrmEmailListResponse([Message("message-1", "Warm intro", CrmMessageDirection.Inbound)], new PageInfo(null, false, null)),
      NotesResponse = new CrmNoteListResponse([Note("note-1", "Existing note")], new PageInfo(null, false, null)),
      UpdateResponse = new CrmContactResponse(Contact("contact-1", "Alice Updated", "alice.updated@example.test", userId: "user-2")),
      LinkResponse = new CrmContactResponse(Contact("contact-1", "Alice Updated", "alice.updated@example.test", userId: "user-2")),
      UnlinkResponse = new CrmContactResponse(Contact("contact-1", "Alice Updated", "alice.updated@example.test", userId: null)),
      SendResponse = new CrmMessageResponse(Message("message-2", "Warm intro", CrmMessageDirection.Outbound)),
      DraftResponse = new CrmEmailDraftResponse(new CrmEmailDraft("Follow up", "<p>Draft</p>", "Draft")),
      CreateNoteResponse = new CrmNoteResponse(Note("note-2", "Met at the conference")),
    };
    var viewModel = new CrmContactDetailViewModel(service);

    await viewModel.LoadAsync("contact-1", TestContext.Current.CancellationToken);
    viewModel.Name = "Alice Updated";
    viewModel.Email = "alice.updated@example.test";
    viewModel.Phone = "+1-415-555-0100";
    viewModel.Vertical = "credit_cards";
    viewModel.ContactType = "influencer";
    viewModel.FollowerCount = "255000";
    viewModel.NotesText = "Updated outreach notes";
    viewModel.LinkUserId = "user-2";
    viewModel.EmailSubject = "Warm intro";
    viewModel.EmailBodyText = "Hi Alice";
    viewModel.EmailBodyHtml = "<p>Hi Alice</p>";
    viewModel.EmailProvider = CrmEmailProvider.GmailSmtp;
    viewModel.DraftPrompt = "Focus on travel content";
    viewModel.DraftTone = "friendly";
    viewModel.NoteBody = "Met at the conference";

    Assert.True(await viewModel.SaveAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ArchiveAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.LinkUserAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.UnlinkUserAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.GenerateDraftAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.SendEmailAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.CreateNoteAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DeleteNoteAsync("note-1", TestContext.Current.CancellationToken));

    Assert.Equal("contact-1", service.LastContactId);
    Assert.Equal("Alice Updated", service.LastUpdateContactBody!.Name);
    Assert.Equal(CrmContactVertical.CreditCards, service.LastUpdateContactBody.Vertical!.Value.Value);
    Assert.Equal("user-2", service.LastLinkBody!.UserId);
    Assert.Equal(CrmEmailProvider.GmailSmtp, service.LastSendEmailBody!.EmailProvider);
    Assert.NotNull(service.LastSendEmailBody.AiGeneratedAt);
    Assert.Equal("Focus on travel content", service.LastDraftBody!.Prompt);
    Assert.Equal("Met at the conference", service.LastCreateNoteBody!.Body);
    Assert.Equal("note-1", service.LastDeletedNoteId);
    Assert.Equal("Follow up", viewModel.EmailSubject);
    Assert.Equal("Draft", viewModel.EmailBodyText);
    Assert.Single(viewModel.Notes);
    Assert.Equal("note-2", viewModel.Notes[0].Id);
  }

  [Fact]
  public async Task ContactPaginationForwardsCursorsAndAppendsRows()
  {
    var service = new RecordingCrmService
    {
      ContactResponses = new Queue<CrmContactListResponse>([
        new([Contact("contact-1")], new PageInfo("contacts-next", true, null)),
        new([Contact("contact-2", "Bob", "bob@example.test")], new PageInfo(null, false, null)),
      ]),
    };
    var viewModel = new CrmContactsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreContactsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new string?[] { null, "contacts-next" }, service.ContactCursors);
    Assert.Equal(new[] { "contact-1", "contact-2" }, viewModel.Contacts.Select(row => row.Id));
  }

  [Fact]
  public async Task DetailPaginationForwardsIndependentEmailAndNoteCursors()
  {
    var service = new RecordingCrmService
    {
      EmailResponses = new Queue<CrmEmailListResponse>([
        new([Message("message-1", "First", CrmMessageDirection.Inbound)], new PageInfo("emails-next", true, null)),
        new([Message("message-2", "Second", CrmMessageDirection.Inbound)], new PageInfo(null, false, null)),
      ]),
      NoteResponses = new Queue<CrmNoteListResponse>([
        new([Note("note-1", "First")], new PageInfo("notes-next", true, null)),
        new([Note("note-2", "Second")], new PageInfo(null, false, null)),
      ]),
    };
    var viewModel = new CrmContactDetailViewModel(service);

    await viewModel.LoadAsync("contact-1", TestContext.Current.CancellationToken);
    await viewModel.LoadMoreEmailsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreNotesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new string?[] { null, "emails-next" }, service.EmailCursors);
    Assert.Equal(new string?[] { null, "notes-next" }, service.NoteCursors);
    Assert.Equal(2, viewModel.Emails.Count);
    Assert.Equal(2, viewModel.Notes.Count);
  }

  [Fact]
  public async Task ContactsViewModelReportsLocalValidationAndErrorOnlyImports()
  {
    var missingName = new CrmContactsViewModel(new RecordingCrmService())
    {
      CreateEmail = "alice@example.test",
    };
    await missingName.CreateAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Name is required.", missingName.ErrorMessage);

    var missingEmail = new CrmContactsViewModel(new RecordingCrmService())
    {
      CreateName = "Alice Creator",
    };
    await missingEmail.CreateAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Email is required.", missingEmail.ErrorMessage);

    var missingCsv = new CrmContactsViewModel(new RecordingCrmService());
    await missingCsv.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal("CSV is required.", missingCsv.ErrorMessage);

    var errorOnlyImport = new CrmContactsViewModel(new RecordingCrmService
    {
      ImportResponse = new CrmImportResponse(false, Error: "Unknown CSV columns: handle"),
    })
    {
      ImportCsv = "handle\nalice",
    };
    await errorOnlyImport.ImportAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Unknown CSV columns: handle", errorOnlyImport.ErrorMessage);
  }

  [Fact]
  public async Task ContactDetailViewModelReportsValidationFailures()
  {
    var viewModel = new CrmContactDetailViewModel(new RecordingCrmService());

    await viewModel.LoadAsync("contact-1", TestContext.Current.CancellationToken);
    viewModel.Name = string.Empty;
    Assert.False(await viewModel.SaveAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Name is required.", viewModel.ErrorMessage);

    viewModel.Name = "Alice Creator";
    viewModel.Email = string.Empty;
    Assert.False(await viewModel.SaveAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Email is required.", viewModel.ErrorMessage);

    viewModel.Email = "alice@example.test";
    viewModel.EmailSubject = "Warm intro";
    viewModel.EmailBodyHtml = string.Empty;
    viewModel.EmailBodyText = string.Empty;
    Assert.False(await viewModel.SendEmailAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Body is required.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ContactDetailViewModelIgnoresStaleLoadCompletion()
  {
    var service = new RacingCrmService();
    var viewModel = new CrmContactDetailViewModel(service);

    var firstLoad = viewModel.LoadAsync("contact-1", TestContext.Current.CancellationToken);
    await Task.Yield();
    await viewModel.LoadAsync("contact-2", TestContext.Current.CancellationToken);

    Assert.Equal("contact-2", viewModel.ContactId);
    Assert.Equal("Bob Creator", viewModel.Name);
    Assert.False(viewModel.IsLoading);

    service.CompleteFirstDetail();
    await firstLoad;

    Assert.Equal("contact-2", viewModel.ContactId);
    Assert.Equal("Bob Creator", viewModel.Name);
    Assert.False(viewModel.IsLoading);
  }

  private sealed class RecordingCrmService : ICrmService
  {
    public CrmContactListResponse ContactsResponse { get; init; } = new([], new PageInfo(null, false, null));
    public CrmContactDetailResponse DetailResponse { get; init; } = new(Contact("contact-1"), []);
    public CrmContactResponse CreateResponse { get; init; } = new(Contact("contact-1"));
    public CrmContactResponse UpdateResponse { get; init; } = new(Contact("contact-1"));
    public CrmContactResponse LinkResponse { get; init; } = new(Contact("contact-1"));
    public CrmContactResponse UnlinkResponse { get; init; } = new(Contact("contact-1"));
    public CrmEmailListResponse EmailsResponse { get; init; } = new([], new PageInfo(null, false, null));
    public CrmNoteListResponse NotesResponse { get; init; } = new([], new PageInfo(null, false, null));
    public CrmMessageResponse SendResponse { get; init; } = new(Message("message-1", "Warm intro", CrmMessageDirection.Outbound));
    public CrmEmailDraftResponse DraftResponse { get; init; } = new(new CrmEmailDraft("Follow up", "<p>Draft</p>", "Draft"));
    public CrmNoteResponse CreateNoteResponse { get; init; } = new(Note("note-1", "Met at the conference"));
    public CrmImportResponse ImportResponse { get; init; } = new(true);

    public string? LastContactId { get; private set; }
    public CrmContactsRequest? LastContactsRequest { get; private set; }
    public CreateCrmContactBody? LastCreateContactBody { get; private set; }
    public UpdateCrmContactBody? LastUpdateContactBody { get; private set; }
    public LinkCrmContactToUserBody? LastLinkBody { get; private set; }
    public SendCrmEmailBody? LastSendEmailBody { get; private set; }
    public GenerateCrmEmailDraftBody? LastDraftBody { get; private set; }
    public CreateCrmNoteBody? LastCreateNoteBody { get; private set; }
    public string? LastDeletedNoteId { get; private set; }
    public string? LastImportCsv { get; private set; }
    public Queue<CrmContactListResponse>? ContactResponses { get; init; }
    public Queue<Func<Task<CrmContactListResponse>>> ContactPageResults { get; } = [];
    public Queue<Func<Task<CrmEmailListResponse>>> EmailPageResults { get; } = [];
    public Queue<CrmEmailListResponse>? EmailResponses { get; init; }
    public Queue<CrmNoteListResponse>? NoteResponses { get; init; }
    public List<string?> ContactCursors { get; } = [];
    public List<string?> EmailCursors { get; } = [];
    public List<string?> NoteCursors { get; } = [];

    public Task<CrmContactListResponse> FetchContactsAsync(CrmContactsRequest request, CancellationToken cancellationToken = default)
    {
      LastContactsRequest = request;
      ContactCursors.Add(request.After);
      return ContactPageResults.Count > 0
          ? ContactPageResults.Dequeue()()
          : Task.FromResult(ContactResponses?.Dequeue() ?? ContactsResponse);
    }

    public Task<CrmContactDetailResponse> FetchContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      return Task.FromResult(DetailResponse);
    }

    public Task<CrmContactResponse> CreateContactAsync(CreateCrmContactBody body, CancellationToken cancellationToken = default)
    {
      LastCreateContactBody = body;
      return Task.FromResult(CreateResponse);
    }

    public Task<CrmContactResponse> UpdateContactAsync(string contactId, UpdateCrmContactBody body, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      LastUpdateContactBody = body;
      return Task.FromResult(UpdateResponse);
    }

    public Task ArchiveContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      return Task.CompletedTask;
    }

    public Task<CrmEmailListResponse> FetchContactEmailsAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      EmailCursors.Add(after);
      return EmailPageResults.Count > 0
          ? EmailPageResults.Dequeue()()
          : Task.FromResult(EmailResponses?.Dequeue() ?? EmailsResponse);
    }

    public Task<CrmMessageResponse> SendEmailAsync(string contactId, SendCrmEmailBody body, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      LastSendEmailBody = body;
      return Task.FromResult(SendResponse);
    }

    public Task<CrmNoteListResponse> FetchContactNotesAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      NoteCursors.Add(after);
      return Task.FromResult(NoteResponses?.Dequeue() ?? NotesResponse);
    }

    public Task<CrmNoteResponse> CreateNoteAsync(string contactId, CreateCrmNoteBody body, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      LastCreateNoteBody = body;
      return Task.FromResult(CreateNoteResponse);
    }

    public Task DeleteNoteAsync(string contactId, string noteId, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      LastDeletedNoteId = noteId;
      return Task.CompletedTask;
    }

    public Task<CrmContactResponse> LinkUserAsync(string contactId, LinkCrmContactToUserBody body, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      LastLinkBody = body;
      return Task.FromResult(LinkResponse);
    }

    public Task<CrmContactResponse> UnlinkUserAsync(string contactId, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      return Task.FromResult(UnlinkResponse);
    }

    public Task<CrmEmailDraftResponse> GenerateEmailDraftAsync(string contactId, GenerateCrmEmailDraftBody body, CancellationToken cancellationToken = default)
    {
      LastContactId = contactId;
      LastDraftBody = body;
      return Task.FromResult(DraftResponse);
    }

    public Task<CrmImportResponse> ImportContactsAsync(string csv, CancellationToken cancellationToken = default)
    {
      LastImportCsv = csv;
      return Task.FromResult(ImportResponse);
    }
  }

  private sealed class RacingCrmService : ICrmService
  {
    private readonly TaskCompletionSource<CrmContactDetailResponse> firstDetail = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void CompleteFirstDetail() =>
        firstDetail.SetResult(new CrmContactDetailResponse(Contact("contact-1", "Alice Creator", "alice@example.test"), []));

    public Task<CrmContactDetailResponse> FetchContactAsync(string contactId, CancellationToken cancellationToken = default) =>
        contactId == "contact-1"
            ? firstDetail.Task
            : Task.FromResult(new CrmContactDetailResponse(Contact("contact-2", "Bob Creator", "bob@example.test"), []));

    public Task<CrmEmailListResponse> FetchContactEmailsAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CrmEmailListResponse([], new PageInfo(null, false, null)));

    public Task<CrmNoteListResponse> FetchContactNotesAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CrmNoteListResponse([], new PageInfo(null, false, null)));

    public Task<CrmContactListResponse> FetchContactsAsync(CrmContactsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmContactResponse> CreateContactAsync(CreateCrmContactBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmContactResponse> UpdateContactAsync(string contactId, UpdateCrmContactBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ArchiveContactAsync(string contactId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmMessageResponse> SendEmailAsync(string contactId, SendCrmEmailBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmNoteResponse> CreateNoteAsync(string contactId, CreateCrmNoteBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteNoteAsync(string contactId, string noteId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmContactResponse> LinkUserAsync(string contactId, LinkCrmContactToUserBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmContactResponse> UnlinkUserAsync(string contactId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmEmailDraftResponse> GenerateEmailDraftAsync(string contactId, GenerateCrmEmailDraftBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CrmImportResponse> ImportContactsAsync(string csv, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private static CrmContact Contact(string id, string name = "Alice Creator", string email = "alice@example.test", CrmContactVertical? vertical = CrmContactVertical.CreditCards, CrmContactType contactType = CrmContactType.Influencer, int? followerCount = 250000, string? notes = "Creator outreach contact", string? userId = "user-1") =>
      new(
          id,
          name,
          email,
          "+1-415-555-0100",
          vertical,
          contactType,
          CrmContactSource.Manual,
          followerCount,
          notes,
          null,
          userId,
          null,
          "created-by",
          null,
          null,
          null,
          null,
          null,
          new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
          new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

  private static CrmContactSocialAccount SocialAccount(string id) =>
      new(id, "contact-1", CrmSocialPlatform.Instagram, "@alice", null, null, null, new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

  private static CrmMessage Message(string id, string subject, CrmMessageDirection direction) =>
      new(id, "conversation-1", direction, "alice@example.test", "team@example.test", subject, "Hi Alice", "<p>Hi Alice</p>", CrmEmailProvider.Ses, null, new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), null, null, null, null, "prompt", new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), "user-1", new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

  private static CrmNote Note(string id, string body) =>
      new(id, "conversation-1", "contact-1", body, "user-1", null, new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
}
