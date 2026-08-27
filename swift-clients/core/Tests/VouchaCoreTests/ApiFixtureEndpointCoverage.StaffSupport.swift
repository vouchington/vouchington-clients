@testable import VouchaAPI

let staffSupportFixtureEndpoints: [String: Endpoint] = [
    "native.staff-support.threads.default": .staffSupportThreads(
        query: "account",
        status: .open,
        limit: 25
    ),
    "native.staff-support.thread-detail.default": .staffSupportThread(
        threadId: "00000000-0000-7000-8000-000000000712"
    ),
    "native.staff-support.thread-assign.default": .assignStaffSupportThread(
        threadId: "00000000-0000-7000-8000-000000000712",
        administratorId: "00000000-0000-7000-8000-000000000001"
    ),
    "native.staff-support.thread-resolve.default": .setStaffSupportThreadResolved(
        threadId: "00000000-0000-7000-8000-000000000712",
        resolved: true
    ),
    "native.staff-support.thread-reopen.default": .setStaffSupportThreadResolved(
        threadId: "00000000-0000-7000-8000-000000000712",
        resolved: false
    ),
    "native.staff-support.messages.default": .staffSupportMessages(
        threadId: "00000000-0000-7000-8000-000000000712"
    ),
    "native.staff-support.message-create.default": .createStaffSupportMessage(
        threadId: "00000000-0000-7000-8000-000000000712",
        bodyText: "Saved outbound reply."
    ),
    "native.staff-support.draft-create.default": .queueStaffSupportDraft(
        threadId: "00000000-0000-7000-8000-000000000712"
    ),
    "native.staff-support.message-edit.default": .updateStaffSupportDraft(
        threadId: "00000000-0000-7000-8000-000000000712",
        messageId: "00000000-0000-7000-8000-000000000713",
        bodyText: "Edited support draft."
    ),
    "native.staff-support.message-approve.default": .approveStaffSupportMessage(
        threadId: "00000000-0000-7000-8000-000000000712",
        messageId: "00000000-0000-7000-8000-000000000713"
    ),
    "native.staff-support.message-send.default": .sendStaffSupportMessage(
        threadId: "00000000-0000-7000-8000-000000000712",
        messageId: "00000000-0000-7000-8000-000000000713"
    ),
    "native.staff-support.contacts.default": .staffSupportContacts(query: "traveler", limit: 25),
    "native.staff-support.contact-detail.default": .staffSupportContact(
        contactId: "00000000-0000-7000-8000-000000000711",
        limit: 25
    ),
    "native.staff-support.contact-update.default": .updateStaffSupportContact(
        contactId: "00000000-0000-7000-8000-000000000711",
        name: "Traveler Support",
        notes: "Updated support notes."
    )
]
