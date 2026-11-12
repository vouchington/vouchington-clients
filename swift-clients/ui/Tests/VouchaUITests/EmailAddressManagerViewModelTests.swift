import Foundation
import Observation
import SwiftUI
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class EmailAddressManagerViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.discardPendingResponses()
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func makeViewModel() -> EmailAddressManagerViewModel {
        EmailAddressManagerViewModel(client: makeClient())
    }

    func testManagerRendersInitialEmailEntryPhase() throws {
        let view = EmailAddressManager(client: makeClient())
        XCTAssertNoThrow(try view.inspect().find(text: "No email addresses yet"))
        XCTAssertNoThrow(try view.inspect().find(ViewType.TextField.self))
    }

    func testManagerRendersTenCharacterCodePlaceholderAfterRequest() async throws {
        defer { CannedFeedURLProtocol.requestHandlers = [:] }
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false}}"#.utf8), 200
        )
        CannedFeedURLProtocol.requestHandlers["POST /api/v1/my/email-addresses"] = (
            Data(#"{"email_address":"user@example.com"}"#.utf8), 200
        )
        let view = EmailAddressManager(client: makeClient())
            .environment(\.locale, Locale(identifier: "en"))
        try await ViewHosting.host(view) {
            try view.inspect().find(ViewType.TextField.self).setInput("user@example.com")
            await waitForManagerControl {
                (try? view.inspect().find(button: "Send verification code").isDisabled()) == false
            }
            try view.inspect().find(button: "Send verification code").tap()
            await waitForManagerControl {
                (try? view.inspect().find(button: "Verify")) != nil
            }
            XCTAssertEqual(
                try view.inspect().find(ViewType.TextField.self).labelView().text().string(),
                "10-character code"
            )
        }
    }

    private func waitForManagerControl(_ isReady: @escaping @MainActor () -> Bool) async {
        let ready = expectation(description: "email verification control is ready")
        var active = true
        defer { active = false }
        func observe() {
            guard active else { return }
            let matched = withObservationTracking { isReady() } onChange: {
                Task { @MainActor in observe() }
            }
            if matched {
                active = false
                ready.fulfill()
            }
        }
        observe()
        await fulfillment(of: [ready], timeout: 2)
    }

    func testManagerRendersRecoveryExplanation() throws {
        let view = EmailAddressManager(client: makeClient(), recoveryMode: true)
        XCTAssertNoThrow(try view.inspect().find(text: "Verify an email address"))
        XCTAssertNoThrow(try view.inspect().find(text: "This action requires a verified email address."))
    }

    func testEmailAddressIdentityUsesAddress() throws {
        let data = Data(
            #"{"email_address":"identity@example.com","is_primary":false,"created_at":"2026-07-11T12:00:00Z"}"#.utf8
        )
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let address = try decoder.decode(EmailAddress.self, from: data)
        XCTAssertEqual(address.id, "identity@example.com")
    }

    func testRecoveryModifierPreservesContentWhileDismissed() throws {
        let view = Text("Action").emailVerificationRecovery(
            client: nil,
            gate: EmailVerificationGatedMutation()
        )
        XCTAssertNoThrow(try view.inspect().find(text: "Action"))
    }

    func testLoadEmptyList() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses"] = (
            Data(
                #"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8
            ),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()
        XCTAssertTrue(viewModel.emailAddresses.isEmpty)
        XCTAssertEqual(viewModel.phase, .enterEmail)
    }

    func testRequestUsesNormalizedResponseAddress() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses"] = (
            Data(#"{"email_address":"user@example.com"}"#.utf8),
            200
        )
        let viewModel = makeViewModel()
        viewModel.emailAddress = " User@Example.com "
        await viewModel.requestVerification()
        XCTAssertEqual(viewModel.emailAddress, "user@example.com")
        XCTAssertEqual(viewModel.phase, .enterCode)
    }

    func testVerifyCompletesWithoutRetryingOriginalAction() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses/user@example.com/verifications"] = (
            Data(
                #"{"results":[{"email_address":"user@example.com","is_primary":true,"created_at":"2026-07-11T12:00:00Z"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
        let viewModel = makeViewModel()
        viewModel.emailAddress = "user@example.com"
        viewModel.verificationCode = "ABCDEFGH23"
        viewModel.phase = .enterCode
        await viewModel.verify()
        XCTAssertEqual(viewModel.phase, .verified)
        XCTAssertEqual(viewModel.statusMessage, UiMessage(.nativeSwiftSettingsEmailVerifiedRetry))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
    }

    func testInvalidCodeStaysOnCodeStep() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses/user@example.com/verifications"] = (
            Data(#"{"message":"Invalid or expired verification code"}"#.utf8),
            400
        )
        let viewModel = makeViewModel()
        viewModel.emailAddress = "user@example.com"
        viewModel.verificationCode = "BADCODE234"
        viewModel.phase = .enterCode
        await viewModel.verify()
        XCTAssertEqual(viewModel.phase, .enterCode)
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testMalformedCodeNeverSendsVerification() async {
        let viewModel = makeViewModel()
        viewModel.emailAddress = "user@example.com"
        viewModel.phase = .enterCode
        for code in ["123456", "ABCDEFGH01", "ABCDEFGH89", "ＡBCDEFGH23", "abcdefgh2 "] {
            viewModel.verificationCode = code
            XCTAssertFalse(viewModel.canVerifyCode)
            await viewModel.verify()
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(viewModel.phase, .enterCode)
    }

    func testRejectedCodeNeverRetriesOrCompletesVerification() async throws {
        let path = "/api/v1/my/email-addresses/user@example.com/verifications"
        for (status, message) in [
            (400, "Invalid code"),
            (400, "Replaced code"),
            (400, "Purpose mismatch"),
            (429, "Quota exhausted")
        ] {
            CannedFeedURLProtocol.capturedURLs = []
            CannedFeedURLProtocol.handlers[path] = try (
                JSONSerialization.data(withJSONObject: ["message": message]), status
            )
            let viewModel = makeViewModel()
            viewModel.emailAddress = "user@example.com"
            viewModel.verificationCode = "abcdefgh23"
            viewModel.phase = .enterCode

            await viewModel.verify()

            XCTAssertEqual(viewModel.verificationCode, "ABCDEFGH23")
            XCTAssertEqual(viewModel.phase, .enterCode)
            XCTAssertTrue(viewModel.canVerifyCode)
            XCTAssertNotNil(viewModel.errorMessage)
            XCTAssertNil(viewModel.statusMessage)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
        }
    }

    func testStartAnotherAddressResetsTransientState() async {
        let viewModel = makeViewModel()
        viewModel.emailAddress = "user@example.com"
        viewModel.verificationCode = "ABCDEFGH23"
        viewModel.phase = .enterCode
        await viewModel.verify()

        viewModel.startAnotherAddress()

        XCTAssertEqual(viewModel.emailAddress, "")
        XCTAssertEqual(viewModel.verificationCode, "")
        XCTAssertEqual(viewModel.phase, .enterEmail)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.statusMessage)
    }

    func testLoadDecodesEmailAddressModel() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses"] = (
            Data(
                #"{"results":[{"email_address":"user@example.com","is_primary":true,"created_at":"2026-07-11T12:00:00Z"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()
        XCTAssertEqual(viewModel.emailAddresses.first?.emailAddress, "user@example.com")
        XCTAssertEqual(viewModel.emailAddresses.first?.isPrimary, true)
    }

    func testLoadMoreAppendsTheNextPageAndUsesTheCursor() async {
        let path = "/api/v1/my/email-addresses"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (emailPage("first@example.com", endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (emailPage("second@example.com", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = makeViewModel()

        await viewModel.load()
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.emailAddresses.map(\.emailAddress), ["first@example.com", "second@example.com"])
        let queryItems = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems
        }
        XCTAssertEqual(queryItems?.first(where: { $0.name == "after" })?.value, "cursor-1")
        XCTAssertFalse(viewModel.pagination.hasMore)
    }

    func testCancelledLoadMorePreservesTheLoadedPageWithoutAnError() async {
        let path = "/api/v1/my/email-addresses"
        CannedFeedURLProtocol.handlers[path] = (
            emailPage("first@example.com", endCursor: "cursor-1", hasNextPage: true),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.suspendResponse(path: path)

        let loadMore = Task { await viewModel.loadMore() }
        for _ in 0 ..< 100 where !CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
            await Task.yield()
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasSuspendedResponse(path: path))
        loadMore.cancel()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await loadMore.value

        XCTAssertEqual(viewModel.emailAddresses.map(\.emailAddress), ["first@example.com"])
        XCTAssertNil(viewModel.pagination.lastError)
        XCTAssertFalse(viewModel.pagination.isLoading)
        XCTAssertTrue(viewModel.pagination.hasMore)
    }

    func testUnexpectedDecodeErrorIsPresented() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-addresses"] = (Data("not-json".utf8), 200)
        let viewModel = makeViewModel()
        await viewModel.load()
        XCTAssertNotNil(viewModel.errorMessage)
    }

    private func emailPage(_ address: String, endCursor: String?, hasNextPage: Bool) -> Data {
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            #"""
            {"results":[{"email_address":"\#(address)","is_primary":false,
            "created_at":"2026-07-11T12:00:00Z"}],"page_info":{"has_next_page":\#(hasNextPage),
            "start_cursor":null,"end_cursor":\#(cursor)}}
            """#.utf8
        )
    }
}
