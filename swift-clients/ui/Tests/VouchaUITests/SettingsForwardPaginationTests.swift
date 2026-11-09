import Foundation
import Observation
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SettingsForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testSettingsContinuationFailuresPreservePagesThenRetriesAppendAndFilterRevokedRows() async throws {
        let apiKeysPath = "/api/v1/my/api-keys"
        let pushesPath = "/api/v1/my/notifications/push-subscriptions"
        let sessionsPath = "/api/v1/auth/sessions"
        CannedFeedURLProtocol.queuedHandlers[apiKeysPath] = failureThen(
            apiKeyPage(ids: ["key-2", "key-revoked"])
        )
        CannedFeedURLProtocol.queuedHandlers[pushesPath] = failureThen(
            pushPage(ids: ["push-2", "push-revoked"])
        )
        CannedFeedURLProtocol.queuedHandlers[sessionsPath] = failureThen(
            sessionPage(ids: ["session-2", "session-revoked"])
        )
        let viewModel = try SettingsViewModel(client: makeClient())
        let apiKeys: SettingsListResponse<ApiKey> = try decode(apiKeyPage(ids: ["key-1"]))
        let pushes: SettingsListResponse<WebPushSubscription> = try decode(pushPage(ids: ["push-1"]))
        let sessions: SettingsListResponse<AuthSession> = try decode(sessionPage(ids: ["session-1"]))
        viewModel.apiKeyPagination.reset(items: apiKeys.results)
        viewModel.apiKeyPagination.restoreContinuation(endCursor: "keys-cursor", hasMore: true)
        viewModel.pushSubscriptionPagination.reset(items: pushes.results)
        viewModel.pushSubscriptionPagination.restoreContinuation(endCursor: "pushes-cursor", hasMore: true)
        viewModel.sessionPagination.reset(items: sessions.results)
        viewModel.sessionPagination.restoreContinuation(endCursor: "sessions-cursor", hasMore: true)
        viewModel.revokedApiKeyIds = ["key-revoked"]
        viewModel.revokedPushSubscriptionIds = ["push-revoked"]
        viewModel.revokedSessionIds = ["session-revoked"]

        await viewModel.loadMoreApiKeys()
        await viewModel.loadMorePushSubscriptions()
        await viewModel.loadMoreSessions()

        XCTAssertEqual(viewModel.apiKeys.map(\.id), ["key-1"])
        XCTAssertEqual(viewModel.pushSubscriptions.map(\.id), ["push-1"])
        XCTAssertEqual(viewModel.sessions.map(\.id), ["session-1"])
        XCTAssertNotNil(viewModel.apiKeyPagination.lastError)
        XCTAssertNotNil(viewModel.pushSubscriptionPagination.lastError)
        XCTAssertNotNil(viewModel.sessionPagination.lastError)

        await viewModel.loadMoreApiKeys()
        await viewModel.loadMorePushSubscriptions()
        await viewModel.loadMoreSessions()

        XCTAssertEqual(viewModel.apiKeys.map(\.id), ["key-1", "key-2"])
        XCTAssertEqual(viewModel.pushSubscriptions.map(\.id), ["push-1", "push-2"])
        XCTAssertEqual(viewModel.sessions.map(\.id), ["session-1", "session-2"])
        XCTAssertNil(viewModel.apiKeyPagination.lastError)
        XCTAssertNil(viewModel.pushSubscriptionPagination.lastError)
        XCTAssertNil(viewModel.sessionPagination.lastError)
        XCTAssertEqual(queries(for: apiKeysPath), ["limit=25&after=keys-cursor", "limit=25&after=keys-cursor"])
        XCTAssertEqual(queries(for: pushesPath), [
            "limit=25&after=pushes-cursor",
            "limit=25&after=pushes-cursor"
        ])
        XCTAssertEqual(queries(for: sessionsPath), [
            "limit=25&after=sessions-cursor",
            "limit=25&after=sessions-cursor"
        ])
    }

    func testEmptyRawSessionPageKeepsRenderedPagerAndForwardsOpaqueCursor() async throws {
        let path = "/api/v1/auth/sessions"
        let cursor = "opaque+/=session-boundary"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(
                #"{"results":[],"page_info":{"has_next_page":true,"start_cursor":null,"end_cursor":"opaque+/=session-boundary"}}"#
                    .utf8
            ), 200, 0),
            (sessionPage(ids: ["later-session"]), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = SettingsViewModel(client: client)
        let firstPage: Page<AuthSession> = try await client.send(.authSessions())
        viewModel.replaceSessionPage(firstPage)
        let sut = SettingsSurface(viewModel: viewModel)

        XCTAssertTrue(viewModel.sessions.isEmpty)
        XCTAssertThrowsError(try sut.sessionsSection.inspect().find(text: "No active sessions"))
        let revokeAll = try sut.sessionsSection.inspect().find(button: "Revoke All Sessions")
        XCTAssertFalse(revokeAll.isDisabled())
        try revokeAll.tap()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        let appended = expectation(description: "later session appended")
        observeSessionAppend(viewModel, completion: appended)
        try sut.sessionsSection.inspect().find(button: "Load more").tap()
        await fulfillment(of: [appended], timeout: 2)

        XCTAssertEqual(viewModel.sessions.map(\.id), ["later-session"])
        let requestedCursor = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(
            URLComponents(url: requestedCursor, resolvingAgainstBaseURL: false)?
                .queryItems?.first(where: { $0.name == "after" })?.value,
            cursor
        )
        XCTAssertNoThrow(try sut.sessionsSection.inspect().find(text: "Mac"))
        XCTAssertThrowsError(try sut.sessionsSection.inspect().find(button: "Load more"))
    }

    func testTerminalEmptySessionPageKeepsRevokeAllAvailableAndSafe() async throws {
        var signedOut = false
        let viewModel = try SettingsViewModel(client: makeClient(), onLogoutRequired: { signedOut = true })
        let sut = SettingsSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.sessionsSection.inspect().find(text: "No active sessions"))
        XCTAssertThrowsError(try sut.sessionsSection.inspect().find(button: "Load more"))
        let revokeAll = try sut.sessionsSection.inspect().find(button: "Revoke All Sessions")
        XCTAssertFalse(revokeAll.isDisabled())
        try revokeAll.tap()
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)

        viewModel.state = .loading
        XCTAssertTrue(try sut.sessionsSection.inspect().find(button: "Revoke All Sessions").isDisabled())

        viewModel.state = .idle
        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions/revocations"] = (Data("{}".utf8), 200)
        await viewModel.revokeAllSessions()

        XCTAssertTrue(signedOut)
        XCTAssertTrue(viewModel.sessions.isEmpty)
        XCTAssertFalse(viewModel.sessionPagination.hasMore)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/auth/sessions/revocations"])
    }

    private func observeSessionAppend(_ viewModel: SettingsViewModel, completion: XCTestExpectation) {
        if !viewModel.sessions.isEmpty {
            completion.fulfill()
            return
        }
        withObservationTracking {
            _ = viewModel.sessions
        } onChange: {
            Task { @MainActor in
                self.observeSessionAppend(viewModel, completion: completion)
            }
        }
    }

    private func failureThen(_ page: Data) -> [(Data, Int, TimeInterval)] {
        [(Data(#"{"message":"offline"}"#.utf8), 503, 0), (page, 200, 0)]
    }

    private func apiKeyPage(ids: [String]) -> Data {
        let rows = ids.map { id in
            #"{"id":"\#(id)","user_id":"user-1","prefix":"voucha","type":"rss","label":"\#(id)","permissions":[],"created_at":"2026-01-01T00:00:00Z","last_used_at":null,"revoked_at":null,"expires_at":null,"expiry_reminder_sent_at":null,"replaced_by_api_key_id":null,"updated_at":"2026-01-01T00:00:00Z"}"#
        }.joined(separator: ",")
        return listPage(rows)
    }

    private func pushPage(ids: [String]) -> Data {
        let rows = ids.map { id in
            #"{"id":"\#(id)","user_id":"user-1","endpoint":"https://push.example/\#(id)","p256dh":"key","auth":"auth","expiration_time_ms":null,"user_agent":"Safari","last_success_at":null,"last_failure_at":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}"#
        }.joined(separator: ",")
        return listPage(rows)
    }

    private func sessionPage(ids: [String]) -> Data {
        let rows = ids.map { id in
            #"{"id":"\#(id)","device_id":"device-1","device_name":"Mac","user_agent":"Safari","ip_address":"203.0.113.1","created_at":"2026-01-01T00:00:00Z","last_seen_at":null,"expires_at":null,"is_current":false}"#
        }.joined(separator: ",")
        return listPage(rows)
    }

    private func listPage(_ rows: String) -> Data {
        Data(
            #"{"results":[\#(rows)],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                .utf8
        )
    }

    private func decode<T: Decodable & Sendable>(_ data: Data) throws -> SettingsListResponse<T> {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(SettingsListResponse<T>.self, from: data)
    }

    private func queries(for path: String) -> [String?] {
        CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.map(\.query)
    }
}
