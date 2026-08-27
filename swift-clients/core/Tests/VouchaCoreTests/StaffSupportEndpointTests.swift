import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class StaffSupportEndpointTests: XCTestCase {
    func testListAndDetailEndpointsForwardFiltersAndCursors() {
        XCTAssertEqual(
            Endpoint.staffSupportThreads(query: "refund", status: .assigned, after: "cursor", limit: 12).queryItems,
            [
                .init(name: "limit", value: "12"),
                .init(name: "q", value: "refund"),
                .init(name: "status", value: "assigned"),
                .init(name: "after", value: "cursor")
            ]
        )
        XCTAssertEqual(
            Endpoint.staffSupportMessages(threadId: "thread/id", after: "older", limit: 8).path,
            "/api/v1/support/threads/thread%2Fid/messages"
        )
        XCTAssertEqual(
            Endpoint.staffSupportContacts(query: "person@example.com", after: "next", limit: 7).queryItems,
            [
                .init(name: "limit", value: "7"),
                .init(name: "q", value: "person@example.com"),
                .init(name: "after", value: "next")
            ]
        )
    }

    func testThreadAndMessageMutationsEncodeExactBodies() {
        assertEndpoint(
            .assignStaffSupportThread(threadId: "thread-1", administratorId: "admin-1"),
            method: .PATCH,
            path: "/api/v1/support/threads/thread-1",
            body: ["assigned_to_id": "admin-1"]
        )
        assertEndpoint(
            .setStaffSupportThreadResolved(threadId: "thread-1", resolved: true),
            method: .PATCH,
            path: "/api/v1/support/threads/thread-1",
            body: ["resolved": true]
        )
        assertEndpoint(
            .createStaffSupportMessage(threadId: "thread-1", bodyText: "Saved reply"),
            method: .POST,
            path: "/api/v1/support/threads/thread-1/messages",
            body: ["body_text": "Saved reply"]
        )
        assertEndpoint(
            .updateStaffSupportDraft(threadId: "thread-1", messageId: "message-1", bodyText: "Edited"),
            method: .PATCH,
            path: "/api/v1/support/threads/thread-1/messages/message-1",
            body: ["body_text": "Edited"]
        )
    }

    func testStrictSupportStatusRejectsUnknownValues() {
        XCTAssertThrowsError(
            try makeVouchaDecoder().decode(
                SupportThreadResponse.self,
                from: Data(
                    #"{"thread":{"id":"1","support_contact_id":"2","subject":"s","conversation_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"future"}}"#
                        .utf8
                )
            )
        )
    }

    func testSupportThreadStatusIsRequiredAndEndpointDefaultsMatchRouteContracts() throws {
        let missingStatus = #"{"thread":{"id":"1","support_contact_id":"2","subject":"s","conversation_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null}}"#
        let nullStatus = missingStatus.replacingOccurrences(of: "}}", with: #","status":null}}"#)

        XCTAssertThrowsError(try makeVouchaDecoder().decode(SupportThreadResponse.self, from: Data(missingStatus.utf8)))
        XCTAssertThrowsError(try makeVouchaDecoder().decode(SupportThreadResponse.self, from: Data(nullStatus.utf8)))
        XCTAssertEqual(Endpoint.staffSupportThreads().queryItems, [.init(name: "limit", value: "30")])
        XCTAssertEqual(
            Endpoint.staffSupportMessages(threadId: "thread").queryItems,
            [.init(name: "limit", value: "50")]
        )
        XCTAssertEqual(Endpoint.staffSupportContacts().queryItems, [.init(name: "limit", value: "30")])
        XCTAssertEqual(
            Endpoint.staffSupportContact(contactId: "contact").queryItems,
            [.init(name: "limit", value: "50")]
        )
    }
}
