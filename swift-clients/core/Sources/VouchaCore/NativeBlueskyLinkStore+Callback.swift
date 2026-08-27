import Foundation

public extension Notification.Name {
    static let blueskyLinkResultDidChange = Notification.Name("blueskyLinkResultDidChange")
}

extension NativeBlueskyLinkStore {
    public func isCallbackURL(_ url: URL) -> Bool {
        url.scheme?.lowercased() == "voucha" && url.host == "auth" && url.path == "/bluesky/callback"
    }

    func completionToken(from url: URL) -> String? {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?
            .queryItems?
            .first(where: { $0.name == "completion_token" })?
            .value
    }

    func recordFinalizationFailure() -> Bool {
        recordResult(.finalizationFailure)
        return false
    }
}
