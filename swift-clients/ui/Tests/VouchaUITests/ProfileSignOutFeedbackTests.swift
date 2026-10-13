import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ProfileSignOutFeedbackTests: XCTestCase {
    func testFailedGrantCleanupIsVisibleOnProfileAndRetrySignsOut() async throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            turnstileSiteKey: "test-site-key"
        )
        let cookieStorage = HTTPCookieStorage()
        let client = APIClient(
            config: config, cookieStorage: cookieStorage,
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let session = SessionManager(client: client, cookieStorage: cookieStorage)
        let user = try JSONDecoder.vouchaFixtureDecoder.decode(
            PrivateUser.self, from: PrivateUserTestFixture.userData(id: "member-a")
        )
        session.synchronize(with: user)
        let viewModel = ProfileViewModel(client: client, config: config, sessionManager: session)
        viewModel.identity = user
        session.setBeforeSignOut { _ in throw SignOutFailure.failed }

        await viewModel.signOut()

        let message = try XCTUnwrap(session.lastSignOutErrorMessage)
        XCTAssertTrue(session.isSignedIn)
        XCTAssertEqual(viewModel.signOutErrorMessage, .verbatim(message))
        XCTAssertEqual(try ProfileView(viewModel: viewModel).inspect().find(text: message).string(), message)

        session.setBeforeSignOut { _ in }
        CannedFeedURLProtocol.handlers["/api/v1/auth/logout"] = (Data("{}".utf8), 200)
        await viewModel.signOut()
        XCTAssertFalse(session.isSignedIn)
        XCTAssertNil(viewModel.signOutErrorMessage)
    }
}

private enum SignOutFailure: Error {
    case failed
}
