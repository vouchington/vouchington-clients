import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class StaffSupportSurfaceInteractionTests: NativeRouteSurfaceViewModelTestCase {
    func testFailedThreadSelectionKeepsSidebarErrorAndRetryVisible() async throws {
        let client = try makeClient()
        let viewModel = StaffSupportViewModel(
            client: client, administratorId: nil, mode: .threads, routeMatch: nil
        )
        viewModel.threads = [thread(id: "thread-1")]
        let threadPath = "/api/v1/support/threads/thread-1"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers[messagesPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.suspendResponse(path: threadPath)
        CannedFeedURLProtocol.suspendResponse(path: messagesPath)
        let threadBarrier = CannedFeedURLProtocol.requestBarrier(path: threadPath, method: "GET")
        let messagesBarrier = CannedFeedURLProtocol.requestBarrier(path: messagesPath, method: "GET")

        var revealCount = 0
        let surface = StaffSupportSurface(client: nil, administratorId: nil, mode: .threads, routeMatch: nil)
        let sidebar = try surface.staffSupportSidebar(viewModel, revealDetail: { revealCount += 1 }).inspect()
        try sidebar.find(button: "Subject thread-1").tap()
        _ = try await threadBarrier.wait()
        _ = try await messagesBarrier.wait()
        CannedFeedURLProtocol.releaseResponse(path: threadPath)
        CannedFeedURLProtocol.releaseResponse(path: messagesPath)
        await waitUntil { viewModel.errorMessage != nil }

        XCTAssertEqual(revealCount, 0)
        let failedSidebar = try surface.staffSupportSidebar(viewModel).inspect()
        XCTAssertNoThrow(try failedSidebar.find(button: "Try Again"))
    }

    func testFailedContactSelectionKeepsSidebarErrorAndRetryVisible() async throws {
        let client = try makeClient()
        let viewModel = StaffSupportViewModel(
            client: client, administratorId: nil, mode: .contacts, routeMatch: nil
        )
        viewModel.contacts = [contact(id: "contact-1")]
        let contactPath = "/api/v1/support/contacts/contact-1"
        CannedFeedURLProtocol.handlers[contactPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.suspendResponse(path: contactPath)
        let contactBarrier = CannedFeedURLProtocol.requestBarrier(path: contactPath, method: "GET")

        var revealCount = 0
        let surface = StaffSupportSurface(client: nil, administratorId: nil, mode: .contacts, routeMatch: nil)
        let sidebar = try surface.staffSupportSidebar(viewModel, revealDetail: { revealCount += 1 }).inspect()
        try sidebar.find(button: "contact-1@example.test").tap()
        _ = try await contactBarrier.wait()
        CannedFeedURLProtocol.releaseResponse(path: contactPath)
        await waitUntil { viewModel.errorMessage != nil }

        XCTAssertEqual(revealCount, 0)
        let failedSidebar = try surface.staffSupportSidebar(viewModel).inspect()
        XCTAssertNoThrow(try failedSidebar.find(button: "Try Again"))
    }

    private func thread(id: String) -> SupportThread {
        decode(
            SupportThread.self,
            "{\"id\":\"" + id + "\",\"support_contact_id\":\"contact-1\",\"subject\":\"Subject " + id + "\",\"conversation_id\":null,\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\",\"assigned_at\":null,\"assigned_to_id\":null,\"resolved_at\":null,\"resolved_by_id\":null,\"status\":\"open\",\"contact_user_id\":null}"
        )
    }

    private func contact(id: String) -> SupportContact {
        decode(
            SupportContact.self,
            "{\"id\":\"" + id + "\",\"email_address\":\"" + id + "@example.test\",\"name\":\"Contact\",\"user_id\":null,\"notes\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"}"
        )
    }

    private func decode<T: Decodable>(_: T.Type, _ json: String) -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try! decoder.decode(T.self, from: Data(json.utf8))
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 300 {
            if condition() {
                return
            }
            await Task.yield()
        }
        XCTFail("Timed out waiting for the expected state")
    }
}
