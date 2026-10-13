import Foundation

/// Apple OAuth uses a production associated-domain callback registered for voucha.ai.
public enum NativeChatMCPAppleOAuthOrigin {
    public static func allows(_ url: URL) -> Bool {
        guard let parts = URLComponents(url: url, resolvingAgainstBaseURL: false) else { return false }
        return parts.scheme == "https" && parts.host == "voucha.ai" && parts.port == nil &&
            parts.user == nil && parts.password == nil &&
            (parts.percentEncodedPath.isEmpty || parts.percentEncodedPath == "/") &&
            parts.percentEncodedQuery == nil && parts.fragment == nil
    }
}
