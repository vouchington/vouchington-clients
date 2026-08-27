using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientStaffSupportTests
{
  [Fact]
  public async Task StaffSupportClientMethodsUseTypedRequestsAndDecodeResponses()
  {
    var handler = new RecordingHandler(
    [
      new(ThreadListJson), new(ThreadJson), new(ThreadJson), new(ThreadJson), new(MessageListJson),
      new(MessageJson), new("{\"queued\":true}"), new(MessageJson), new(MessageJson), new(MessageJson),
      new(ContactListJson), new(ContactDetailJson), new(ContactJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var cancellationToken = TestContext.Current.CancellationToken;

    Assert.Equal("thread", (await client.FetchStaffSupportThreadsAsync("account", StaffSupportThreadStatusFilter.Assigned, "after", 25, cancellationToken)).Results.Single().Id);
    Assert.Equal("/api/v1/support/threads?after=after&limit=25&q=account&status=assigned", handler.PathAndQuery);
    await client.FetchStaffSupportThreadAsync("thread", cancellationToken);
    await client.AssignStaffSupportThreadAsync("thread", "admin", cancellationToken);
    Assert.Contains("\"assigned_to_id\":\"admin\"", handler.RequestBody, StringComparison.Ordinal);
    await client.ResolveStaffSupportThreadAsync("thread", true, cancellationToken);
    Assert.Contains("\"resolved\":true", handler.RequestBody, StringComparison.Ordinal);
    await client.FetchStaffSupportMessagesAsync("thread", "older", 20, cancellationToken);
    Assert.Equal("/api/v1/support/threads/thread/messages?after=older&limit=20", handler.PathAndQuery);
    await client.CreateStaffSupportMessageAsync("thread", "saved", cancellationToken);
    await client.QueueStaffSupportDraftAsync("thread", cancellationToken);
    await client.UpdateStaffSupportDraftAsync("thread", "message", "edited", cancellationToken);
    await client.ApproveStaffSupportMessageAsync("thread", "message", cancellationToken);
    await client.SendStaffSupportMessageAsync("thread", "message", cancellationToken);
    await client.FetchStaffSupportContactsAsync("traveler", "after", 15, cancellationToken);
    Assert.Equal("/api/v1/support/contacts?after=after&limit=15&q=traveler", handler.PathAndQuery);
    await client.FetchStaffSupportContactAsync("contact", "older", 10, cancellationToken);
    await client.UpdateStaffSupportContactAsync("contact", new UpdateSupportContactBody("Updated", "Notes"), cancellationToken);

    Assert.Equal(13, handler.Requests.Count);
    Assert.Equal(HttpMethod.Patch, handler.Method);
    Assert.Contains("\"name\":\"Updated\"", handler.RequestBody, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(SupportThreadStatus.Open, "open")]
  [InlineData(SupportThreadStatus.Assigned, "assigned")]
  [InlineData(SupportThreadStatus.Resolved, "resolved")]
  [InlineData(SupportThreadStatus.Closed, "closed")]
  public void ThreadStatusesHaveStrictWireValues(SupportThreadStatus status, string expected) =>
      Assert.Equal(expected, status.ToWireValue());

  [Theory]
  [InlineData(StaffSupportThreadStatusFilter.Open, "open")]
  [InlineData(StaffSupportThreadStatusFilter.Assigned, "assigned")]
  [InlineData(StaffSupportThreadStatusFilter.Resolved, "resolved")]
  public void StaffThreadFiltersHaveWireValues(StaffSupportThreadStatusFilter status, string expected) =>
      Assert.Equal(expected, status.ToWireValue());

  [Fact]
  public async Task StaffSupportServiceForwardsEverySupportedOperation()
  {
    var handler = new RecordingHandler(
    [
      new(ThreadListJson), new(ThreadJson), new(ThreadJson), new(ThreadJson), new(MessageListJson),
      new(MessageJson), new("{\"queued\":true}"), new(MessageJson), new(MessageJson), new(MessageJson),
      new(ContactListJson), new(ContactDetailJson),
    ]);
    var service = new ApiStaffSupportService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var cancellationToken = TestContext.Current.CancellationToken;

    await service.FetchThreadsAsync(null, null, null, 30, cancellationToken);
    await service.FetchThreadAsync("thread", cancellationToken);
    await service.AssignAsync("thread", "admin", cancellationToken);
    await service.ResolveAsync("thread", true, cancellationToken);
    await service.FetchMessagesAsync("thread", null, 50, cancellationToken);
    await service.CreateMessageAsync("thread", "reply", cancellationToken);
    await service.QueueDraftAsync("thread", cancellationToken);
    await service.UpdateDraftAsync("thread", "message", "reply", cancellationToken);
    await service.ApproveAsync("thread", "message", cancellationToken);
    await service.SendAsync("thread", "message", cancellationToken);
    await service.FetchContactsAsync(null, null, 30, cancellationToken);
    await service.FetchContactAsync("contact", null, 50, cancellationToken);

    Assert.Equal(12, handler.Requests.Count);
  }

  private const string ThreadJson = """
      {"thread":{"id":"thread","support_contact_id":"contact","subject":"Help","conversation_id":null,"created_at":"2026-07-01T12:00:00Z","updated_at":"2026-07-01T12:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user"}}
      """;
  private const string ThreadListJson = """
      {"results":[{"id":"thread","support_contact_id":"contact","subject":"Help","conversation_id":null,"created_at":"2026-07-01T12:00:00Z","updated_at":"2026-07-01T12:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user"}],"page_info":{"has_next_page":false,"start_cursor":"thread","end_cursor":null}}
      """;
  private const string MessageJson = """
      {"message":{"id":"message","support_thread_id":"thread","direction":"outbound","body_text":"Saved","body_html":"<p>Saved</p>","created_at":"2026-07-01T12:00:00Z","created_by_id":"admin","updated_at":"2026-07-01T12:00:00Z","email_message_id":null,"email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null}}
      """;
  private const string MessageListJson = """
      {"results":[{"id":"message","support_thread_id":"thread","direction":"outbound","body_text":"Saved","body_html":"<p>Saved</p>","created_at":"2026-07-01T12:00:00Z","created_by_id":"admin","updated_at":"2026-07-01T12:00:00Z","email_message_id":null,"email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null}],"page_info":{"has_next_page":false,"start_cursor":"message","end_cursor":null}}
      """;
  private const string ContactJson = """
      {"contact":{"id":"contact","email_address":"contact@example.test","name":"Contact","user_id":"user","notes":"Notes","created_at":"2026-07-01T12:00:00Z","updated_at":"2026-07-01T12:00:00Z"}}
      """;
  private const string ContactListJson = """
      {"results":[{"id":"contact","email_address":"contact@example.test","name":"Contact","user_id":"user","notes":"Notes","created_at":"2026-07-01T12:00:00Z","updated_at":"2026-07-01T12:00:00Z"}],"page_info":{"has_next_page":false,"start_cursor":"contact","end_cursor":null}}
      """;
  private const string ContactDetailJson = """
      {"contact":{"id":"contact","email_address":"contact@example.test","name":"Contact","user_id":"user","notes":"Notes","created_at":"2026-07-01T12:00:00Z","updated_at":"2026-07-01T12:00:00Z"},"threads":[],"thread_page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
      """;
}
