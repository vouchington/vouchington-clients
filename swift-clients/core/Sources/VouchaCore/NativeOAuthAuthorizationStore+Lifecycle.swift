import Foundation

public extension Notification.Name {
    static let nativeOAuthAuthorizationDidChange = Notification.Name("nativeOAuthAuthorizationDidChange")
}

public struct NativeOAuthAuthorizationSnapshot: Sendable {
    public let pendingStatus: PendingNativeOAuthAuthorizationStatus
    public let result: NativeOAuthAuthorizationResult?
}

public extension NativeOAuthAuthorizationStore {
    func discardAuthorization() throws {
        try clearPending()
        try acknowledgeResult()
    }
}
