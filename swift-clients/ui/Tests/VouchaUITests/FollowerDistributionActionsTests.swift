import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class FollowerDistributionActionsTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    override func tearDown() {
        CannedFeedURLProtocol.discardPendingResponses()
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    func testRenderedShareActionSendsSuccessAndFailureRequests() async throws {
        let path = "/api/v1/posts/post-1/shares"
        CannedFeedURLProtocol.handlers[path] = (acceptedResponse(), 202)
        let success = actions(target: .post("post-1"))

        try await ViewHosting.host(success) {
            let menu = try success.inspect().find(ViewType.Menu.self)
            XCTAssertNoThrow(try menu.labelView().image())
            XCTAssertEqual(try menu.accessibilityLabel().string(), "Follower actions")
            try menu.find(button: "Share with followers").tap()
            await waitUntil { CannedFeedURLProtocol.hasCapturedRequest(path: path) }

            XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.last?.method, "POST")
        }

        CannedFeedURLProtocol.reset()
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 500)
        let failure = actions(target: .post("post-1"))

        try await ViewHosting.host(failure) {
            try failure.inspect().find(ViewType.Menu.self).find(button: "Share with followers").tap()
            await waitUntil { CannedFeedURLProtocol.capturedPathCount(path) == 1 }
            XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.last?.method, "POST")
        }
    }

    func testRenderedSendSheetSearchesSelectsAndSendsRecipients() async throws {
        let followersPath = "/api/v1/users/viewer-1/users/followers"
        let sendPath = "/api/v1/posts/post-1/sends"
        CannedFeedURLProtocol.handlers[followersPath] = (followersPage(ids: [recipientId(1), recipientId(2)]), 200)
        CannedFeedURLProtocol.handlers[sendPath] = (acceptedResponse(), 202)
        let viewModel = makeViewModel()
        let sut = sendSheet(viewModel)

        try await ViewHosting.host(sut) {
            XCTAssertNoThrow(try sut.inspect().find(text: "All followers"))
            XCTAssertFalse(try sut.inspect().find(button: "Send").isDisabled())

            try sut.inspect().find(ViewType.Picker.self).select(value: false)
            await waitUntil { (try? sut.inspect().find(ViewType.TextField.self)) != nil }
            try sut.inspect().find(ViewType.TextField.self).setInput("alex")
            await waitUntil { viewModel.recipients.count == 2 }
            try sut.inspect().find(ViewType.Toggle.self).tap()
            XCTAssertEqual(viewModel.selectedRecipientIds, [recipientId(1)])
            XCTAssertFalse(try sut.inspect().find(button: "Send").isDisabled())
            try sut.inspect().find(button: "Send").tap()
            await waitUntil { CannedFeedURLProtocol.hasCapturedRequest(path: sendPath) }

            let body = try requestJSON(CannedFeedURLProtocol.capturedRequests.last?.body)
            XCTAssertEqual(body["audience"] as? String, "selected_followers")
            XCTAssertEqual(body["recipient_user_ids"] as? [String], [recipientId(1)])
        }
    }

    func testRenderedSendFailureShowsAndClearsSheetError() async throws {
        let sendPath = "/api/v1/posts/post-1/sends"
        CannedFeedURLProtocol.handlers[sendPath] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        let sut = sendSheet(viewModel)

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Send").tap()
            await waitUntil { CannedFeedURLProtocol.hasCapturedRequest(path: sendPath) }
            await waitUntil { viewModel.error != nil }
            XCTAssertNotNil(viewModel.error)
            viewModel.clearError()
            XCTAssertNil(viewModel.error)
        }
    }

    func testRenderedSendSheetLoadsAdditionalRecipientPage() async throws {
        let followersPath = "/api/v1/users/viewer-1/users/followers"
        CannedFeedURLProtocol.queuedHandlers[followersPath] = [
            (followersPage(ids: [recipientId(1)], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (followersPage(ids: [recipientId(2)]), 200, 0)
        ]
        let viewModel = makeViewModel()
        viewModel.sendsToAllFollowers = false
        let sut = sendSheet(viewModel)

        try await ViewHosting.host(sut) {
            try sut.inspect().find(ViewType.TextField.self).setInput("alex")
            await waitUntil { viewModel.canLoadMoreRecipients }
            XCTAssertNoThrow(try sut.inspect().find(button: "Load more followers"))
            try sut.inspect().find(button: "Load more followers").tap()
            await waitUntil { viewModel.recipients.map(\.id) == [self.recipientId(1), self.recipientId(2)] }

            let requests = CannedFeedURLProtocol.capturedRequests
            XCTAssertEqual(queryValue(named: "after", in: requests.last?.url), "cursor-1")
            XCTAssertThrowsError(try sut.inspect().find(button: "Load more followers"))
        }
    }

    func testRenderedSendSheetKeepsSelectedRecipientsVisibleAcrossSearches() async throws {
        let followersPath = "/api/v1/users/viewer-1/users/followers"
        CannedFeedURLProtocol.queuedHandlers[followersPath] = [
            (followersPage(ids: [recipientId(1)]), 200, 0),
            (followersPage(ids: [recipientId(2)], startIndex: 2), 200, 0)
        ]
        let viewModel = makeViewModel()
        viewModel.sendsToAllFollowers = false
        let sut = sendSheet(viewModel)

        try await ViewHosting.host(sut) {
            let search = try sut.inspect().find(ViewType.TextField.self)
            try search.setInput("alex")
            await waitUntil { viewModel.recipients.map(\.id) == [self.recipientId(1)] }
            try sut.inspect().find(ViewType.Toggle.self).tap()

            try search.setInput("bea")
            await waitUntil { viewModel.recipients.map(\.id) == [self.recipientId(2)] }
            XCTAssertNoThrow(try sut.inspect().find(text: "follower0"))
            try sut.inspect().find(ViewType.Toggle.self, containing: "follower0").tap()
            XCTAssertTrue(viewModel.selectedRecipientIds.isEmpty)
        }
    }

    private func actions(target: FollowerDistributionTarget) -> some View {
        FollowerDistributionActions(client: makeClient(), currentUserId: "viewer-1", target: target)
            .environment(\.locale, Locale(identifier: "en_US"))
    }

    private func sendSheet(_ viewModel: FollowerDistributionViewModel) -> some View {
        FollowerDistributionSendSheet(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
    }

    private func makeViewModel() -> FollowerDistributionViewModel {
        .init(client: makeClient(), currentUserId: "viewer-1", target: .post("post-1"))
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: .init(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test"),
            cookieStorage: .init(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func followersPage(
        ids: [String],
        hasMore: Bool = false,
        endCursor: String? = nil,
        startIndex: Int = 0
    ) -> Data {
        let users = ids.enumerated().map { index, id in
            #"{"id":"\#(id)","username":"follower\#(startIndex + index)"}"#
        }.joined(separator: ",")
        let cursor = endCursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(users)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(cursor)}}"#.utf8
        )
    }

    private func acceptedResponse() -> Data {
        Data(#"{"status":"accepted","distribution_id":"distribution-1"}"#.utf8)
    }

    private func recipientId(_ number: Int) -> String {
        String(format: "01900000-0000-7000-8000-%012d", number)
    }

    private func queryValue(named name: String, in url: URL?) -> String? {
        url
            .flatMap {
                URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == name }?.value
            }
    }

    private func requestJSON(_ body: String?) throws -> [String: Any] {
        let data = try Data(XCTUnwrap(body).utf8)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        let clock = ContinuousClock()
        let deadline = clock.now.advanced(by: .seconds(3))
        while !condition(), clock.now < deadline {
            try? await clock.sleep(for: .milliseconds(10))
        }
        XCTAssertTrue(condition(), "Timed out waiting for rendered follower-distribution state")
    }
}
