/// Implemented by a configured native provider SDK. Each call must acquire a new nonce-bound assertion.
public protocol FreshProviderAssertion: Sendable {
    func assertion(nonce: String) async throws -> String
}
