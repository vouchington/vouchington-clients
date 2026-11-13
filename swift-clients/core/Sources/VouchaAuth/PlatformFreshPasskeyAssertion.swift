#if canImport(AuthenticationServices)
    import AuthenticationServices
    import Foundation
    import VouchaModels

    @MainActor
    struct PlatformFreshPasskeyAssertion: FreshPasskeyAssertion {
        let anchor: ASPresentationAnchor

        func assertion(options: ReAuthPasskeyOptions) async throws -> DecodedJSONObject {
            let converted = PasskeyAuthenticationOptions(
                challenge: options.challenge, rpId: options.rpId,
                userVerification: options.userVerification,
                allowCredentials: options.allowCredentials?.map { PasskeyCredentialDescriptor(id: $0.id) }
            )
            let result = try await PasskeyAssertionController(presentationAnchor: anchor).assertion(for: converted)
            return try JSONDecoder().decode(DecodedJSONObject.self, from: JSONEncoder().encode(result))
        }
    }
#endif
