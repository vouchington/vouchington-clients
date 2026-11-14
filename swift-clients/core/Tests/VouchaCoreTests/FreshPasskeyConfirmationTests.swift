import Foundation
@testable import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaModels
import XCTest

@MainActor
final class FreshPasskeyConfirmationTests: XCTestCase {
    override func setUp() {
        super.setUp()
        OAuthCoordinatorURLProtocol.lock.withLock {
            OAuthCoordinatorURLProtocol.responses = [:]
            OAuthCoordinatorURLProtocol.requests = []
        }
    }

    func testOwnedPasskeyUsesFreshOptionsAndDoesNotRefreshOrUpgradeSession() async throws {
        let service = makeService()
        installOptions()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/re-auth/passkeys/verification"] = [
            (Data(#"{"re_auth_token":"passkey-proof"}"#.utf8), 200)
        ]
        let adapter = ConfirmationPasskeySpy()
        let proof = try await service.verifyPasskey(id: "owned-passkey", assertionProvider: adapter)
        XCTAssertEqual(proof, "passkey-proof")
        XCTAssertEqual(adapter.options?.rpId, "voucha.ai")
        XCTAssertEqual(OAuthCoordinatorURLProtocol.requests.map(\.url?.path), [
            "/api/v1/auth/re-auth/passkeys/options", "/api/v1/auth/re-auth/passkeys/verification"
        ])
    }

    func testCanceledAuthenticatorAndChangedOwnershipNeverReturnAProof() async throws {
        let service = makeService()
        installOptions()
        let canceled = ConfirmationPasskeySpy()
        canceled.canceled = true
        do {
            _ = try await service.verifyPasskey(id: "owned-passkey", assertionProvider: canceled)
            XCTFail("Canceled authenticator must not return proof")
        } catch is CancellationError {}
        XCTAssertFalse(OAuthCoordinatorURLProtocol.requests.contains {
            $0.url?.path == "/api/v1/auth/re-auth/passkeys/verification"
        })
        installOptions()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/re-auth/passkeys/verification"] = [
            (Data(#"{"message":"Passkey ownership changed"}"#.utf8), 403)
        ]
        do {
            _ = try await service.verifyPasskey(id: "owned-passkey", assertionProvider: ConfirmationPasskeySpy())
            XCTFail("Changed ownership must not return proof")
        } catch let error as VouchaError {
            guard case .forbidden = error else { return XCTFail("Expected forbidden ownership change") }
        }
    }

    private func makeService() -> FreshAccountConfirmationService {
        FreshAccountConfirmationService(client: APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [OAuthCoordinatorURLProtocol.self], bootstrapSession: false
        ))
    }

    private func installOptions() {
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/re-auth/passkeys/options"] = [
            (ApiFixtureLoader.data("native.auth.re-auth.passkeys.options"), 200)
        ]
    }
}

@MainActor
private final class ConfirmationPasskeySpy: FreshPasskeyAssertion {
    var options: ReAuthPasskeyOptions?
    var canceled = false
    func assertion(options: ReAuthPasskeyOptions) async throws -> DecodedJSONObject {
        self.options = options
        if canceled { throw CancellationError() }
        return ["id": .string("credential"), "response": .object([:]), "type": .string("public-key")]
    }
}
