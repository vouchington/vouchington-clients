import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidConversationHistoryTests: XCTestCase {
    func testConversationCreationAndMessageCursorPaginationUseContractRoutes() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let host = fixture.host
        let recorder = fixture.recorder
        let client = fixture.client
        defer { AndroidTranscriptURLProtocol.uninstall(host: host) }
        let conversationID = "0198ffff-0000-7000-8000-000000000001"
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8)
        )
        recorder.enqueue(
            path: "/api/v1/conversations",
            data: AndroidTranscriptTestFactory.conversationResponse(id: conversationID, title: "Chat")
        )
        try recorder.enqueue(
            path: "/api/v1/my/conversations/\(conversationID)/messages",
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: client, userID: "fixture-user")
        await viewModel.createConversation()

        XCTAssertEqual(viewModel.selectedConversationID, conversationID)
        XCTAssertEqual(viewModel.messages.map(\.id), ["0198ffff-0001-7000-8000-000000000002"])
        XCTAssertTrue(recorder.requestSnapshot(path: "/api/v1/conversations").first?.httpMethod == "POST")
    }

    func testOlderMessagePageForwardsCursorAndDeduplicatesServerIDs() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let host = fixture.host
        let recorder = fixture.recorder
        let client = fixture.client
        defer { AndroidTranscriptURLProtocol.uninstall(host: host) }
        let conversationID = "0198ffff-0000-7000-8000-000000000001"
        let path = "/api/v1/my/conversations/\(conversationID)/messages"
        try recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try recorder.enqueue(path: path, data: AndroidTranscriptFixtures.data("native.chat.page-1"))
        try recorder.enqueue(path: path, data: AndroidTranscriptFixtures.data("native.chat.page-2"))

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: client, userID: "fixture-user")
        await viewModel.loadOlderMessages()

        XCTAssertEqual(viewModel.messages.map(\.id), [
            "0198ffff-0001-7000-8000-000000000001",
            "0198ffff-0001-7000-8000-000000000002"
        ])
        let requests = recorder.requestSnapshot(path: path)
        let query = try XCTUnwrap(URLComponents(url: XCTUnwrap(requests.last?.url), resolvingAgainstBaseURL: false)?
            .queryItems)
        XCTAssertTrue(query.contains(URLQueryItem(
            name: "after",
            value: "eyJpZCI6IjAxOThmZmZmLTAwMDEtNzAwMC04MDAwLTAwMDAwMDAwMDAwMiJ9"
        )))
    }

}
