import Foundation
@testable import VouchaAPI
import VouchaCore
import XCTest

final class RssFeedDeletionTests: XCTestCase {
    private let feedId = "70000000-0000-7000-8000-000000003680"

    override func tearDown() {
        InvitationURLProtocol.reset()
        super.tearDown()
    }

    func testPublishedAcceptedAndRetainedImportFixturesHaveDistinctOutcomesWithoutPolling() async throws {
        for (fixture, status, expected) in [
            ("accepted", 202, RssFeedDeletionOutcome.accepted(rssFeedId: feedId)),
            ("retained-import-conflict", 409, .blockedByAdminImports)
        ] {
            InvitationURLProtocol.reset()
            let outcome = try await perform(
                data: ApiFixtureLoader.data("native.rss-feeds.delete.\(fixture)"),
                status: status
            )
            XCTAssertEqual(outcome, expected)
            XCTAssertEqual(InvitationURLProtocol.capturedMethods, ["DELETE"])
            XCTAssertEqual(InvitationURLProtocol.capturedURLs.map(\.path), ["/api/v1/rss-feeds/\(feedId)"])
        }
    }

    func testConflictClassificationUsesExactCodeAndNeverMessage() async throws {
        let outcome = try await perform(
            data: Data(#"{"code":"RSS_FEED_ADMIN_IMPORT_REFERENCED","message":"Changed producer prose"}"#.utf8),
            status: 409
        )
        XCTAssertEqual(outcome, .blockedByAdminImports)
        for body in [
            #"{"code":"OTHER","message":"Feed is referenced by admin import history"}"#,
            #"{"message":"Feed is referenced by admin import history"}"#,
            #"{"code":"rss_feed_admin_import_referenced"}"#,
            "invalid-json"
        ] {
            await assertFailure(data: Data(body.utf8), status: 409)
        }
    }

    func testAcceptanceRequires202AndDeletingPayload() async {
        let accepted = ApiFixtureLoader.data("native.rss-feeds.delete.accepted")
        for status in [200, 201, 204, 400, 401, 403, 404, 500] {
            await assertFailure(data: accepted, status: status)
        }
        let conflict = ApiFixtureLoader.data("native.rss-feeds.delete.retained-import-conflict")
        for status in [202, 400, 403, 500] {
            await assertFailure(data: conflict, status: status)
        }
        for body in [
            "{}",
            "",
            "invalid-json",
            #"{"rss_feed_id":"feed","status":"deleted"}"#,
            #"{"rss_feed_id":12,"status":"deleting"}"#
        ] {
            await assertFailure(data: Data(body.utf8), status: 202)
        }
    }

    func testTransportFailureRemainsNetworkError() async throws {
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [SessionRefreshURLProtocol.self]
        )
        var failure: VouchaError?
        do { _ = try await client.deleteRssFeed(rssFeedId: feedId) } catch { failure = error as? VouchaError }
        var code: URLError.Code?
        if case let .network(error)? = failure { code = error.code }
        XCTAssertEqual(code, .unsupportedURL)
    }

    private func perform(data: Data, status: Int) async throws -> RssFeedDeletionOutcome {
        InvitationURLProtocol.requestHandlers = ["DELETE /api/v1/rss-feeds/\(feedId)": (data, status)]
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [InvitationURLProtocol.self]
        )
        return try await client.deleteRssFeed(rssFeedId: feedId)
    }

    private func assertFailure(data: Data, status: Int) async {
        var failure: Error?
        do { _ = try await perform(data: data, status: status) } catch { failure = error }
        XCTAssertTrue(failure is VouchaError)
    }
}
