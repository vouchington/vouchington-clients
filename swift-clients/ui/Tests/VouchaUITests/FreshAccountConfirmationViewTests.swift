import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FreshAccountConfirmationViewTests: XCTestCase {
    func testEmailPaneOffersExplicitSendAndVerificationAndCanonicalFailureCopy() throws {
        let model = try makeModel()
        let view = FreshAccountConfirmationView(model: model)
        XCTAssertNoThrow(try view.inspect().find(text: "Re-authenticate"))
        XCTAssertNoThrow(try view.inspect().find(text: "Verify your identity to change your sign-in settings."))
        XCTAssertNoThrow(try view.inspect().find(button: "Send code"))
        model.emailSent = true
        model.code = "abcdefgh23"
        XCTAssertNoThrow(try view.inspect().find(button: "Resend"))
        XCTAssertFalse(try view.inspect().find(button: "Verify").isDisabled())
        model.isWorking = true
        XCTAssertNoThrow(try view.inspect().find(button: "Verifying..."))
        model.errorMessage = .init(.extractedMyEmailManagerInvalidOrExpiredVerificationCode9b5b98fe)
        XCTAssertNoThrow(try view.inspect().find(text: "Invalid or expired verification code"))
    }

    func testAuthenticatorPaneChangesInputPurposeAndCancelClearsPresentation() throws {
        let model = try makeModel()
        let view = FreshAccountConfirmationView(model: model)
        try view.inspect().find(button: "Authenticator app").tap()
        XCTAssertFalse(model.usingEmail)
        XCTAssertNoThrow(try view.inspect().find(text: "Enter the 6-digit code from your authenticator app"))
        model.code = "123456"
        XCTAssertFalse(try view.inspect().find(button: "Verify").isDisabled())
        try view.inspect().find(button: "Email verification").tap()
        XCTAssertTrue(model.usingEmail)
        XCTAssertEqual(model.code, "")
        model.isPresented = true
        try view.inspect().find(button: "Cancel").tap()
        XCTAssertFalse(model.isPresented)
    }

    func testProviderOnlyMethodsNeverOfferEmailAndUnconfiguredGoogleIsDisabled() throws {
        let model =
            try makeModel(
                methods: #"{"has_email":false,"has_totp":false,"has_passkey":false,"providers":["google","github"]}"#
            )
        let view = FreshAccountConfirmationView(model: model)
        XCTAssertThrowsError(try view.inspect().find(button: "Email verification"))
        XCTAssertTrue(try view.inspect().find(button: "Google").isDisabled())
        XCTAssertTrue(try view.inspect().find(button: "GitHub").isDisabled())
        XCTAssertNoThrow(try view.inspect().find(text: "Unable to verify. Please try again."))
    }

    func testPasskeyMethodUsesOwnedCredentialNameWithoutVisibleUUID() throws {
        let model = try makeModel(methods: #"{"has_email":false,"has_totp":false,"has_passkey":true,"providers":[]}"#)
        let credential = try JSONDecoder().decode(
            AccountConfirmationPasskey.self,
            from: Data(#"{"id":"private-credential-id","name":"My device"}"#
                .utf8)
        )
        model.passkeyPagination.reset(items: [credential])
        let view = FreshAccountConfirmationOtherMethods(model: model)
        XCTAssertNoThrow(try view.inspect().find(text: "Passkeys"))
        #if canImport(AuthenticationServices)
            XCTAssertNoThrow(try view.inspect().find(button: "My device"))
        #endif
        XCTAssertThrowsError(try view.inspect().find(text: "private-credential-id"))
    }

    private func makeModel(methods: String =
        #"{"has_email":true,"has_totp":true,"has_passkey":false,"providers":[]}"#) throws
        -> FreshAccountConfirmationViewModel {
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let model = FreshAccountConfirmationViewModel(client: client)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        model.methods = try decoder.decode(ReAuthMethodsResponse.self, from: Data(methods.utf8))
        return model
    }
}
