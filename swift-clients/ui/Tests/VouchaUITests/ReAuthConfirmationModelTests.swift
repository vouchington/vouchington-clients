import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class ReAuthConfirmationModelTests: XCTestCase {
    private let expired = VouchaError.api(statusCode: 410, preconditionCode: "MFA_REAUTH_REQUIRED")
    private let otherSession = VouchaError.forbidden(preconditionCode: "MFA_REAUTH_REQUIRED")

    func testMapperPicksMessagePerCodeAndContext() {
        let removal = AccountChangeFailureMessage.Context.signInMethodRemoval
        let reauth = AccountChangeFailureMessage.Context.providerReAuth
        XCTAssertEqual(
            AccountChangeFailureMessage.key(
                for: VouchaError.api(statusCode: 400, preconditionCode: "LAST_SIGN_IN_METHOD_REMOVAL_FORBIDDEN"),
                context: removal
            ),
            .sharedSignInMethodRemovalLastMethod
        )
        XCTAssertEqual(
            AccountChangeFailureMessage.key(
                for: VouchaError.forbidden(preconditionCode: "ADMINISTRATOR_MFA_REQUIRED"), context: removal
            ),
            .sharedSignInMethodRemovalAdministratorMfa
        )
        XCTAssertEqual(
            AccountChangeFailureMessage.key(for: otherSession, context: removal),
            .sharedReauthConfirmationExpired
        )
        XCTAssertEqual(
            AccountChangeFailureMessage.key(for: expired, context: reauth),
            .sharedReauthConfirmationExpired
        )
        XCTAssertEqual(
            AccountChangeFailureMessage.key(for: otherSession, context: reauth),
            .sharedReauthConfirmationChooseAnotherMethod
        )
        XCTAssertEqual(
            AccountChangeFailureMessage.message(for: expired, context: reauth),
            .message(.sharedReauthConfirmationExpired)
        )
    }

    func testUnknownCodesAndForeignErrorsFallBack() {
        let unknown = VouchaError.api(statusCode: 400, preconditionCode: "SOMETHING_NEW")
        XCTAssertNil(AccountChangeFailureMessage.key(for: unknown, context: .providerReAuth))
        XCTAssertNil(AccountChangeFailureMessage.key(for: URLError(.timedOut), context: .signInMethodRemoval))
        XCTAssertNil(AccountChangeFailureMessage.message(for: unknown, context: .signInMethodRemoval))
    }

    func testDisconnectFailureUsesCodedCopyElseGenericDescription() {
        let viewModel = SettingsViewModel(client: nil)
        let last = VouchaError.api(statusCode: 400, preconditionCode: "LAST_SIGN_IN_METHOD_REMOVAL_FORBIDDEN")
        XCTAssertEqual(viewModel.disconnectErrorMessage(for: last), .message(.sharedSignInMethodRemovalLastMethod))
        XCTAssertEqual(
            viewModel.disconnectErrorMessage(for: VouchaError.unexpected("boom")),
            .verbatim("boom")
        )
    }

    func testExpiredCanceledAndRejectedThenChooseAnotherMethodReturnsToSelection() {
        let model = ReAuthConfirmationModel()
        XCTAssertNil(model.message)

        model.recordFailure(expired)
        XCTAssertEqual(model.state, .expired)
        XCTAssertEqual(model.message, UiMessage(.sharedReauthConfirmationExpired))

        model.recordCancellation()
        XCTAssertEqual(model.state, .canceled)
        XCTAssertEqual(model.message, UiMessage(.sharedReauthConfirmationCanceled))

        model.recordFailure(otherSession)
        XCTAssertEqual(model.state, .rejected)
        XCTAssertEqual(model.message, UiMessage(.sharedReauthConfirmationChooseAnotherMethod))

        model.chooseAnotherMethod()
        XCTAssertEqual(model.state, .selectingMethod)
        XCTAssertNil(model.message)
        XCTAssertEqual(model.prompt, UiMessage(.sharedReauthConfirmationChooseAnotherMethod))

        model.beginAttempt()
        XCTAssertNil(model.prompt)
    }

    func testUnrecognizedFailureIsPassedThroughWithoutChangingState() {
        let model = ReAuthConfirmationModel()
        model.recordFailure(VouchaError.api(statusCode: 500, preconditionCode: nil))
        XCTAssertEqual(model.state, .selectingMethod)
        XCTAssertNotNil(model.unrecognizedError)
        model.recordFailure(expired)
        XCTAssertNil(model.unrecognizedError)
    }
}
