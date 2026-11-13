import Crypto
import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels
#if canImport(AuthenticationServices)
    import AuthenticationServices
#endif

/// Obtains account-control proof without refreshing, replacing, or upgrading the active session.
public struct FreshAccountConfirmationService: Sendable {
    public let client: APIClient

    public init(client: APIClient) {
        self.client = client
    }

    public static func appleNonceHash(_ serverNonce: String) -> String {
        SHA256.hash(data: Data(serverNonce.utf8)).map { String(format: "%02x", $0) }.joined()
    }

    public func methods() async throws -> ReAuthMethodsResponse {
        try await client.send(.reAuthMethods)
    }

    public func sendEmail() async throws {
        let _: EmptyResponse = try await client.send(.requestReAuthEmailToken)
    }

    public func verifyEmail(_ code: String) async throws -> String {
        let response: ReAuthTokenResponse = try await client.send(.verifyReAuthEmailCode(code))
        return response.reAuthToken
    }

    public func verifyTOTP(_ code: String) async throws -> String {
        guard code.utf8.count == 6, code.utf8.allSatisfy({ (48 ... 57).contains($0) }) else {
            throw AccountConfirmationError.invalidProof
        }
        let response: ReAuthTokenResponse = try await client.send(.verifyReAuthTOTP(code))
        return response.reAuthToken
    }

    @MainActor
    public func verifyPasskey(id: String, assertionProvider: any FreshPasskeyAssertion) async throws -> String {
        let response: ReAuthPasskeyOptionsResponse = try await client.send(.reAuthPasskeyOptions(passkeyId: id))
        let assertion = try await assertionProvider.assertion(options: response.options)
        try Task.checkCancellation()
        let proof: ReAuthTokenResponse = try await client.send(.verifyReAuthPasskey(
            passkeyId: id, flowId: response.flowId, response: assertion
        ))
        return proof.reAuthToken
    }

    #if canImport(AuthenticationServices)
        @MainActor
        public func verifyPasskey(id: String, presentationAnchor: ASPresentationAnchor) async throws -> String {
            try await verifyPasskey(
                id: id,
                assertionProvider: PlatformFreshPasskeyAssertion(anchor: presentationAnchor)
            )
        }
    #endif
}
