import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
func makeFreshAccountSettingsTestClient() throws -> APIClient {
    let cookies = HTTPCookieStorage()
    for name in ["dt", "st"] {
        try cookies.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: name, .value: "settings-\(name)", .domain: "localhost", .path: "/"
        ])))
    }
    CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
        Data(#"{"has_email":false,"has_passkey":false,"has_totp":true,"providers":[]}"#.utf8), 200
    )
    CannedFeedURLProtocol.handlers["/api/v1/auth/mfa/re-auth/totp/verification"] = (
        Data(#"{"re_auth_token":"fresh-settings-proof"}"#.utf8), 200
    )
    return APIClient(
        config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key"),
        cookieStorage: cookies,
        protocolClasses: [CannedFeedURLProtocol.self],
        bootstrapSession: false
    )
}

extension XCTestCase {
    @MainActor
    func confirmAccountChange(
        _ confirmation: FreshAccountConfirmationViewModel,
        operation: @escaping @MainActor () async -> Void
    ) async {
        let owner = confirmation.generation
        let operationTask = Task { await operation() }
        let ready = expectation(for: NSPredicate { _, _ in
            MainActor
                .assumeIsolated {
                    confirmation.generation != owner && confirmation.isPresented && confirmation.methods != nil
                }
        }, evaluatedWith: nil)
        await fulfillment(of: [ready], timeout: 2)
        guard confirmation.generation != owner, confirmation.isPresented, confirmation.methods != nil else {
            confirmation.cancel()
            operationTask.cancel()
            await operationTask.value
            return
        }
        confirmation.usingEmail = false
        confirmation.code = "123456"
        await confirmation.verifyCode()
        await operationTask.value
    }
}
