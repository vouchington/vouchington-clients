import VouchaModels

/// Uses the native authenticator with options fetched for the selected, owned passkey.
public protocol FreshPasskeyAssertion: Sendable {
    @MainActor
    func assertion(options: ReAuthPasskeyOptions) async throws -> DecodedJSONObject
}
