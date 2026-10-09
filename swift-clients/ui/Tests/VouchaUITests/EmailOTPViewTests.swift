import ViewInspector
@testable import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class EmailOTPViewTests: XCTestCase {
    func testEmailStepRequiresTurnstileBeforeSending() throws {
        let viewModel = makeViewModel()
        viewModel.email = "alice@example.com"
        let sut = EmailOTPEmailStep(viewModel: viewModel, showingTurnstile: .constant(false))

        XCTAssertNoThrow(try sut.inspect().find(button: "Verify"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Send Code"))
    }

    func testEmailStepShowsVerifiedState() throws {
        let viewModel = makeViewModel()
        viewModel.turnstileToken = "token"
        let sut = EmailOTPEmailStep(viewModel: viewModel, showingTurnstile: .constant(false))

        XCTAssertNoThrow(try sut.inspect().find(button: "Verified"))
    }

    func testFailedCodeRequestShowsErrorAndKeepsSendCodeRetryable() async throws {
        let viewModel = makeViewModel()
        viewModel.email = "alice@example.com"
        viewModel.turnstileToken = "token"

        await viewModel.requestCode()

        XCTAssertEqual(viewModel.step, .enterEmail)
        XCTAssertFalse(viewModel.isLoading)
        XCTAssertTrue(viewModel.canRequestCode)
        let error = try XCTUnwrap(uiEnglish(viewModel.errorMessage))
        XCTAssertFalse(error.isEmpty)
        let sut = EmailOTPEmailStep(viewModel: viewModel, showingTurnstile: .constant(false))
        XCTAssertEqual(try sut.inspect().find(text: error).string(), error)
        XCTAssertFalse(try sut.inspect().find(button: "Send Code").isDisabled())
    }

    func testCodeStepRendersVerificationControls() throws {
        let viewModel = makeViewModel()
        viewModel.email = "alice@example.com"
        viewModel.step = .enterCode
        let sut = EmailOTPCodeStep(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "Check your email").string(), "Check your email")
        XCTAssertNoThrow(try sut.inspect().find(button: "Verify Code"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Use a different email"))
    }

    func testEmailOTPViewRendersEmailStep() throws {
        let viewModel = makeViewModel()
        let sut = EmailOTPView(viewModel: viewModel, signInService: makeService(), turnstileSiteKey: "site-key") {}

        XCTAssertNoThrow(try sut.inspect().find(text: "Enter your email"))
    }

    func testMFAChallengeViewRendersLocalizedControlsAndError() throws {
        let viewModel = MFAChallengeViewModel(
            loginAttemptId: "attempt-1",
            signInService: makeService()
        )
        viewModel.errorMessage = .message(.nativeAuthVerificationTokenRequired)
        let sut = MFAChallengeView(viewModel: viewModel) {}

        XCTAssertNoThrow(try sut.inspect().find(text: "Enter Authenticator Code"))
        XCTAssertNoThrow(
            try sut.inspect().find(text: "Open your authenticator app and enter the 6-digit code.")
        )
        XCTAssertNoThrow(try sut.inspect().find(text: "Verification token required"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Verify"))
    }

    func testMFAChallengeCancelNotifiesOwner() throws {
        let viewModel = MFAChallengeViewModel(
            loginAttemptId: "attempt-1",
            signInService: makeService()
        )
        var didCancel = false
        let sut = MFAChallengeView(
            viewModel: viewModel,
            onSuccess: {},
            onCancel: { didCancel = true }
        )

        try sut.inspect().find(button: "Cancel").tap()

        XCTAssertTrue(didCancel)
    }

    private func makeViewModel() -> EmailOTPViewModel {
        EmailOTPViewModel(signInService: makeService())
    }

    private func makeService() -> SignInService {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let storage = HTTPCookieStorage()
        let client = APIClient(
            config: config,
            cookieStorage: storage,
            protocolClasses: [EmailOTPViewFailingURLProtocol.self]
        )
        return SignInService(client: client, sessionManager: SessionManager(client: client, cookieStorage: storage))
    }
}
