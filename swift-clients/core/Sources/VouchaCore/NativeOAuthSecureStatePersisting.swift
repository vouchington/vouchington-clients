import Foundation

/// Durable, platform-secure storage for OAuth verifier, callback token, and result state.
///
/// Production adapters must survive process recreation and must not use plaintext preferences.
public protocol NativeOAuthSecureStatePersisting: Sendable {
    func read() throws -> Data?
    func write(_ data: Data) -> Bool
    func delete() throws
    func writeForOAuthCallbackClaim(_ data: Data) throws
}

public extension NativeOAuthSecureStatePersisting {
    func writeForOAuthCallbackClaim(_ data: Data) throws {
        guard write(data) else {
            throw NativeOAuthSecureStateError.unavailable
        }
    }
}

public enum NativeOAuthSecureStateError: LocalizedError, Sendable {
    case unavailable

    public var errorDescription: String? {
        "Secure authorization state is temporarily unavailable."
    }
}
