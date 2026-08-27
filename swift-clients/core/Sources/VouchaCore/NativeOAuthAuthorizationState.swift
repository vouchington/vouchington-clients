import Foundation

public enum NativeOAuthProvider: String, CaseIterable, Codable, Identifiable, Sendable {
    case facebook
    // swiftlint:disable:next identifier_name
    case x
    case github

    public var id: String {
        rawValue
    }
}

public enum NativeOAuthAuthorizationPurpose: String, Codable, Sendable {
    case authenticate
    case connect
}

public struct PendingNativeOAuthAuthorization: Codable, Equatable, Sendable {
    public let flowId: String
    public let provider: NativeOAuthProvider
    public let purpose: NativeOAuthAuthorizationPurpose
    public let completionProofVerifier: String
    public let expiresAt: Date
    public let completionToken: String?

    public var isFinalizing: Bool {
        completionToken != nil
    }
}

public enum NativeOAuthAuthorizationResult: Codable, Equatable, Sendable {
    case authenticated(provider: NativeOAuthProvider)
    case mfaRequired(provider: NativeOAuthProvider, loginAttemptId: String)
    case connected(provider: NativeOAuthProvider)
    case expired(provider: NativeOAuthProvider, purpose: NativeOAuthAuthorizationPurpose)

    public var mfaLoginAttemptId: String? {
        guard case let .mfaRequired(_, loginAttemptId) = self else { return nil }
        return loginAttemptId
    }
}

public enum PendingNativeOAuthAuthorizationStatus: Equatable, Sendable {
    case none
    case active(PendingNativeOAuthAuthorization)
    case expired(PendingNativeOAuthAuthorization)
}
