using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Crm;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientCrmCoverageTests
{
  [Fact]
  public async Task ApiCrmServiceForwardsEveryNativeCrmOperation()
  {
    var handler = new RecordingHandler(
        [
          Response("native.crm.contacts.default"),
          Response("native.crm.contact-detail.default"),
          Response("native.crm.contact-create.default"),
          Response("native.crm.contact-update.default"),
          new("{}"),
          Response("native.crm.contact-emails.default"),
          Response("native.crm.contact-email-send.default"),
          Response("native.crm.contact-notes.default"),
          Response("native.crm.contact-note-create.default"),
          new("{}"),
          Response("native.crm.contact-link-user.default"),
          Response("native.crm.contact-unlink-user.default"),
          Response("native.crm.contact-email-draft.default"),
          Response("native.crm.import.success.default"),
        ]);
    var service = new ApiCrmService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var contacts = await service.FetchContactsAsync(new CrmContactsRequest("alice", "new", "credit_cards"), TestContext.Current.CancellationToken);
    var detail = await service.FetchContactAsync(ContactId, TestContext.Current.CancellationToken);
    var created = await service.CreateContactAsync(new CreateCrmContactBody("Alice", "alice@example.test"), TestContext.Current.CancellationToken);
    var updated = await service.UpdateContactAsync(ContactId, new UpdateCrmContactBody(Email: "alice.updated@example.test"), TestContext.Current.CancellationToken);
    await service.ArchiveContactAsync(ContactId, TestContext.Current.CancellationToken);
    var emails = await service.FetchContactEmailsAsync(ContactId, "email-cursor", 10, TestContext.Current.CancellationToken);
    var sentEmail = await service.SendEmailAsync(ContactId, new SendCrmEmailBody("Warm intro", BodyText: "Hi Alice"), TestContext.Current.CancellationToken);
    var notes = await service.FetchContactNotesAsync(ContactId, "note-cursor", 5, TestContext.Current.CancellationToken);
    var createdNote = await service.CreateNoteAsync(ContactId, new CreateCrmNoteBody("Met at the conference"), TestContext.Current.CancellationToken);
    await service.DeleteNoteAsync(ContactId, NoteId, TestContext.Current.CancellationToken);
    var linked = await service.LinkUserAsync(ContactId, new LinkCrmContactToUserBody(UserId), TestContext.Current.CancellationToken);
    var unlinked = await service.UnlinkUserAsync(ContactId, TestContext.Current.CancellationToken);
    var draft = await service.GenerateEmailDraftAsync(ContactId, new GenerateCrmEmailDraftBody("Focus on travel", "friendly"), TestContext.Current.CancellationToken);
    var import = await service.ImportContactsAsync("name,email\nAlice,alice@example.test", TestContext.Current.CancellationToken);

    Assert.Equal("alice@example.test", contacts.Results[0].Email);
    Assert.NotEmpty(detail.SocialAccounts);
    Assert.Equal("Alice Creator", created.Contact.Name);
    Assert.Equal("alice@example.test", updated.Contact.Email);
    Assert.Equal("Warm intro", emails.Results[0].Subject);
    Assert.Equal(CrmMessageDirection.Outbound, sentEmail.Message.Direction);
    Assert.Equal("Met at the conference", notes.Results[0].Body);
    Assert.Equal("Met at the conference", createdNote.Note.Body);
    Assert.Equal(UserId, linked.Contact.UserId);
    Assert.Null(unlinked.Contact.UserId);
    Assert.Equal("Warm intro", draft.Draft.Subject);
    Assert.True(import.Valid);

    Assert.Equal(
        [
          "/api/v1/crm/contacts?limit=25&q=alice&status=new&vertical=credit_cards",
          $"/api/v1/crm/contacts/{ContactId}",
          "/api/v1/crm/contacts",
          $"/api/v1/crm/contacts/{ContactId}",
          $"/api/v1/crm/contacts/{ContactId}",
          $"/api/v1/crm/contacts/{ContactId}/emails?after=email-cursor&limit=10",
          $"/api/v1/crm/contacts/{ContactId}/emails",
          $"/api/v1/crm/contacts/{ContactId}/notes?after=note-cursor&limit=5",
          $"/api/v1/crm/contacts/{ContactId}/notes",
          $"/api/v1/crm/contacts/{ContactId}/notes/{NoteId}",
          $"/api/v1/crm/contacts/{ContactId}/user-link",
          $"/api/v1/crm/contacts/{ContactId}/user-link",
          $"/api/v1/crm/contacts/{ContactId}/email-drafts",
          "/api/v1/imports/crm-contacts",
        ],
        handler.Requests.Select(request => request.PathAndQuery));
  }

  [Fact]
  public void SendCrmEmailBodySerializesAndDeserializesOptionalAiTimestamp()
  {
    var json = JsonSerializer.Serialize(
        new SendCrmEmailBody("Warm intro", AiGeneratedAt: new DateTimeOffset(2026, 7, 1, 5, 29, 30, TimeSpan.FromHours(-7))),
        VouchaApiJson.Options);

    Assert.Contains("\"ai_generated_at\":\"2026-07-01T12:29:30Z\"", json, StringComparison.Ordinal);

    var nullWriteOptions = new JsonSerializerOptions(VouchaApiJson.Options)
    {
      DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
    var nullJson = JsonSerializer.Serialize(new SendCrmEmailBody("Warm intro", AiGeneratedAt: null), nullWriteOptions);
    var productionNullJson = JsonSerializer.Serialize(new SendCrmEmailBody("Warm intro", AiGeneratedAt: null), VouchaApiJson.Options);
    var decodedNull = JsonSerializer.Deserialize<SendCrmEmailBody>("{\"subject\":\"Warm intro\",\"ai_generated_at\":null}", VouchaApiJson.Options);
    var decodedValue = JsonSerializer.Deserialize<SendCrmEmailBody>("{\"subject\":\"Warm intro\",\"ai_generated_at\":\"2026-07-01T12:29:30Z\"}", VouchaApiJson.Options);

    Assert.Contains("\"ai_generated_at\":null", nullJson, StringComparison.Ordinal);
    Assert.DoesNotContain("ai_generated_at", productionNullJson, StringComparison.Ordinal);
    Assert.Null(decodedNull!.AiGeneratedAt);
    Assert.Equal(new DateTimeOffset(2026, 7, 1, 12, 29, 30, TimeSpan.Zero), decodedValue!.AiGeneratedAt);
  }

  [Fact]
  public void UpdateCrmContactBodySerializesExplicitNullClearFields()
  {
    var omittedJson = JsonSerializer.Serialize(new UpdateCrmContactBody(Email: "alice@example.test"), VouchaApiJson.Options);
    var clearJson = JsonSerializer.Serialize(
        new UpdateCrmContactBody(
            Email: "alice@example.test",
            Phone: JsonNullableString.Null,
            Vertical: JsonNullableCrmContactVertical.Null,
            FollowerCount: JsonNullableInt.Null,
            Notes: JsonNullableString.Null),
        VouchaApiJson.Options);

    Assert.DoesNotContain("phone", omittedJson, StringComparison.Ordinal);
    Assert.Contains("\"phone\":null", clearJson, StringComparison.Ordinal);
    Assert.Contains("\"vertical\":null", clearJson, StringComparison.Ordinal);
    Assert.Contains("\"follower_count\":null", clearJson, StringComparison.Ordinal);
    Assert.Contains("\"notes\":null", clearJson, StringComparison.Ordinal);
  }

  private const string ContactId = "00000000-0000-7000-8000-000000000584";
  private const string NoteId = "00000000-0000-7000-8000-000000000901";
  private const string UserId = "00000000-0000-7000-8000-000000000001";

  private static RecordedResponse Response(string fixtureId) =>
      new(ApiFixtureLoader.LoadResponse(fixtureId));
}
