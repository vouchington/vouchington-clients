using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Theory]
  [MemberData(nameof(CrmEndpointCases))]
  public void CrmEndpointsMirrorSharedRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  public static IEnumerable<object[]> CrmEndpointCases()
  {
    yield return Case("crmContacts", VouchaApiEndpoints.CrmContacts(new CrmContactsRequest("alice", "new", "credit_cards", null, null, 25)), HttpMethod.Get, "/api/v1/crm/contacts", Query(("q", "alice"), ("status", "new"), ("vertical", "credit_cards"), ("limit", "25")));
    yield return Case("crmContact", VouchaApiEndpoints.CrmContact("00000000-0000-7000-8000-000000000584"), HttpMethod.Get, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584", Query());
    yield return Case("createCrmContact", VouchaApiEndpoints.CreateCrmContact(new CreateCrmContactBody("Alice Creator", "alice@example.test", null, CrmContactVertical.CreditCards, CrmContactType.Influencer, 250000, "Creator outreach contact")), HttpMethod.Post, "/api/v1/crm/contacts", Query(), hasBody: true);
    yield return Case("updateCrmContact", VouchaApiEndpoints.UpdateCrmContact("00000000-0000-7000-8000-000000000584", new UpdateCrmContactBody("Alice Creator", "alice@example.test", JsonNullableString.FromString("+1-415-555-0100"), JsonNullableCrmContactVertical.FromVertical(CrmContactVertical.CreditCards), null, JsonNullableInt.FromInt(255000), JsonNullableString.FromString("Updated outreach notes"))), HttpMethod.Patch, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584", Query(), hasBody: true);
    yield return Case("archiveCrmContact", VouchaApiEndpoints.ArchiveCrmContact("00000000-0000-7000-8000-000000000584"), HttpMethod.Delete, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584", Query());
    yield return Case("crmContactEmails", VouchaApiEndpoints.CrmContactEmails("00000000-0000-7000-8000-000000000584", limit: 25), HttpMethod.Get, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/emails", Query(("limit", "25")));
    yield return Case("sendCrmEmail", VouchaApiEndpoints.SendCrmEmail("00000000-0000-7000-8000-000000000584", new SendCrmEmailBody("Warm intro", null, "Hi Alice, great to connect.", CrmEmailProvider.Ses, null, "Write a warm intro", new DateTimeOffset(2026, 7, 1, 12, 29, 30, TimeSpan.Zero))), HttpMethod.Post, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/emails", Query(), hasBody: true);
    yield return Case("crmContactNotes", VouchaApiEndpoints.CrmContactNotes("00000000-0000-7000-8000-000000000584", limit: 25), HttpMethod.Get, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/notes", Query(("limit", "25")));
    yield return Case("createCrmNote", VouchaApiEndpoints.CreateCrmNote("00000000-0000-7000-8000-000000000584", new CreateCrmNoteBody("Met at the conference")), HttpMethod.Post, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/notes", Query(), hasBody: true);
    yield return Case("deleteCrmNote", VouchaApiEndpoints.DeleteCrmNote("00000000-0000-7000-8000-000000000584", "00000000-0000-7000-8000-000000000901"), HttpMethod.Delete, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/notes/00000000-0000-7000-8000-000000000901", Query());
    yield return Case("linkCrmContactToUser", VouchaApiEndpoints.LinkCrmContactToUser("00000000-0000-7000-8000-000000000584", new LinkCrmContactToUserBody("00000000-0000-7000-8000-000000000001")), HttpMethod.Put, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/user-link", Query(), hasBody: true);
    yield return Case("unlinkCrmContactFromUser", VouchaApiEndpoints.UnlinkCrmContactFromUser("00000000-0000-7000-8000-000000000584"), HttpMethod.Delete, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/user-link", Query());
    yield return Case("generateCrmEmailDraft", VouchaApiEndpoints.GenerateCrmEmailDraft("00000000-0000-7000-8000-000000000584", new GenerateCrmEmailDraftBody("Focus on travel content", "friendly")), HttpMethod.Post, "/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/email-drafts", Query(), hasBody: true);
    yield return Case("importCrmContacts", VouchaApiEndpoints.ImportCrmContacts(new ImportCrmContactsBody("name,email\nAlice Creator,alice@example.test")), HttpMethod.Post, "/api/v1/imports/crm-contacts", Query(), hasBody: true);
  }
}
