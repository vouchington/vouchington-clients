import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsViewModelDataRequestTests: NativeRouteSurfaceViewModelTestCase {
    func testRequestDataExportPublishesReadyDownloadURL() async throws {
        seedPollingResponses()
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/data-request"] = (
            dataRequestJSON(status: "ready", downloadURL: "https://example.com/export.zip"),
            200
        )
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedURLs = []
        await viewModel.requestDataExport()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/users/user-1/data-request"])
        XCTAssertEqual(viewModel.dataRequest?.status, .ready)
        XCTAssertEqual(viewModel.dataRequestDownloadURL, URL(string: "https://example.com/export.zip"))
    }

    func testExpiredReadyExportDoesNotExposeDownloadURL() async throws {
        seedPollingResponses()
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/data-request"] = (
            dataRequestJSON(
                status: "ready",
                downloadURL: "https://example.com/export.zip",
                expiresAt: "2026-03-08T10:00:00Z"
            ),
            200
        )
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertNil(viewModel.dataRequestDownloadURL)
        XCTAssertTrue(viewModel.shouldPollDataRequest)
    }

    func testPollDataRequestAdvancesUntilReadyDownloadUrlAppears() async throws {
        seedPollingResponses()

        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-1/data-request"] = [
            (dataRequestJSON(status: "pending", downloadURL: nil), 200, 0),
            (dataRequestJSON(status: "processing", downloadURL: nil), 200, 0),
            (dataRequestJSON(status: "ready", downloadURL: "not-a-url"), 200, 0),
            (dataRequestJSON(status: "ready", downloadURL: "https://example.com/export.zip"), 200, 0)
        ]

        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        XCTAssertTrue(viewModel.shouldPollDataRequest)
        XCTAssertNil(viewModel.dataRequestDownloadURL)

        await viewModel.pollDataRequestIfNeeded(sleep: { _ in })

        XCTAssertEqual(viewModel.dataRequest?.status, .ready)
        XCTAssertEqual(viewModel.dataRequestDownloadURL, URL(string: "https://example.com/export.zip"))
        XCTAssertFalse(viewModel.shouldPollDataRequest)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users/user-1/data-request" }.count,
            4
        )
    }

    private func seedPollingResponses() {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Native bio"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/push-subscriptions"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data(#"{"membership":null}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/plans"] = (
            Data(#"{"plans":{}}"#.utf8),
            200
        )
    }

    private func dataRequestJSON(
        status: String,
        downloadURL: String?,
        expiresAt: String = "2027-03-08T10:00:00Z"
    ) -> Data {
        let downloadURLJSON = downloadURL.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {
              "id": "req-1",
              "status": "\(status)",
              "created_at": "2026-03-01T10:00:00Z",
              "expires_at": "\(expiresAt)",
              "download_url": \(downloadURLJSON)
            }
            """.utf8
        )
    }
}
