using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private const string StaffSupportAdministratorId = "00000000-0000-7000-8000-000000000001";
  private const string StaffSupportContactId = "00000000-0000-7000-8000-000000000711";
  private const string StaffSupportThreadId = "00000000-0000-7000-8000-000000000712";
  private const string StaffSupportMessageId = "00000000-0000-7000-8000-000000000713";

  private static Dictionary<string, ApiRequest> WithStaffSupportEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.staff-support.threads.default"] = VouchaApiEndpoints.StaffSupportThreads("account", StaffSupportThreadStatusFilter.Open, limit: 25);
    registry["native.staff-support.thread-detail.default"] = VouchaApiEndpoints.StaffSupportThread(StaffSupportThreadId);
    registry["native.staff-support.thread-assign.default"] = VouchaApiEndpoints.AssignStaffSupportThread(StaffSupportThreadId, StaffSupportAdministratorId);
    registry["native.staff-support.thread-resolve.default"] = VouchaApiEndpoints.ResolveStaffSupportThread(StaffSupportThreadId, true);
    registry["native.staff-support.thread-reopen.default"] = VouchaApiEndpoints.ResolveStaffSupportThread(StaffSupportThreadId, false);
    registry["native.staff-support.messages.default"] = VouchaApiEndpoints.StaffSupportMessages(StaffSupportThreadId);
    registry["native.staff-support.message-create.default"] = VouchaApiEndpoints.CreateStaffSupportMessage(StaffSupportThreadId, "Saved outbound reply.");
    registry["native.staff-support.draft-create.default"] = VouchaApiEndpoints.QueueStaffSupportDraft(StaffSupportThreadId);
    registry["native.staff-support.message-edit.default"] = VouchaApiEndpoints.UpdateStaffSupportDraft(StaffSupportThreadId, StaffSupportMessageId, "Edited support draft.");
    registry["native.staff-support.message-approve.default"] = VouchaApiEndpoints.ApproveStaffSupportMessage(StaffSupportThreadId, StaffSupportMessageId);
    registry["native.staff-support.message-send.default"] = VouchaApiEndpoints.SendStaffSupportMessage(StaffSupportThreadId, StaffSupportMessageId);
    registry["native.staff-support.contacts.default"] = VouchaApiEndpoints.StaffSupportContacts("traveler", limit: 25);
    registry["native.staff-support.contact-detail.default"] = VouchaApiEndpoints.StaffSupportContact(StaffSupportContactId, limit: 25);
    registry["native.staff-support.contact-update.default"] = VouchaApiEndpoints.UpdateStaffSupportContact(StaffSupportContactId, new("Traveler Support", "Updated support notes."));
    return registry;
  }
}
