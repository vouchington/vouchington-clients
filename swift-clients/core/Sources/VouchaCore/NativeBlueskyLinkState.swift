import Foundation

public enum NativeBlueskyCallback: Equatable, Sendable {
    case completion(PendingNativeBlueskyLink)
    case failure(flowId: String, code: String)
}

public struct PendingNativeBlueskyLink: Codable, Equatable, Sendable {
    public let flowId: String
    public let completionProofVerifier: String
    public let startedAt: Date
    public let completionToken: String?

    public var expiresAt: Date {
        startedAt.addingTimeInterval(NativeBlueskyLinkStore.lifetime)
    }

    public var isFinalizing: Bool {
        completionToken != nil
    }
}

public enum NativeBlueskyLinkResult: String, Codable, Equatable, Sendable {
    case finalizing
    case success
    case providerFailure
    case finalizationFailure
    case expired
}

public enum PendingNativeBlueskyLinkStatus: Equatable, Sendable {
    case none
    case active(PendingNativeBlueskyLink)
    case expired
}
